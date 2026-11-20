using System;

namespace Negiri.Core.Typing
{
    /// <summary>1回のタイピングチャレンジの結果。</summary>
    public readonly struct TypingResult
    {
        public readonly Rank Rank;
        public readonly int DiscountBp;
        public readonly float RemainingRatio;
        public readonly float Accuracy;
        public readonly int CorrectKeys;
        public readonly int MissKeys;

        public TypingResult(Rank rank, int discountBp, float remainingRatio, float accuracy, int correctKeys, int missKeys)
        {
            Rank = rank;
            DiscountBp = discountBp;
            RemainingRatio = remainingRatio;
            Accuracy = accuracy;
            CorrectKeys = correctKeys;
            MissKeys = missKeys;
        }
    }

    /// <summary>制限時間と評価の計算。画面やUnityには依存しない。</summary>
    public static class TypingEvaluator
    {
        /// <summary>制限時間(秒) = 最短打鍵数 ÷ 基準打鍵速度 ＋ 余裕時間。</summary>
        public static float TimeLimitSeconds(int shortestKeys, TypingBalance balance)
        {
            if (balance.keysPerSecond <= 0) throw new ArgumentException("keysPerSecond は正の数にしてください");
            return shortestKeys / balance.keysPerSecond + balance.graceSeconds;
        }

        public static float TimeLimitSeconds(string reading, TypingBalance balance)
        {
            return TimeLimitSeconds(RomajiMatcher.ShortestKeyCount(reading), balance);
        }

        /// <summary>正確率 = 正しい打鍵 ÷ (正しい打鍵 ＋ ミス打鍵)。1打もしていなければ 1。</summary>
        public static float Accuracy(int correctKeys, int missKeys)
        {
            int total = correctKeys + missKeys;
            return total == 0 ? 1f : (float)correctKeys / total;
        }

        /// <summary>
        /// 評価を決める。completed が false（時間切れ）なら MISS。
        /// remainingRatio = 残り時間 ÷ 制限時間。
        /// </summary>
        public static TypingResult Evaluate(bool completed, float remainingRatio, int correctKeys, int missKeys, TypingBalance balance)
        {
            float accuracy = Accuracy(correctKeys, missKeys);
            if (!completed)
            {
                return new TypingResult(Rank.Miss, 0, 0f, accuracy, correctKeys, missKeys);
            }

            // 浮動小数の誤差で境目ちょうどが下の評価にならないよう、わずかに余裕を持たせる
            const float eps = 1e-5f;
            foreach (var rule in balance.ranks)
            {
                bool timeOk = remainingRatio + eps >= rule.minRemainingRatio;
                bool accuracyOk = accuracy + eps >= rule.minAccuracy;
                bool missOk = rule.maxMisses < 0 || missKeys <= rule.maxMisses;
                if (timeOk && accuracyOk && missOk)
                {
                    return new TypingResult(rule.RankValue, rule.discountBp, remainingRatio, accuracy, correctKeys, missKeys);
                }
            }
            return new TypingResult(Rank.Good, balance.goodDiscountBp, remainingRatio, accuracy, correctKeys, missKeys);
        }

        /// <summary>
        /// 時間は足りていたのに、正確率かミスの条件で届かなかった評価のうち、いちばん近いもの。
        /// 結果画面で「正確率があと○%でGREAT」のように理由を出すために使う。なければ null。
        /// </summary>
        public static RankRule MissedByAccuracy(TypingResult result, TypingBalance balance)
        {
            if (result.Rank == Rank.Miss) return null;
            const float eps = 1e-5f;
            RankRule nearest = null;
            foreach (var rule in balance.ranks)
            {
                if (rule.RankValue <= result.Rank) continue;
                if (result.RemainingRatio + eps < rule.minRemainingRatio) continue;
                if (nearest == null || rule.RankValue < nearest.RankValue) nearest = rule;
            }
            return nearest;
        }
    }
}
