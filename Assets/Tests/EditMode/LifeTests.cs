using System;
using System.Collections.Generic;
using Negiri.Core.Data;
using Negiri.Core.Life;
using Negiri.Core.Save;
using Negiri.Core.Shopping;
using Negiri.Core.Typing;
using NUnit.Framework;

namespace Negiri.Core.Tests
{
    public class LifeTests
    {
        static ItemData Food(string id, int price, int days) => new ItemData { id = id, name = id, kind = "essential", basePrice = price, effects = new[] { new ItemEffect { stat = "food", amount = days } } };
        static ItemData Daily(string id, int price, int days) => new ItemData { id = id, name = id, kind = "essential", basePrice = price, effects = new[] { new ItemEffect { stat = "daily", amount = days } } };
        static ItemData Treat(string id, int price, int sat) => new ItemData { id = id, name = id, kind = "free", basePrice = price, effects = new[] { new ItemEffect { stat = "satisfaction", amount = sat } } };

        static ShoppingRule Rule(int interval = 1, int foodCap = 7, ShortageRule shortage = ShortageRule.ChooseReturn, int penalty = 3)
        {
            return new ShoppingRule { label = "test", maxItems = 12, shortage = shortage.ToString(), satisfactionPerReturn = penalty, shopIntervalDays = interval, foodCap = foodCap };
        }

        static MonthState NewMonth(int interval = 1, int foodCap = 7, int budget = 19000, ShortageRule shortage = ShortageRule.ChooseReturn)
        {
            var life = new LifeBalance();
            life.month.budget = budget;
            return MonthState.Start(life, Rule(interval, foodCap, shortage), 0);
        }

        /// <summary>items を全部 rank で打って会計し、今月に反映する。返品する番号を渡せる（自分で選ぶ難易度用）。</summary>
        static (Receipt receipt, LifeChange change) Shop(MonthState m, Rank rank, int typingBp, ItemData[] items, params int[] returns)
        {
            var cart = new Cart(m.Rule.maxItems);
            foreach (var i in items) cart.Add(i, 0);
            var c = new Checkout(cart.Lines, m.Rule);
            for (int i = 0; i < c.Lines.Count; i++) c.Record(i, rank, typingBp);
            foreach (var r in returns) c.ToggleReturn(r);
            var receipt = c.Pay(m.Wallet);
            return (receipt, m.ApplyShopping(receipt));
        }

        // ---------------- 日付 ----------------

        [Test]
        public void 一日目は月曜で七日ごとに曜日が戻る()
        {
            Assert.AreEqual("月", Calendar.WeekdayName(1, 0));
            Assert.AreEqual("日", Calendar.WeekdayName(7, 0));
            Assert.AreEqual("月", Calendar.WeekdayName(8, 0));
            Assert.AreEqual("金", Calendar.WeekdayName(5, 0));
            Assert.AreEqual("4月5日(金)", Calendar.DateLabel(5, new MonthBalance()));
        }

        [TestCase(1, 0, 1, ExpectedResult = 1)]
        [TestCase(1, 1, 1, ExpectedResult = 2)]
        [TestCase(2, 1, 3, ExpectedResult = 4)]
        [TestCase(5, 1, 3, ExpectedResult = 5)]
        [TestCase(3, 1, 7, ExpectedResult = 8)]
        public int 次に買い物できる日(int today, int lastShopDay, int interval) => Calendar.NextShopDay(today, lastShopDay, interval);

        [Test]
        public void 中級は三日に一回しか買い物できない()
        {
            var m = NewMonth(interval: 3);
            Assert.IsTrue(m.CanShop);
            Shop(m, Rank.Great, 1000, new[] { Food("milk", 200, 1) });
            Assert.IsFalse(m.CanShop);
            Assert.IsTrue(m.ShoppedToday);
            m.EndDay();
            Assert.AreEqual(2, m.Day);
            Assert.IsFalse(m.CanShop);
            Assert.AreEqual(4, m.NextShopDay);
            m.EndDay();
            m.EndDay();
            Assert.AreEqual(4, m.Day);
            Assert.IsTrue(m.CanShop);
        }

        [Test]
        public void 同じ日に二回は買い物できない()
        {
            var m = NewMonth(interval: 1);
            Shop(m, Rank.Great, 1000, new[] { Food("milk", 200, 1) });
            Assert.Throws<InvalidOperationException>(() => Shop(m, Rank.Great, 1000, new[] { Food("egg", 200, 1) }));
        }

        [Test]
        public void 三十日目の夜で月が終わり_それ以上は進まない()
        {
            var m = NewMonth();
            NightReport last = null;
            for (int i = 0; i < 30; i++) last = m.EndDay();
            Assert.IsTrue(m.IsOver);
            Assert.IsTrue(last.MonthOver);
            Assert.AreEqual(30, last.Day);
            Assert.AreEqual(30, m.Day);
            Assert.IsFalse(m.CanShop);
            Assert.Throws<InvalidOperationException>(() => m.EndDay());
        }

        // ---------------- 在庫 ----------------

        [Test]
        public void 上限を超えた分は捨てられる()
        {
            var m = NewMonth(foodCap: 7);
            Assert.AreEqual(3, m.Stock.Food);
            var preview = m.Preview(new[] { Food("rice", 2480, 10) });
            Assert.AreEqual(7, preview.FoodAfter);
            Assert.AreEqual(6, preview.FoodWasted);

            var (_, change) = Shop(m, Rank.Miss, 0, new[] { Food("rice", 2480, 10) });
            Assert.AreEqual(3, change.FoodBefore);
            Assert.AreEqual(7, change.FoodAfter);
            Assert.AreEqual(6, change.FoodWasted);
            Assert.AreEqual(7, m.Stock.Food);
        }

        [Test]
        public void 返品した商品は在庫にも家計簿にも入らない()
        {
            var m = NewMonth();
            var (receipt, change) = Shop(m, Rank.Great, 1000, new[] { Food("egg", 248, 2), Daily("soap", 498, 10) }, 1);
            Assert.AreEqual(5, m.Stock.Food);
            Assert.AreEqual(7, m.Stock.Daily);
            Assert.AreEqual(1, m.Ledger.Entries.Count);
            Assert.AreEqual("egg", m.Ledger.Entries[0].itemId);
            Assert.AreEqual(receipt.Paid, m.Ledger.TotalPaid);
            Assert.AreEqual(receipt.Saved, m.Ledger.TotalSaved);
            Assert.AreEqual(3, change.ReturnPenalty);
        }

        [Test]
        public void 在庫が0の夜は在庫切れとして数え_満足度が下がる()
        {
            var m = NewMonth();
            // 食料3日分：1〜3日目の夜は足りる
            for (int i = 0; i < 3; i++)
            {
                var r = m.EndDay();
                Assert.IsFalse(r.FoodOut, $"{r.Day}日目");
            }
            Assert.AreEqual(0, m.Stock.Food);
            int before = m.Satisfaction.Value;
            var night = m.EndDay();
            Assert.IsTrue(night.FoodOut);
            Assert.IsFalse(night.DailyOut);
            Assert.AreEqual(1, m.Stock.FoodOutDays);
            Assert.AreEqual(before - 1 - 8, m.Satisfaction.Value);
        }

        [Test]
        public void 日用品が切れた夜も満足度が下がる()
        {
            var m = NewMonth();
            for (int i = 0; i < 7; i++) m.EndDay();
            Assert.AreEqual(0, m.Stock.Daily);
            int before = m.Satisfaction.Value;
            var night = m.EndDay();
            Assert.IsTrue(night.DailyOut);
            Assert.AreEqual(1, m.Stock.DailyOutDays);
            Assert.AreEqual(before - 1 - 8 - 4, m.Satisfaction.Value, "食料も日用品も切れている");
        }

        // ---------------- 満足度 ----------------

        [Test]
        public void 満足度は0未満にも上限超えにもならない()
        {
            var s = new Satisfaction(95, 100);
            Assert.AreEqual(5, s.Change(20));
            Assert.AreEqual(100, s.Value);
            Assert.AreEqual(-100, s.Change(-500));
            Assert.AreEqual(0, s.Value);
        }

        [Test]
        public void 自由商品で上がり_返品で下がり_毎晩1下がる()
        {
            var m = NewMonth();
            Assert.AreEqual(60, m.Satisfaction.Value);
            var (_, change) = Shop(m, Rank.Perfect, 2000, new[] { Treat("magazine", 690, 8), Treat("chips", 148, 3) }, 1);
            Assert.AreEqual(8, change.SatisfactionGain);
            Assert.AreEqual(3, change.ReturnPenalty);
            Assert.AreEqual(65, change.SatisfactionAfter);
            Assert.AreEqual(65, m.Satisfaction.Value);
            var night = m.EndDay();
            Assert.AreEqual(64, night.SatisfactionAfter);
        }

        // ---------------- 数日進める ----------------

        [Test]
        public void 数日進めるは次に買い物できる日で止まる()
        {
            var m = NewMonth(interval: 3);
            Shop(m, Rank.Great, 1000, new[] { Food("rice", 2480, 10), Daily("soap", 498, 10) });
            Assert.AreEqual(3, m.SuggestedNights);
            var reports = m.AdvanceDays(m.SuggestedNights);
            Assert.AreEqual(3, reports.Count);
            Assert.AreEqual(4, m.Day);
            Assert.IsTrue(m.CanShop);
        }

        [Test]
        public void 数日進めるは在庫が切れる日の朝で止まる()
        {
            var m = NewMonth(interval: 7);
            Shop(m, Rank.Great, 1000, new[] { Daily("soap", 498, 10) });
            // 食料3日分：3晩で0になり、4日目の朝で止まる
            var reports = m.AdvanceDays(6);
            Assert.AreEqual(3, reports.Count);
            Assert.AreEqual(4, m.Day);
            Assert.AreEqual(0, m.Stock.Food);
            Assert.AreEqual(0, m.Stock.FoodOutDays);
        }

        [Test]
        public void 数日進めるは少なくとも一晩は進み_月末で止まる()
        {
            var m = NewMonth(interval: 7);
            for (int i = 0; i < 28; i++) m.EndDay();
            Assert.AreEqual(29, m.Day);
            Assert.AreEqual(2, m.NightsLeft);
            var reports = m.AdvanceDays(5);
            Assert.AreEqual(2, reports.Count);
            Assert.IsTrue(m.IsOver);
            Assert.AreEqual(0, m.NightsLeft);
        }

        [Test]
        public void 毎日買い物できても選んだ日数だけ進む()
        {
            var m = NewMonth(interval: 1);
            Shop(m, Rank.Great, 1000, new[] { Food("rice", 2480, 10), Daily("soap", 498, 10) });
            Assert.AreEqual(3, m.SuggestedNights);
            var reports = m.AdvanceDays(4);
            Assert.AreEqual(4, reports.Count);
            Assert.AreEqual(5, m.Day);
        }

        [Test]
        public void すでに在庫切れなら数日進めるは止まらない()
        {
            var m = NewMonth(interval: 7);
            Assert.AreEqual(3, m.AdvanceDays(10).Count); // 食料3日分で止まる
            Assert.AreEqual(0, m.Stock.Food);
            Assert.AreEqual(2, m.AdvanceDays(2).Count);
            Assert.AreEqual(2, m.Stock.FoodOutDays);
            Assert.Throws<ArgumentOutOfRangeException>(() => m.AdvanceDays(0));
        }

        // ---------------- お金 ----------------

        [Test]
        public void 月初に予算が財布に入り_ロックは財布から移るだけ()
        {
            var m = NewMonth(budget: 19000);
            Assert.AreEqual(19000, m.Wallet.Cash);
            m.Lock(5000);
            Assert.AreEqual(14000, m.Wallet.Cash);
            Assert.AreEqual(5000, m.Wallet.Locked);
            Assert.IsTrue(m.Wallet.IsBalanced);
            Assert.Throws<ArgumentException>(() => m.Lock(20000));
        }

        [Test]
        public void 一か月をランダムに千回動かしてもお金の式が成り立つ()
        {
            var rnd = new Random(12345);
            var shelf = new[] { Food("milk", 238, 1), Food("rice", 2480, 10), Daily("soap", 498, 10), Treat("chips", 148, 3), Treat("bag", 9800, 20) };
            var ranks = new[] { (Rank.Miss, 0), (Rank.Good, 500), (Rank.Great, 1000), (Rank.Excellent, 1500), (Rank.Perfect, 2000) };
            int months = 0;
            var m = NewMonth(interval: 1, budget: 19000);
            for (int step = 0; step < 1000; step++)
            {
                if (m.IsOver)
                {
                    m = NewMonth(interval: 1 + rnd.Next(7), budget: 19000);
                    months++;
                }
                switch (rnd.Next(4))
                {
                    case 0:
                        if (m.Wallet.Cash > 0) m.Lock(rnd.Next(m.Wallet.Cash + 1) / 4);
                        break;
                    case 1:
                        if (!m.CanShop) break;
                        var cart = new Cart(12);
                        int n = 1 + rnd.Next(6);
                        for (int i = 0; i < n; i++) cart.Add(shelf[rnd.Next(shelf.Length)], rnd.Next(2) == 0 ? 0 : 1000);
                        var c = new Checkout(cart.Lines, m.Rule);
                        for (int i = 0; i < c.Lines.Count; i++)
                        {
                            var (rank, bp) = ranks[rnd.Next(ranks.Length)];
                            c.Record(i, rank, bp);
                        }
                        // 足りなければ高い物から返品する
                        while (c.Shortage(m.Wallet.Cash) > 0)
                        {
                            int worst = -1;
                            for (int i = 0; i < c.Lines.Count; i++)
                            {
                                if (c.Lines[i].Returned) continue;
                                if (worst < 0 || c.Lines[i].Price > c.Lines[worst].Price) worst = i;
                            }
                            c.ToggleReturn(worst);
                        }
                        int spentBefore = m.Wallet.TotalSpent;
                        var receipt = c.Pay(m.Wallet);
                        m.ApplyShopping(receipt);
                        Assert.AreEqual(spentBefore + receipt.Paid, m.Wallet.TotalSpent);
                        break;
                    case 2:
                        m.EndDay();
                        break;
                    default:
                        if (!m.IsOver) m.AdvanceDays(rnd.Next(1, 8));
                        break;
                }
                Assert.IsTrue(m.Wallet.IsBalanced, $"step {step}");
                Assert.AreEqual(m.Wallet.TotalSpent, m.Ledger.TotalPaid, $"step {step}: 家計簿の合計と支出合計が合わない");
                Assert.IsTrue(m.Satisfaction.Value >= 0 && m.Satisfaction.Value <= 100);
                Assert.IsTrue(m.Stock.Food >= 0 && m.Stock.Food <= m.Rule.foodCap);
            }
            Assert.IsTrue(months > 0, "月をまたいで試している");
        }

        // ---------------- セーブ ----------------

        [Test]
        public void セーブして戻すと同じ状態になる()
        {
            var m = NewMonth(interval: 3);
            m.Lock(3000);
            Shop(m, Rank.Excellent, 1500, new[] { Food("egg", 248, 2), Treat("chips", 148, 3) });
            m.EndDay();
            m.EndDay();
            for (int i = 0; i < 4; i++) m.EndDay();

            var save = m.ToSave();
            var r = MonthState.FromSave(save, m.Life, m.Rule);
            Assert.AreEqual(m.Day, r.Day);
            Assert.AreEqual(m.IsOver, r.IsOver);
            Assert.AreEqual(m.LastShopDay, r.LastShopDay);
            Assert.AreEqual(m.CanShop, r.CanShop);
            Assert.AreEqual(m.Wallet.Cash, r.Wallet.Cash);
            Assert.AreEqual(m.Wallet.Locked, r.Wallet.Locked);
            Assert.AreEqual(m.Wallet.TotalIncome, r.Wallet.TotalIncome);
            Assert.AreEqual(m.Wallet.TotalSpent, r.Wallet.TotalSpent);
            Assert.AreEqual(m.Stock.Food, r.Stock.Food);
            Assert.AreEqual(m.Stock.Daily, r.Stock.Daily);
            Assert.AreEqual(m.Stock.FoodOutDays, r.Stock.FoodOutDays);
            Assert.AreEqual(m.Satisfaction.Value, r.Satisfaction.Value);
            Assert.AreEqual(m.Ledger.Entries.Count, r.Ledger.Entries.Count);
            Assert.AreEqual(m.Ledger.TotalSaved, r.Ledger.TotalSaved);

            // 同じセーブからは同じ展開になる
            var a = m.AdvanceDays(3);
            var b = r.AdvanceDays(3);
            Assert.AreEqual(a.Count, b.Count);
            Assert.AreEqual(m.Satisfaction.Value, r.Satisfaction.Value);
        }

        [Test]
        public void お金の合わないセーブや未来の版は読まない()
        {
            var m = NewMonth();
            var save = m.ToSave();
            save.cash += 100;
            Assert.Throws<ArgumentException>(() => MonthState.FromSave(save, m.Life, m.Rule));

            var future = m.ToSave();
            future.version = SaveData.CurrentVersion + 1;
            Assert.Throws<ArgumentException>(() => MonthState.FromSave(future, m.Life, m.Rule));
        }

        // ---------------- データ ----------------

        [Test]
        public void 曜日の特売はその曜日だけ効く()
        {
            var store = new StoreData
            {
                id = "s",
                name = "s",
                sales = new[]
                {
                    new SaleData { itemId = "egg", discountBp = 2000, days = "火" },
                    new SaleData { itemId = "egg", discountBp = 1000, days = "土日" },
                    new SaleData { itemId = "milk", discountBp = 500, days = "" },
                },
            };
            Assert.AreEqual(2000, store.SaleBp("egg", 1));
            Assert.AreEqual(0, store.SaleBp("egg", 0));
            Assert.AreEqual(1000, store.SaleBp("egg", 6));
            Assert.AreEqual(500, store.SaleBp("milk", 3));
            Assert.AreEqual(500, store.SaleBp("milk"));
            Assert.AreEqual(0, store.SaleBp("egg"));
            Assert.AreEqual(2, store.SalesOn(1).Count);
        }

        [Test]
        public void 特売の曜日の書き間違いと重複を見つける()
        {
            var items = new ItemList { items = new[] { Food("egg", 248, 2) } };
            var stores = new StoreList
            {
                stores = new[]
                {
                    new StoreData
                    {
                        id = "s",
                        name = "s",
                        sales = new[]
                        {
                            new SaleData { itemId = "egg", discountBp = 2000, days = "火" },
                            new SaleData { itemId = "egg", discountBp = 1000, days = "火水" },
                            new SaleData { itemId = "egg", discountBp = 1000, days = "X" },
                        },
                    },
                },
            };
            var errors = DataValidator.ValidateStores(stores, items);
            Assert.IsTrue(errors.Exists(e => e.Contains("重複")), string.Join("\n", errors));
            Assert.IsTrue(errors.Exists(e => e.Contains("曜日が不明")), string.Join("\n", errors));
        }

        [Test]
        public void 一か月の数値のチェック()
        {
            Assert.IsEmpty(DataValidator.ValidateLife(new LifeBalance(), new ShoppingBalance()));
            var bad = new LifeBalance();
            bad.month.budget = 0;
            bad.stock.foodStart = 20;
            bad.satisfaction.start = 150;
            var errors = DataValidator.ValidateLife(bad, new ShoppingBalance());
            Assert.IsTrue(errors.Exists(e => e.Contains("budget")));
            Assert.IsTrue(errors.Exists(e => e.Contains("食料の初期値")));
            Assert.IsTrue(errors.Exists(e => e.Contains("satisfaction.start")));
        }
    }
}
