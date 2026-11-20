using System;

namespace Negiri.Core.Typing
{
    public enum SessionState
    {
        /// <summary>問題文は見えているが、時間はまだ動いていない。</summary>
        Ready,
        Running,
        Finished,
    }

    /// <summary>
    /// 1つの商品のタイピングチャレンジ。経過時間は呼び出し側（画面）から渡す。
    /// </summary>
    public sealed class TypingSession
    {
        readonly TypingBalance balance;

        public RomajiMatcher Matcher { get; }
        public float TimeLimit { get; }
        public float Elapsed { get; private set; }
        public SessionState State { get; private set; } = SessionState.Ready;
        public TypingResult? Result { get; private set; }

        public float Remaining => Math.Max(0f, TimeLimit - Elapsed);
        public float RemainingRatio => TimeLimit <= 0 ? 0f : Remaining / TimeLimit;

        public TypingSession(string reading, TypingBalance balance)
        {
            this.balance = balance ?? throw new ArgumentNullException(nameof(balance));
            Matcher = new RomajiMatcher(reading);
            TimeLimit = TypingEvaluator.TimeLimitSeconds(reading, balance);
        }

        public void Start()
        {
            if (State == SessionState.Ready) State = SessionState.Running;
        }

        /// <summary>時間を進める。制限時間を過ぎたら MISS で終わる。</summary>
        public void Tick(float deltaSeconds)
        {
            if (State != SessionState.Running) return;
            Elapsed += deltaSeconds;
            if (Elapsed >= TimeLimit)
            {
                Elapsed = TimeLimit;
                Finish(false);
            }
        }

        public KeyResult Input(char key)
        {
            if (State != SessionState.Running) return KeyResult.AlreadyCompleted;
            var r = Matcher.Input(key);
            if (Matcher.IsCompleted) Finish(true);
            return r;
        }

        /// <summary>今このまま打ち終えたら取れる評価（画面の見込み表示用）。</summary>
        public TypingResult Preview()
        {
            return TypingEvaluator.Evaluate(true, RemainingRatio, Matcher.CorrectKeys, Matcher.MissKeys, balance);
        }

        /// <summary>IMEがオンだったときなどに、同じ文章をやり直す。</summary>
        public TypingSession Restart()
        {
            return new TypingSession(Matcher.Reading, balance);
        }

        void Finish(bool completed)
        {
            State = SessionState.Finished;
            Result = TypingEvaluator.Evaluate(completed, RemainingRatio, Matcher.CorrectKeys, Matcher.MissKeys, balance);
        }
    }
}
