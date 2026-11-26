using Negiri.Core.Life;
using Negiri.Game.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Negiri.Game.Screens
{
    /// <summary>家計簿。今月買った物（新しい順）、支出と節約額の合計、ロック貯金の追加。</summary>
    public sealed class LedgerScreen : ScreenBase
    {
        const int VisibleRows = 12;

        readonly Text titleText, totalsText, lockText, scrollText;
        readonly RowList list;
        int scroll;
        int lockAmount;
        bool confirming;

        public LedgerScreen(ShoppingFlow flow, RectTransform parent) : base(flow, parent, "Ledger")
        {
            UiFactory.MakeBox(Root, UiFactory.Panel, new Vector2(0, 1), new Vector2(24, -48), new Vector2(560, 400));
            titleText = UiFactory.MakeText(Root, "", 20, UiFactory.Ink, new Vector2(0, 1), new Vector2(40, -54), new Vector2(520, 28), TextAnchor.MiddleLeft);
            list = new RowList(Root, new Vector2(30, -88), 548, 26, VisibleRows, 16,
                new Column(10, 120),
                new Column(130, 200),
                new Column(330, 110),
                new Column(440, 96, TextAnchor.MiddleRight));
            scrollText = UiFactory.MakeText(Root, "", 13, UiFactory.Quiet, new Vector2(0, 1), new Vector2(24, -420), new Vector2(560, 20), TextAnchor.MiddleCenter);

            UiFactory.MakeBox(Root, UiFactory.Panel, new Vector2(0, 1), new Vector2(600, -48), new Vector2(336, 400));
            totalsText = UiFactory.MakeText(Root, "", 17, UiFactory.Ink, new Vector2(0, 1), new Vector2(616, -60), new Vector2(310, 170), TextAnchor.UpperLeft);
            totalsText.lineSpacing = 1.3f;
            lockText = UiFactory.MakeText(Root, "", 17, UiFactory.Ink, new Vector2(0, 1), new Vector2(616, -250), new Vector2(310, 180), TextAnchor.UpperLeft);
            lockText.lineSpacing = 1.3f;
        }

        public override void Enter()
        {
            scroll = 0;
            lockAmount = 0;
            confirming = false;
        }

        int Step => Mathf.Max(1, Flow.Life.month.lockStep);
        int MaxScroll => Mathf.Max(0, Flow.Month.Ledger.Entries.Count - VisibleRows);

        public override void Tick(Keyboard kb, float deltaTime)
        {
            if (Pressed(kb.escapeKey))
            {
                if (confirming) confirming = false;
                else Flow.ShowMyRoom();
                return;
            }
            if (Pressed(kb.upArrowKey)) scroll = Mathf.Max(0, scroll - 1);
            if (Pressed(kb.downArrowKey)) scroll = Mathf.Min(MaxScroll, scroll + 1);
            int cash = Flow.Wallet.Cash;
            if (!confirming)
            {
                if (Pressed(kb.rightArrowKey)) lockAmount = Mathf.Min(cash - cash % Step, lockAmount + Step);
                if (Pressed(kb.leftArrowKey)) lockAmount = Mathf.Max(0, lockAmount - Step);
            }
            if (!Decide(kb) || lockAmount <= 0) return;
            if (!confirming)
            {
                confirming = true;
                return;
            }
            Flow.AddLock(lockAmount);
            lockAmount = 0;
            confirming = false;
        }

        public override void Refresh()
        {
            var m = Flow.Month;
            if (m == null) return;
            var ledger = m.Ledger;
            titleText.text = $"家計簿　{ledger.Entries.Count}品";

            // 新しい順に並べる
            int count = ledger.Entries.Count;
            int shown = Mathf.Min(VisibleRows, count - scroll);
            list.SetCount(Mathf.Max(0, shown));
            for (int i = 0; i < shown; i++)
            {
                var e = ledger.Entries[count - 1 - scroll - i];
                list.Set(i, UiFactory.Ink, false, Calendar.DateLabel(e.day, m.Life.month), e.name, e.rank, UiFactory.Yen(e.paid));
            }
            scrollText.text = count == 0 ? "まだ何も買っていません" : (MaxScroll > 0 ? "↑↓でスクロール" : "");

            totalsText.text =
                $"今月の支出　{UiFactory.Paint(UiFactory.Yen(ledger.TotalPaid), UiFactory.Bad)}\n" +
                $"今日の支出　{UiFactory.Yen(ledger.PaidOn(m.Day))}\n" +
                $"今月の節約額　{UiFactory.Paint(UiFactory.Yen(ledger.TotalSaved), UiFactory.Good)}\n" +
                $"平均の値引き　{ledger.AverageTypingBp / 100f:0.#}%　PERFECT {ledger.PerfectCount}回\n" +
                $"財布　{UiFactory.Paint(UiFactory.Yen(m.Wallet.Cash), UiFactory.Warn)}";

            lockText.text =
                $"ロック貯金　{UiFactory.Paint(UiFactory.Yen(m.Wallet.Locked), UiFactory.Gold)}\n" +
                $"追加する額　<b>{UiFactory.Yen(lockAmount)}</b>\n" +
                (confirming
                    ? UiFactory.Paint($"{UiFactory.Yen(lockAmount)}をロックします。解除できません。\nよければもう一度スペース", UiFactory.Bad)
                    : UiFactory.Paint("←→で額を決めてスペース\nロックしたお金は目標商品にしか使えません", UiFactory.Quiet));

            Message.text = confirming
                ? "スペース：ロックする　Esc：やめる"
                : "↑↓：スクロール　←→：ロックに追加する額　スペース：ロックする　Esc：部屋へ戻る";
        }
    }
}
