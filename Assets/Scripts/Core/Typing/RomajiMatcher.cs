using System;
using System.Collections.Generic;
using System.Text;

namespace Negiri.Core.Typing
{
    /// <summary>1打ごとの判定結果。</summary>
    public enum KeyResult
    {
        Correct,
        Miss,
        /// <summary>すでに打ち終わっている。</summary>
        AlreadyCompleted,
    }

    /// <summary>
    /// 読みがな（ひらがな）に対して、ローマ字のキー入力を1文字ずつ判定する。
    /// 「し」= shi / si のような複数の打ち方を受け付ける。
    /// 「ん」は次の文字が母音・や行・な行でなければ n 1回でもよく、最後の「ん」は nn が必要。
    /// 「っ」は次の文字の子音を重ねる（kka）か、xtu / ltu などで打つ。
    /// </summary>
    public sealed class RomajiMatcher
    {
        static readonly string[] SmallTsuAlone = { "xtu", "ltu", "xtsu", "ltsu" };
        static readonly string[] NAlone = { "nn", "xn" };

        readonly List<string> units;
        int unitIndex;
        string buffer = "";
        readonly StringBuilder typed = new StringBuilder();

        public string Reading { get; }
        public int CorrectKeys { get; private set; }
        public int MissKeys { get; private set; }
        public bool IsCompleted => unitIndex >= units.Count;

        /// <summary>これまでに正しく打ったローマ字。</summary>
        public string TypedRomaji => typed.ToString();

        public RomajiMatcher(string reading)
        {
            Reading = reading ?? throw new ArgumentNullException(nameof(reading));
            units = Tokenize(reading);
        }

        /// <summary>読みがなをひらがなの単位（拗音は2文字で1単位）に分ける。</summary>
        public static List<string> Tokenize(string reading)
        {
            var result = new List<string>();
            int i = 0;
            while (i < reading.Length)
            {
                if (i + 1 < reading.Length)
                {
                    var two = reading.Substring(i, 2);
                    if (KanaTable.Contains(two))
                    {
                        result.Add(two);
                        i += 2;
                        continue;
                    }
                }
                var one = reading.Substring(i, 1);
                if (one != "ん" && one != "っ" && !KanaTable.Contains(one))
                {
                    throw new ArgumentException($"読みがなに使えない文字があります: '{one}'（{reading}）");
                }
                result.Add(one);
                i += 1;
            }
            return result;
        }

        /// <summary>
        /// 読みがなを打つのに必要な最短の打鍵数。制限時間の計算に使う。
        /// </summary>
        public static int ShortestKeyCount(string reading)
        {
            var list = Tokenize(reading);
            int total = 0;
            for (int i = 0; i < list.Count; i++)
            {
                int best = int.MaxValue;
                foreach (var c in CandidatesFor(list, i))
                {
                    if (c.Length < best) best = c.Length;
                }
                total += best;
            }
            return total;
        }

        /// <summary>1文字を入力する。大文字は小文字として扱う。</summary>
        public KeyResult Input(char key)
        {
            if (IsCompleted) return KeyResult.AlreadyCompleted;
            key = char.ToLowerInvariant(key);

            if (TryConsume(key))
            {
                CorrectKeys++;
                typed.Append(key);
                return KeyResult.Correct;
            }

            MissKeys++;
            return KeyResult.Miss;
        }

        bool TryConsume(char key)
        {
            var candidates = CandidatesFor(units, unitIndex);
            var next = buffer + key;

            if (HasPrefix(candidates, next))
            {
                buffer = next;
                // ちょうど打ち終わり、これより長い候補がなければ次の単位へ
                if (Contains(candidates, buffer) && !HasLongerWithPrefix(candidates, buffer))
                {
                    Advance();
                }
                return true;
            }

            // 「ん」を n 1回で打ち終えたとみなして、次の単位でこのキーを試す
            if (buffer.Length > 0 && Contains(candidates, buffer))
            {
                var savedIndex = unitIndex;
                var savedBuffer = buffer;
                Advance();
                if (!IsCompleted && TryConsume(key)) return true;
                unitIndex = savedIndex;
                buffer = savedBuffer;
            }

            return false;
        }

        void Advance()
        {
            unitIndex++;
            buffer = "";
        }

        /// <summary>
        /// ガイド表示用。これまでに打った部分と、残りのおすすめの打ち方を返す。
        /// </summary>
        public (string typed, string remaining) Guide()
        {
            var sb = new StringBuilder();
            for (int i = unitIndex; i < units.Count; i++)
            {
                var candidates = CandidatesFor(units, i);
                string pick = candidates[0];
                if (i == unitIndex && buffer.Length > 0)
                {
                    foreach (var c in candidates)
                    {
                        if (c.StartsWith(buffer, StringComparison.Ordinal)) { pick = c; break; }
                    }
                    pick = pick.Substring(buffer.Length);
                }
                sb.Append(pick);
            }
            return (TypedRomaji, sb.ToString());
        }

        /// <summary>
        /// i 番目の単位の打ち方の候補。先頭がガイドに出すおすすめ。
        /// </summary>
        static List<string> CandidatesFor(List<string> list, int i)
        {
            var unit = list[i];
            var result = new List<string>();

            if (unit == "ん")
            {
                bool hasNext = i + 1 < list.Count;
                if (hasNext && CanUseSingleN(CandidatesFor(list, i + 1)))
                {
                    result.Add("n");
                }
                result.AddRange(NAlone);
                // ガイドは「次が子音なら n、それ以外は nn」
                return result;
            }

            if (unit == "っ")
            {
                if (i + 1 < list.Count)
                {
                    foreach (var c in CandidatesFor(list, i + 1))
                    {
                        var head = c[0];
                        if (IsDoublableConsonant(head))
                        {
                            var s = head.ToString();
                            if (!result.Contains(s)) result.Add(s);
                        }
                    }
                }
                result.AddRange(SmallTsuAlone);
                return result;
            }

            KanaTable.TryGet(unit, out var cands);
            result.AddRange(cands);
            return result;
        }

        static bool CanUseSingleN(List<string> nextCandidates)
        {
            foreach (var c in nextCandidates)
            {
                var head = c[0];
                if ("aiueoyn'".IndexOf(head) >= 0) return false;
            }
            return true;
        }

        static bool IsDoublableConsonant(char c)
        {
            return c >= 'a' && c <= 'z' && "aiueon".IndexOf(c) < 0;
        }

        static bool HasPrefix(List<string> candidates, string prefix)
        {
            foreach (var c in candidates)
            {
                if (c.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        static bool Contains(List<string> candidates, string s)
        {
            foreach (var c in candidates)
            {
                if (c == s) return true;
            }
            return false;
        }

        static bool HasLongerWithPrefix(List<string> candidates, string prefix)
        {
            foreach (var c in candidates)
            {
                if (c.Length > prefix.Length && c.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
