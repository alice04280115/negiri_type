using System.Collections.Generic;
using Negiri.Core.Shopping;
using Negiri.Game.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Negiri.Game.Screens
{
    /// <summary>
    /// 財布が足りないときの返品。
    /// 初級・中級は返品する商品を自分で選ぶ。上級・達人は自動で返品される商品と金額を先に見せ、確認してから会計する。
    /// </summary>
    public sealed class ReturnScreen : ScreenBase
    {
        readonly Text titleText, ruleText, summaryText, payText;
        readonly Image payBox;
        readonly RowList list;
        int cursor;
        List<int> plan = new List<int>();

        public ReturnScreen(ShoppingFlow flow, RectTransform parent) : base(flow, parent, "Return")
        {
            titleText = UiFactory.MakeText(Root, "", 26, UiFactory.Bad, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(900, 36), TextAnchor.MiddleCenter);
            ruleText = UiFactory.MakeText(Root, "", 16, UiFactory.Quiet, new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(900, 24), TextAnchor.MiddleCenter);
            UiFactory.MakeBox(Root, UiFactory.Panel, new Vector2(0.5f, 1), new Vector2(0, -108), new Vector2(620, 288));
            list = new RowList(Root, new Vector2(170, -112), 620, 23, 12, 17,
                new Column(16, 260),
                new Column(276, 110),
                new Column(386, 100, TextAnchor.MiddleRight),
                new Column(500, 110));
            // 会計ボタン（初級・中級は一覧の最後の行、上級・達人はこれだけを選ぶ）
            payBox = UiFactory.MakeBox(Root, UiFactory.Bg, new Vector2(0.5f, 1), new Vector2(0, -402), new Vector2(300, 30));
            payText = UiFactory.MakeText(Root, "", 18, UiFactory.Ink, new Vector2(0.5f, 1), new Vector2(0, -402), new Vector2(300, 30), TextAnchor.MiddleCenter);
            summaryText = UiFactory.MakeText(Root, "", 18, UiFactory.Ink, new Vector2(0.5f, 0), new Vector2(0, 52), new Vector2(900, 52), TextAnchor.LowerCenter);
            summaryText.lineSpacing = 1.1f;
        }

        bool Auto => Flow.Checkout.Rule.IsAutoReturn;

        public override void Enter()
        {
            cursor = 0;
            plan = Auto ? Flow.Checkout.PlanAutoReturn(Flow.Wallet.Cash) : new List<int>();
        }

        public override void Tick(Keyboard kb, float deltaTime)
        {
            var checkout = Flow.Checkout;
            if (Auto)
            {
                if (Decide(kb))
                {
                    checkout.ApplyAutoReturn(Flow.Wallet.Cash);
                    Flow.Pay();
                }
                return;
            }

            // 商品の行 ＋ 最後に「会計する」ボタン
            int count = checkout.Lines.Count;
            cursor = MoveCursor(kb, cursor, count + 1);
            if (!Decide(kb)) return;
            if (cursor < count) checkout.ToggleReturn(cursor);
            else if (checkout.Shortage(Flow.Wallet.Cash) == 0) Flow.Pay();
        }

        public override void Refresh()
        {
            var checkout = Flow.Checkout;
            if (checkout == null || checkout.IsPaid) return;
            int cash = Flow.Wallet.Cash;
            var rule = checkout.Rule;

            // 返品後の合計（自動のときは計画どおりに返品したら、で計算する）
            int returnedAmount = 0;
            int returnCount = 0;
            for (int i = 0; i < checkout.Lines.Count; i++)
            {
                var l = checkout.Lines[i];
                if (l.Returned || plan.Contains(i)) { returnedAmount += l.Price; returnCount++; }
            }
            int total = Auto ? checkout.Total - PlannedAmount(checkout) : checkout.Total;
            int shortage = System.Math.Max(0, total - cash);

            int before = 0;
            foreach (var l in checkout.Lines) before += l.Price;
            titleText.text = $"財布が{UiFactory.Yen(System.Math.Max(0, before - cash))}足りません";
            ruleText.text = $"{rule.label}のルール：{TitleScreen.ShortageDescription(rule)}" + (rule.satisfactionPerReturn > 0 ? "　※段階2では満足度は記録のみ" : "");

            int shown = Mathf.Min(checkout.Lines.Count, list.Capacity);
            list.SetCount(shown);
            for (int i = 0; i < shown; i++)
            {
                var l = checkout.Lines[i];
                bool returned = l.Returned || plan.Contains(i);
                string status = returned ? (Auto ? "返品されます" : "返品する") : "";
                var color = returned ? UiFactory.Bad : UiFactory.Ink;
                bool selected = !Auto && i == cursor;
                list.Set(i, color, selected, l.Cart.Item.name, UiFactory.RankLabel(l.Rank), UiFactory.Yen(l.Price), status);
            }

            var summary = $"返品 {returnCount}品（{UiFactory.Yen(returnedAmount)}）　お支払い {UiFactory.Yen(total)}　財布 {UiFactory.Yen(cash)}";
            if (rule.satisfactionPerReturn > 0) summary += $"　満足度 −{returnCount * rule.satisfactionPerReturn}";
            summary += "\n" + (shortage > 0
                ? UiFactory.Paint($"あと{UiFactory.Yen(shortage)}分の返品が必要です", UiFactory.Bad)
                : UiFactory.Paint("財布で払えます", UiFactory.Good));
            summaryText.text = summary;

            bool paySelected = Auto || cursor == checkout.Lines.Count;
            bool canPay = Auto || shortage == 0;
            payBox.color = paySelected && canPay ? UiFactory.Warn : UiFactory.Bg;
            payText.text = Auto ? "▶ この内容で返品して会計" : (canPay ? "▶ 会計する" : "会計する（まだ足りません）");
            payText.color = paySelected && canPay ? UiFactory.Bg : (canPay ? UiFactory.Ink : UiFactory.Quiet);

            Message.text = Auto
                ? "スペース：この内容で返品して会計"
                : "↑↓：選ぶ　スペース：返品する／取り消す、会計する";
            Message.color = UiFactory.Ink;
        }

        int PlannedAmount(Checkout checkout)
        {
            int sum = 0;
            foreach (var i in plan) sum += checkout.Lines[i].Price;
            return sum;
        }
    }
}
