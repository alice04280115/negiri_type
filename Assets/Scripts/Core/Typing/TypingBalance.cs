using System;
using System.Collections.Generic;

namespace Negiri.Core.Typing
{
    /// <summary>
    /// タイピング評価の数値。すべて仕様書の仮案で、balance.json から上書きする。
    /// 割引率はベーシスポイント（1% = 100）で持ち、小数の誤差を避ける。
    /// </summary>
    [Serializable]
    public class TypingBalance
    {
        /// <summary>基準打鍵速度（打鍵/秒）。初級2.0、中級2.5、上級3.0。</summary>
        public float keysPerSecond = 2.5f;

        /// <summary>問題文を読むための余裕時間（秒）。</summary>
        public float graceSeconds = 2.0f;

        /// <summary>上の評価から順に並べる。どれにも当てはまらなければ GOOD（打ち切れた場合）。</summary>
        public List<RankRule> ranks = DefaultRanks();

        /// <summary>時間内に打ち切ったときの最低評価の割引。</summary>
        public int goodDiscountBp = 500;

        public static List<RankRule> DefaultRanks()
        {
            return new List<RankRule>
            {
                new RankRule { rank = "Perfect", minRemainingRatio = 0.45f, minAccuracy = 1.0f, maxMisses = 0, discountBp = 2000 },
                new RankRule { rank = "Excellent", minRemainingRatio = 0.45f, minAccuracy = 0.95f, maxMisses = -1, discountBp = 1500 },
                new RankRule { rank = "Great", minRemainingRatio = 0.25f, minAccuracy = 0.90f, maxMisses = -1, discountBp = 1000 },
            };
        }
    }

    [Serializable]
    public class RankRule
    {
        /// <summary>JSON で読みやすいように文字列で持つ（Perfect / Excellent / Great）。</summary>
        public string rank;
        public Rank RankValue => (Rank)Enum.Parse(typeof(Rank), rank, true);
        public float minRemainingRatio;
        public float minAccuracy;
        /// <summary>-1 なら制限なし。</summary>
        public int maxMisses = -1;
        public int discountBp;
    }

    public enum Rank
    {
        Miss,
        Good,
        Great,
        Excellent,
        Perfect,
    }
}
