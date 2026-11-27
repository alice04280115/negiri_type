using System.Collections.Generic;
using Negiri.Core.Shopping;
using Negiri.Game.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Negiri.Game.Screens
{
    /// <summary>
    /// 財布が足りないとき。
    /// 初級・中級は返品する商品を自分で選ぶ。
    /// 上級・達人は「借金して購入」か「返品して会計」を選ぶ。選んでいる方で返品される商品と金額を先に見せ、
    /// スペースで決める。借金は不足分ちょうどで、限度額を超える分は先に自動で返品される。
    /// </summary>
    public sealed class ReturnScreen : ScreenBase
    {
        readonly Text titleText, ruleText, summaryText, payText, loanText;
        readonly Image payBox, loanBox;
        readonly RowList list;
        int cursor;
        /// <summary>上級・達人で選んでいる方。0：借金して購入、1：返品して会計。</summary>
        int option;
        List<int> returnPlan = new List<int>();
        List<int> loanPlan = new List<int>();

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
            // 会計ボタン（初級・中級は一覧の最後の行、上級・達人は「借金して購入」と並べて左右で選ぶ）
            loanBox = UiFactory.MakeBox(Root, UiFactory.Bg, new Vector2(0.5f, 1), new Vector2(-160, -402), new Vector2(310, 30));
            loanText = UiFactory.MakeText(Root, "", 17, UiFactory.Ink, new Vector2(0.5f, 1), new Vector2(-160, -402), new Vector2(310, 30), TextAnchor.MiddleCenter);
            payBox = UiFactory.MakeBox(Root, UiFactory.Bg, new Vector2(0.5f, 1), new Vector2(0, -402), new Vector2(300, 30));
            payText = UiFactory.MakeText(Root, "", 18, UiFactory.Ink, new Vector2(0.5f, 1), new Vector2(0, -402), new Vector2(300, 30), TextAnchor.MiddleCenter);
            summaryText = UiFactory.MakeText(Root, "", 18, UiFactory.Ink, new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(900, 60), TextAnchor.LowerCenter);
            summaryText.lineSpacing = 1.1f;
        }

        bool Auto => Flow.Checkout.Rule.IsAutoReturn;
        bool CanBorrow => Flow.Checkout.Rule.CanBorrow;
        string Lender => Flow.Checkout.Rule.borrowFrom;
        int Room => Flow.Month.Debt.Room(Lender);

        /// <summary>借金で払うときに借りる額（限度額に収めるための返品の後）。</summary>
        int LoanAmount(Checkout checkout) => System.Math.Max(0, checkout.Total - Sum(checkout, loanPlan) - Flow.Wallet.Cash);

        /// <summary>借りられるか（限度額いっぱいなら借りられない）。</summary>
        bool LoanUsable => CanBorrow && Room > 0;

        public override void Enter()
        {
            cursor = 0;
            var checkout = Flow.Checkout;
            int cash = Flow.Wallet.Cash;
            returnPlan = Auto ? checkout.PlanAutoReturn(cash) : new List<int>();
            loanPlan = CanBorrow ? checkout.PlanReturnForLoan(cash, Room) : new List<int>();
            option = LoanUsable ? 0 : 1;
            bool showLoan = CanBorrow;
            loanBox.gameObject.SetActive(showLoan);
            loanText.gameObject.SetActive(showLoan);
            // 借金を並べるときは会計ボタンを右に寄せる
            var pos = showLoan ? new Vector2(160, -402) : new Vector2(0, -402);
            payBox.rectTransform.anchoredPosition = pos;
            payText.rectTransform.anchoredPosition = pos;
        }

        public override void Tick(Keyboard kb, float deltaTime)
        {
            var checkout = Flow.Checkout;
            if (Auto)
            {
                if (CanBorrow)
                {
                    if (Pressed(kb.leftArrowKey) || Pressed(kb.upArrowKey)) option = 0;
                    if (Pressed(kb.rightArrowKey) || Pressed(kb.downArrowKey)) option = 1;
                    if (!LoanUsable) option = 1;
                }
                if (!Decide(kb)) return;
                if (CanBorrow && option == 0)
                {
                    Flow.PayWithLoan();
                    return;
                }
                checkout.ApplyAutoReturn(Flow.Wallet.Cash);
                Flow.Pay();
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
            bool loan = CanBorrow && option == 0;
            var plan = loan ? loanPlan : returnPlan;

            // 返品後の合計（自動のときは計画どおりに返品したら、で計算する）
            int returnedAmount = 0;
            int returnCount = 0;
            for (int i = 0; i < checkout.Lines.Count; i++)
            {
                var l = checkout.Lines[i];
                if (l.Returned || plan.Contains(i)) { returnedAmount += l.Price; returnCount++; }
            }
            int total = Auto ? checkout.Total - Sum(checkout, plan) : checkout.Total;
            int shortage = System.Math.Max(0, total - cash);

            int before = 0;
            foreach (var l in checkout.Lines) before += l.Price;
            titleText.text = $"財布が{UiFactory.Yen(System.Math.Max(0, before - cash))}足りません";
            ruleText.text = $"{rule.label}のルール：{TitleScreen.ShortageDescription(rule)}";

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

            string satisfaction = rule.satisfactionPerReturn > 0 && returnCount > 0 ? $"　満足度 －{returnCount * rule.satisfactionPerReturn}" : "";
            if (loan)
            {
                var debt = Flow.Month.Debt;
                var lender = debt.Lender(Lender);
                int borrow = LoanAmount(checkout);
                int now = debt.BalanceOf(Lender);
                var sb = new System.Text.StringBuilder();
                sb.Append($"{lender.name}で {UiFactory.Paint(UiFactory.Yen(borrow), UiFactory.Bad)} 借りる　お支払い {UiFactory.Yen(total)}");
                if (returnCount > 0) sb.Append($"　返品 {returnCount}品（{UiFactory.Yen(returnedAmount)}）{satisfaction}");
                sb.Append('\n');
                sb.Append(UiFactory.Paint($"借金 {UiFactory.Yen(now)} → {UiFactory.Yen(now + borrow)}（限度額 {UiFactory.Yen(lender.limit)}）　月末に{lender.interestBp / 100f:0.##}%の利息がつきます", UiFactory.Warn));
                if (returnCount > 0) sb.Append('\n').Append(UiFactory.Paint("限度額を超える分は返品されます", UiFactory.Bad));
                summaryText.text = sb.ToString();
            }
            else
            {
                var summary = $"返品 {returnCount}品（{UiFactory.Yen(returnedAmount)}）　お支払い {UiFactory.Yen(total)}　財布 {UiFactory.Yen(cash)}{satisfaction}";
                summary += "\n" + (shortage > 0
                    ? UiFactory.Paint($"あと{UiFactory.Yen(shortage)}分の返品が必要です", UiFactory.Bad)
                    : UiFactory.Paint("財布で払えます", UiFactory.Good));
                if (CanBorrow && !LoanUsable) summary += "\n" + UiFactory.Paint("借金が限度額いっぱいなので、借りられません", UiFactory.Quiet);
                summaryText.text = summary;
            }

            // ボタン
            bool paySelected = Auto ? !loan : cursor == checkout.Lines.Count;
            bool canPay = Auto || shortage == 0;
            payBox.color = paySelected && canPay ? UiFactory.Warn : UiFactory.Bg;
            payText.text = Auto ? (CanBorrow ? "▶ 返品して会計" : "▶ この内容で返品して会計") : (canPay ? "▶ 会計する" : "会計する（まだ足りません）");
            payText.color = paySelected && canPay ? UiFactory.Bg : (canPay ? UiFactory.Ink : UiFactory.Quiet);
            if (CanBorrow)
            {
                loanBox.color = loan ? UiFactory.Bad : UiFactory.Bg;
                loanText.text = LoanUsable ? "▶ 借金して購入" : "借金して購入（限度額いっぱい）";
                loanText.color = loan ? UiFactory.Bg : (LoanUsable ? UiFactory.Ink : UiFactory.Quiet);
            }

            if (Auto && CanBorrow) Message.text = "←→：借金して購入／返品して会計を選ぶ　スペース：決定";
            else if (Auto) Message.text = "スペース：この内容で返品して会計";
            else Message.text = "↑↓：選ぶ　スペース：返品する／取り消す、会計する";
            Message.color = UiFactory.Ink;
        }

        static int Sum(Checkout checkout, List<int> plan)
        {
            int sum = 0;
            foreach (var i in plan) sum += checkout.Lines[i].Price;
            return sum;
        }
    }
}
