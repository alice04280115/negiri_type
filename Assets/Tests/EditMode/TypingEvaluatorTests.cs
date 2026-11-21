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
        [TestCase(0.30f, 23, 2, Rank.Great, 1000)]
        [TestCase(0.50f, 24, 1, Rank.Excellent, 1500)] // ミスがあると PERFECT にはならない
        [TestCase(0.50f, 20, 0, Rank.Perfect, 2000)]
        [TestCase(0.50f, 17, 3, Rank.Excellent, 1500)] // 正確率は問わない（ミスは残り時間で効く）
        [TestCase(0.25f, 20, 0, Rank.Great, 1000)]     // 境目ちょうどは上の評価
        [TestCase(0.45f, 20, 0, Rank.Perfect, 2000)]   // 境目ちょうどは上の評価
        public void 評価と割引(float remaining, int correct, int miss, Rank rank, int bp)
        {
            var r = TypingEvaluator.Evaluate(true, remaining, correct, miss, balance);
            Assert.AreEqual(rank, r.Rank);
            Assert.AreEqual(bp, r.DiscountBp);
        }

        [Test]
        public void 正確率の条件を使う設定でも動く()
        {
            // balance.json で正確率の条件を戻した場合：残り26%でも正確率84%なら GOOD、届かなかったのは GREAT
            var strict = new TypingBalance();
            strict.ranks[2].minAccuracy = 0.9f;
            var r = TypingEvaluator.Evaluate(true, 0.26f, 21, 4, strict);
            Assert.AreEqual(Rank.Good, r.Rank);
            Assert.AreEqual(Rank.Great, TypingEvaluator.MissedByAccuracy(r, strict).RankValue);
        }

        [Test]
        public void 時間が足りないときは理由を返さない()
        {
            var r = TypingEvaluator.Evaluate(true, 0.10f, 20, 0, balance);
            Assert.IsNull(TypingEvaluator.MissedByAccuracy(r, balance));
            var perfect = TypingEvaluator.Evaluate(true, 0.50f, 20, 0, balance);
            Assert.IsNull(TypingEvaluator.MissedByAccuracy(perfect, balance));
        }

        [Test]
        public void ミス1回でPERFECTを逃したときはPERFECTを返す()
        {
            var r = TypingEvaluator.Evaluate(true, 0.50f, 30, 1, balance); // 正確率97%
            Assert.AreEqual(Rank.Excellent, r.Rank);
            Assert.AreEqual(Rank.Perfect, TypingEvaluator.MissedByAccuracy(r, balance).RankValue);
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
        public void セッション_ミスで残り時間が減る()
        {
            var s = new TypingSession("かい", balance);
            s.Start();
            var before = s.Remaining;
            Assert.AreEqual(KeyResult.Miss, s.Input('x'));
            Assert.AreEqual(before - balance.missPenaltySeconds, s.Remaining, 1e-4f);
            Assert.AreEqual(KeyResult.Correct, s.Input('k')); // 正しい打鍵では減らない
            Assert.AreEqual(before - balance.missPenaltySeconds, s.Remaining, 1e-4f);
        }

        [Test]
        public void セッション_ミスで時間を使い切るとMISS()
        {
            var s = new TypingSession("か", balance);
            s.Start();
            s.Tick(s.TimeLimit - 0.1f);
            s.Input('x');
            Assert.AreEqual(SessionState.Finished, s.State);
            Assert.AreEqual(Rank.Miss, s.Result.Value.Rank);
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
