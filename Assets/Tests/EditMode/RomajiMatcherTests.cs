using Negiri.Core.Typing;
using NUnit.Framework;

namespace Negiri.Core.Tests
{
    public class RomajiMatcherTests
    {
        static bool TypeAll(string reading, string keys, out RomajiMatcher m)
        {
            m = new RomajiMatcher(reading);
            foreach (var k in keys)
            {
                if (m.Input(k) != KeyResult.Correct) return false;
            }
            return m.IsCompleted;
        }

        [TestCase("しんぶん", "shinbunn")]
        [TestCase("しんぶん", "sinbunn")]
        [TestCase("しんぶん", "shinnbunn")]
        [TestCase("きんえん", "kinnenn")]
        [TestCase("がっこう", "gakkou")]
        [TestCase("がっこう", "gaxtukou")]
        [TestCase("がっこう", "galtukou")]
        [TestCase("ちゃ", "cha")]
        [TestCase("ちゃ", "tya")]
        [TestCase("ちゃ", "cya")]
        [TestCase("ちゃ", "chixya")]
        [TestCase("ぎゅうにゅうをかう", "gyuunyuuwokau")]
        [TestCase("ぎゅうにゅうをかう", "gilyuunixyuuwokau")]
        [TestCase("こんや", "konnya")]
        [TestCase("ぺーぱー", "pe-pa-")]
        [TestCase("ぽてとちっぷす", "potetochippusu")]
        [TestCase("ぽてとちっぷす", "potetotixtupusu")]
        [TestCase("ふく", "huku")]
        [TestCase("じゃ", "zya")]
        [TestCase("ぷれみあむmilk", "puremiamumilk")]
        [TestCase("ふぁみりーぱっく2", "famiri-pakku2")]
        [TestCase("ゔぃんてーじ", "vinte-ji")]
        [TestCase("でぃなーせっと", "dhina-setto")]
        [TestCase("ぷれみあむ milk", "puremiamu milk")]
        public void 受け付ける打ち方(string reading, string keys)
        {
            Assert.IsTrue(TypeAll(reading, keys, out _), $"{reading} を {keys} で打てるはず");
        }

        [Test]
        public void きねんと区別するため_きんえんはkinenでは打てない()
        {
            var m = new RomajiMatcher("きんえん");
            Assert.AreEqual(KeyResult.Correct, m.Input('k'));
            Assert.AreEqual(KeyResult.Correct, m.Input('i'));
            Assert.AreEqual(KeyResult.Correct, m.Input('n'));
            // 「ん」の次が母音なので、n 1回では「ん」にならない
            Assert.AreEqual(KeyResult.Miss, m.Input('e'));
        }

        [Test]
        public void 最後のんはnnが必要()
        {
            var m = new RomajiMatcher("きんえん");
            foreach (var k in "kinnen") m.Input(k);
            Assert.IsFalse(m.IsCompleted);
            Assert.AreEqual(KeyResult.Correct, m.Input('n'));
            Assert.IsTrue(m.IsCompleted);
        }

        [Test]
        public void 途中まで打った状態でも正誤を返す()
        {
            var m = new RomajiMatcher("し");
            Assert.AreEqual(KeyResult.Correct, m.Input('s'));
            var m2 = new RomajiMatcher("し");
            m2.Input('s');
            Assert.AreEqual(KeyResult.Correct, m2.Input('h'));
            var m3 = new RomajiMatcher("し");
            m3.Input('s');
            Assert.AreEqual(KeyResult.Correct, m3.Input('i'));
            Assert.IsTrue(m3.IsCompleted);
        }

        [Test]
        public void ミスは入力が進まず回数だけ数える()
        {
            var m = new RomajiMatcher("か");
            Assert.AreEqual(KeyResult.Miss, m.Input('x'));
            Assert.AreEqual(KeyResult.Miss, m.Input('z'));
            Assert.AreEqual(2, m.MissKeys);
            Assert.AreEqual(KeyResult.Correct, m.Input('k'));
            Assert.AreEqual(KeyResult.Correct, m.Input('a'));
            Assert.IsTrue(m.IsCompleted);
            Assert.AreEqual(2, m.CorrectKeys);
        }

        [Test]
        public void 大文字も受け付ける()
        {
            Assert.IsTrue(TypeAll("か", "KA", out _));
        }

        [Test]
        public void ガイドは打った部分と残りを返す()
        {
            var m = new RomajiMatcher("しんぶん");
            Assert.AreEqual("shinbunn", m.Guide().remaining);
            m.Input('s');
            m.Input('i');
            var g = m.Guide();
            Assert.AreEqual("si", g.typed);
            Assert.AreEqual("nbunn", g.remaining);
        }

        [Test]
        public void ガイドの促音は子音を重ねる()
        {
            var m = new RomajiMatcher("がっこう");
            Assert.AreEqual("gakkou", m.Guide().remaining);
        }

        [TestCase("ぎゅうにゅうをかう", 13)]
        [TestCase("しんぶん", 7)]
        [TestCase("がっこう", 6)]
        [TestCase("milk", 4)]
        public void 最短打鍵数(string reading, int expected)
        {
            Assert.AreEqual(expected, RomajiMatcher.ShortestKeyCount(reading));
        }

        [Test]
        public void 使えない文字があると例外()
        {
            Assert.Throws<System.ArgumentException>(() => new RomajiMatcher("牛乳"));
            Assert.Throws<System.ArgumentException>(() => new RomajiMatcher("Milk")); // 英字は小文字で書く
        }
    }
}
