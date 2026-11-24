using System.Collections.Generic;

namespace Negiri.Core.Typing
{
    /// <summary>
    /// ひらがな1単位（1文字、または拗音などの2文字）に対するローマ字の打ち方の一覧。
    /// 先頭の候補が「ガイドに表示する打ち方」になる。
    /// 「ん」と「っ」は前後の文字で打ち方が変わるため、ここには含めず RomajiMatcher で扱う。
    /// </summary>
    public static class KanaTable
    {
        static readonly Dictionary<string, string[]> Map = Build();

        public static bool TryGet(string kana, out string[] candidates)
        {
            return Map.TryGetValue(kana, out candidates);
        }

        public static bool Contains(string kana)
        {
            return Map.ContainsKey(kana);
        }

        static Dictionary<string, string[]> Build()
        {
            var m = new Dictionary<string, string[]>();

            void Add(string kana, params string[] romaji) => m[kana] = romaji;

            // 母音
            Add("あ", "a"); Add("い", "i", "yi"); Add("う", "u", "wu", "whu"); Add("え", "e"); Add("お", "o");
            // か行
            Add("か", "ka", "ca"); Add("き", "ki"); Add("く", "ku", "cu", "qu"); Add("け", "ke"); Add("こ", "ko", "co");
            // さ行
            Add("さ", "sa"); Add("し", "shi", "si", "ci"); Add("す", "su"); Add("せ", "se", "ce"); Add("そ", "so");
            // た行
            Add("た", "ta"); Add("ち", "chi", "ti"); Add("つ", "tsu", "tu"); Add("て", "te"); Add("と", "to");
            // な行
            Add("な", "na"); Add("に", "ni"); Add("ぬ", "nu"); Add("ね", "ne"); Add("の", "no");
            // は行
            Add("は", "ha"); Add("ひ", "hi"); Add("ふ", "fu", "hu"); Add("へ", "he"); Add("ほ", "ho");
            // ま行
            Add("ま", "ma"); Add("み", "mi"); Add("む", "mu"); Add("め", "me"); Add("も", "mo");
            // や行
            Add("や", "ya"); Add("ゆ", "yu"); Add("よ", "yo");
            // ら行
            Add("ら", "ra"); Add("り", "ri"); Add("る", "ru"); Add("れ", "re"); Add("ろ", "ro");
            // わ行
            Add("わ", "wa"); Add("を", "wo");
            // 濁音・半濁音
            Add("が", "ga"); Add("ぎ", "gi"); Add("ぐ", "gu"); Add("げ", "ge"); Add("ご", "go");
            Add("ざ", "za"); Add("じ", "ji", "zi"); Add("ず", "zu"); Add("ぜ", "ze"); Add("ぞ", "zo");
            Add("だ", "da"); Add("ぢ", "di"); Add("づ", "du"); Add("で", "de"); Add("ど", "do");
            Add("ば", "ba"); Add("び", "bi"); Add("ぶ", "bu"); Add("べ", "be"); Add("ぼ", "bo");
            Add("ぱ", "pa"); Add("ぴ", "pi"); Add("ぷ", "pu"); Add("ぺ", "pe"); Add("ぽ", "po");
            Add("ゔ", "vu");
            // 小さい文字
            Add("ぁ", "xa", "la"); Add("ぃ", "xi", "li", "xyi", "lyi"); Add("ぅ", "xu", "lu");
            Add("ぇ", "xe", "le", "xye", "lye"); Add("ぉ", "xo", "lo");
            Add("ゃ", "xya", "lya"); Add("ゅ", "xyu", "lyu"); Add("ょ", "xyo", "lyo"); Add("ゎ", "xwa", "lwa");

            // 拗音（2文字）
            AddYoon(m, "き", "ky");
            AddYoon(m, "ぎ", "gy");
            AddYoon(m, "に", "ny");
            AddYoon(m, "ひ", "hy");
            AddYoon(m, "び", "by");
            AddYoon(m, "ぴ", "py");
            AddYoon(m, "み", "my");
            AddYoon(m, "り", "ry");
            AddYoon(m, "ぢ", "dy");
            Add("しゃ", "sha", "sya"); Add("しぃ", "syi"); Add("しゅ", "shu", "syu"); Add("しぇ", "she", "sye"); Add("しょ", "sho", "syo");
            Add("ちゃ", "cha", "tya", "cya"); Add("ちぃ", "tyi", "cyi"); Add("ちゅ", "chu", "tyu", "cyu"); Add("ちぇ", "che", "tye", "cye"); Add("ちょ", "cho", "tyo", "cyo");
            Add("じゃ", "ja", "zya", "jya"); Add("じぃ", "zyi", "jyi"); Add("じゅ", "ju", "zyu", "jyu"); Add("じぇ", "je", "zye", "jye"); Add("じょ", "jo", "zyo", "jyo");
            // 外来語でよく使う組み合わせ
            Add("てぃ", "thi"); Add("でぃ", "dhi"); Add("てゅ", "thu"); Add("でゅ", "dhu");
            Add("ふぁ", "fa"); Add("ふぃ", "fi"); Add("ふぇ", "fe"); Add("ふぉ", "fo");
            Add("うぃ", "wi"); Add("うぇ", "we"); Add("うぉ", "who");
            Add("ゔぁ", "va"); Add("ゔぃ", "vi"); Add("ゔぇ", "ve"); Add("ゔぉ", "vo");
            Add("とぅ", "twu"); Add("どぅ", "dwu");

            // 記号
            Add("ー", "-"); Add("、", ","); Add("。", "."); Add("！", "!"); Add("？", "?");
            Add("・", "/"); Add("　", " "); Add(" ", " ");

            // 2文字の単位は「1文字目＋小さい文字」と分けて打つこともできる（例：きゃ = ki + xya）
            var twoCharUnits = new List<string>();
            foreach (var key in m.Keys)
            {
                if (key.Length == 2) twoCharUnits.Add(key);
            }
            foreach (var unit in twoCharUnits)
            {
                var first = unit.Substring(0, 1);
                var second = unit.Substring(1, 1);
                if (!m.TryGetValue(first, out var firstCands) || !m.TryGetValue(second, out var secondCands)) continue;
                var list = new List<string>(m[unit]);
                foreach (var a in firstCands)
                {
                    foreach (var b in secondCands)
                    {
                        var combined = a + b;
                        if (!list.Contains(combined)) list.Add(combined);
                    }
                }
                m[unit] = list.ToArray();
            }

            // 英単語（達人の商品名など）は読みがなに小文字の英字・数字をそのまま書く
            for (var c = 'a'; c <= 'z'; c++) m[c.ToString()] = new[] { c.ToString() };
            for (var c = '0'; c <= '9'; c++) m[c.ToString()] = new[] { c.ToString() };

            return m;
        }

        static void AddYoon(Dictionary<string, string[]> m, string head, string prefix)
        {
            m[head + "ゃ"] = new[] { prefix + "a" };
            m[head + "ぃ"] = new[] { prefix + "i" };
            m[head + "ゅ"] = new[] { prefix + "u" };
            m[head + "ぇ"] = new[] { prefix + "e" };
            m[head + "ょ"] = new[] { prefix + "o" };
        }
    }
}
