using Negiri.Game.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Negiri.Game.Screens
{
    /// <summary>
    /// 月末のまとめ（段階3の仮）。残金・在庫切れの日数・満足度・節約額を出す。
    /// 決戦タイピング（目標商品の購入）と採点・ランクは段階4で作る。
    /// </summary>
    public sealed class MonthEndScreen : ScreenBase
    {
        static readonly string[] Menu = { "もう一度（同じ難易度ではじめから）", "タイトルへ" };

        readonly Text headText, bodyText, menuText;
        int cursor;

        public MonthEndScreen(ShoppingFlow flow, RectTransform parent) : base(flow, parent, "MonthEnd")
        {
            headText = UiFactory.MakeText(Root, "", 30, UiFactory.Ink, new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(900, 40), TextAnchor.MiddleCenter);
            UiFactory.MakeBox(Root, UiFactory.Panel, new Vector2(0.5f, 1), new Vector2(0, -104), new Vector2(640, 290));
            bodyText = UiFactory.MakeText(Root, "", 19, UiFactory.Ink, new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(580, 270), TextAnchor.UpperLeft);
            bodyText.lineSpacing = 1.3f;
            menuText = UiFactory.MakeText(Root, "", 20, UiFactory.Ink, new Vector2(0.5f, 1), new Vector2(0, -410), new Vector2(580, 60), TextAnchor.UpperLeft);
            menuText.lineSpacing = 1.3f;
        }

        public override void Enter()
        {
            cursor = 0;
        }

        public override void Tick(Keyboard kb, float deltaTime)
        {
            cursor = MoveCursor(kb, cursor, Menu.Length);
            if (!Decide(kb)) return;
            if (cursor == 0) Flow.Restart();
            else Flow.ShowTitle();
        }

        public override void Refresh()
        {
            var m = Flow.Month;
            if (m == null) return;
            var life = m.Life;
            headText.text = $"{life.month.month}月が終わりました";

            int sat = m.Satisfaction.Value;
            int outDays = m.Stock.FoodOutDays + m.Stock.DailyOutDays;
            int total = m.Wallet.Cash + m.Wallet.Locked;
            int target = life.month.targetPrice;
            bool reach = total >= target;
            bodyText.text =
                $"残金　{UiFactory.Yen(total)}" + UiFactory.Paint($"（財布 {UiFactory.Yen(m.Wallet.Cash)}＋貯金箱 {UiFactory.Yen(m.Wallet.Locked)}）", UiFactory.Quiet) + "\n" +
                $"{life.month.targetName}（{UiFactory.Yen(target)}）　" +
                (reach ? UiFactory.Paint("定価でも買えます", UiFactory.Good) : UiFactory.Paint($"あと{UiFactory.Yen(target - total)}足りません", UiFactory.Bad)) + "\n" +
                $"在庫切れ　食料 {m.Stock.FoodOutDays}日・日用品 {m.Stock.DailyOutDays}日" + (outDays == 0 ? UiFactory.Paint("　切らさずに過ごせました", UiFactory.Good) : "") + "\n" +
                $"満足度　{UiFactory.Paint(sat.ToString(), sat < life.satisfaction.passLine ? UiFactory.Bad : UiFactory.Good)}" + UiFactory.Paint($"（目標 {life.satisfaction.passLine}以上）", UiFactory.Quiet) + "\n" +
                $"今月の節約額　{UiFactory.Paint(UiFactory.Yen(m.Ledger.TotalSaved), UiFactory.Good)}\n" +
                $"平均の値引き　{m.Ledger.AverageTypingBp / 100f:0.#}%　PERFECT {m.Ledger.PerfectCount}回\n" +
                UiFactory.Paint("※目標商品の決戦タイピングと採点は、段階4で作ります", UiFactory.Quiet);

            menuText.text = UiFactory.Menu(Menu, cursor);
            Message.text = "↑↓：選ぶ　スペース：決定";
        }
    }
}
