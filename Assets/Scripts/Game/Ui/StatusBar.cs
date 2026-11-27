using Negiri.Core.Life;
using UnityEngine;
using UnityEngine.UI;

namespace Negiri.Game.Ui
{
    /// <summary>画面上部のバー。日付・目標額・財布・貯金箱・借金をどの画面でも同じ場所に出す（満足度はマイルームのメーターで見る）。</summary>
    public sealed class StatusBar
    {
        readonly Text left, right;

        public StatusBar(RectTransform stage)
        {
            left = UiFactory.MakeText(stage, "", 16, UiFactory.Quiet, new Vector2(0, 1), new Vector2(24, -14), new Vector2(460, 24), TextAnchor.UpperLeft);
            right = UiFactory.MakeText(stage, "", 17, UiFactory.Ink, new Vector2(1, 1), new Vector2(-24, -13), new Vector2(560, 24), TextAnchor.UpperRight);
        }

        public void Show(MonthState month, string difficultyLabel)
        {
            if (month == null)
            {
                left.text = $"ネギリタイプ　難易度：{difficultyLabel}";
                right.text = "";
                return;
            }
            var day = month.IsOver ? "月末" : $"{month.Day}日目／{month.Days}日";
            left.text = $"<b>{month.DateLabel}</b>　{day}　{difficultyLabel}";

            right.text =
                $"目標 {UiFactory.Yen(month.Life.month.targetPrice)}　" +
                $"財布 {UiFactory.Paint(UiFactory.Yen(month.Wallet.Cash), UiFactory.Warn)}　" +
                $"貯金箱 {UiFactory.Paint(UiFactory.Yen(month.Wallet.Locked), UiFactory.Gold)}";
            int debt = month.Debt.Total;
            if (debt > 0) right.text += $"　借金 {UiFactory.Paint(UiFactory.Yen(debt), UiFactory.Bad)}";
        }
    }
}
