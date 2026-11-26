using System.Text;
using Negiri.Core.Shopping;
using Negiri.Game.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Negiri.Game.Screens
{
    /// <summary>難易度を選んで、買い物かタイピング練習を始める。</summary>
    public sealed class TitleScreen : ScreenBase
    {
        static readonly string[] Menu = { "買い物をはじめる", "タイピング練習" };

        readonly Text difficultyText, ruleText, menuText;
        int cursor;

        public TitleScreen(ShoppingFlow flow, RectTransform parent) : base(flow, parent, "Title")
        {
            UiFactory.MakeText(Root, "ネギリタイプ", 48, UiFactory.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(900, 60), TextAnchor.MiddleCenter);
            UiFactory.MakeText(Root, "タイピングがうまいほど、買い物が安くなる", 18, UiFactory.Quiet, new Vector2(0.5f, 0.5f), new Vector2(0, 124), new Vector2(900, 28), TextAnchor.MiddleCenter);
            difficultyText = UiFactory.MakeText(Root, "", 26, UiFactory.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, 64), new Vector2(900, 36), TextAnchor.MiddleCenter);
            ruleText = UiFactory.MakeText(Root, "", 18, UiFactory.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(900, 60), TextAnchor.MiddleCenter);
            ruleText.lineSpacing = 1.2f;
            menuText = UiFactory.MakeText(Root, "", 24, UiFactory.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(900, 80), TextAnchor.MiddleCenter);
            menuText.lineSpacing = 1.3f;
        }

        public override void Enter()
        {
            cursor = 0;
        }

        public override void Tick(Keyboard kb, float deltaTime)
        {
            int count = Difficulties.All.Length;
            if (Pressed(kb.leftArrowKey)) Flow.SetDifficulty((Flow.Difficulty + count - 1) % count);
            if (Pressed(kb.rightArrowKey)) Flow.SetDifficulty((Flow.Difficulty + 1) % count);
            if (Pressed(kb.digit1Key)) Flow.SetDifficulty(0);
            if (Pressed(kb.digit2Key)) Flow.SetDifficulty(1);
            if (Pressed(kb.digit3Key)) Flow.SetDifficulty(2);
            if (Pressed(kb.digit4Key)) Flow.SetDifficulty(3);
            cursor = MoveCursor(kb, cursor, Menu.Length);
            if (Decide(kb))
            {
                if (cursor == 0) Flow.StartShopping();
                else Flow.StartPractice();
            }
        }

        public override void Refresh()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Difficulties.All.Length; i++)
            {
                var label = Difficulties.All[i].Label;
                sb.Append(i == Flow.Difficulty ? UiFactory.Paint($"［{label}］", UiFactory.Warn) : UiFactory.Paint($"　{label}　", UiFactory.Quiet));
                if (i < Difficulties.All.Length - 1) sb.Append("　");
            }
            difficultyText.text = "◀ " + sb + " ▶";

            var rule = Flow.Rule;
            ruleText.text =
                $"財布 {UiFactory.Yen(Flow.Db.Balance.shopping.startingCash)}　｜　1回に買える品数 {rule.maxItems}品\n" +
                $"会計で財布が足りないとき：{ShortageDescription(rule)}";

            var menu = new StringBuilder();
            for (int i = 0; i < Menu.Length; i++)
            {
                if (i > 0) menu.Append('\n');
                menu.Append(i == cursor ? UiFactory.Paint($"▶ {Menu[i]}", UiFactory.Warn) : UiFactory.Paint($"　{Menu[i]}", UiFactory.Quiet));
            }
            menuText.text = menu.ToString();

            Message.text = "←→：難易度　↑↓：選ぶ　スペース：決定";
        }

        public static string ShortageDescription(ShoppingRule rule)
        {
            string penalty = rule.satisfactionPerReturn > 0 ? $"1品ごとに満足度−{rule.satisfactionPerReturn}" : "ペナルティなし";
            switch (rule.ShortageValue)
            {
                case ShortageRule.AutoReturnHighestFirst: return $"支払額の高い順に自動で返品（{penalty}）";
                case ShortageRule.AutoReturnLowestFirst: return $"支払額の安い順に自動で返品（{penalty}）";
                default: return $"返品する商品を自分で選ぶ（{penalty}）";
            }
        }
    }
}
