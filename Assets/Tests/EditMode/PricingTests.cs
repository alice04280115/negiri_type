using Negiri.Core.Data;
using Negiri.Core.Economy;
using NUnit.Framework;

namespace Negiri.Core.Tests
{
    public class PricingTests
    {
        // 通常価格, タイピング割引, セール割引, 上限, 期待する支払額
        [TestCase(500, 1000, 2000, 5000, 350)]  // 足し算で合算する
        [TestCase(238, 500, 0, 5000, 226)]      // 226.1円 → 円未満切り捨て
        [TestCase(1000, 2000, 4000, 5000, 500)] // 上限50%で止まる
        [TestCase(300, 0, 0, 5000, 300)]        // MISS は割引なし
        public void 支払額(int basePrice, int typingBp, int saleBp, int capBp, int expected)
        {
            Assert.AreEqual(expected, Pricing.Price(basePrice, typingBp, saleBp, capBp));
        }

        [Test]
        public void 節約額は差額()
        {
            Assert.AreEqual(150, Pricing.Saved(500, 350));
        }

        [Test]
        public void データチェック_不備を見つける()
        {
            var items = new ItemList { items = new[] { new ItemData { id = "milk", name = "牛乳", kind = "essential", basePrice = 238 } } };
            var sentences = new SentenceList
            {
                sentences = new[]
                {
                    new SentenceData { id = "s1", itemIds = new[] { "milk" }, reading = "ぎゅうにゅう" },
                    new SentenceData { id = "s2", itemIds = new[] { "egg" }, reading = "たまご" },
                    new SentenceData { id = "s3", itemIds = new[] { "milk" }, reading = "牛乳" },
                },
            };
            var errors = DataValidator.Validate(items, sentences);
            Assert.AreEqual(2, errors.Count, string.Join("\n", errors));
        }
    }
}
