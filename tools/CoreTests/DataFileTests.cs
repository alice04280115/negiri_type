using System.IO;
using System.Text.Json;
using Negiri.Core.Data;
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
    }
}
