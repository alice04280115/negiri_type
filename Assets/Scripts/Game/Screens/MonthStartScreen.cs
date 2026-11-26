using Negiri.Game.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Negiri.Game.Screens
{
    /// <summary>
    /// 月初。予算を受け取り、目標商品を見て、最初のロック貯金を決める。
    /// ロックは解除できないので、確認を出してから確定する。
    /// </summary>
    public sealed class MonthStartScreen : ScreenBase
    {
        readonly Text headText, budgetText, lockText, hintText, confirmText;
        int lockAmount;
        bool confirming;

        public MonthStartScreen(ShoppingFlow flow, RectTransform parent) : base(flow, parent, "MonthStart")
        {
            headText = UiFactory.MakeText(Root, "", 30, UiFactory.Ink, new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(900, 40), TextAnchor.MiddleCenter);

            // 左：目標商品の絵
            UiFactory.MakeBox(Root, UiFactory.Panel, new Vector2(0, 1), new Vector2(60, -104), new Vector2(300, 300));
            ArtSlot.Make(Root, "target", "目標商品の絵", UiFactory.PanelLight, new Vector2(0, 1), new Vector2(110, -124), new Vector2(200, 200));
            budgetText = UiFactory.MakeText(Root, "", 18, UiFactory.Ink, new Vector2(0, 1), new Vector2(76, -334), new Vector2(270, 60), TextAnchor.UpperCenter);
            budgetText.lineSpacing = 1.2f;

            // 右：ロック額
            UiFactory.MakeBox(Root, UiFactory.Panel, new Vector2(0, 1), new Vector2(390, -104), new Vector2(510, 300));
            UiFactory.MakeText(Root, "ロック貯金（目標商品にしか使えないお金）", 18, UiFactory.Quiet, new Vector2(0, 1), new Vector2(410, -118), new Vector2(480, 26), TextAnchor.MiddleLeft);
            lockText = UiFactory.MakeText(Root, "", 36, UiFactory.Gold, new Vector2(0, 1), new Vector2(410, -156), new Vector2(480, 50), TextAnchor.MiddleCenter);
            hintText = UiFactory.MakeText(Root, "", 17, UiFactory.Ink, new Vector2(0, 1), new Vector2(410, -222), new Vector2(480, 170), TextAnchor.UpperLeft);
            hintText.lineSpacing = 1.25f;

            confirmText = UiFactory.MakeText(Root, "", 20, UiFactory.Bad, new Vector2(0.5f, 0), new Vector2(0, 62), new Vector2(900, 30), TextAnchor.MiddleCenter);
        }

        int Step => Mathf.Max(1, Flow.Life.month.lockStep);

        public override void Enter()
        {
            lockAmount = 0;
            confirming = false;
        }

        public override void Tick(Keyboard kb, float deltaTime)
        {
            int cash = Flow.Wallet.Cash;
            if (Pressed(kb.escapeKey))
            {
                if (confirming) confirming = false;
                else Flow.ShowTitle();
                return;
            }
            if (!confirming)
            {
                if (Pressed(kb.rightArrowKey) || Pressed(kb.upArrowKey)) lockAmount = Mathf.Min(cash - cash % Step, lockAmount + Step);
                if (Pressed(kb.leftArrowKey) || Pressed(kb.downArrowKey)) lockAmount = Mathf.Max(0, lockAmount - Step);
            }
            if (!Decide(kb)) return;
            if (lockAmount > 0 && !confirming)
            {
                confirming = true;
                return;
            }
            Flow.ConfirmMonthStart(lockAmount);
        }

        public override void Refresh()
        {
            var m = Flow.Month;
            if (m == null) return;
            var month = Flow.Life.month;
            headText.text = $"{month.month}月のはじまり";
            budgetText.text = $"目標：<b>{month.targetName}</b>\n{UiFactory.Yen(month.targetPrice)}（月末に買う）";

            int cash = m.Wallet.Cash;
            lockText.text = UiFactory.Yen(lockAmount);
            int left = cash - lockAmount;
            hintText.text =
                $"今月の予算　{UiFactory.Yen(cash)}\n" +
                $"ロックした後の財布　{UiFactory.Paint(UiFactory.Yen(left), left < month.essentialsEstimate ? UiFactory.Bad : UiFactory.Warn)}\n" +
                UiFactory.Paint($"1か月の必需品の目安：約{UiFactory.Yen(month.essentialsEstimate)}（通常価格）", UiFactory.Quiet) + "\n" +
                UiFactory.Paint($"買い物：{TitleScreen.IntervalLabel(m.Rule.shopIntervalDays)}　食料は{m.Rule.foodCap}日分まで", UiFactory.Quiet) + "\n" +
                UiFactory.Paint("ロックは後から家計簿で追加できます。解除はできません。", UiFactory.Quiet);

            confirmText.text = confirming
                ? $"{UiFactory.Yen(lockAmount)}をロックします。解除できません。よければもう一度スペース"
                : "";
            Message.text = confirming
                ? "スペース：確定　Esc：やめる"
                : $"←→：ロック額（{UiFactory.Yen(Step)}ずつ）　スペース：決定して1日目へ　Esc：タイトル";
        }
    }
}
