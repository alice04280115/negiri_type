using System;
using System.Collections.Generic;
using Negiri.Core.Data;
using Negiri.Core.Economy;
using Negiri.Core.Shopping;
using Negiri.Core.Typing;
using NUnit.Framework;

namespace Negiri.Core.Tests
{
    public class ShoppingTests
    {
        const int PerfectBp = 2000;

        static ItemData Item(string id, int price) => new ItemData { id = id, name = id, kind = "essential", basePrice = price };

        static ShoppingRule Rule(ShortageRule shortage, int maxItems = 12, int penalty = 3)
        {
            return new ShoppingRule { label = shortage.ToString(), maxItems = maxItems, shortage = shortage.ToString(), satisfactionPerReturn = penalty };
        }

        /// <summary>カートの商品を全部同じ評価で打ったことにする。</summary>
        static Checkout ScanAll(Cart cart, ShoppingRule rule, Rank rank, int typingBp)
        {
            var c = new Checkout(cart.Lines, rule);
            for (int i = 0; i < c.Lines.Count; i++) c.Record(i, rank, typingBp);
            return c;
        }

        // ---------------- カート ----------------

        [Test]
        public void 品数の上限を超えてカートに入れられない()
        {
            var cart = new Cart(2);
            Assert.IsTrue(cart.Add(Item("a", 100), 0));
            Assert.IsTrue(cart.Add(Item("b", 100), 0));
            Assert.IsTrue(cart.IsFull);
            Assert.IsFalse(cart.Add(Item("c", 100), 0));
            Assert.AreEqual(2, cart.Count);

            cart.RemoveAt(0);
            Assert.IsTrue(cart.Add(Item("c", 100), 0));
        }

        [Test]
        public void カートの合計は財布で制限しない()
        {
            var cart = new Cart(12);
            Assert.IsTrue(cart.Add(Item("rice", 2480), 0));
            Assert.IsTrue(cart.Add(Item("rice", 2480), 0));
            Assert.AreEqual(4960, cart.BaseTotal);
            Assert.AreEqual(1960, cart.WorstShortage(3000));
            Assert.AreEqual(0, cart.WorstShortage(5000));
        }

        [Test]
        public void 支払額の幅は1品ずつ計算した合計と一致する()
        {
            var cart = new Cart(12);
            cart.Add(Item("milk", 238), 0);
            cart.Add(Item("egg", 248), 2000);
            cart.Add(Item("natto", 98), 1000);

            int best = Pricing.Price(238, PerfectBp, 0, 5000) + Pricing.Price(248, PerfectBp, 2000, 5000) + Pricing.Price(98, PerfectBp, 1000, 5000);
            int worst = Pricing.Price(238, 0, 0, 5000) + Pricing.Price(248, 0, 2000, 5000) + Pricing.Price(98, 0, 1000, 5000);
            Assert.AreEqual(best, cart.BestTotal(PerfectBp));
            Assert.AreEqual(worst, cart.WorstTotal);

            // 全部その評価で打ったときの会計とも一致する
            Assert.AreEqual(best, ScanAll(cart, Rule(ShortageRule.ChooseReturn), Rank.Perfect, PerfectBp).Total);
            Assert.AreEqual(worst, ScanAll(cart, Rule(ShortageRule.ChooseReturn), Rank.Miss, 0).Total);
        }

        [Test]
        public void 特売とタイピング割引の合算は上限50パーセントで止まる()
        {
            var cart = new Cart(12);
            cart.Add(Item("a", 1000), 4000);
            var c = ScanAll(cart, Rule(ShortageRule.ChooseReturn), Rank.Perfect, PerfectBp);
            Assert.AreEqual(5000, c.Lines[0].DiscountBp);
            Assert.AreEqual(500, c.Total);
            Assert.AreEqual(500, c.Lines[0].Saved);
        }

        // ---------------- 会計 ----------------

        [Test]
        public void 全品打ち終えるまで会計できない()
        {
            var cart = new Cart(12);
            cart.Add(Item("a", 100), 0);
            cart.Add(Item("b", 100), 0);
            var c = new Checkout(cart.Lines, Rule(ShortageRule.ChooseReturn));
            c.Record(0, Rank.Great, 1000);
            Assert.AreEqual(1, c.NextToScan);
            Assert.IsFalse(c.AllScanned);
            Assert.Throws<InvalidOperationException>(() => c.Pay(new Wallet(1000)));
        }

        [Test]
        public void 財布が足りれば払えてお金の式が成り立つ()
        {
            var cart = new Cart(12);
            cart.Add(Item("a", 1000), 0);
            cart.Add(Item("b", 500), 1000);
            var c = ScanAll(cart, Rule(ShortageRule.ChooseReturn), Rank.Great, 1000);
            var w = new Wallet(3000);

            var r = c.Pay(w);
            Assert.AreEqual(900 + 400, r.Paid);
            Assert.AreEqual(3000, r.CashBefore);
            Assert.AreEqual(1700, r.CashAfter);
            Assert.AreEqual(1700, w.Cash);
            Assert.AreEqual(1500, r.BaseTotal);
            Assert.AreEqual(200, r.Saved);
            Assert.AreEqual(0, r.ReturnCount);
            Assert.IsTrue(w.IsBalanced);
            Assert.IsTrue(c.IsPaid);
            Assert.Throws<InvalidOperationException>(() => c.Pay(w));
        }

        [Test]
        public void 足りないときは返品して財布以内にするまで払えない()
        {
            var cart = new Cart(12);
            cart.Add(Item("a", 2000), 0);
            cart.Add(Item("b", 1500), 0);
            var c = ScanAll(cart, Rule(ShortageRule.ChooseReturn, penalty: 3), Rank.Miss, 0);
            var w = new Wallet(3000);

            Assert.AreEqual(500, c.Shortage(w.Cash));
            Assert.Throws<InvalidOperationException>(() => c.Pay(w));
            Assert.AreEqual(3000, w.Cash);
            Assert.IsTrue(w.IsBalanced);

            c.ToggleReturn(1);
            Assert.AreEqual(0, c.Shortage(w.Cash));
            var r = c.Pay(w);
            Assert.AreEqual(2000, r.Paid);
            Assert.AreEqual(1, r.ReturnCount);
            Assert.AreEqual(3, r.SatisfactionLoss);
            Assert.AreEqual(2000, r.BaseTotal);
            Assert.IsTrue(w.IsBalanced);
        }

        [Test]
        public void 返品した商品は支出にも節約額にも入らない()
        {
            var cart = new Cart(12);
            cart.Add(Item("a", 1000), 0);
            cart.Add(Item("b", 1000), 0);
            var c = ScanAll(cart, Rule(ShortageRule.ChooseReturn), Rank.Perfect, PerfectBp);
            c.ToggleReturn(0);
            var w = new Wallet(3000);
            var r = c.Pay(w);
            Assert.AreEqual(800, r.Paid);
            Assert.AreEqual(200, r.Saved);
            Assert.AreEqual(0, c.Lines[0].Saved);
            Assert.AreEqual(2200, w.Cash);
            Assert.IsTrue(w.IsBalanced);
        }

        [Test]
        public void 返品は取り消せる()
        {
            var cart = new Cart(12);
            cart.Add(Item("a", 1000), 0);
            var c = ScanAll(cart, Rule(ShortageRule.ChooseReturn), Rank.Miss, 0);
            c.ToggleReturn(0);
            Assert.AreEqual(0, c.Total);
            c.ToggleReturn(0);
            Assert.AreEqual(1000, c.Total);
            Assert.AreEqual(0, c.ReturnCount);
        }

        [Test]
        public void 全品返品もできる()
        {
            var cart = new Cart(12);
            cart.Add(Item("a", 5000), 0);
            cart.Add(Item("b", 4000), 0);
            var c = ScanAll(cart, Rule(ShortageRule.ChooseReturn, penalty: 0), Rank.Miss, 0);
            c.ToggleReturn(0);
            c.ToggleReturn(1);
            var w = new Wallet(3000);
            var r = c.Pay(w);
            Assert.AreEqual(0, r.Paid);
            Assert.AreEqual(0, r.Saved);
            Assert.AreEqual(3000, w.Cash);
            Assert.AreEqual(0, r.SatisfactionLoss);
            Assert.IsTrue(w.IsBalanced);
        }

        [Test]
        public void 自動返品の難易度では自分で返品を選べない()
        {
            var cart = new Cart(12);
            cart.Add(Item("a", 1000), 0);
            var c = ScanAll(cart, Rule(ShortageRule.AutoReturnHighestFirst), Rank.Miss, 0);
            Assert.Throws<InvalidOperationException>(() => c.ToggleReturn(0));

            var manual = ScanAll(cart, Rule(ShortageRule.ChooseReturn), Rank.Miss, 0);
            Assert.Throws<InvalidOperationException>(() => manual.ApplyAutoReturn(0));
        }

        // ---------------- 自動返品 ----------------

        static Cart SampleCart()
        {
            // 入れた順：牛乳238、お米2480、納豆98、卵248、豚肉398 → 全部 MISS で 3462円
            var cart = new Cart(12);
            cart.Add(Item("milk", 238), 0);
            cart.Add(Item("rice", 2480), 0);
            cart.Add(Item("natto", 98), 0);
            cart.Add(Item("egg", 248), 0);
            cart.Add(Item("pork", 398), 0);
            return cart;
        }

        [Test]
        public void 上級は支払額の高い順に財布以内になるまで返品する()
        {
            var c = ScanAll(SampleCart(), Rule(ShortageRule.AutoReturnHighestFirst), Rank.Miss, 0);
            var w = new Wallet(3000);

            var plan = c.PlanAutoReturn(w.Cash);
            CollectionAssert.AreEqual(new[] { 1 }, plan); // お米だけで足りる
            Assert.AreEqual(0, c.ReturnCount, "計画しただけでは返品しない");

            c.ApplyAutoReturn(w.Cash);
            var r = c.Pay(w);
            Assert.AreEqual(982, r.Paid);
            Assert.AreEqual(1, r.ReturnCount);
            Assert.AreEqual(3, r.SatisfactionLoss);
            Assert.IsTrue(w.IsBalanced);
        }

        [Test]
        public void 達人は支払額の安い順に財布以内になるまで返品する()
        {
            var c = ScanAll(SampleCart(), Rule(ShortageRule.AutoReturnLowestFirst), Rank.Miss, 0);
            var w = new Wallet(3000);

            // 不足 462円 → 納豆98、牛乳238、卵248 で 584円戻して足りる
            var plan = c.ApplyAutoReturn(w.Cash);
            CollectionAssert.AreEqual(new[] { 2, 0, 3 }, plan);
            var r = c.Pay(w);
            Assert.AreEqual(2878, r.Paid);
            Assert.AreEqual(3, r.ReturnCount);
            Assert.AreEqual(9, r.SatisfactionLoss);
            Assert.IsTrue(w.IsBalanced);
        }

        [Test]
        public void 自動返品の順番は割引後の支払額で決まる()
        {
            var cart = new Cart(12);
            cart.Add(Item("a", 1000), 0);
            cart.Add(Item("b", 900), 0);
            var c = new Checkout(cart.Lines, Rule(ShortageRule.AutoReturnHighestFirst));
            c.Record(0, Rank.Perfect, PerfectBp); // 1000 → 800
            c.Record(1, Rank.Miss, 0);            // 900
            CollectionAssert.AreEqual(new[] { 1 }, c.PlanAutoReturn(1000));
        }

        [Test]
        public void 同じ金額なら後からカートに入れた商品から返品する()
        {
            var cart = new Cart(12);
            cart.Add(Item("first", 500), 0);
            cart.Add(Item("second", 500), 0);
            cart.Add(Item("third", 500), 0);

            var high = ScanAll(cart, Rule(ShortageRule.AutoReturnHighestFirst), Rank.Miss, 0);
            CollectionAssert.AreEqual(new[] { 2, 1 }, high.PlanAutoReturn(500));

            var low = ScanAll(cart, Rule(ShortageRule.AutoReturnLowestFirst), Rank.Miss, 0);
            CollectionAssert.AreEqual(new[] { 2, 1 }, low.PlanAutoReturn(500));
        }

        [Test]
        public void 財布が足りていれば自動返品しない()
        {
            var c = ScanAll(SampleCart(), Rule(ShortageRule.AutoReturnLowestFirst), Rank.Miss, 0);
            Assert.IsEmpty(c.PlanAutoReturn(5000));
        }

        [Test]
        public void 財布が0円なら全品返品になる()
        {
            var c = ScanAll(SampleCart(), Rule(ShortageRule.AutoReturnHighestFirst), Rank.Miss, 0);
            var w = new Wallet(0);
            Assert.AreEqual(5, c.ApplyAutoReturn(w.Cash).Count);
            Assert.AreEqual(0, c.Pay(w).Paid);
            Assert.IsTrue(w.IsBalanced);
        }

        [Test]
        public void レジの見込みはまだ打っていない商品の幅()
        {
            var cart = new Cart(12);
            cart.Add(Item("a", 1000), 0);
            cart.Add(Item("b", 500), 0);
            var c = new Checkout(cart.Lines, Rule(ShortageRule.ChooseReturn));
            c.Record(0, Rank.Great, 1000);
            Assert.AreEqual(900, c.Total);
            var (best, worst) = c.RemainingRange(PerfectBp);
            Assert.AreEqual(400, best);
            Assert.AreEqual(500, worst);
            Assert.AreEqual(1, c.ScannedCount);
        }

        // ---------------- レシート ----------------

        [Test]
        public void レシートは特売後の価格とタイピング値引を分けて書く()
        {
            var cart = new Cart(12);
            cart.Add(Item("rice", 2480), 0);
            cart.Add(Item("egg", 248), 2000);
            cart.Add(Item("toilet_paper", 398), 0);
            cart.Add(Item("cabbage", 198), 1000);
            var c = new Checkout(cart.Lines, Rule(ShortageRule.AutoReturnHighestFirst));
            c.Record(0, Rank.Miss, 0);
            c.Record(1, Rank.Miss, 0);      // 特売だけ：198
            c.Record(2, Rank.Great, 1000);  // 398 → 358
            c.Record(3, Rank.Great, 1000);  // 特売10%＋GREAT10%：198 → 178（特売後 178.2 → 178、合算 20% → 158）
            var w = new Wallet(3000);
            c.ApplyAutoReturn(w.Cash);
            var receipt = c.Pay(w);

            var rows = ReceiptLines.Build(receipt);
            var items = rows.FindAll(r => r.Kind == ReceiptRowKind.Item);
            Assert.AreEqual(3, items.Count);
            Assert.AreEqual("egg", items[0].Left);
            Assert.AreEqual("特", items[0].Mark);
            Assert.AreEqual("¥198", items[0].Right);
            Assert.AreEqual("", items[1].Mark);
            Assert.AreEqual("¥398", items[1].Right);
            Assert.AreEqual("¥178", items[2].Right);

            var discounts = rows.FindAll(r => r.Kind == ReceiptRowKind.Discount);
            Assert.AreEqual(2, discounts.Count);
            Assert.AreEqual("-¥40", discounts[0].Right);
            Assert.AreEqual("-¥20", discounts[1].Right);
            Assert.AreEqual(1, rows.FindAll(r => r.Kind == ReceiptRowKind.Note).Count, "MISS は値引きなしの行");

            var total = rows.Find(r => r.Kind == ReceiptRowKind.Total);
            Assert.AreEqual(ReceiptLines.Yen(receipt.Paid), total.Right);
            Assert.AreEqual(198 + 358 + 158, receipt.Paid);

            var returned = rows.FindAll(r => r.Kind == ReceiptRowKind.Returned);
            Assert.AreEqual(1, returned.Count);
            Assert.AreEqual("rice", returned[0].Left);
        }

        [Test]
        public void レシートの小計からタイピング値引を引くと合計になる()
        {
            var cart = new Cart(12);
            cart.Add(Item("a", 1000), 4000); // 上限で止まる：特売40%＋PERFECT20% → 50%
            cart.Add(Item("b", 333), 0);
            var c = ScanAll(cart, Rule(ShortageRule.ChooseReturn), Rank.Perfect, PerfectBp);
            var receipt = c.Pay(new Wallet(3000));
            int sum = 0;
            foreach (var l in receipt.Lines) sum += ReceiptLines.ShelfPrice(l) - ReceiptLines.TypingDiscount(l);
            Assert.AreEqual(receipt.Paid, sum);
            Assert.AreEqual(100, ReceiptLines.TypingDiscount(receipt.Lines[0]), "上限で止まった分は値引に入らない");
        }

        // ---------------- 決まりとデータ ----------------

        [Test]
        public void 初期値の難易度ごとの決まり()
        {
            var rules = ShoppingBalance.DefaultRules();
            CollectionAssert.AreEqual(new[] { 4, 7, 12, 12 }, Array.ConvertAll(rules, r => r.maxItems));
            Assert.IsFalse(rules[0].IsAutoReturn);
            Assert.IsFalse(rules[1].IsAutoReturn);
            Assert.AreEqual(ShortageRule.AutoReturnHighestFirst, rules[2].ShortageValue);
            Assert.AreEqual(ShortageRule.AutoReturnLowestFirst, rules[3].ShortageValue);
            Assert.IsEmpty(DataValidator.ValidateShopping(new ShoppingBalance(), 4));
        }

        [Test]
        public void 買い物の数値の不備を見つける()
        {
            var b = new ShoppingBalance
            {
                startingCash = 0,
                difficulties = new[] { new ShoppingRule { label = "x", maxItems = 0, shortage = "Debt", satisfactionPerReturn = -1 } },
            };
            var errors = DataValidator.ValidateShopping(b, 4);
            Assert.AreEqual(5, errors.Count, string.Join("\n", errors));
        }

        [Test]
        public void 店舗データの不備を見つける()
        {
            var items = new ItemList { items = new[] { Item("milk", 238) } };
            var stores = new StoreList
            {
                stores = new[]
                {
                    new StoreData
                    {
                        id = "s", name = "店",
                        sales = new[]
                        {
                            new SaleData { itemId = "milk", discountBp = 1000 },
                            new SaleData { itemId = "milk", discountBp = 1000 },
                            new SaleData { itemId = "egg", discountBp = 0 },
                        },
                    },
                },
            };
            var errors = DataValidator.ValidateStores(stores, items);
            Assert.AreEqual(3, errors.Count, string.Join("\n", errors));
            Assert.AreEqual(1000, stores.stores[0].SaleBp("milk"));
            Assert.AreEqual(0, stores.stores[0].SaleBp("natto"));
        }

        [Test]
        public void 効果の表示()
        {
            Assert.AreEqual("食料+2日", new ItemEffect { stat = "food", amount = 2 }.Label());
            Assert.AreEqual("日用品+10日", new ItemEffect { stat = "daily", amount = 10 }.Label());
            Assert.AreEqual("満足度+3", new ItemEffect { stat = "satisfaction", amount = 3 }.Label());
        }

        [Test]
        public void 文章は難易度のlevelに合うものから選ぶ()
        {
            var sentences = new SentenceList
            {
                sentences = new[]
                {
                    new SentenceData { id = "m1", itemIds = new[] { "milk" }, reading = "ぎゅうにゅう", level = 1 },
                    new SentenceData { id = "m3a", itemIds = new[] { "milk" }, reading = "じゃーじー", level = 3 },
                    new SentenceData { id = "m3b", itemIds = new[] { "milk" }, reading = "じゃーじーぎゅうにゅう", level = 3 },
                    new SentenceData { id = "e1", itemIds = new[] { "egg" }, reading = "たまご", level = 1 },
                },
            };
            var seen = new List<int>();
            Assert.AreEqual("m3b", SentenceCatalog.Pick(sentences, "milk", 3, n => { seen.Add(n); return 1; }).id);
            CollectionAssert.AreEqual(new[] { 2 }, seen);
            // level 2 がないときは近い方、同じ差なら低い方
            Assert.AreEqual("m1", SentenceCatalog.Pick(sentences, "milk", 2, n => 0).id);
            Assert.AreEqual("m3a", SentenceCatalog.Pick(sentences, "milk", 4, n => 0).id);
            Assert.AreEqual("e1", SentenceCatalog.Pick(sentences, "egg", 4, n => 0).id);
            Assert.IsNull(SentenceCatalog.Pick(sentences, "rice", 1, n => 0));
        }
            [Test]
        public void 最近出した文章は避けて選ぶ()
        {
            var sentences = new SentenceList
            {
                sentences = new[]
                {
                    new SentenceData { id = "a", itemIds = new[] { "milk" }, reading = "ぎゅうにゅう", level = 1 },
                    new SentenceData { id = "b", itemIds = new[] { "milk" }, reading = "みるく", level = 1 },
                    new SentenceData { id = "c", itemIds = new[] { "milk" }, reading = "せいにゅう", level = 1 },
                    new SentenceData { id = "e", itemIds = new[] { "egg" }, reading = "たまご", level = 1 },
                },
            };
            // a と c は最近出したので b しか残らない
            Assert.AreEqual("b", SentenceCatalog.Pick(sentences, "milk", 1, n => { Assert.AreEqual(1, n); return 0; }, new List<string> { "a", "c" }).id);
            // 全部出していたら、いちばん最近のもの（b）以外から
            var picked = SentenceCatalog.Pick(sentences, "milk", 1, n => { Assert.AreEqual(2, n); return 1; }, new List<string> { "a", "c", "b" });
            Assert.AreEqual("c", picked.id);
            // 候補が1つだけなら、最近出していてもそれを出す
            Assert.AreEqual("e", SentenceCatalog.Pick(sentences, "egg", 1, n => 0, new List<string> { "e" }).id);
        }

        [Test]
        public void 読みがなにスペースや記号があるとエラー()
        {
            var items = new ItemList { items = new[] { new ItemData { id = "milk", name = "牛乳", category = "food", kind = "essential", basePrice = 100, effects = new[] { new ItemEffect { stat = "food", amount = 1 } } } } };
            var sentences = new SentenceList
            {
                sentences = new[]
                {
                    new SentenceData { id = "ok", itemIds = new[] { "milk" }, reading = "premiumみるく", level = 1 },
                    new SentenceData { id = "sp", itemIds = new[] { "milk" }, reading = "premium みるく", level = 1 },
                    new SentenceData { id = "up", itemIds = new[] { "milk" }, reading = "Premiumみるく", level = 1 },
                },
            };
            var errors = DataValidator.Validate(items, sentences);
            Assert.IsFalse(errors.Exists(e => e.StartsWith("ok:")), string.Join("\n", errors));
            Assert.IsTrue(errors.Exists(e => e.StartsWith("sp:")), string.Join("\n", errors));
            Assert.IsTrue(errors.Exists(e => e.StartsWith("up:")), string.Join("\n", errors));
        }
    }
}
