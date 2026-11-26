using Negiri.Core.Typing;

namespace Negiri.Game
{
    /// <summary>読みがな（とローマ字の見本）の出し方。</summary>
    public enum ReadingMode
    {
        /// <summary>常に出す。</summary>
        Always,
        /// <summary>Rキーで隠せる。隠しても、残り時間が EXCELLENT の線を切ると出てくる。</summary>
        OptionalWithReveal,
        /// <summary>Rキーで隠せる。隠したら最後まで出ない。</summary>
        Optional,
    }

    public sealed class DifficultyDef
    {
        public string Label;
        public float KeysPerSecond;
        public float GraceSeconds;
        public float MissPenaltySeconds;
        /// <summary>出てくる商品名の level。1：かんたんな単語 / 2：漢字多め / 3：見慣れない漢字・カタカナ多め / 4：英語とサブタイトルつき</summary>
        public int Level;
        public ReadingMode Reading;
    }

    /// <summary>
    /// 難易度ごとのタイピングの数値（段階1から移動。数値はすべて（仮））。
    /// 買い物の数値（品数の上限・不足時の扱い）は balance.json の shopping にある。
    /// </summary>
    public static class Difficulties
    {
        public static readonly DifficultyDef[] All =
        {
            new DifficultyDef { Label = "初級", KeysPerSecond = 2.0f, GraceSeconds = 2.0f, MissPenaltySeconds = 0.5f, Level = 1, Reading = ReadingMode.Always },
            new DifficultyDef { Label = "中級", KeysPerSecond = 2.5f, GraceSeconds = 2.0f, MissPenaltySeconds = 0.5f, Level = 2, Reading = ReadingMode.Always },
            new DifficultyDef { Label = "上級", KeysPerSecond = 3.5f, GraceSeconds = 1.5f, MissPenaltySeconds = 1.0f, Level = 3, Reading = ReadingMode.OptionalWithReveal },
            new DifficultyDef { Label = "達人", KeysPerSecond = 5.0f, GraceSeconds = 1.0f, MissPenaltySeconds = 2.0f, Level = 4, Reading = ReadingMode.Optional },
        };

        /// <summary>balance.json の評価の決まりを使い、難易度の速度と時間だけ差し替えたコピーを作る。</summary>
        public static TypingBalance CreateBalance(TypingBalance source, int index)
        {
            var d = All[index];
            return new TypingBalance
            {
                keysPerSecond = d.KeysPerSecond,
                graceSeconds = d.GraceSeconds,
                missPenaltySeconds = d.MissPenaltySeconds,
                ranks = source.ranks,
                goodDiscountBp = source.goodDiscountBp,
            };
        }

        /// <summary>PERFECT の割引（全部PERFECTの見込み表示用）。</summary>
        public static int BestDiscountBp(TypingBalance balance)
        {
            int best = balance.goodDiscountBp;
            foreach (var r in balance.ranks) if (r.discountBp > best) best = r.discountBp;
            return best;
        }
    }
}
