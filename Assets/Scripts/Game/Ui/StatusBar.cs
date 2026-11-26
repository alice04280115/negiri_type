using Negiri.Core.Life;
using UnityEngine;
using UnityEngine.UI;

namespace Negiri.Game.Ui
{
    /// <summary>画面上部のバー。日付・財布・ロック貯金・満足度をどの画面でも同じ場所に出す。</summary>
    public sealed class StatusBar
    {
        readonly Text left, right;

        public StatusBar(RectTransform stage)
        {
            left = UiFactory.MakeText(stage, "", 16, UiFactory.Quiet, new Vector2(0, 1), new Vector2(24, -14), new Vector2(460, 24), TextAnchor.UpperLeft);
            right = UiFactory.MakeText(stage, "", 17, UiFactory.Ink, new Vector2(1, 1), new Vector2(-24, -13), new Vector2(520, 24), TextAnchor.UpperRight);
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

            int sat = month.Satisfaction.Value;
            var satColor = sat < month.Life.satisfaction.passLine ? UiFactory.Bad : UiFactory.Ink;
            right.text =
                $"財布 {UiFactory.Paint(UiFactory.Yen(month.Wallet.Cash), UiFactory.Warn)}　" +
                $"ロック {UiFactory.Paint(UiFactory.Yen(month.Wallet.Locked), UiFactory.Gold)}　" +
                $"満足度 {UiFactory.Paint(sat.ToString(), satColor)}";
        }
    }
}
