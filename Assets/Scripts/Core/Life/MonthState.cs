using System;
using System.Collections.Generic;
using Negiri.Core.Data;
using Negiri.Core.Economy;
using Negiri.Core.Shopping;

namespace Negiri.Core.Life
{
    /// <summary>この買い物（またはカート）で在庫と満足度がどう変わるか。</summary>
    public sealed class LifeChange
    {
        public int FoodBefore, FoodAfter, FoodWasted;
        public int DailyBefore, DailyAfter, DailyWasted;
        /// <summary>自由商品などで上がる満足度。</summary>
        public int SatisfactionGain;
        /// <summary>返品で下がる満足度（正の数）。</summary>
        public int ReturnPenalty;
        public int SatisfactionBefore, SatisfactionAfter;
    }

    /// <summary>1晩の結果（夜のまとめに出す）。</summary>
    public sealed class NightReport
    {
        /// <summary>終わった日。</summary>
        public int Day;
        public int FoodBefore, FoodAfter, DailyBefore, DailyAfter;
        public bool FoodOut, DailyOut;
        public int SatisfactionBefore, SatisfactionAfter;
        /// <summary>この夜で月が終わったか（30日目の夜）。</summary>
        public bool MonthOver;
    }

    /// <summary>
    /// 今月の状態（日付・財布・在庫・満足度・家計簿）と、1日の進め方。
    /// 画面はこのクラスを呼ぶだけにして、計算はここに集める。セーブはこのクラスを SaveData にするだけ。
    /// </summary>
    public sealed class MonthState
    {
        public LifeBalance Life { get; }
        public ShoppingRule Rule { get; }
        public int Difficulty { get; }

        /// <summary>今日（1〜日数）。月が終わった後は最後の日のまま。</summary>
        public int Day { get; private set; }
        /// <summary>最後の日の夜を越えたら true。</summary>
        public bool IsOver { get; private set; }
        /// <summary>最後に買い物した日。まだなら 0。</summary>
        public int LastShopDay { get; private set; }

        public Wallet Wallet { get; }
        public Stock Stock { get; }
        public Satisfaction Satisfaction { get; }
        public Ledger Ledger { get; } = new Ledger();

        MonthState(LifeBalance life, ShoppingRule rule, int difficulty, int day, bool isOver, int lastShopDay, Wallet wallet, Stock stock, Satisfaction satisfaction)
        {
            Life = life ?? throw new ArgumentNullException(nameof(life));
            Rule = rule ?? throw new ArgumentNullException(nameof(rule));
            Difficulty = difficulty;
            Day = day;
            IsOver = isOver;
            LastShopDay = lastShopDay;
            Wallet = wallet;
            Stock = stock;
            Satisfaction = satisfaction;
        }

        /// <summary>月初：予算を財布に入れ、在庫と満足度を最初の値にする。</summary>
        public static MonthState Start(LifeBalance life, ShoppingRule rule, int difficulty)
        {
            if (life == null) throw new ArgumentNullException(nameof(life));
            var s = life.stock;
            return new MonthState(life, rule, difficulty, 1, false, 0,
                new Wallet(life.month.budget),
                new Stock(s.foodStart, s.dailyStart, rule.foodCap, s.dailyCap),
                new Satisfaction(life.satisfaction.start, life.satisfaction.max));
        }

        public int Days => Life.month.days;
        public string DateLabel => Calendar.DateLabel(Day, Life.month);
        public int Weekday => Calendar.Weekday(Day, Life.month.firstWeekday);

        public int NextShopDay => Calendar.NextShopDay(Day, LastShopDay, Rule.shopIntervalDays);
        public bool ShoppedToday => LastShopDay == Day;
        public bool CanShop => !IsOver && NextShopDay == Day;
        /// <summary>次に買い物できる日が月の外なら、今月はもう行けない。</summary>
        public bool NoMoreShopping => IsOver || NextShopDay > Days;

        /// <summary>財布からロック貯金へ移す（解除はできない）。</summary>
        public void Lock(int amount)
        {
            if (IsOver) throw new InvalidOperationException("月が終わっています");
            Wallet.Lock(amount);
        }

        /// <summary>このカートを全部買ったら在庫と満足度がどうなるか（店内の表示用）。</summary>
        public LifeChange Preview(IEnumerable<ItemData> items)
        {
            int food = 0, daily = 0, sat = 0;
            foreach (var item in items) Sum(item, ref food, ref daily, ref sat);
            return MakeChange(food, daily, sat, 0);
        }

        LifeChange MakeChange(int food, int daily, int sat, int returnPenalty)
        {
            var (foodAfter, foodWasted) = Stock.Preview(Stock.Food, food, Stock.FoodCap);
            var (dailyAfter, dailyWasted) = Stock.Preview(Stock.Daily, daily, Stock.DailyCap);
            int satAfter = Math.Max(0, Math.Min(Satisfaction.Max, Satisfaction.Value + sat - returnPenalty));
            return new LifeChange
            {
                FoodBefore = Stock.Food, FoodAfter = foodAfter, FoodWasted = foodWasted,
                DailyBefore = Stock.Daily, DailyAfter = dailyAfter, DailyWasted = dailyWasted,
                SatisfactionGain = sat, ReturnPenalty = returnPenalty,
                SatisfactionBefore = Satisfaction.Value, SatisfactionAfter = satAfter,
            };
        }

        static void Sum(ItemData item, ref int food, ref int daily, ref int sat)
        {
            foreach (var e in item.effects ?? Array.Empty<ItemEffect>())
            {
                switch (e.stat)
                {
                    case "food": food += e.amount; break;
                    case "daily": daily += e.amount; break;
                    case "satisfaction": sat += e.amount; break;
                }
            }
        }

        /// <summary>
        /// 会計の済んだ買い物を反映する。返品しなかった商品だけ在庫と満足度に足し、家計簿に書く。
        /// 返品の満足度ペナルティもここで引く。お金は会計（Checkout.Pay）で動いている。
        /// </summary>
        public LifeChange ApplyShopping(Receipt receipt)
        {
            if (receipt == null) throw new ArgumentNullException(nameof(receipt));
            if (!CanShop) throw new InvalidOperationException("今日は買い物できません");

            int food = 0, daily = 0, sat = 0;
            foreach (var l in receipt.Lines)
            {
                if (l.Returned) continue;
                Sum(l.Cart.Item, ref food, ref daily, ref sat);
                Ledger.Add(new LedgerEntry
                {
                    day = Day,
                    itemId = l.Cart.Item.id,
                    name = l.Cart.Item.name,
                    basePrice = l.BasePrice,
                    paid = l.Price,
                    rank = ReceiptLines.RankName(l.Rank),
                    typingBp = l.TypingBp,
                });
            }

            var change = MakeChange(food, daily, sat, receipt.SatisfactionLoss);
            Stock.AddFood(food);
            Stock.AddDaily(daily);
            Satisfaction.Change(sat - receipt.SatisfactionLoss);
            LastShopDay = Day;
            return change;
        }

        /// <summary>夜：在庫を使い、満足度を下げ、次の日へ進む。最後の日の夜なら月が終わる。</summary>
        public NightReport EndDay()
        {
            if (IsOver) throw new InvalidOperationException("月が終わっています");
            var st = Life.stock;
            var r = new NightReport
            {
                Day = Day,
                FoodBefore = Stock.Food,
                DailyBefore = Stock.Daily,
                SatisfactionBefore = Satisfaction.Value,
            };
            var (foodOut, dailyOut) = Stock.ConsumeNight(st.foodPerDay, st.dailyPerDay);
            int delta = -Life.satisfaction.dailyDecay;
            if (foodOut) delta -= st.foodOutPenalty;
            if (dailyOut) delta -= st.dailyOutPenalty;
            Satisfaction.Change(delta);

            r.FoodOut = foodOut;
            r.DailyOut = dailyOut;
            r.FoodAfter = Stock.Food;
            r.DailyAfter = Stock.Daily;
            r.SatisfactionAfter = Satisfaction.Value;

            if (Day >= Days) IsOver = true;
            else Day++;
            r.MonthOver = IsOver;
            return r;
        }

        /// <summary>
        /// 「数日進める」：少なくとも1晩進め、買い物できる日・在庫が切れる日（その朝）・月末のどれかで止まる。
        /// </summary>
        public List<NightReport> AdvanceDays()
        {
            var reports = new List<NightReport>();
            do
            {
                reports.Add(EndDay());
                if (IsOver || CanShop) break;
                if (Stock.Food <= 0 || Stock.Daily <= 0) break;
            } while (true);
            return reports;
        }

        // ---------------- セーブ ----------------

        public Save.SaveData ToSave()
        {
            return new Save.SaveData
            {
                version = Save.SaveData.CurrentVersion,
                difficulty = Difficulty,
                day = Day,
                isOver = IsOver,
                lastShopDay = LastShopDay,
                totalIncome = Wallet.TotalIncome,
                totalSpent = Wallet.TotalSpent,
                cash = Wallet.Cash,
                locked = Wallet.Locked,
                food = Stock.Food,
                daily = Stock.Daily,
                foodOutDays = Stock.FoodOutDays,
                dailyOutDays = Stock.DailyOutDays,
                satisfaction = Satisfaction.Value,
                ledger = new List<LedgerEntry>(Ledger.Entries).ToArray(),
            };
        }

        /// <summary>セーブから戻す。数値（予算や上限）は今のデータファイルのものを使う。</summary>
        public static MonthState FromSave(Save.SaveData save, LifeBalance life, ShoppingRule rule)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (save.version < 1 || save.version > Save.SaveData.CurrentVersion) throw new ArgumentException($"読めないセーブです（version {save.version}）");
            if (save.day < 1 || save.day > life.month.days) throw new ArgumentException($"セーブの日付がおかしいです（{save.day}日目）");
            var s = new MonthState(life, rule, save.difficulty, save.day, save.isOver, save.lastShopDay,
                Wallet.Restore(save.totalIncome, save.totalSpent, save.cash, save.locked),
                new Stock(save.food, save.daily, rule.foodCap, life.stock.dailyCap, save.foodOutDays, save.dailyOutDays),
                new Satisfaction(save.satisfaction, life.satisfaction.max));
            foreach (var e in save.ledger ?? Array.Empty<LedgerEntry>()) s.Ledger.Add(e);
            return s;
        }
    }
}
