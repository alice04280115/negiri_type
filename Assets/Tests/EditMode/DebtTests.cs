using System;
using System.Collections.Generic;
using Negiri.Core.Data;
using Negiri.Core.Economy;
using Negiri.Core.Life;
using Negiri.Core.Save;
using Negiri.Core.Shopping;
using Negiri.Core.Typing;
using NUnit.Framework;

namespace Negiri.Core.Tests
{
    /// <summary>借金（段階3-2）：カードローン、借りて払う、返済、利息、セーブ、お金の式。</summary>
    public class DebtTests
    {
        static ItemData Item(string id, int price) => new ItemData { id = id, name = id, kind = "essential", basePrice = price, effects = new[] { new ItemEffect { stat = "food", amount = 1 } } };

        static ShoppingRule Rule(ShortageRule shortage, string borrowFrom = "card")
        {
            return new ShoppingRule { label = "test", maxItems = 12, shortage = shortage.ToString(), satisfactionPerReturn = 3, shopIntervalDays = 1, foodCap = 99, borrowFrom = borrowFrom };
        }

        static MonthState NewMonth(int budget, ShortageRule shortage = ShortageRule.AutoReturnHighestFirst, string borrowFrom = "card", int limit = 10000)
        {
            var life = new LifeBalance();
            life.month.budget = budget;
            life.debt.lenders[0].limit = limit;
            return MonthState.Start(life, Rule(shortage, borrowFrom), 2);
        }

        /// <summary>全品 MISS（割引なし）で打ち終えたレジ。</summary>
        static Checkout Scanned(MonthState m, params int[] prices)
        {
            var cart = new Cart(12);
            for (int i = 0; i < prices.Length; i++) cart.Add(Item("p" + i, prices[i]), 0);
            var c = new Checkout(cart.Lines, m.Rule);
            for (int i = 0; i < c.Lines.Count; i++) c.Record(i, Rank.Miss, 0);
            return c;
        }

        static Receipt ShopWithLoan(MonthState m, params int[] prices)
        {
            var c = Scanned(m, prices);
            var r = c.PayWithLoan(m.Wallet, m.Debt, m.Day);
            m.ApplyShopping(r);
            return r;
        }

        // ---------------- 借りて払う ----------------

        [Test]
        public void 借りるのは不足分ちょうどで_財布は0になる()
        {
            var m = NewMonth(1000);
            var r = ShopWithLoan(m, 800, 700);
            Assert.AreEqual(500, r.Borrowed);
            Assert.AreEqual(1500, r.Paid);
            Assert.AreEqual(1000, r.CashBefore);
            Assert.AreEqual(0, r.CashAfter);
            Assert.AreEqual(0, r.ReturnCount);
            Assert.AreEqual("カードローン", r.LenderName);
            Assert.AreEqual(500, r.DebtAfter);
            Assert.AreEqual(500, m.Debt.Total);
            Assert.AreEqual(0, m.Wallet.Cash);
            Assert.IsTrue(m.IsBalanced);
        }

        [Test]
        public void 足りていれば借りずに払う()
        {
            var m = NewMonth(3000);
            var r = ShopWithLoan(m, 800, 700);
            Assert.AreEqual(0, r.Borrowed);
            Assert.AreEqual(0, m.Debt.Total);
            Assert.AreEqual(1500, m.Wallet.Cash);
        }

        [Test]
        public void 限度額を超える分は先に高い順に返品してから借りる()
        {
            // 財布1,000円、限度3,000円。合計 3,000＋1,500＋500＝5,000円 → 3,000円の商品を返品して 2,000円 → 1,000円借りる
            var m = NewMonth(1000, ShortageRule.AutoReturnHighestFirst, limit: 3000);
            var r = ShopWithLoan(m, 500, 3000, 1500);
            Assert.AreEqual(1, r.ReturnCount);
            Assert.IsTrue(r.Lines[1].Returned);
            Assert.AreEqual(1000, r.Borrowed);
            Assert.AreEqual(1000, m.Debt.Total);
            Assert.IsTrue(m.IsBalanced);
        }

        [Test]
        public void 達人は限度額を超える分を安い順に返品してから借りる()
        {
            // 財布1,000円、限度3,000円。合計5,000円 → 500円、1,500円の順に返品して 3,000円 → 2,000円借りる
            var m = NewMonth(1000, ShortageRule.AutoReturnLowestFirst, limit: 3000);
            var r = ShopWithLoan(m, 500, 3000, 1500);
            Assert.AreEqual(2, r.ReturnCount);
            Assert.IsTrue(r.Lines[0].Returned);
            Assert.IsTrue(r.Lines[2].Returned);
            Assert.AreEqual(2000, r.Borrowed);
        }

        [Test]
        public void 限度額を超えて借りられない()
        {
            var m = NewMonth(0 + 1, limit: 2000);
            ShopWithLoan(m, 1501);
            Assert.AreEqual(1500, m.Debt.Total);
            Assert.AreEqual(500, m.Debt.Room("card"));
            m.EndDay();
            var r = ShopWithLoan(m, 400, 300);
            // 700円のうち借りられるのは500円まで → 安い物を残すため高い順に400円を返品して、300円を借りる
            Assert.AreEqual(1, r.ReturnCount);
            Assert.AreEqual(300, r.Borrowed);
            Assert.AreEqual(1800, m.Debt.Total);
            Assert.Throws<InvalidOperationException>(() => m.Debt.Borrow("card", 201, m.Day));
            Assert.IsTrue(m.IsBalanced);
        }

        [Test]
        public void 初級と中級では買い物で借りられない()
        {
            var m = NewMonth(1000, ShortageRule.ChooseReturn, borrowFrom: "");
            var c = Scanned(m, 1500);
            Assert.IsFalse(m.Rule.CanBorrow);
            Assert.Throws<InvalidOperationException>(() => c.PayWithLoan(m.Wallet, m.Debt, m.Day));
            Assert.AreEqual(0, m.Debt.Total);

            // 実際のデータでも、借りられるのは上級と達人だけ
            var rules = ShoppingBalance.DefaultRules();
            Assert.IsFalse(rules[0].CanBorrow);
            Assert.IsFalse(rules[1].CanBorrow);
            Assert.IsTrue(rules[2].CanBorrow);
            Assert.IsTrue(rules[3].CanBorrow);
        }

        // ---------------- 利息と返済 ----------------

        [Test]
        public void 利息は月末に借金だけを増やし_財布は変わらない()
        {
            var m = NewMonth(1000);
            ShopWithLoan(m, 2234); // 1,234円借りる
            int cash = m.Wallet.Cash;
            while (m.Day < m.Days)
            {
                var night = m.EndDay();
                Assert.AreEqual(0, night.Interest, "月末までは利息がつかない");
            }
            var last = m.EndDay();
            Assert.IsTrue(last.MonthOver);
            // 1,234 × 1.5% ＝ 18.51 → 切り上げて19円
            Assert.AreEqual(19, last.Interest);
            Assert.AreEqual(1253, last.DebtAfter);
            Assert.AreEqual(1253, m.Debt.Total);
            Assert.AreEqual(cash, m.Wallet.Cash);
            Assert.IsTrue(m.IsBalanced);
        }

        [Test]
        public void 借金がなければ利息もつかない()
        {
            var m = NewMonth(19000);
            for (int i = 0; i < m.Days; i++) m.EndDay();
            Assert.AreEqual(0, m.Debt.Total);
            Assert.AreEqual(0, m.Debt.Log.Count);
        }

        [Test]
        public void 返済で財布と借金が同じ額だけ減る()
        {
            var m = NewMonth(1000);
            ShopWithLoan(m, 4500); // 3,500円借りる
            m.Wallet.Receive(5000); // テスト用に財布を足す
            CollectionAssert.AreEqual(new[] { 1000, 2000, 3000, 3500 }, m.RepayChoices("card"));

            m.Repay("card", 2000);
            Assert.AreEqual(3000, m.Wallet.Cash);
            Assert.AreEqual(1500, m.Debt.Total);
            m.Repay("card", 1500);
            Assert.AreEqual(1500, m.Wallet.Cash);
            Assert.AreEqual(0, m.Debt.Total);
            Assert.AreEqual(3500, m.Wallet.TotalRepaid);
            Assert.IsTrue(m.IsBalanced);
            Assert.IsEmpty(m.RepayChoices("card"));
        }

        [Test]
        public void 返済は単位か全額で_財布より多くは返せない()
        {
            var m = NewMonth(1000);
            ShopWithLoan(m, 4500); // 3,500円借りる、財布0
            Assert.IsEmpty(m.RepayChoices("card"), "財布が0なら返せない");
            m.Wallet.Receive(2500);
            CollectionAssert.AreEqual(new[] { 1000, 2000 }, m.RepayChoices("card"), "全額（3,500円）は財布が足りない");
            Assert.Throws<ArgumentException>(() => m.Repay("card", 1500));
            Assert.Throws<ArgumentException>(() => m.Repay("card", 3000));
            Assert.AreEqual(3500, m.Debt.Total);
        }

        [Test]
        public void 初めからある借金は財布に入らず_限度額に数える()
        {
            var m = NewMonth(5000, ShortageRule.ChooseReturn, borrowFrom: "");
            m.Debt.SetInitial("card", 4000);
            Assert.AreEqual(4000, m.Debt.Total);
            Assert.AreEqual(6000, m.Debt.Room("card"));
            Assert.AreEqual(5000, m.Wallet.Cash);
            Assert.IsTrue(m.IsBalanced);
            m.Repay("card", 1000);
            Assert.AreEqual(3000, m.Debt.Total);
            Assert.AreEqual(4000, m.Wallet.Cash);
            Assert.IsTrue(m.IsBalanced);
        }

        // ---------------- セーブ ----------------

        [Test]
        public void 借金もセーブして戻すと同じになる()
        {
            var m = NewMonth(1000);
            ShopWithLoan(m, 3000);
            m.Wallet.Receive(1000);
            m.Repay("card", 1000);
            var r = MonthState.FromSave(m.ToSave(), m.Life, m.Rule);
            Assert.AreEqual(m.Debt.Total, r.Debt.Total);
            Assert.AreEqual(m.Debt.TotalBorrowed, r.Debt.TotalBorrowed);
            Assert.AreEqual(m.Debt.TotalRepaid, r.Debt.TotalRepaid);
            Assert.AreEqual(m.Debt.Log.Count, r.Debt.Log.Count);
            Assert.AreEqual(m.Wallet.TotalBorrowed, r.Wallet.TotalBorrowed);
            Assert.AreEqual(m.Wallet.TotalRepaid, r.Wallet.TotalRepaid);
            Assert.AreEqual(m.Debt.Room("card"), r.Debt.Room("card"));
            Assert.IsTrue(r.IsBalanced);
        }

        [Test]
        public void 借金のない古いセーブも読める()
        {
            var m = NewMonth(19000);
            var save = m.ToSave();
            save.version = 1;
            save.totalBorrowed = 0;
            save.totalRepaid = 0;
            save.debts = null;
            save.debtLog = null;
            var r = MonthState.FromSave(save, m.Life, m.Rule);
            Assert.AreEqual(0, r.Debt.Total);
            Assert.AreEqual(19000, r.Wallet.Cash);
            Assert.IsTrue(r.IsBalanced);
        }

        [Test]
        public void 借金の記録が合わないセーブは読まない()
        {
            var m = NewMonth(1000);
            ShopWithLoan(m, 3000);
            var save = m.ToSave();
            save.debts[0].borrowed += 100;
            Assert.Throws<ArgumentException>(() => MonthState.FromSave(save, m.Life, m.Rule));
        }

        // ---------------- 通し ----------------

        [Test]
        public void 借入と返済を混ぜて一か月をランダムに千回動かしてもお金の式が成り立つ()
        {
            var rnd = new Random(777);
            var prices = new[] { 148, 238, 498, 980, 2480, 4980 };
            var ranks = new[] { (Rank.Miss, 0), (Rank.Great, 1000), (Rank.Perfect, 2000) };
            var shortages = new[] { ShortageRule.AutoReturnHighestFirst, ShortageRule.AutoReturnLowestFirst };
            int months = 0, borrowed = 0, repaid = 0, interest = 0;
            var m = NewMonth(19000, shortages[0]);
            for (int step = 0; step < 1000; step++)
            {
                if (m.IsOver)
                {
                    m = NewMonth(2000 + rnd.Next(20000), shortages[rnd.Next(2)], limit: 1000 + rnd.Next(12000));
                    months++;
                }
                switch (rnd.Next(5))
                {
                    case 0:
                        if (m.MaxLockToday > 0) m.Lock(rnd.Next(m.MaxLockToday + 1));
                        break;
                    case 1:
                    case 2:
                    {
                        if (!m.CanShop) break;
                        var cart = new Cart(12);
                        int n = 1 + rnd.Next(8);
                        for (int i = 0; i < n; i++) cart.Add(Item("x" + i, prices[rnd.Next(prices.Length)]), rnd.Next(2) == 0 ? 0 : 1000);
                        var c = new Checkout(cart.Lines, m.Rule);
                        for (int i = 0; i < c.Lines.Count; i++)
                        {
                            var (rank, bp) = ranks[rnd.Next(ranks.Length)];
                            c.Record(i, rank, bp);
                        }
                        int room = m.Debt.Room("card");
                        int debtBefore = m.Debt.Total;
                        Receipt receipt;
                        if (rnd.Next(2) == 0)
                        {
                            receipt = c.PayWithLoan(m.Wallet, m.Debt, m.Day);
                            Assert.IsTrue(receipt.Borrowed <= room, $"step {step}: 限度額を超えて借りた");
                            Assert.AreEqual(debtBefore + receipt.Borrowed, m.Debt.Total);
                            if (receipt.Borrowed > 0) Assert.AreEqual(0, receipt.CashAfter, $"step {step}: 借りたのに財布が残っている");
                            borrowed += receipt.Borrowed;
                        }
                        else
                        {
                            c.ApplyAutoReturn(m.Wallet.Cash);
                            receipt = c.Pay(m.Wallet);
                            Assert.AreEqual(0, receipt.Borrowed);
                        }
                        m.ApplyShopping(receipt);
                        break;
                    }
                    case 3:
                    {
                        // 借りると財布は0になるので、ときどき臨時収入を入れて返せるようにする
                        if (m.Debt.Total > 0 && m.Wallet.Cash < m.Debt.RepayStep) m.Wallet.Receive(rnd.Next(500, 5000));
                        var choices = m.RepayChoices("card");
                        if (choices.Count == 0) break;
                        int amount = choices[rnd.Next(choices.Count)];
                        int cash = m.Wallet.Cash, debt = m.Debt.Total;
                        m.Repay("card", amount);
                        Assert.AreEqual(cash - amount, m.Wallet.Cash);
                        Assert.AreEqual(debt - amount, m.Debt.Total);
                        repaid += amount;
                        break;
                    }
                    default:
                    {
                        int cash = m.Wallet.Cash;
                        var nights = m.AdvanceDays(rnd.Next(1, 8));
                        foreach (var night in nights) interest += night.Interest;
                        Assert.AreEqual(cash, m.Wallet.Cash, $"step {step}: 夜に財布が変わった");
                        break;
                    }
                }
                Assert.IsTrue(m.IsBalanced, $"step {step}: お金の式が成り立たない");
                Assert.IsTrue(m.Wallet.Cash >= 0 && m.Debt.Total >= 0);
                var lender = m.Debt.Lender("card");
                Assert.IsTrue(m.Debt.TotalBorrowed <= lender.limit + m.Debt.TotalRepaid, $"step {step}: 限度額を超えて借りた");
                Assert.AreEqual(m.Wallet.TotalSpent, m.Ledger.TotalPaid, $"step {step}: 家計簿の合計と支出合計が合わない");
            }
            Assert.IsTrue(months > 0, "月をまたいで試している");
            Assert.IsTrue(borrowed > 0 && repaid > 0 && interest > 0, $"借入・返済・利息をすべて試している（借入 {borrowed}・返済 {repaid}・利息 {interest}・{months}か月）");
        }

        // ---------------- データ ----------------

        [Test]
        public void 借金の数値のチェック()
        {
            Assert.IsEmpty(DataValidator.ValidateLife(new LifeBalance(), new ShoppingBalance()));

            var life = new LifeBalance();
            life.debt.repayStep = 0;
            life.debt.lenders = new[] { new LenderData { id = "card", name = "", limit = 0, interestBp = -1 }, new LenderData { id = "card" } };
            var shopping = new ShoppingBalance();
            shopping.difficulties[2].borrowFrom = "family";
            var errors = DataValidator.ValidateLife(life, shopping);
            Assert.IsTrue(errors.Exists(e => e.Contains("repayStep")));
            Assert.IsTrue(errors.Exists(e => e.Contains("重複")));
            Assert.IsTrue(errors.Exists(e => e.Contains("name")));
            Assert.IsTrue(errors.Exists(e => e.Contains("limit")));
            Assert.IsTrue(errors.Exists(e => e.Contains("interestBp")));
            Assert.IsTrue(errors.Exists(e => e.Contains("family")));
        }
    }
}
