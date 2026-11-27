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
        /// <summary>月末に借金に足された利息（月末の夜だけ）。</summary>
        public int Interest;
        /// <summary>利息を足した後の借金残高（月末の夜だけ）。</summary>
        public int DebtAfter;
        /// <summary>次の朝が給料日なら、財布に入った額（夜が明けたときに入る）。</summary>
        public int Payday;
    }

    /// <summary>
    /// 今月の状態（日付・財布・借金・在庫・満足度・家計簿）と、1日の進め方。
    /// 画面はこのクラスを呼ぶだけにして、計算はここに集める。セーブはこのクラスを SaveData にするだけ。
    /// </summary>
    public sealed class MonthState
    {
        public LifeBalance Life { get; }
        public ShoppingRule Rule { get; }
        public int Difficulty { get; }

        /// <summary>今日（1～日数）。月が終わった後は最後の日のまま。</summary>
        public int Day { get; private set; }
        /// <summary>最後の日の夜を越えたら true。</summary>
        public bool IsOver { get; private set; }
        /// <summary>最後に買い物した日。まだなら 0。</summary>
        public int LastShopDay { get; private set; }

        public Wallet Wallet { get; }
        public Debt Debt { get; }
        public Stock Stock { get; }
        public Satisfaction Satisfaction { get; }
        public Ledger Ledger { get; } = new Ledger();
        /// <summary>今日、家計簿から貯金箱に入れた額。夜が明けると0に戻る。</summary>
        public int LockedToday { get; private set; }

        MonthState(LifeBalance life, ShoppingRule rule, int difficulty, int day, bool isOver, int lastShopDay, Wallet wallet, Debt debt, Stock stock, Satisfaction satisfaction)
        {
            Debt = debt ?? throw new ArgumentNullException(nameof(debt));
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
                new Debt(life.debt ?? new DebtBalance()),
                new Stock(s.foodStart, s.dailyStart, rule.foodCap, s.dailyCap),
                new Satisfaction(life.satisfaction.start, life.satisfaction.max));
        }

        public int Days => Life.month.days;

        /// <summary>
        /// お金の式がすべて成り立っているか（テストと開発中のチェック用）。
        /// 収入合計 ＋ 借入合計 ＝ 支出合計 ＋ 返済合計 ＋ 財布 ＋ ロック貯金、
        /// 借金残高 ＝ 初めからある借金 ＋ 借入合計 ＋ 利息合計 － 返済合計。
        /// </summary>
        public bool IsBalanced
        {
            get
            {
                if (!Wallet.IsBalanced) return false;
                if (Wallet.TotalBorrowed != Debt.TotalBorrowed || Wallet.TotalRepaid != Debt.TotalRepaid) return false;
                int initial = 0;
                foreach (var a in Debt.Accounts) initial += a.initial;
                return Debt.Total == initial + Debt.TotalBorrowed + Debt.TotalInterest - Debt.TotalRepaid && Debt.Total >= 0;
            }
        }

        /// <summary>買い物で借りる借入先（この難易度で借りられなければ空）。</summary>
        public string ShopLender => Rule.borrowFrom ?? "";

        /// <summary>
        /// 返せる額の候補（小さい順）。返済の単位（repayStep）ごとと、財布で払えるなら全額。
        /// 借金がないか、財布が足りなければ空。
        /// </summary>
        public List<int> RepayChoices(string lender)
        {
            var list = new List<int>();
            if (IsOver) return list;
            int bal = Debt.BalanceOf(lender);
            int max = Math.Min(bal, Wallet.Cash);
            int step = Debt.RepayStep;
            for (int a = step; a <= max && a < bal; a += step) list.Add(a);
            if (bal > 0 && bal <= Wallet.Cash) list.Add(bal);
            return list;
        }

        /// <summary>財布から借金を返す。返済の単位か全額でなければ例外にする。</summary>
        public void Repay(string lender, int amount)
        {
            if (IsOver) throw new InvalidOperationException("月が終わっています");
            if (!RepayChoices(lender).Contains(amount)) throw new ArgumentException($"{amount}円は返せません（{Debt.RepayStep}円単位か全額）");
            Wallet.Repay(amount);
            Debt.Repay(lender, amount, Day);
        }
        public string DateLabel => Calendar.DateLabel(Day, Life.month);

        /// <summary>次の給料日（今日より後）。もうなければ0。</summary>
        public int NextPayday => IsOver ? 0 : Life.month.NextPaydayAfter(Day);

        /// <summary>今日が給料日か（朝にお金が入った日）。</summary>
        public bool IsPaydayToday => Life.month.PaydayOn(Day) > 0;

        /// <summary>ここまでに入ったお金（1日目の予算と、今日までの給料）。家計簿に出す。</summary>
        public List<(int day, string name, int amount)> IncomeLog
        {
            get
            {
                var list = new List<(int, string, int)> { (1, "今月の予算", Life.month.budget) };
                foreach (var p in Life.month.paydays ?? Array.Empty<PaydayData>())
                    if (p != null && p.day > 1 && p.day <= Day && p.amount > 0) list.Add((p.day, p.name, p.amount));
                list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                return list;
            }
        }
        public int Weekday => Calendar.Weekday(Day, Life.month.firstWeekday);

        public int NextShopDay => Calendar.NextShopDay(Day, LastShopDay, Rule.shopIntervalDays);
        public bool ShoppedToday => LastShopDay == Day;
        public bool CanShop => !IsOver && NextShopDay == Day;
        /// <summary>次に買い物できる日が月の外なら、今月はもう行けない。</summary>
        public bool NoMoreShopping => IsOver || NextShopDay > Days;

        /// <summary>貯金箱の合計の上限まで、あといくら入るか。上限なしなら int.MaxValue。</summary>
        int RoomInBox => Life.month.lockTotalMax > 0 ? Math.Max(0, Life.month.lockTotalMax - Wallet.Locked) : int.MaxValue;

        /// <summary>単位（lockStep）で切り捨て、財布と合計の上限も考えた、入れられる最大額。</summary>
        int Fit(int limit)
        {
            int step = Math.Max(1, Life.month.lockStep);
            int n = Math.Min(Math.Min(limit, Wallet.Cash), RoomInBox);
            return Math.Max(0, n - n % step);
        }

        /// <summary>月初に貯金箱へ入れられる最大額。</summary>
        public int MaxStartLock => IsOver ? 0 : Fit(Life.month.lockStartMax);

        /// <summary>今日、家計簿から貯金箱へ入れられる最大額。</summary>
        public int MaxLockToday => IsOver ? 0 : Fit(Life.month.lockDailyMax - LockedToday);

        /// <summary>月初に財布から貯金箱へ移す（取り出せない）。上限は lockStartMax。</summary>
        public void LockAtStart(int amount)
        {
            if (amount > MaxStartLock) throw new ArgumentException("月初に貯金箱へ入れられる額を超えています");
            Lock(amount, false);
        }

        /// <summary>家計簿から財布の一部を貯金箱へ移す（取り出せない）。上限は1日 lockDailyMax。</summary>
        public void Lock(int amount) => Lock(amount, true);

        void Lock(int amount, bool daily)
        {
            if (IsOver) throw new InvalidOperationException("月が終わっています");
            if (amount < 0) throw new ArgumentException("貯金箱に入れる額は0以上にしてください");
            if (daily && amount > MaxLockToday) throw new ArgumentException("今日貯金箱へ入れられる額を超えています");
            Wallet.Lock(amount);
            if (daily) LockedToday += amount;
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

            LockedToday = 0;
            if (Day >= Days)
            {
                // 月末：借金に利息を足す（財布は変わらない）
                r.Interest = Debt.AddInterest(Day);
                r.DebtAfter = Debt.Total;
                IsOver = true;
            }
            else
            {
                Day++;
                // 給料日：朝に財布へ入る
                int pay = Life.month.PaydayOn(Day);
                if (pay > 0)
                {
                    Wallet.Receive(pay);
                    r.Payday = pay;
                }
            }
            r.MonthOver = IsOver;
            return r;
        }

        /// <summary>今日を含めて残っている夜の数（30日目なら1）。</summary>
        public int NightsLeft => IsOver ? 0 : Days - Day + 1;

        /// <summary>「数日進める」で最初に出す日数：次の買い物の日まで（2日以上先のとき）。それ以外は3日。</summary>
        public int SuggestedNights
        {
            get
            {
                if (IsOver) return 0;
                int next = NextShopDay;
                int n = next - Day >= 2 && next <= Days ? next - Day : 3;
                return Math.Max(1, Math.Min(n, NightsLeft));
            }
        }

        /// <summary>
        /// 「数日進める」：選んだ晩数だけ進める。途中で在庫が切れた（その朝）か月末なら、そこで止まる。
        /// 進める前からすでに切れている在庫では止まらない。
        /// </summary>
        public List<NightReport> AdvanceDays(int nights)
        {
            if (nights < 1) throw new ArgumentOutOfRangeException(nameof(nights));
            bool hadFood = Stock.Food > 0, hadDaily = Stock.Daily > 0;
            var reports = new List<NightReport>();
            while (reports.Count < nights && !IsOver)
            {
                reports.Add(EndDay());
                if ((hadFood && Stock.Food <= 0) || (hadDaily && Stock.Daily <= 0)) break;
            }
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
                totalBorrowed = Wallet.TotalBorrowed,
                totalRepaid = Wallet.TotalRepaid,
                debts = CopyAccounts(),
                debtLog = new List<DebtEvent>(Debt.Log).ToArray(),
                lockedToday = LockedToday,
                food = Stock.Food,
                daily = Stock.Daily,
                foodOutDays = Stock.FoodOutDays,
                dailyOutDays = Stock.DailyOutDays,
                satisfaction = Satisfaction.Value,
                ledger = new List<LedgerEntry>(Ledger.Entries).ToArray(),
            };
        }

        DebtAccount[] CopyAccounts()
        {
            var list = new List<DebtAccount>();
            foreach (var a in Debt.Accounts)
                list.Add(new DebtAccount { lender = a.lender, initial = a.initial, borrowed = a.borrowed, interest = a.interest, repaid = a.repaid });
            return list.ToArray();
        }

        /// <summary>セーブから戻す。数値（予算や上限）は今のデータファイルのものを使う。version 1（借金なし）のセーブも読める。</summary>
        public static MonthState FromSave(Save.SaveData save, LifeBalance life, ShoppingRule rule)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (save.version < 1 || save.version > Save.SaveData.CurrentVersion) throw new ArgumentException($"読めないセーブです（version {save.version}）");
            if (save.day < 1 || save.day > life.month.days) throw new ArgumentException($"セーブの日付がおかしいです（{save.day}日目）");
            var s = new MonthState(life, rule, save.difficulty, save.day, save.isOver, save.lastShopDay,
                Wallet.Restore(save.totalIncome, save.totalSpent, save.cash, save.locked, save.totalBorrowed, save.totalRepaid),
                Debt.Restore(life.debt ?? new DebtBalance(), save.debts, save.debtLog),
                new Stock(save.food, save.daily, rule.foodCap, life.stock.dailyCap, save.foodOutDays, save.dailyOutDays),
                new Satisfaction(save.satisfaction, life.satisfaction.max));
            foreach (var e in save.ledger ?? Array.Empty<LedgerEntry>()) s.Ledger.Add(e);
            s.LockedToday = Math.Max(0, save.lockedToday);
            if (!s.IsBalanced) throw new ArgumentException("セーブのお金の値が合いません（借入合計・返済合計と借金の記録が一致しません）");
            return s;
        }
    }
}
