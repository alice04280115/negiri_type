using System.Collections.Generic;
using Negiri.Core.Shopping;
using Negiri.Game.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Negiri.Game.Screens
{
    /// <summary>
    /// 会計結果。左に本物のような紙のレシート（特売は「特」と特売後の価格、その下にタイピング値引）、
    /// 右に今回の節約額と財布の残高の変化、在庫と満足度の変化。スペースで夜へ進む。
    /// </summary>
    public sealed class ReceiptScreen : ScreenBase
    {
        const int VisibleRows = 19;

        static readonly Color PaperColor = new Color32(0xf7, 0xf5, 0xef, 0xff);
        static readonly Color PaperInk = new Color32(0x2a, 0x2a, 0x30, 0xff);
        static readonly Color PaperQuiet = new Color32(0x8a, 0x8a, 0x94, 0xff);
        static readonly Color PaperRed = new Color32(0xc2, 0x3b, 0x3b, 0xff);

        readonly Text storeText, dateText, scrollText, savedText, walletText, returnText, menuText;
        readonly RowList paper;
        List<ReceiptRow> rows = new List<ReceiptRow>();
        int scroll;

        public ReceiptScreen(ShoppingFlow flow, RectTransform parent) : base(flow, parent, "Receipt")
        {
            // 左：紙のレシート
            UiFactory.MakeBox(Root, PaperColor, new Vector2(0, 1), new Vector2(110, -40), new Vector2(360, 452));
            storeText = UiFactory.MakeText(Root, "", 20, PaperInk, new Vector2(0, 1), new Vector2(110, -50), new Vector2(360, 26), TextAnchor.MiddleCenter);
            dateText = UiFactory.MakeText(Root, "", 14, PaperInk, new Vector2(0, 1), new Vector2(110, -78), new Vector2(360, 22), TextAnchor.MiddleCenter);
            paper = new RowList(Root, new Vector2(110, -104), 360, 19, VisibleRows, 15,
                new Column(16, 240),
                new Column(250, 24, TextAnchor.MiddleCenter),
                new Column(270, 76, TextAnchor.MiddleRight));
            scrollText = UiFactory.MakeText(Root, "", 13, PaperQuiet, new Vector2(0, 1), new Vector2(110, -470), new Vector2(360, 18), TextAnchor.MiddleCenter);

            // 右：まとめとメニュー
            UiFactory.MakeText(Root, "今回の節約額", 18, UiFactory.Quiet, new Vector2(0, 1), new Vector2(520, -90), new Vector2(380, 26), TextAnchor.MiddleLeft);
            savedText = UiFactory.MakeText(Root, "", 44, UiFactory.Good, new Vector2(0, 1), new Vector2(520, -122), new Vector2(380, 56), TextAnchor.MiddleLeft);
            walletText = UiFactory.MakeText(Root, "", 20, UiFactory.Ink, new Vector2(0, 1), new Vector2(520, -200), new Vector2(380, 56), TextAnchor.UpperLeft);
            walletText.lineSpacing = 1.2f;
            returnText = UiFactory.MakeText(Root, "", 16, UiFactory.Ink, new Vector2(0, 1), new Vector2(520, -262), new Vector2(400, 100), TextAnchor.UpperLeft);
            returnText.lineSpacing = 1.25f;
            menuText = UiFactory.MakeText(Root, "", 20, UiFactory.Ink, new Vector2(0, 1), new Vector2(520, -400), new Vector2(400, 40), TextAnchor.UpperLeft);
            menuText.lineSpacing = 1.4f;
        }

        public override void Enter()
        {
            scroll = 0;
            rows = Flow.Receipt != null ? ReceiptLines.Build(Flow.Receipt) : new List<ReceiptRow>();
        }

        int MaxScroll => Mathf.Max(0, rows.Count - VisibleRows);

        public override void Tick(Keyboard kb, float deltaTime)
        {
            if (Pressed(kb.upArrowKey)) scroll = Mathf.Max(0, scroll - 1);
            if (Pressed(kb.downArrowKey)) scroll = Mathf.Min(MaxScroll, scroll + 1);
            if (Decide(kb)) Flow.EndDay();
        }

        public override void Refresh()
        {
            var r = Flow.Receipt;
            if (r == null) return;
            storeText.text = $"<b>{Flow.Db.Store.name}</b>";
            var m = Flow.Month;
            dateText.text = m != null ? $"{m.Life.month.month}月{m.Day}日({Negiri.Core.Life.Calendar.WeekdayNames[m.Weekday]})　レジ#01　＜領 収 書＞" : "＜領 収 書＞";

            int shown = Mathf.Min(VisibleRows, rows.Count - scroll);
            paper.SetCount(shown);
            for (int i = 0; i < shown; i++) SetRow(i, rows[scroll + i]);
            scrollText.text = MaxScroll > 0
                ? (scroll < MaxScroll ? "▼ ↑↓でスクロール" : "▲ ↑↓でスクロール")
                : "";

            savedText.text = UiFactory.Yen(r.Saved);
            walletText.text = $"財布 {UiFactory.Yen(r.CashBefore)} → <b>{UiFactory.Yen(r.CashAfter)}</b>\n" +
                              UiFactory.Paint($"支払い {UiFactory.Yen(r.Paid)}（通常価格なら {UiFactory.Yen(r.BaseTotal)}）", UiFactory.Quiet);
            var sb = new System.Text.StringBuilder();
            var c = Flow.LastChange;
            if (c != null)
            {
                sb.Append($"食料　{c.FoodBefore} → <b>{c.FoodAfter}日分</b>");
                if (c.FoodWasted > 0) sb.Append(UiFactory.Paint($"（{c.FoodWasted}日分ムダ）", UiFactory.Bad));
                sb.Append($"\n日用品　{c.DailyBefore} → <b>{c.DailyAfter}日分</b>");
                if (c.DailyWasted > 0) sb.Append(UiFactory.Paint($"（{c.DailyWasted}日分ムダ）", UiFactory.Bad));
                sb.Append($"\n満足度　{c.SatisfactionBefore} → <b>{c.SatisfactionAfter}</b>");
            }
            if (r.ReturnCount > 0)
            {
                sb.Append('\n').Append(UiFactory.Paint($"返品 {r.ReturnCount}品" + (r.SatisfactionLoss > 0 ? $"（満足度 －{r.SatisfactionLoss}）" : ""), UiFactory.Warn));
            }
            returnText.text = sb.ToString();

            menuText.text = UiFactory.Paint("▶ 家に帰って夜へ", UiFactory.Warn);

            Message.text = MaxScroll > 0 ? "↑↓：レシートをスクロール　スペース：夜へ" : "スペース：夜へ";
            Message.color = UiFactory.Ink;
        }

        void SetRow(int i, ReceiptRow row)
        {
            switch (row.Kind)
            {
                case ReceiptRowKind.Separator:
                    paper.Set(i, PaperQuiet, false, "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -");
                    break;
                case ReceiptRowKind.Discount:
                    paper.Set(i, PaperRed, false, row.Left, row.Mark, row.Right);
                    break;
                case ReceiptRowKind.Note:
                case ReceiptRowKind.Returned:
                    paper.Set(i, PaperQuiet, false, row.Left, row.Mark, row.Right);
                    break;
                case ReceiptRowKind.Total:
                    paper.Set(i, PaperInk, false, $"<b><size=18>{row.Left}</size></b>", row.Mark, $"<b><size=18>{row.Right}</size></b>");
                    break;
                default:
                    paper.Set(i, PaperInk, false, row.Left, row.Mark, row.Right);
                    break;
            }
        }
    }
}
