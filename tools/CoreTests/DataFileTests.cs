using System.Collections.Generic;
using System.Linq;
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
            Assert.AreEqual(5000, b.life.month.budget);
            Assert.AreEqual(14000, b.life.month.PaydayOn(15));
            Assert.AreEqual(19000, b.life.month.TotalBudget);
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

        // 読みがなの文字数の目安（商品名データ 見直し・拡張計画）。初級2～8・中級6～13・上級10～20・達人18～35
        static readonly int[] MinReading = { 0, 2, 6, 10, 18 };
        static readonly int[] MaxReading = { 0, 8, 13, 20, 35 };

        [Test]
        public void 同じ食材名の商品名が続けて出ない()
        {
            var items = Load<ItemList>("items.json");
            var sentences = Load<SentenceList>("sentences.json");
            var rnd = new System.Random(42);
            foreach (var item in items.items)
            {
                Assert.IsNotEmpty(item.foods, $"{item.name} の foods");
                string last = null;
                var used = new Dictionary<int, List<string>>();
                for (int i = 0; i < 400; i++)
                {
                    int level = 1 + rnd.Next(4);
                    if (!used.TryGetValue(level, out var u)) used[level] = u = new List<string>();
                    var picked = SentenceCatalog.Pick(sentences, item.id, level, n => rnd.Next(n), u, item.foods, last);
                    if (u.Contains(picked.id)) u.Clear();
                    u.Add(picked.id);
                    var key = SentenceCatalog.FoodKey(picked.text, item.foods);
                    // 候補がすべて同じ食材名（納豆など）のときだけ続いてよい
                    bool allSame = sentences.sentences.Where(x => x.level == level && x.itemIds.Contains(item.id)).All(x => SentenceCatalog.FoodKey(x.text, item.foods) == key);
                    if (!allSame) Assert.AreNotEqual(last, key, $"{item.name} レベル{level}：{picked.text}");
                    last = key;
                }
            }
        }

        [Test]
        public void 食材名は商品名に入っているいちばん短い語()
        {
            var foods = new[] { "キャベツ", "春キャベツ", "レタス", "サニーレタス" };
            Assert.AreEqual("キャベツ", SentenceCatalog.FoodKey("千葉県産朝採れ春キャベツ", foods));
            Assert.AreEqual("キャベツ", SentenceCatalog.FoodKey("高原キャベツ一玉", foods));
            Assert.AreEqual("レタス", SentenceCatalog.FoodKey("サニーレタス", foods));
            Assert.AreEqual("サラダ用ミックス野菜", SentenceCatalog.FoodKey("サラダ用ミックス野菜", foods));
        }

        [Test]
        public void 商品名の読みの長さが難易度ごとの目安に収まる()
        {
            var bad = new List<string>();
            foreach (var s in Load<SentenceList>("sentences.json").sentences)
            {
                int n = s.reading.Length;
                if (n < MinReading[s.level] || n > MaxReading[s.level]) bad.Add($"{s.id} {s.text}（level {s.level}・{n}文字）");
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void 同じ商品名が二つない()
        {
            var seen = new HashSet<string>();
            var dup = new List<string>();
            foreach (var s in Load<SentenceList>("sentences.json").sentences)
            {
                if (!seen.Add(s.text)) dup.Add($"{s.id} {s.text}");
            }
            Assert.IsEmpty(dup, string.Join("\n", dup));
        }

        [Test]
        public void 商品と商品名の文字がすべてフォントにある()
        {
            var dir = Path.GetDirectoryName(Path.GetDirectoryName(DataDir()));
            var glyphs = FontCharacters(Path.Combine(dir, "Resources", "Fonts", "main.ttf"));
            var missing = new SortedSet<string>();
            void Check(string id, string text)
            {
                foreach (var ch in text)
                {
                    if (!glyphs.Contains(ch)) missing.Add($"「{ch}」（{id} {text}）");
                }
            }
            foreach (var item in Load<ItemList>("items.json").items) Check(item.id, item.name);
            foreach (var s in Load<SentenceList>("sentences.json").sentences) Check(s.id, s.text);
            Assert.IsEmpty(missing, string.Join("\n", missing));
        }

        /// <summary>TrueType フォントの cmap（Windows Unicode BMP・format 4）から、字形のある文字を集める。</summary>
        static HashSet<char> FontCharacters(string path)
        {
            var b = File.ReadAllBytes(path);
            int U16(int o) => (b[o] << 8) | b[o + 1];
            uint U32(int o) => (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
            int numTables = U16(4);
            int cmap = -1;
            for (int i = 0; i < numTables; i++)
            {
                int rec = 12 + 16 * i;
                if (b[rec] == 'c' && b[rec + 1] == 'm' && b[rec + 2] == 'a' && b[rec + 3] == 'p') cmap = (int)U32(rec + 8);
            }
            Assert.AreNotEqual(-1, cmap, "フォントに cmap がありません");
            int sub = -1;
            for (int i = 0; i < U16(cmap + 2); i++)
            {
                int rec = cmap + 4 + 8 * i;
                if (U16(rec) == 3 && U16(rec + 2) == 1) sub = cmap + (int)U32(rec + 4);
            }
            Assert.AreNotEqual(-1, sub, "フォントに Unicode の cmap がありません");
            Assert.AreEqual(4, U16(sub), "cmap が format 4 ではありません");
            int segX2 = U16(sub + 6);
            int ends = sub + 14, starts = ends + segX2 + 2, deltas = starts + segX2, offsets = deltas + segX2;
            var set = new HashSet<char>();
            for (int i = 0; i < segX2 / 2; i++)
            {
                int end = U16(ends + 2 * i), start = U16(starts + 2 * i);
                int delta = U16(deltas + 2 * i), rangeOffset = U16(offsets + 2 * i);
                for (int c = start; c <= end && c != 0xFFFF; c++)
                {
                    int glyph;
                    if (rangeOffset == 0) glyph = (c + delta) & 0xFFFF;
                    else
                    {
                        int g = U16(offsets + 2 * i + rangeOffset + 2 * (c - start));
                        glyph = g == 0 ? 0 : (g + delta) & 0xFFFF;
                    }
                    if (glyph != 0) set.Add((char)c);
                }
            }
            return set;
        }
    }
}
