using Negiri.Core.Typing;
using NUnit.Framework;

namespace Negiri.Core.Tests
{
    public class TypingEvaluatorTests
    {
        readonly TypingBalance balance = new TypingBalance();

        [Test]
        public void 制限時間_牛乳を買うは中級で7_2秒()
        {
            Assert.AreEqual(7.2f, TypingEvaluator.TimeLimitSeconds("ぎゅうにゅうをかう", balance), 1e-4f);
        }

        [Test]
        public void 時間切れはMISSで割引なし()
        {
            var r = TypingEvaluator.Evaluate(false, 0f, 5, 0, balance);
            Assert.AreEqual(Rank.Miss, r.Rank);
            Assert.AreEqual(0, r.DiscountBp);
        }

        // 残り時間率, 正しい打鍵, ミス, 期待する評価, 期待する割引
        [TestCase(0.10f, 20, 0, Rank.Good, 500)]
        [TestCase(0.30f, 23, 2, Rank.Great, 1000)]     // 正確率 92%
        [TestCase(0.50f, 24, 1, Rank.Excellent, 1500)] // 正確率 96%
        [TestCase(0.50f, 20, 0, Rank.Perfect, 2000)]
        [TestCase(0.50f, 17, 3, Rank.Good, 500)]       // 正確率 85%：速くても正確率不足
        [TestCase(0.25f, 20, 0, Rank.Great, 1000)]     // 境目ちょうどは上の評価
        [TestCase(0.45f, 20, 0, Rank.Perfect, 2000)]   // 境目ちょうどは上の評価
        public void 評価と割引(float remaining, int correct, int miss, Rank rank, int bp)
        {
            var r = TypingEvaluator.Evaluate(true, remaining, correct, miss, balance);
            Assert.AreEqual(rank, r.Rank);
            Assert.AreEqual(bp, r.DiscountBp);
        }

        [Test]
        public void セッション_打ち切ると評価が出る()
        {
            var s = new TypingSession("か", balance);
            Assert.AreEqual(KeyResult.AlreadyCompleted, s.Input('k')); // 開始前は受け付けない
            s.Start();
            s.Tick(0.5f);
            s.Input('k');
            s.Input('a');
            Assert.AreEqual(SessionState.Finished, s.State);
            Assert.IsTrue(s.Result.HasValue);
            Assert.AreEqual(Rank.Perfect, s.Result.Value.Rank);
        }

        [Test]
        public void セッション_時間切れでMISS()
        {
            var s = new TypingSession("か", balance);
            s.Start();
            s.Input('k');
            s.Tick(s.TimeLimit + 1f);
            Assert.AreEqual(SessionState.Finished, s.State);
            Assert.AreEqual(Rank.Miss, s.Result.Value.Rank);
            Assert.AreEqual(0f, s.Remaining);
        }
    }
}
