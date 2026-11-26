using System.Collections.Generic;
using System.Text;
using Negiri.Core.Shopping;
using Negiri.Game.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Negiri.Game.Screens
{
    /// <summary>難易度を選んで、はじめから・つづきから・タイピング練習を選ぶ。</summary>
    public sealed class TitleScreen : ScreenBase
    {
        enum Item { NewGame, Continue, Practice }

        readonly Text difficultyText, ruleText, menuText, noticeText;
        readonly List<Item> items = new List<Item>();
        int cursor;
        /// <summary>セーブがあるときの「はじめから」は、もう一度スペースで確定する。</summary>
        bool confirmingNew;
        string continueLabel = "";
        string notice = "";

        public TitleScreen(ShoppingFlow flow, RectTransform parent) : base(flow, parent, "Title")
        {
            UiFactory.MakeText(Root, "ネギリタイプ", 48, UiFactory.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(900, 60), TextAnchor.MiddleCenter);
            UiFactory.MakeText(Root, "タイピングがうまいほど、買い物が安くなる。30日間のやりくり", 18, UiFactory.Quiet, new Vector2(0.5f, 0.5f), new Vector2(0, 134), new Vector2(900, 28), TextAnchor.MiddleCenter);
            difficultyText = UiFactory.MakeText(Root, "", 26, UiFactory.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(900, 36), TextAnchor.MiddleCenter);
            ruleText = UiFactory.MakeText(Root, "", 17, UiFactory.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, 16), new Vector2(900, 80), TextAnchor.MiddleCenter);
            ruleText.lineSpacing = 1.2f;
            menuText = UiFactory.MakeText(Root, "", 24, UiFactory.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(900, 110), TextAnchor.MiddleCenter);
            menuText.lineSpacing = 1.3f;
            noticeText = UiFactory.MakeText(Root, "", 17, UiFactory.Warn, new Vector2(0.5f, 0), new Vector2(0, 56), new Vector2(900, 26), TextAnchor.MiddleCenter);
        }

        public override void Enter()
        {
            items.Clear();
            items.Add(Item.NewGame);
            var save = SaveStore.Load();
            if (save != null && save.difficulty >= 0 && save.difficulty < Difficulties.All.Length)
            {
                items.Add(Item.Continue);
                continueLabel = save.isOver
                    ? $"つづきから（{Difficulties.All[save.difficulty].Label}・月末のまとめ）"
                    : $"つづきから（{Difficulties.All[save.difficulty].Label}・{save.day}日目の朝）";
            }
            items.Add(Item.Practice);
            // セーブがあれば「つづきから」を最初に選んでおく
            cursor = items.Contains(Item.Continue) ? 1 : 0;
            confirmingNew = false;
            notice = "";
        }

        public override void Tick(Keyboard kb, float deltaTime)
        {
            int count = Difficulties.All.Length;
            int before = Flow.Difficulty;
            if (Pressed(kb.leftArrowKey)) Flow.SetDifficulty((Flow.Difficulty + count - 1) % count);
            if (Pressed(kb.rightArrowKey)) Flow.SetDifficulty((Flow.Difficulty + 1) % count);
            if (Pressed(kb.digit1Key)) Flow.SetDifficulty(0);
            if (Pressed(kb.digit2Key)) Flow.SetDifficulty(1);
            if (Pressed(kb.digit3Key)) Flow.SetDifficulty(2);
            if (Pressed(kb.digit4Key)) Flow.SetDifficulty(3);
            int moved = MoveCursor(kb, cursor, items.Count);
            if (moved != cursor || before != Flow.Difficulty) confirmingNew = false;
            cursor = moved;

            if (Pressed(kb.escapeKey)) confirmingNew = false;
            if (!Decide(kb)) return;

            switch (items[cursor])
            {
                case Item.NewGame:
                    if (SaveStore.Exists && !confirmingNew)
                    {
                        confirmingNew = true;
                        return;
                    }
                    Flow.NewGame();
                    break;
                case Item.Continue:
                    if (!Flow.ContinueGame()) notice = "セーブを読み込めませんでした。「はじめから」で始めてください";
                    break;
                default:
                    Flow.StartPractice();
                    break;
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
            var month = Flow.Life.month;
            ruleText.text =
                $"月の予算 {UiFactory.Yen(month.budget)}　｜　買い物 {IntervalLabel(rule.shopIntervalDays)}　｜　1回に{rule.maxItems}品まで　｜　食料は{rule.foodCap}日分まで\n" +
                $"会計で財布が足りないとき：{ShortageDescription(rule)}";

            var labels = new string[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                switch (items[i])
                {
                    case Item.NewGame: labels[i] = $"はじめから（{Flow.Def.Label}）"; break;
                    case Item.Continue: labels[i] = continueLabel; break;
                    default: labels[i] = "タイピング練習"; break;
                }
            }
            menuText.text = UiFactory.Menu(labels, cursor);

            noticeText.text = confirmingNew
                ? UiFactory.Paint("今のセーブを消して、はじめからにします。よければもう一度スペース（Escでやめる）", UiFactory.Bad)
                : notice;
            Message.text = "←→：難易度（はじめから用）　↑↓：選ぶ　スペース：決定";
        }

        public static string IntervalLabel(int days) => days <= 1 ? "毎日" : $"{days}日に1回";

        public static string ShortageDescription(ShoppingRule rule)
        {
            string penalty = rule.satisfactionPerReturn > 0 ? $"1品ごとに満足度－{rule.satisfactionPerReturn}" : "ペナルティなし";
            switch (rule.ShortageValue)
            {
                case ShortageRule.AutoReturnHighestFirst: return $"支払額の高い順に自動で返品（{penalty}）";
                case ShortageRule.AutoReturnLowestFirst: return $"支払額の安い順に自動で返品（{penalty}）";
                default: return $"返品する商品を自分で選ぶ（{penalty}）";
            }
        }
    }
}
