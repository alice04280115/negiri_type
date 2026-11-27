using System.Collections.Generic;
using System.Text;
using Negiri.Core.Life;
using Negiri.Game.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Negiri.Game.Screens
{
    /// <summary>
    /// マイルーム（1日の拠点）。左に部屋とキャラ、右に在庫・満足度・借金・次の買い物・今日の特売・目標、下にコマンド。
    /// </summary>
    public sealed class MyRoomScreen : ScreenBase
    {
        enum Command { Shop, Stay, Advance, Ledger, Repay, Quit }

        static string Label(Command c)
        {
            switch (c)
            {
                case Command.Shop: return "買い物に行く";
                case Command.Stay: return "家で過ごす（1日進む）";
                case Command.Advance: return "数日進める";
                case Command.Ledger: return "家計簿";
                case Command.Repay: return "借金を返す";
                default: return "中断する";
            }
        }

        /// <summary>今出しているコマンド。「借金を返す」は借金があるか、買い物で借りられる難易度のときだけ出す。</summary>
        readonly List<Command> commands = new List<Command>();

        readonly Text stockText, shopText, saleText, goalText, menuText, noticeText;
        int cursor;
        bool choosingDays;
        int nights;
        bool choosingRepay;
        int repayIndex;
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
            saleText = UiFactory.MakeText(Root, "", 16, UiFactory.Ink, new Vector2(0, 1), new Vector2(476, -190), new Vector2(450, 96), TextAnchor.UpperLeft);
            saleText.lineSpacing = 1.1f;
            goalText = UiFactory.MakeText(Root, "", 16, UiFactory.Ink, new Vector2(0, 1), new Vector2(476, -294), new Vector2(450, 48), TextAnchor.UpperLeft);
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
            BuildCommands();
            cursor = m != null && m.CanShop ? 0 : 1;
            choosingDays = false;
            choosingRepay = false;
            notice = "";
        }

        void BuildCommands()
        {
            var m = Flow.Month;
            commands.Clear();
            commands.Add(Command.Shop);
            commands.Add(Command.Stay);
            commands.Add(Command.Advance);
            commands.Add(Command.Ledger);
            if (m != null && (m.Debt.Total > 0 || m.Rule.CanBorrow)) commands.Add(Command.Repay);
            commands.Add(Command.Quit);
            cursor = Mathf.Clamp(cursor, 0, commands.Count - 1);
        }

        /// <summary>返す借入先（残高のある最初の借入先。なければ買い物で借りる借入先）。</summary>
        static string RepayLender(MonthState m)
        {
            foreach (var a in m.Debt.Accounts) if (a.Balance > 0) return a.lender;
            return m.ShopLender;
        }

        /// <summary>「借金を返す」の額を選ぶ。←→で額、スペースで返す、Escでやめる。</summary>
        void TickChoosingRepay(Keyboard kb)
        {
            var m = Flow.Month;
            var choices = m.RepayChoices(RepayLender(m));
            if (Pressed(kb.escapeKey) || choices.Count == 0)
            {
                choosingRepay = false;
                return;
            }
            if (Pressed(kb.rightArrowKey) || Pressed(kb.upArrowKey)) repayIndex = Mathf.Min(choices.Count - 1, repayIndex + 1);
            if (Pressed(kb.leftArrowKey) || Pressed(kb.downArrowKey)) repayIndex = Mathf.Max(0, repayIndex - 1);
            repayIndex = Mathf.Clamp(repayIndex, 0, choices.Count - 1);
            if (!Decide(kb)) return;
            int amount = choices[repayIndex];
            Flow.Repay(RepayLender(m), amount);
            choosingRepay = false;
            ShowNotice($"{UiFactory.Yen(amount)}返しました" + (m.Debt.Total == 0 ? "。借金はなくなりました" : ""));
        }

        /// <summary>「数日進める」の日数を選ぶ。←→で日数、スペースで進める、Escでやめる。</summary>
        void TickChoosingDays(Keyboard kb)
        {
            var m = Flow.Month;
            if (Pressed(kb.escapeKey))
            {
                choosingDays = false;
                return;
            }
            if (Pressed(kb.rightArrowKey) || Pressed(kb.upArrowKey)) nights = Mathf.Min(m.NightsLeft, nights + 1);
            if (Pressed(kb.leftArrowKey) || Pressed(kb.downArrowKey)) nights = Mathf.Max(1, nights - 1);
            if (!Decide(kb)) return;
            choosingDays = false;
            Flow.AdvanceDays(nights);
        }

        public override void Tick(Keyboard kb, float deltaTime)
        {
            noticeTime = Mathf.Max(0f, noticeTime - deltaTime);
            if (choosingDays)
            {
                TickChoosingDays(kb);
                return;
            }
            if (choosingRepay)
            {
                TickChoosingRepay(kb);
                return;
            }
            BuildCommands();
            cursor = MoveCursor(kb, cursor, commands.Count);
            int chosen = -1;
            var digits = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key, kb.digit6Key };
            for (int i = 0; i < commands.Count && i < digits.Length; i++)
            {
                if (Pressed(digits[i])) chosen = i;
            }
            if (Decide(kb)) chosen = cursor;
            if (chosen < 0) return;
            cursor = chosen;

            var m = Flow.Month;
            switch (commands[chosen])
            {
                case Command.Shop:
                    if (m.CanShop) Flow.GoShopping();
                    else ShowNotice(m.ShoppedToday ? "今日はもう買い物に行きました" : $"買い物に行けるのは{ShopDayLabel(m)}です");
                    break;
                case Command.Stay: Flow.EndDay(); break;
                case Command.Advance:
                    nights = m.SuggestedNights;
                    choosingDays = true;
                    break;
                case Command.Ledger: Flow.OpenLedger(); break;
                case Command.Repay:
                    var choices = m.RepayChoices(RepayLender(m));
                    if (m.Debt.Total <= 0) ShowNotice("借金はありません");
                    else if (choices.Count == 0) ShowNotice($"財布が{UiFactory.Yen(m.Debt.RepayStep)}未満なので返せません");
                    else
                    {
                        repayIndex = choices.Count - 1;
                        choosingRepay = true;
                    }
                    break;
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
            int debt = m.Debt.Total;
            if (debt > 0)
            {
                var lender = m.Debt.Lender(RepayLender(m));
                sb.Append('\n').Append("借金　　").Append(UiFactory.Paint(UiFactory.Yen(debt), UiFactory.Bad));
                if (lender != null) sb.Append(UiFactory.Paint($"（月末に{lender.interestBp / 100f:0.##}%の利息）", UiFactory.Quiet));
            }
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
            string box = month.lockTotalMax > 0
                ? $"貯金箱 {UiFactory.Paint(UiFactory.Yen(m.Wallet.Locked), UiFactory.Gold)}／{UiFactory.Yen(month.lockTotalMax)}"
                : $"貯金箱 {UiFactory.Paint(UiFactory.Yen(m.Wallet.Locked), UiFactory.Gold)}";
            goalText.text = $"目標：{month.targetName} {UiFactory.Yen(month.targetPrice)}\n" +
                            $"{box}　" + UiFactory.Paint($"財布と合わせて {UiFactory.Yen(have)}", UiFactory.Quiet);

            var labels = new string[commands.Count];
            var disabled = new bool[commands.Count];
            for (int i = 0; i < commands.Count; i++)
            {
                labels[i] = $"{i + 1} {Label(commands[i])}";
                if (commands[i] == Command.Shop) disabled[i] = !m.CanShop;
                if (commands[i] == Command.Repay) disabled[i] = m.Debt.Total <= 0;
            }
            if (choosingRepay)
            {
                var lender = RepayLender(m);
                var choices = m.RepayChoices(lender);
                int amount = choices.Count > 0 ? choices[Mathf.Clamp(repayIndex, 0, choices.Count - 1)] : 0;
                int bal = m.Debt.BalanceOf(lender);
                string all = amount == bal ? "（全額）" : "";
                menuText.text = $"{m.Debt.LenderName(lender)}にいくら返しますか？\n" +
                                $"◀ <b>{UiFactory.Yen(amount)}</b>{all} ▶\n" +
                                UiFactory.Paint($"借金 {UiFactory.Yen(bal)} → {UiFactory.Yen(bal - amount)}\n財布 {UiFactory.Yen(m.Wallet.Cash)} → {UiFactory.Yen(m.Wallet.Cash - amount)}", UiFactory.Quiet);
                Message.text = "←→：額　スペース：返す　Esc：やめる";
            }
            else if (choosingDays)
            {
                int until = m.Day + nights;
                string untilLabel = until > m.Days ? "月末まで" : $"{Calendar.DateLabel(until, life.month)}の朝まで";
                menuText.text = "何日進めますか？\n" +
                                $"◀ <b>{nights}日</b> ▶　{untilLabel}\n" +
                                UiFactory.Paint("途中で在庫が切れたら、その朝で止まります", UiFactory.Quiet);
                Message.text = "←→：日数　スペース：進める　Esc：やめる";
            }
            else
            {
                menuText.text = UiFactory.Menu(labels, cursor, disabled);
                Message.text = $"↑↓／1～{commands.Count}：選ぶ　スペース：決定";
            }
            noticeText.text = noticeTime > 0f ? notice : "";
        }

        /// <summary>「■■■□□□□ 3日分」。今夜使い切るなら赤、もう0なら「在庫切れ」（今夜から満足度が下がる）。</summary>
        static string StockMeter(int value, int max, int perDay)
        {
            var meter = UiFactory.Meter(value, max, 10);
            if (value <= 0) return UiFactory.Paint(meter + " 在庫切れ！今夜から満足度が下がります", UiFactory.Bad);
            if (value <= perDay) return UiFactory.Paint(meter + $" {value}日分（明日の夜に切れます）", UiFactory.Bad);
            return UiFactory.Paint(meter, UiFactory.Good) + $" {value}日分";
        }

        /// <summary>1週間の特売の予定。曜日ごとに1行（例：「　火曜　卵・葉物野菜」）。</summary>
        string WeekSales(MonthState m)
        {
            var sb = new StringBuilder();
            for (int w = 0; w < 7; w++)
            {
                var list = Flow.Db.Store.SalesOn(w);
                if (list.Count == 0) continue;
                sb.Append(sb.Length == 0 ? "特売の曜日　" : "\n　　　　　　");
                sb.Append(Calendar.WeekdayNames[w]).Append("曜　");
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append('・');
                    sb.Append(Flow.Db.ItemById.TryGetValue(list[i].itemId, out var item) ? item.name : list[i].itemId);
                }
            }
            return sb.ToString();
        }
    }
}
