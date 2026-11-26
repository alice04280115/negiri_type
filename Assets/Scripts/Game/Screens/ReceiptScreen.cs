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
    /// 右に今回の節約額と財布の残高の変化、次にすることのメニュー。
    /// </summary>
    public sealed class ReceiptScreen : ScreenBase
    {
        const int VisibleRows = 19;

        static readonly Color PaperColor = new Color32(0xf7, 0xf5, 0xef, 0xff);
        static readonly Color PaperInk = new Color32(0x2a, 0x2a, 0x30, 0xff);
        static readonly Color PaperQuiet = new Color32(0x8a, 0x8a, 0x94, 0xff);
        static readonly Color PaperRed = new Color32(0xc2, 0x3b, 0x3b, 0xff);

        readonly Text storeText, scrollText, savedText, walletText, returnText, menuText;
        readonly RowList paper;
        List<ReceiptRow> rows = new List<ReceiptRow>();
        int scroll;
        int cursor;

        public ReceiptScreen(ShoppingFlow flow, RectTransform parent) : base(flow, parent, "Receipt")
        {
            // 左：紙のレシート
            UiFactory.MakeBox(Root, PaperColor, new Vector2(0, 1), new Vector2(110, -40), new Vector2(360, 452));
            storeText = UiFactory.MakeText(Root, "", 20, PaperInk, new Vector2(0, 1), new Vector2(110, -50), new Vector2(360, 26), TextAnchor.MiddleCenter);
            UiFactory.MakeText(Root, "＜領 収 書＞", 16, PaperInk, new Vector2(0, 1), new Vector2(110, -78), new Vector2(360, 22), TextAnchor.MiddleCenter);
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
            returnText = UiFactory.MakeText(Root, "", 16, UiFactory.Warn, new Vector2(0, 1), new Vector2(520, -270), new Vector2(380, 48), TextAnchor.UpperLeft);
            returnText.lineSpacing = 1.2f;
            menuText = UiFactory.MakeText(Root, "", 20, UiFactory.Ink, new Vector2(0, 1), new Vector2(520, -360), new Vector2(400, 70), TextAnchor.UpperLeft);
            menuText.lineSpacing = 1.4f;
        }

        public override void Enter()
        {
            cursor = 0;
            scroll = 0;
            rows = Flow.Receipt != null ? ReceiptLines.Build(Flow.Receipt) : new List<ReceiptRow>();
        }

        int MaxScroll => Mathf.Max(0, rows.Count - VisibleRows);

        public override void Tick(Keyboard kb, float deltaTime)
        {
            if (Pressed(kb.upArrowKey)) scroll = Mathf.Max(0, scroll - 1);
            if (Pressed(kb.downArrowKey)) scroll = Mathf.Min(MaxScroll, scroll + 1);
            if (Pressed(kb.leftArrowKey)) cursor = 0;
            if (Pressed(kb.rightArrowKey)) cursor = 1;
            if (!Decide(kb)) return;
            if (cursor == 0) Flow.StartShopping();
            else Flow.ShowTitle();
        }

        public override void Refresh()
        {
            var r = Flow.Receipt;
            if (r == null) return;
            storeText.text = $"<b>{Flow.Db.Store.name}</b>";

            int shown = Mathf.Min(VisibleRows, rows.Count - scroll);
            paper.SetCount(shown);
            for (int i = 0; i < shown; i++) SetRow(i, rows[scroll + i]);
            scrollText.text = MaxScroll > 0
                ? (scroll < MaxScroll ? "▼ ↑↓でスクロール" : "▲ ↑↓でスクロール")
                : "";

            savedText.text = UiFactory.Yen(r.Saved);
            walletText.text = $"財布 {UiFactory.Yen(r.CashBefore)} → <b>{UiFactory.Yen(r.CashAfter)}</b>\n" +
                              UiFactory.Paint($"支払い {UiFactory.Yen(r.Paid)}（通常価格なら {UiFactory.Yen(r.BaseTotal)}）", UiFactory.Quiet);
            if (r.ReturnCount > 0)
            {
                returnText.text = $"返品 {r.ReturnCount}品" +
                    (r.SatisfactionLoss > 0 ? $"　満足度 −{r.SatisfactionLoss}\n" + UiFactory.Paint("※段階2では満足度は記録のみ", UiFactory.Quiet) : "");
            }
            else
            {
                returnText.text = "";
            }

            var again = $"もう一度買い物（財布は{UiFactory.Yen(Flow.Db.Balance.shopping.startingCash)}に戻ります）";
            menuText.text = (cursor == 0 ? UiFactory.Paint("▶ " + again, UiFactory.Warn) : UiFactory.Paint("　" + again, UiFactory.Quiet))
                + "\n" + (cursor == 1 ? UiFactory.Paint("▶ タイトルへ", UiFactory.Warn) : UiFactory.Paint("　タイトルへ", UiFactory.Quiet));

            Message.text = MaxScroll > 0 ? "↑↓：レシートをスクロール　←→：選ぶ　スペース：決定" : "←→：選ぶ　スペース：決定";
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
