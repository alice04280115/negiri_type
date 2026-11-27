using Negiri.Core.Data;
using Negiri.Core.Typing;
using UnityEngine;
using UnityEngine.UI;

namespace Negiri.Game.Ui
{
    /// <summary>
    /// 商品名・読みがな・ローマ字の見本・時間バー・残り時間の表示（段階1の TypingPrototype から切り出し）。
    /// タイピング練習とレジの両方で使う。結果や操作の案内は、使う側の画面が出す。
    /// </summary>
    public sealed class TypingView
    {
        readonly Text sentenceText, readingText, guideText, barRankText, previewText, ruleText;
        readonly RectTransform barFill;
        readonly Image barFillImage;

        public TypingView(RectTransform root)
        {
            sentenceText = UiFactory.MakeText(root, "", 40, UiFactory.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, 100), new Vector2(900, 52), TextAnchor.MiddleCenter);
            // 画面下の操作案内（「スペースキーで開始」など）のすぐ上に出す
            ruleText = UiFactory.MakeText(root, "", 16, UiFactory.Warn, new Vector2(0.5f, 0), new Vector2(0, 74), new Vector2(900, 22), TextAnchor.MiddleCenter);
            readingText = UiFactory.MakeText(root, "", 20, UiFactory.Quiet, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(900, 28), TextAnchor.MiddleCenter);
            guideText = UiFactory.MakeText(root, "", 28, UiFactory.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(900, 40), TextAnchor.MiddleCenter);

            var frame = UiFactory.NewRect("TimeBar", root);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(640, 18);
            frame.anchoredPosition = new Vector2(0, -20);
            frame.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.12f);

            barFill = UiFactory.NewRect("Fill", frame);
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = Vector2.one;
            barFill.offsetMin = barFill.offsetMax = Vector2.zero;
            barFillImage = barFill.gameObject.AddComponent<Image>();

            barRankText = UiFactory.MakeText(frame, "", 18, UiFactory.Ink, new Vector2(1, 0.5f), new Vector2(12, 0), new Vector2(180, 24), TextAnchor.MiddleLeft);
            barRankText.rectTransform.pivot = new Vector2(0, 0.5f);

            // 評価の境目（残り45%と25%）に目盛りを入れる
            AddMarker(frame, 0.45f, "EXCELLENT");
            AddMarker(frame, 0.25f, "GREAT");

            previewText = UiFactory.MakeText(root, "", 18, UiFactory.Ink, new Vector2(1, 0), new Vector2(-24, 20), new Vector2(260, 50), TextAnchor.LowerRight);
        }

        static void AddMarker(RectTransform frame, float ratio, string label)
        {
            var line = UiFactory.NewRect("Marker", frame);
            line.anchorMin = new Vector2(ratio, 0);
            line.anchorMax = new Vector2(ratio, 1);
            line.sizeDelta = new Vector2(2, 8);
            line.gameObject.AddComponent<Image>().color = UiFactory.Ink;
            var t = UiFactory.MakeText(frame, label, 12, UiFactory.Quiet, new Vector2(ratio, 0), new Vector2(0, -14), new Vector2(120, 16), TextAnchor.MiddleCenter);
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>
        /// 表示を更新する。ready は開始前（時間が動いていない）、finished は打ち終えた（時間切れを含む）。
        /// readingOn は上級・達人でプレイヤーが読みがなを出しているかどうか。
        /// </summary>
        public void Render(TypingSession session, SentenceData sentence, DifficultyDef difficulty, bool readingOn, TypingBalance balance, float missFlash)
        {
            bool ready = session.State == SessionState.Ready;
            bool finished = session.State == SessionState.Finished;

            sentenceText.text = sentence.text;
            ruleText.text = TypingRuleHint(sentence.text);
            // 達人のサブタイトルつきの長い商品名でも画面からはみ出さないよう、文字を小さくする
            sentenceText.fontSize = sentence.text.Length <= 14 ? 40 : sentence.text.Length <= 22 ? 32 : 26;
            var (typed, remaining) = session.Matcher.Guide();
            if (ShowReading(session, difficulty, readingOn, balance))
            {
                readingText.text = sentence.reading;
                guideText.text = UiFactory.Paint(typed, UiFactory.Quiet) + UiFactory.Paint(remaining, UiFactory.Ink);
            }
            else
            {
                // 読みがなを隠しているときは、打った分のローマ字だけを出す
                readingText.text = difficulty.Reading == ReadingMode.OptionalWithReveal
                    ? "（読みがなは残り時間がEXCELLENTの線を切ると出ます）"
                    : "（読みがななし）";
                guideText.text = UiFactory.Paint(typed, UiFactory.Quiet) + UiFactory.Paint("_", UiFactory.Ink);
            }

            // バーは実際の評価：時間で減り、ミスでも減る。色と右の文字は、いま打ち終えたときの評価
            var p = session.Preview();
            float ratio = ready ? 1f : session.RemainingRatio;
            barFill.anchorMax = new Vector2(ratio, 1f);
            barFillImage.color = missFlash > 0f ? UiFactory.Ink : UiFactory.RankColor(finished ? session.Result.Value.Rank : p.Rank);
            barRankText.text = session.State == SessionState.Running ? $"{UiFactory.RankLabel(p.Rank)}  -{p.DiscountBp / 100}%" : "";
            barRankText.color = UiFactory.RankColor(p.Rank);

            previewText.text = ready
                ? $"制限時間 {session.TimeLimit:0.0}秒\nミス1回で -{balance.missPenaltySeconds:0.0}秒"
                : $"残り {session.Remaining:0.0}秒\n正確率 {p.Accuracy * 100:0}%　ミス {session.Matcher.MissKeys}";
        }

        /// <summary>
        /// 商品名にスペース・記号・英字があるとき、打たなくていいものを案内する。
        /// 読みがなはひらがなと小文字の英字だけなので、スペースと記号は打たない。英字は Shift なしでよい。
        /// </summary>
        public static string TypingRuleHint(string text)
        {
            bool symbol = false, letter = false;
            foreach (var ch in text ?? "")
            {
                if (ch >= 'A' && ch <= 'Z' || ch >= 'a' && ch <= 'z') letter = true;
                else if (ch == ' ' || ch == '　' || ch == '～' || ch == '・' || ch == '！' || ch == '？' || ch == '「' || ch == '」') symbol = true;
            }
            if (symbol && letter) return "スペースと記号（～ など）は打ちません。英字は小文字のまま打てばOK";
            if (symbol) return "スペースと記号（～ など）は打ちません";
            if (letter) return "英字は小文字のまま打てばOK";
            return "";
        }

        static bool ShowReading(TypingSession session, DifficultyDef difficulty, bool readingOn, TypingBalance balance)
        {
            var mode = difficulty.Reading;
            if (mode == ReadingMode.Always || readingOn) return true;
            if (mode == ReadingMode.Optional) return false;
            // 上級：隠していても、EXCELLENT の線（いちばん高い時間の条件）を切ったら出す
            if (session.State == SessionState.Ready) return false;
            float line = 0f;
            foreach (var rule in balance.ranks) line = Mathf.Max(line, rule.minRemainingRatio);
            return session.RemainingRatio < line;
        }

        /// <summary>時間は足りていたのに正確率で届かなかったとき、その理由（結果表示に添える）。</summary>
        public static string MissedHint(TypingResult r, TypingBalance balance)
        {
            var rule = TypingEvaluator.MissedByAccuracy(r, balance);
            if (rule == null) return "";
            string why;
            if (r.Accuracy + 1e-5f < rule.minAccuracy)
                why = $"正確率があと{System.Math.Ceiling((rule.minAccuracy - r.Accuracy) * 100 - 0.001):0}%あれば";
            else
                why = "ミスなしなら";
            return "\n<size=16>" + UiFactory.Paint($"時間は{UiFactory.RankLabel(rule.RankValue)}に届いています。{why}{UiFactory.RankLabel(rule.RankValue)}でした", UiFactory.Warn) + "</size>";
        }
    }
}
