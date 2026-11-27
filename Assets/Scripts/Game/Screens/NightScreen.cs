using System.Text;
using Negiri.Core.Life;
using Negiri.Game.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Negiri.Game.Screens
{
    /// <summary>夜のまとめ。在庫と満足度がどう変わったかを見せる（数日進めたときは1晩ずつ並べる）。</summary>
    public sealed class NightScreen : ScreenBase
    {
        const int MaxLines = 10;

        readonly Text headText, bodyText;

        public NightScreen(ShoppingFlow flow, RectTransform parent) : base(flow, parent, "Night")
        {
            headText = UiFactory.MakeText(Root, "", 28, UiFactory.Ink, new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(900, 40), TextAnchor.MiddleCenter);
            UiFactory.MakeBox(Root, UiFactory.Panel, new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(760, 340));
            bodyText = UiFactory.MakeText(Root, "", 18, UiFactory.Ink, new Vector2(0.5f, 1), new Vector2(0, -126), new Vector2(720, 310), TextAnchor.UpperLeft);
            bodyText.lineSpacing = 1.25f;
        }

        public override void Tick(Keyboard kb, float deltaTime)
        {
            if (Decide(kb)) Flow.AfterNight();
        }

        public override void Refresh()
        {
            var nights = Flow.LastNights;
            if (nights == null || nights.Count == 0) return;
            var month = Flow.Life.month;
            var first = nights[0];
            var last = nights[nights.Count - 1];

            headText.text = nights.Count == 1
                ? $"{Calendar.DateLabel(first.Day, month)}の夜"
                : $"{Calendar.DateLabel(first.Day, month)}～{Calendar.DateLabel(last.Day, month)}（{nights.Count}晩）";

            var sb = new StringBuilder();
            if (nights.Count == 1)
            {
                sb.Append(Line("食料", first.FoodBefore, first.FoodAfter, first.FoodOut, Flow.Life.stock.foodOutPenalty)).Append('\n');
                sb.Append(Line("日用品", first.DailyBefore, first.DailyAfter, first.DailyOut, Flow.Life.stock.dailyOutPenalty)).Append('\n');
                sb.Append($"満足度　{first.SatisfactionBefore} → <b>{first.SatisfactionAfter}</b>");
                sb.Append(UiFactory.Paint($"（毎晩－{Flow.Life.satisfaction.dailyDecay}）", UiFactory.Quiet));
            }
            else
            {
                int start = Mathf.Max(0, nights.Count - MaxLines);
                if (start > 0) sb.Append(UiFactory.Paint("…\n", UiFactory.Quiet));
                for (int i = start; i < nights.Count; i++)
                {
                    var n = nights[i];
                    sb.Append($"{Calendar.DateLabel(n.Day, month)}　食料 {n.FoodAfter}　日用品 {n.DailyAfter}　満足度 {n.SatisfactionAfter}");
                    if (n.FoodOut) sb.Append(UiFactory.Paint("　食料切れ", UiFactory.Bad));
                    if (n.DailyOut) sb.Append(UiFactory.Paint("　日用品切れ", UiFactory.Bad));
                    sb.Append('\n');
                }
                sb.Append($"\n満足度　{first.SatisfactionBefore} → <b>{last.SatisfactionAfter}</b>");
            }

            if (last.Interest > 0 && Flow.Month != null)
            {
                sb.Append("\n\n").Append(UiFactory.Paint($"月末なので借金に利息 {UiFactory.Yen(last.Interest)} がつきました（借金 {UiFactory.Yen(last.DebtAfter)}）", UiFactory.Bad));
            }

            var m = Flow.Month;
            if (!last.MonthOver && m != null)
            {
                sb.Append("\n\n");
                if (m.CanShop) sb.Append(UiFactory.Paint("明日は買い物に行けます", UiFactory.Good));
                else if (m.Stock.Food <= 0 || m.Stock.Daily <= 0) sb.Append(UiFactory.Paint("在庫が切れています。買い物に行ける日を確かめましょう", UiFactory.Bad));
            }
            bodyText.text = sb.ToString();

            Message.text = last.MonthOver ? "スペース：月末のまとめへ" : "スペース：次の朝へ（自動でセーブしました）";
        }

        static string Line(string label, int before, int after, bool outOfStock, int penalty)
        {
            if (outOfStock) return UiFactory.Paint($"{label}　在庫切れ！　満足度－{penalty}", UiFactory.Bad);
            var s = $"{label}　{before}日分 → <b>{after}日分</b>";
            return after <= 0 ? UiFactory.Paint(s + "（明日の夜に切れます）", UiFactory.Warn) : s;
        }
    }
}
