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
        readonly Text titleText, difficultyText, ruleText;

        public TitleScreen(ShoppingFlow flow, RectTransform parent) : base(flow, parent, "Title")
        {
            titleText = UiFactory.MakeText(Root, "ネギリタイプ", 48, UiFactory.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(900, 60), TextAnchor.MiddleCenter);
            UiFactory.MakeText(Root, "タイピングがうまいほど、買い物が安くなる", 18, UiFactory.Quiet, new Vector2(0.5f, 0.5f), new Vector2(0, 104), new Vector2(900, 28), TextAnchor.MiddleCenter);
            difficultyText = UiFactory.MakeText(Root, "", 26, UiFactory.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(900, 36), TextAnchor.MiddleCenter);
            ruleText = UiFactory.MakeText(Root, "", 18, UiFactory.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(900, 100), TextAnchor.MiddleCenter);
            ruleText.lineSpacing = 1.2f;
        }

        public override void Tick(Keyboard kb, float deltaTime)
        {
            if (Pressed(kb.digit1Key)) Flow.SetDifficulty(0);
            if (Pressed(kb.digit2Key)) Flow.SetDifficulty(1);
            if (Pressed(kb.digit3Key)) Flow.SetDifficulty(2);
            if (Pressed(kb.digit4Key)) Flow.SetDifficulty(3);
            if (EnterPressed(kb)) Flow.StartShopping();
            else if (Pressed(kb.tKey)) Flow.StartPractice();
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
            difficultyText.text = sb.ToString();

            var rule = Flow.Rule;
            ruleText.text =
                $"財布 {UiFactory.Yen(Flow.Db.Balance.shopping.startingCash)}　｜　1回に買える品数 {rule.maxItems}品\n" +
                $"会計で財布が足りないとき：{ShortageDescription(rule)}";

            Message.text = "1〜4：難易度　Enter：買い物をはじめる　T：タイピング練習";
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
