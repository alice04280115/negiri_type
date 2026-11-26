using Negiri.Core.Shopping;
using Negiri.Game.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Negiri.Game.Screens
{
    /// <summary>会計結果。レシート風の表と、今回の節約額、財布の残高の変化。</summary>
    public sealed class ReceiptScreen : ScreenBase
    {
        readonly Text titleText, summaryText;
        readonly RowList header, list;

        static readonly Column[] Columns =
        {
            new Column(16, 220),
            new Column(236, 100, TextAnchor.MiddleRight),
            new Column(350, 110, TextAnchor.MiddleCenter),
            new Column(460, 70, TextAnchor.MiddleRight),
            new Column(540, 100, TextAnchor.MiddleRight),
        };

        public ReceiptScreen(ShoppingFlow flow, RectTransform parent) : base(flow, parent, "Receipt")
        {
            UiFactory.MakeBox(Root, UiFactory.Panel, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(680, 378));
            titleText = UiFactory.MakeText(Root, "", 22, UiFactory.Ink, new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(660, 30), TextAnchor.MiddleCenter);
            header = new RowList(Root, new Vector2(140, -84), 680, 22, 1, 15, Columns);
            list = new RowList(Root, new Vector2(140, -108), 680, 22, 12, 16, Columns);
            summaryText = UiFactory.MakeText(Root, "", 18, UiFactory.Ink, new Vector2(0.5f, 0), new Vector2(0, 52), new Vector2(900, 64), TextAnchor.LowerCenter);
            summaryText.lineSpacing = 1.1f;
        }

        public override void Tick(Keyboard kb, float deltaTime)
        {
            if (EnterPressed(kb)) Flow.StartShopping();
            else if (Pressed(kb.tabKey) || Pressed(kb.escapeKey)) Flow.ShowTitle();
        }

        public override void Refresh()
        {
            var r = Flow.Receipt;
            if (r == null) return;
            titleText.text = $"レシート　{Flow.Db.Store.name}";

            header.SetCount(1);
            header.Set(0, UiFactory.Quiet, false, "品名", "通常価格", "評価", "割引", "支払額");

            int shown = Mathf.Min(r.Lines.Count, list.Capacity);
            list.SetCount(shown);
            for (int i = 0; i < shown; i++)
            {
                var l = r.Lines[i];
                if (l.Returned)
                {
                    list.Set(i, UiFactory.Quiet, false, l.Cart.Item.name, UiFactory.Yen(l.BasePrice), "返品", "", "—");
                    continue;
                }
                var rank = UiFactory.Paint(UiFactory.RankLabel(l.Rank), UiFactory.RankColor(l.Rank));
                var discount = l.DiscountBp > 0 ? $"-{l.DiscountBp / 100.0:0.#}%" : "";
                list.Set(i, UiFactory.Ink, false, l.Cart.Item.name, UiFactory.Yen(l.BasePrice), rank, discount, UiFactory.Yen(l.Price));
            }

            var summary = $"合計 <b>{UiFactory.Yen(r.Paid)}</b>（通常価格 {UiFactory.Yen(r.BaseTotal)}）　今回の節約額 " + UiFactory.Paint(UiFactory.Yen(r.Saved), UiFactory.Good);
            summary += $"\n財布 {UiFactory.Yen(r.CashBefore)} → {UiFactory.Yen(r.CashAfter)}";
            if (r.ReturnCount > 0)
            {
                summary += $"　返品 {r.ReturnCount}品";
                if (r.SatisfactionLoss > 0) summary += $"（満足度 −{r.SatisfactionLoss}　※段階2では記録のみ）";
            }
            summaryText.text = summary;

            Message.text = $"Enter：もう一度買い物（財布は{UiFactory.Yen(Flow.Db.Balance.shopping.startingCash)}に戻ります）　Tab：タイトルへ";
            Message.color = UiFactory.Ink;
        }
    }
}
