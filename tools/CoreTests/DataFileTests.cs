using System.IO;
using System.Text.Json;
using Negiri.Core.Data;
using Negiri.Core.Life;
using Negiri.Core.Save;
using Negiri.Core.Shopping;
using Negiri.Core.Typing;
using NUnit.Framework;

namespace Negiri.Core.Tests
{
    /// <summary>
    /// 実際のデータファイル（Assets/Resources/Data）に不備がないかを確かめる。
    /// Unity の JsonUtility の代わりに System.Text.Json で読むので、このファイルは .NET 側だけで使う。
    /// </summary>
    public class DataFileTests
    {
        static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };

        static string DataDir()
        {
            var dir = TestContext.CurrentContext.TestDirectory;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "Assets"))) dir = Path.GetDirectoryName(dir);
            Assert.IsNotNull(dir, "リポジトリのルートが見つかりません");
            return Path.Combine(dir, "Assets", "Resources", "Data");
        }

        static T Load<T>(string file) => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(DataDir(), file)), Options);

        [Test]
        public void 商品と文章のデータに不備がない()
        {
            var errors = DataValidator.Validate(Load<ItemList>("items.json"), Load<SentenceList>("sentences.json"));
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void すべての文章がおすすめの打ち方で最後まで打てる()
        {
            foreach (var s in Load<SentenceList>("sentences.json").sentences)
            {
                var m = new RomajiMatcher(s.reading);
                var guide = m.Guide().remaining;
                foreach (var k in guide)
                {
                    Assert.AreEqual(KeyResult.Correct, m.Input(k), $"{s.id}: {s.reading} / {guide}");
                }
                Assert.IsTrue(m.IsCompleted, s.id);
                Assert.AreEqual(RomajiMatcher.ShortestKeyCount(s.reading) <= guide.Length, true, s.id);
            }
        }

        [Test]
        public void バランス設定を読める()
        {
            var b = Load<BalanceData>("balance.json");
            Assert.AreEqual(2.5f, b.typing.keysPerSecond, 1e-6f);
            Assert.AreEqual(3, b.typing.ranks.Count);
            Assert.AreEqual(Rank.Perfect, b.typing.ranks[0].RankValue);
        }

        [Test]
        public void 買い物の設定を読めて不備がない()
        {
            var b = Load<BalanceData>("balance.json");
            var errors = DataValidator.ValidateShopping(b.shopping, 4);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual(3000, b.shopping.startingCash);
            Assert.AreEqual(5f, b.shopping.resultSeconds, 1e-6f);
            Assert.AreEqual(ShortageRule.AutoReturnHighestFirst, b.shopping.difficulties[2].ShortageValue);
            Assert.AreEqual(ShortageRule.AutoReturnLowestFirst, b.shopping.difficulties[3].ShortageValue);
            Assert.AreEqual(new[] { 1, 3, 7, 7 }, System.Array.ConvertAll(b.shopping.difficulties, d => d.shopIntervalDays));
            Assert.AreEqual(new[] { 7, 7, 10, 10 }, System.Array.ConvertAll(b.shopping.difficulties, d => d.foodCap));
        }

        [Test]
        public void 一か月の設定を読めて不備がない()
        {
            var b = Load<BalanceData>("balance.json");
            var errors = DataValidator.ValidateLife(b.life, b.shopping);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual(19000, b.life.month.budget);
            Assert.AreEqual(30, b.life.month.days);
            Assert.AreEqual(60, b.life.satisfaction.start);
        }

        [Test]
        public void 曜日の特売がある()
        {
            var store = Load<StoreList>("stores.json").stores[0];
            int days = 0;
            for (int w = 0; w < 7; w++) if (store.SalesOn(w).Count > 0) days++;
            Assert.IsTrue(days >= 2, "特売の曜日が2日以上ある");
            Assert.IsTrue(store.SalesOn(0).Count < store.sales.Length, "毎日すべてが特売ではない");
        }

        [Test]
        public void セーブをJSONにして戻せる()
        {
            var b = Load<BalanceData>("balance.json");
            var rule = b.shopping.difficulties[1];
            var m = MonthState.Start(b.life, rule, 1);
            m.Lock(2000);
            m.EndDay();
            var json = JsonSerializer.Serialize(m.ToSave(), Options);
            var back = MonthState.FromSave(JsonSerializer.Deserialize<SaveData>(json, Options), b.life, rule);
            Assert.AreEqual(m.Day, back.Day);
            Assert.AreEqual(m.Wallet.Cash, back.Wallet.Cash);
            Assert.AreEqual(m.Wallet.Locked, back.Wallet.Locked);
            Assert.AreEqual(m.Satisfaction.Value, back.Satisfaction.Value);
        }

        [Test]
        public void 店舗と特売のデータに不備がない()
        {
            var errors = DataValidator.ValidateStores(Load<StoreList>("stores.json"), Load<ItemList>("items.json"));
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void すべての商品に効果と各難易度の文章がある()
        {
            var items = Load<ItemList>("items.json");
            var sentences = Load<SentenceList>("sentences.json");
            foreach (var item in items.items)
            {
                Assert.IsNotEmpty(item.effects, item.id);
                for (int level = 1; level <= 4; level++)
                {
                    var s = SentenceCatalog.Pick(sentences, item.id, level, n => 0);
                    Assert.IsNotNull(s, $"{item.id} level {level}");
                }
            }
        }
    }
}
