using System;
using System.Collections.Generic;
using System.Text;
using Negiri.Core.Data;
using Negiri.Game.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Negiri.Game.Screens
{
    /// <summary>
    /// 店内。左に商品棚（カテゴリのタブ）、右にカートと「レジへ進む」ボタン。
    /// ←→で 食料 → 日用品 → お菓子・趣味 → カート と移り、↑↓で選び、スペースで決定する。
    /// カートの合計は財布で制限しない。品数だけ難易度で制限する。
    /// </summary>
    public sealed class StoreScreen : ScreenBase
    {
        static readonly (string label, string[] categories)[] Tabs =
        {
            ("食料", new[] { "food", "drink" }),
            ("日用品", new[] { "daily" }),
            ("お菓子・趣味", new[] { "snack", "hobby" }),
        };

        const int CartRows = 12;

        readonly Text storeNameText, tabsText, cartTitleText, totalsText, noticeText, checkoutText;
        readonly Image checkoutBox;
        readonly RowList shelf, cartList;
        readonly List<ItemData>[] shelfItems = new List<ItemData>[Tabs.Length];

        /// <summary>0〜2：売り場、3（Tabs.Length）：カート。</summary>
        int tab;
        int shelfCursor;
        /// <summary>カートを選んでいる間も棚に出しておく、直前の売り場。</summary>
        int lastShelfTab;
        /// <summary>カートの行。最後（カートの品数と同じ値）は「レジへ進む」ボタン。</summary>
        int cartCursor;
        string notice = "";
        float noticeTime;

        bool CartFocus => tab == Tabs.Length;

        public StoreScreen(ShoppingFlow flow, RectTransform parent) : base(flow, parent, "Store")
        {
            // 左：商品棚
            UiFactory.MakeBox(Root, UiFactory.Panel, new Vector2(0, 1), new Vector2(24, -48), new Vector2(528, 400));
            storeNameText = UiFactory.MakeText(Root, "", 20, UiFactory.Ink, new Vector2(0, 1), new Vector2(36, -54), new Vector2(500, 28), TextAnchor.MiddleLeft);
            tabsText = UiFactory.MakeText(Root, "", 18, UiFactory.Ink, new Vector2(0, 1), new Vector2(36, -86), new Vector2(500, 28), TextAnchor.MiddleLeft);
            shelf = new RowList(Root, new Vector2(30, -122), 516, 30, 10, 17,
                new Column(10, 170),
                new Column(176, 150),
                new Column(326, 100),
                new Column(420, 86, TextAnchor.MiddleRight));
            noticeText = UiFactory.MakeText(Root, "", 16, UiFactory.Warn, new Vector2(0, 0), new Vector2(36, 100), new Vector2(500, 24), TextAnchor.LowerLeft);

            // 右：カート
            UiFactory.MakeBox(Root, UiFactory.Panel, new Vector2(0, 1), new Vector2(568, -48), new Vector2(368, 400));
            cartTitleText = UiFactory.MakeText(Root, "", 20, UiFactory.Ink, new Vector2(0, 1), new Vector2(580, -54), new Vector2(340, 28), TextAnchor.MiddleLeft);
            cartList = new RowList(Root, new Vector2(574, -86), 356, 20, CartRows, 15,
                new Column(10, 220),
                new Column(236, 110, TextAnchor.MiddleRight));

            // 会計ボタン
            checkoutBox = UiFactory.MakeBox(Root, UiFactory.Bg, new Vector2(0, 1), new Vector2(584, -334), new Vector2(336, 30));
            checkoutText = UiFactory.MakeText(Root, "", 18, UiFactory.Ink, new Vector2(0, 1), new Vector2(584, -334), new Vector2(336, 30), TextAnchor.MiddleCenter);

            totalsText = UiFactory.MakeText(Root, "", 16, UiFactory.Ink, new Vector2(1, 0), new Vector2(-36, 100), new Vector2(350, 64), TextAnchor.LowerRight);
            totalsText.lineSpacing = 1.1f;

            for (int t = 0; t < Tabs.Length; t++) shelfItems[t] = new List<ItemData>();
        }

        public override void Enter()
        {
            for (int t = 0; t < Tabs.Length; t++)
            {
                shelfItems[t].Clear();
                foreach (var item in Flow.Db.Items.items)
                {
                    if (Array.IndexOf(Tabs[t].categories, item.category) >= 0) shelfItems[t].Add(item);
                }
            }
            tab = 0;
            lastShelfTab = 0;
            shelfCursor = 0;
            cartCursor = 0;
            notice = "";
        }

        public override void Tick(Keyboard kb, float deltaTime)
        {
            noticeTime = Mathf.Max(0f, noticeTime - deltaTime);

            if (Pressed(kb.escapeKey))
            {
                Flow.ShowTitle();
                return;
            }

            int tabCount = Tabs.Length + 1;
            if (Pressed(kb.leftArrowKey)) { tab = (tab + tabCount - 1) % tabCount; shelfCursor = 0; cartCursor = 0; }
            if (Pressed(kb.rightArrowKey)) { tab = (tab + 1) % tabCount; shelfCursor = 0; cartCursor = 0; }

            if (!CartFocus) lastShelfTab = tab;
            if (CartFocus) TickCart(kb);
            else TickShelf(kb);
        }

        void TickShelf(Keyboard kb)
        {
            var items = shelfItems[tab];
            if (items.Count == 0) return;
            shelfCursor = MoveCursor(kb, shelfCursor, items.Count);
            if (Decide(kb))
            {
                var item = items[shelfCursor];
                if (Flow.Cart.Add(item, Flow.Db.Store.SaleBp(item.id))) ShowNotice($"{item.name}をカートに入れました");
                else ShowNotice($"これ以上入りません（{Flow.Def.Label}は{Flow.Cart.MaxItems}品まで）");
            }
        }

        void TickCart(Keyboard kb)
        {
            // カートの行 ＋ 最後に「レジへ進む」ボタン
            int count = Flow.Cart.Count;
            cartCursor = MoveCursor(kb, cartCursor, count + 1);
            if (!Decide(kb)) return;

            if (cartCursor == count)
            {
                if (count == 0) ShowNotice("カートが空です");
                else Flow.GoToRegister();
                return;
            }
            var name = Flow.Cart.Lines[cartCursor].Item.name;
            Flow.Cart.RemoveAt(cartCursor);
            ShowNotice($"{name}を棚に戻しました");
            cartCursor = Mathf.Clamp(cartCursor, 0, Flow.Cart.Count);
        }

        void ShowNotice(string s)
        {
            notice = s;
            noticeTime = 2.5f;
        }

        public override void Refresh()
        {
            var store = Flow.Db.Store;
            storeNameText.text = store.name;

            var tabs = new StringBuilder();
            for (int t = 0; t < Tabs.Length; t++)
            {
                tabs.Append(t == tab ? UiFactory.Paint($"［{Tabs[t].label}］", UiFactory.Warn) : UiFactory.Paint($"　{Tabs[t].label}　", UiFactory.Quiet));
            }
            tabs.Append(UiFactory.Paint("　→カート", UiFactory.Quiet));
            tabsText.text = tabs.ToString();

            // 商品棚：名前／必須・特売／効果／通常価格。カートを選んでいる間は直前の売り場を薄く出す
            var items = shelfItems[CartFocus ? lastShelfTab : tab];
            int shown = Mathf.Min(items.Count, shelf.Capacity);
            shelf.SetCount(shown);
            for (int i = 0; i < shown; i++)
            {
                var item = items[i];
                int saleBp = store.SaleBp(item.id);
                var tags = new StringBuilder();
                if (item.kind == "essential") tags.Append("必須 ");
                if (saleBp > 0) tags.Append(UiFactory.Paint($"特売-{saleBp / 100}%", UiFactory.Warn));
                var effects = new StringBuilder();
                foreach (var e in item.effects ?? Array.Empty<ItemEffect>()) effects.Append(e.Label()).Append(' ');
                bool selected = !CartFocus && i == shelfCursor;
                shelf.Set(i, selected ? UiFactory.Ink : UiFactory.Quiet, selected, item.name, tags.ToString(), effects.ToString(), UiFactory.Yen(item.basePrice));
            }

            // カート
            var cart = Flow.Cart;
            cartTitleText.text = (CartFocus ? UiFactory.Paint("［カート］", UiFactory.Warn) : "カート") + $"　{cart.Count}／{cart.MaxItems}品";
            int cartShown = Mathf.Min(cart.Count, cartList.Capacity);
            cartList.SetCount(cartShown);
            for (int i = 0; i < cartShown; i++)
            {
                var line = cart.Lines[i];
                bool selected = CartFocus && i == cartCursor;
                var price = UiFactory.Yen(line.Item.basePrice);
                if (line.SaleBp > 0) price = UiFactory.Paint("特売 ", UiFactory.Warn) + price;
                cartList.Set(i, selected ? UiFactory.Ink : UiFactory.Quiet, selected, line.Item.name, price);
            }

            bool buttonSelected = CartFocus && cartCursor == cart.Count;
            checkoutBox.color = buttonSelected ? UiFactory.Warn : UiFactory.Bg;
            checkoutText.text = cart.Count == 0 ? "レジへ進む（カートが空です）" : "▶ レジへ進む";
            checkoutText.color = buttonSelected ? UiFactory.Bg : (cart.Count == 0 ? UiFactory.Quiet : UiFactory.Ink);

            int cash = Flow.Wallet.Cash;
            int best = cart.BestTotal(Flow.PerfectBp);
            int worst = cart.WorstTotal;
            var totals = new StringBuilder();
            totals.Append($"通常価格の合計 {UiFactory.Yen(cart.BaseTotal)}\n");
            totals.Append($"全部PERFECTなら {UiFactory.Paint(UiFactory.Yen(best), UiFactory.Good)}　全部MISSなら {UiFactory.Paint(UiFactory.Yen(worst), UiFactory.Bad)}\n");
            int shortage = cart.WorstShortage(cash);
            totals.Append(shortage > 0
                ? UiFactory.Paint($"全部MISSだと{UiFactory.Yen(shortage)}足りません", UiFactory.Bad)
                : UiFactory.Paint("全部MISSでも財布で払えます", UiFactory.Quiet));
            totalsText.text = totals.ToString();

            noticeText.text = noticeTime > 0f ? notice : "";

            Message.text = CartFocus
                ? "↑↓：選ぶ　スペース：棚に戻す／レジへ進む　←：売り場へ　Esc：タイトル"
                : "←→：売り場とカート　↑↓：商品　スペース：カートに入れる　Esc：タイトル";
        }
    }
}
