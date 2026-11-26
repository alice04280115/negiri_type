using System.Text;
using Negiri.Core.Life;
using Negiri.Game.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Negiri.Game.Screens
{
    /// <summary>
    /// マイルーム（1日の拠点）。左に部屋とキャラ、右に在庫・満足度・次の買い物・今日の特売・目標、下にコマンド。
    /// </summary>
    public sealed class MyRoomScreen : ScreenBase
    {
        static readonly string[] Commands = { "1 買い物に行く", "2 家で過ごす（1日進む）", "3 数日進める", "4 家計簿", "5 中断する" };

        readonly Text stockText, shopText, saleText, goalText, menuText, noticeText;
        int cursor;
        string notice = "";
        float noticeTime;

        public MyRoomScreen(ShoppingFlow flow, RectTransform parent) : base(flow, parent, "MyRoom")
        {
            // 左：部屋の背景とキャラ（絵がなければ四角）
            ArtSlot.Make(Root, "room", "部屋の背景", UiFactory.Panel, new Vector2(0, 1), new Vector2(24, -48), new Vector2(420, 300));
            ArtSlot.Make(Root, "character", "キャラ", UiFactory.PanelLight, new Vector2(0, 1), new Vector2(170, -150), new Vector2(128, 170));

            // 右：状況
            UiFactory.MakeBox(Root, UiFactory.Panel, new Vector2(0, 1), new Vector2(460, -48), new Vector2(476, 300));
            stockText = UiFactory.MakeText(Root, "", 17, UiFactory.Ink, new Vector2(0, 1), new Vector2(476, -58), new Vector2(450, 96), TextAnchor.UpperLeft);
            stockText.lineSpacing = 1.25f;
            shopText = UiFactory.MakeText(Root, "", 17, UiFactory.Ink, new Vector2(0, 1), new Vector2(476, -160), new Vector2(450, 26), TextAnchor.UpperLeft);
            saleText = UiFactory.MakeText(Root, "", 16, UiFactory.Ink, new Vector2(0, 1), new Vector2(476, -192), new Vector2(450, 70), TextAnchor.UpperLeft);
            saleText.lineSpacing = 1.15f;
            goalText = UiFactory.MakeText(Root, "", 16, UiFactory.Ink, new Vector2(0, 1), new Vector2(476, -290), new Vector2(450, 48), TextAnchor.UpperLeft);
            goalText.lineSpacing = 1.15f;

            // 下：コマンド
            UiFactory.MakeBox(Root, UiFactory.Panel, new Vector2(0, 1), new Vector2(24, -362), new Vector2(420, 136));
            menuText = UiFactory.MakeText(Root, "", 18, UiFactory.Ink, new Vector2(0, 1), new Vector2(40, -370), new Vector2(400, 124), TextAnchor.UpperLeft);
            menuText.lineSpacing = 1.15f;
            noticeText = UiFactory.MakeText(Root, "", 17, UiFactory.Warn, new Vector2(0, 1), new Vector2(476, -380), new Vector2(450, 60), TextAnchor.UpperLeft);
        }

        public override void Enter()
        {
            var m = Flow.Month;
            cursor = m != null && m.CanShop ? 0 : 1;
            notice = "";
        }

        public override void Tick(Keyboard kb, float deltaTime)
        {
            noticeTime = Mathf.Max(0f, noticeTime - deltaTime);
            cursor = MoveCursor(kb, cursor, Commands.Length);
            int chosen = -1;
            if (Pressed(kb.digit1Key)) chosen = 0;
            if (Pressed(kb.digit2Key)) chosen = 1;
            if (Pressed(kb.digit3Key)) chosen = 2;
            if (Pressed(kb.digit4Key)) chosen = 3;
            if (Pressed(kb.digit5Key)) chosen = 4;
            if (Decide(kb)) chosen = cursor;
            if (chosen < 0) return;
            cursor = chosen;

            var m = Flow.Month;
            switch (chosen)
            {
                case 0:
                    if (m.CanShop) Flow.GoShopping();
                    else ShowNotice(m.ShoppedToday ? "今日はもう買い物に行きました" : $"買い物に行けるのは{ShopDayLabel(m)}です");
                    break;
                case 1: Flow.EndDay(); break;
                case 2: Flow.AdvanceDays(); break;
                case 3: Flow.OpenLedger(); break;
                default: Flow.Quit(); break;
            }
        }

        void ShowNotice(string s)
        {
            notice = s;
            noticeTime = 3f;
        }

        static string ShopDayLabel(MonthState m)
        {
            int next = m.NextShopDay;
            if (next > m.Days) return "来月";
            return $"{Calendar.DateLabel(next, m.Life.month)}（あと{next - m.Day}日）";
        }

        public override void Refresh()
        {
            var m = Flow.Month;
            if (m == null) return;
            var st = m.Stock;
            var life = m.Life;

            var sb = new StringBuilder();
            sb.Append("食料　　").Append(StockMeter(st.Food, st.FoodCap > 0 ? st.FoodCap : 10, life.stock.foodPerDay)).Append('\n');
            sb.Append("日用品　").Append(StockMeter(st.Daily, st.DailyCap > 0 ? st.DailyCap : 14, life.stock.dailyPerDay)).Append('\n');
            int sat = m.Satisfaction.Value;
            var satColor = sat < life.satisfaction.passLine ? UiFactory.Bad : UiFactory.Good;
            sb.Append("満足度　").Append(UiFactory.Paint(UiFactory.Meter(sat, m.Satisfaction.Max, 10), satColor))
              .Append($" {sat}").Append(UiFactory.Paint($"（月末に{life.satisfaction.passLine}以上）", UiFactory.Quiet));
            stockText.text = sb.ToString();

            if (m.CanShop) shopText.text = UiFactory.Paint("今日は買い物に行けます", UiFactory.Good);
            else if (m.ShoppedToday) shopText.text = UiFactory.Paint("今日は買い物に行きました", UiFactory.Quiet);
            else shopText.text = $"次の買い物：{ShopDayLabel(m)}";

            var sales = Flow.Db.Store.SalesOn(m.Weekday);
            var saleSb = new StringBuilder($"今日（{Calendar.WeekdayNames[m.Weekday]}曜）の特売：");
            if (sales.Count == 0) saleSb.Append(UiFactory.Paint("なし", UiFactory.Quiet));
            for (int i = 0; i < sales.Count; i++)
            {
                if (i > 0) saleSb.Append("、");
                var name = Flow.Db.ItemById.TryGetValue(sales[i].itemId, out var item) ? item.name : sales[i].itemId;
                saleSb.Append(UiFactory.Paint($"{name} -{sales[i].discountBp / 100}%", UiFactory.Warn));
            }
            saleSb.Append('\n').Append(UiFactory.Paint(WeekSales(m), UiFactory.Quiet));
            saleText.text = saleSb.ToString();

            var month = life.month;
            int have = m.Wallet.Locked + m.Wallet.Cash;
            int rest = Mathf.Max(0, month.targetPrice - m.Wallet.Locked);
            goalText.text = $"目標：{month.targetName} {UiFactory.Yen(month.targetPrice)}\n" +
                            $"ロック済み {UiFactory.Paint(UiFactory.Yen(m.Wallet.Locked), UiFactory.Gold)}　あと {UiFactory.Yen(rest)}" +
                            UiFactory.Paint($"（財布と合わせて {UiFactory.Yen(have)}）", UiFactory.Quiet);

            var disabled = new bool[Commands.Length];
            disabled[0] = !m.CanShop;
            menuText.text = UiFactory.Menu(Commands, cursor, disabled);
            noticeText.text = noticeTime > 0f ? notice : "";

            Message.text = "↑↓／1〜5：選ぶ　スペース：決定";
        }

        /// <summary>「■■■□□□□ 3日分」。今夜使い切るなら赤、もう0なら「在庫切れ」（今夜から満足度が下がる）。</summary>
        static string StockMeter(int value, int max, int perDay)
        {
            var meter = UiFactory.Meter(value, max, 10);
            if (value <= 0) return UiFactory.Paint(meter + " 在庫切れ！今夜から満足度が下がります", UiFactory.Bad);
            if (value <= perDay) return UiFactory.Paint(meter + $" {value}日分（明日の夜に切れます）", UiFactory.Bad);
            return UiFactory.Paint(meter, UiFactory.Good) + $" {value}日分";
        }

        /// <summary>1週間の特売の予定（例：火 卵・キャベツ／金 豚肉・チョコ）。</summary>
        string WeekSales(MonthState m)
        {
            var sb = new StringBuilder("特売の曜日：");
            bool any = false;
            for (int w = 0; w < 7; w++)
            {
                var list = Flow.Db.Store.SalesOn(w);
                if (list.Count == 0) continue;
                if (any) sb.Append("／");
                sb.Append(Calendar.WeekdayNames[w]).Append(' ');
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append('・');
                    sb.Append(Flow.Db.ItemById.TryGetValue(list[i].itemId, out var item) ? item.name : list[i].itemId);
                }
                any = true;
            }
            return any ? sb.ToString() : "";
        }
    }
}
