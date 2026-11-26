using System;
using System.Collections.Generic;
using Negiri.Core.Data;
using Negiri.Core.Economy;
using Negiri.Core.Typing;
using Negiri.Game.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Negiri.Game
{
    /// <summary>
    /// タイピング練習（段階1のプロトタイプ）：商品の文章を打ち、評価と値引き後の価格を見るだけの1画面。
    /// 段階2からはタイトルの「タイピング練習」で開き、開始前か結果表示中に Esc でタイトルに戻る。
    /// 表示の部品は TypingView / UiFactory に切り出し、レジ画面と共通にしている。
    /// </summary>
    public sealed class TypingPrototype : MonoBehaviour
    {
        enum Phase { Ready, Running, Result, ImeWarning }

        GameDatabase db;
        bool[] readingOn;
        Action onExit;
        TypingBalance balance;
        readonly Queue<string> recent = new Queue<string>();

        int difficulty = 1;
        Phase phase;
        TypingSession session;
        SentenceData sentence;
        ItemData item;
        int lastPrice;
        float missFlash;
        bool initialized;

        // 統計（調整の参考に表示する）
        int challenges;
        int discountBpSum;
        int perfects;
        int savedTotal;

        RectTransform canvas;
        TypingView view;
        Text priceTagText, messageText, resultText, statsText, errorText;

        DifficultyDef Def => Difficulties.All[difficulty];

        /// <summary>練習を始める。readingOn は難易度ごとの読みがなの表示（買い物と共通）。抜けるときに onExit を呼ぶ。</summary>
        public void Init(GameDatabase database, int startDifficulty, bool[] readingSettings, Action exit)
        {
            db = database;
            readingOn = readingSettings;
            onExit = exit;
            ApplyDifficulty(startDifficulty);
            BuildUi();
            initialized = true;
            NextSentence();
        }

        void OnEnable()
        {
            if (Keyboard.current != null) Keyboard.current.onTextInput += OnTextInput;
        }

        void OnDisable()
        {
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
        }

        void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }

        void NextSentence()
        {
            var pool = new List<SentenceData>();
            var allowed = new List<SentenceData>();
            foreach (var s in db.Sentences.sentences)
            {
                if (s.level == Def.Level) allowed.Add(s);
            }
            foreach (var s in allowed)
            {
                if (!recent.Contains(s.id)) pool.Add(s);
            }
            if (pool.Count == 0) pool.AddRange(allowed);

            sentence = pool[UnityEngine.Random.Range(0, pool.Count)];
            recent.Enqueue(sentence.id);
            while (recent.Count > 3) recent.Dequeue();

            item = db.ItemById[sentence.itemIds[0]];
            session = new TypingSession(sentence.reading, balance);
            phase = Phase.Ready;
            Refresh();
        }

        // ---------------- 入力 ----------------

        void Update()
        {
            if (!initialized) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            switch (phase)
            {
                case Phase.Ready:
                    if (kb.escapeKey.wasPressedThisFrame)
                    {
                        onExit?.Invoke();
                        return;
                    }
                    if (kb.digit1Key.wasPressedThisFrame) SetDifficulty(0);
                    if (kb.digit2Key.wasPressedThisFrame) SetDifficulty(1);
                    if (kb.digit3Key.wasPressedThisFrame) SetDifficulty(2);
                    if (kb.digit4Key.wasPressedThisFrame) SetDifficulty(3);
                    if (kb.rKey.wasPressedThisFrame && Def.Reading != ReadingMode.Always)
                    {
                        readingOn[difficulty] = !readingOn[difficulty];
                    }
                    if (kb.spaceKey.wasPressedThisFrame)
                    {
                        session.Start();
                        phase = Phase.Running;
                    }
                    break;

                case Phase.Running:
                    if (kb.escapeKey.wasPressedThisFrame)
                    {
                        NextSentence();
                        return;
                    }
                    session.Tick(Time.deltaTime);
                    missFlash = Mathf.Max(0f, missFlash - Time.deltaTime);
                    if (session.State == SessionState.Finished) OnFinished();
                    break;

                case Phase.Result:
                    if (kb.escapeKey.wasPressedThisFrame)
                    {
                        onExit?.Invoke();
                        return;
                    }
                    if (kb.spaceKey.wasPressedThisFrame) NextSentence();
                    break;

                case Phase.ImeWarning:
                    if (kb.spaceKey.wasPressedThisFrame)
                    {
                        session = session.Restart();
                        phase = Phase.Ready;
                    }
                    break;
            }
            Refresh();
        }

        void OnTextInput(char c)
        {
            if (!initialized || phase != Phase.Running) return;
            // スペースは開始キーだが、打っている最中は英語の商品名の空白として判定に回す

            // 英数字以外（全角文字）が来たら IME がオンになっている
            if (c > 0x7f)
            {
                phase = Phase.ImeWarning;
                Refresh();
                return;
            }
            if (char.IsControl(c)) return;

            if (session.Input(c) == KeyResult.Miss) missFlash = 0.2f;
            if (session.State == SessionState.Finished) OnFinished();
        }

        void SetDifficulty(int index)
        {
            if (index == difficulty) return;
            ApplyDifficulty(index);
            NextSentence();
        }

        void ApplyDifficulty(int index)
        {
            difficulty = index;
            balance = Difficulties.CreateBalance(db.Balance.typing, index);
        }

        void OnFinished()
        {
            if (phase == Phase.Result) return;
            var r = session.Result.Value;
            lastPrice = Pricing.Price(item.basePrice, r.DiscountBp, 0, item.maxDiscountBp);

            challenges++;
            discountBpSum += r.DiscountBp;
            if (r.Rank == Rank.Perfect) perfects++;
            savedTotal += Pricing.Saved(item.basePrice, lastPrice);

            phase = Phase.Result;
        }

        // ---------------- 表示 ----------------

        void Refresh()
        {
            priceTagText.text = $"定価 {item.basePrice:N0}円";
            view.Render(session, sentence, Def, readingOn[difficulty], balance, missFlash);

            switch (phase)
            {
                case Phase.Ready:
                    var readingHelp = Def.Reading == ReadingMode.Always
                        ? ""
                        : $"　読みがな：{(readingOn[difficulty] ? "あり" : "なし")}（Rで切り替え）";
                    messageText.text = $"スペースキーで開始　（難易度：{Def.Label}　1～4で切り替え）{readingHelp}　Esc：タイトルへ";
                    messageText.color = UiFactory.Quiet;
                    resultText.text = "";
                    break;
                case Phase.Running:
                    messageText.text = "半角英数で入力　Escで飛ばす";
                    messageText.color = UiFactory.Quiet;
                    resultText.text = "";
                    break;
                case Phase.Result:
                    var r = session.Result.Value;
                    var color = r.Rank == Rank.Miss ? UiFactory.Bad : UiFactory.Good;
                    var after = r.Rank == Rank.Miss
                        ? "定価でお買い上げ"
                        : $"{item.basePrice:N0}円 → <b>{lastPrice:N0}円</b>（{Pricing.Saved(item.basePrice, lastPrice):N0}円おトク）";
                    resultText.text = UiFactory.Paint($"<size=44>{UiFactory.RankLabel(r.Rank)}</size>", color) + $"\n{after}\n<size=16>正確率 {r.Accuracy * 100:0}%　ミス {r.MissKeys}（-{r.MissKeys * balance.missPenaltySeconds:0.0}秒）　残り時間 {r.RemainingRatio * 100:0}%</size>" + TypingView.MissedHint(r, balance);
                    messageText.text = "スペースで次の商品　Esc：タイトルへ";
                    messageText.color = UiFactory.Ink;
                    break;
                case Phase.ImeWarning:
                    messageText.text = "日本語入力がオンになっています。半角英数に切り替えて、スペースキーでやり直してください";
                    messageText.color = UiFactory.Warn;
                    resultText.text = "";
                    break;
            }

            var avg = challenges == 0 ? 0f : discountBpSum / 100f / challenges;
            statsText.text = $"挑戦 {challenges}回　平均割引 {avg:0.0}%　PERFECT {perfects}回　節約 {savedTotal:N0}円";
            errorText.text = db.Errors;
        }

        // ---------------- 画面の組み立て ----------------

        void BuildUi()
        {
            canvas = UiFactory.CreateCanvas("PracticeCanvas");
            var root = UiFactory.CreateStage(canvas);

            UiFactory.MakeText(root, "ネギリタイプ　タイピング練習", 16, UiFactory.Quiet, new Vector2(0, 1), new Vector2(24, -16), new Vector2(600, 24), TextAnchor.UpperLeft);
            statsText = UiFactory.MakeText(root, "", 16, UiFactory.Quiet, new Vector2(1, 1), new Vector2(-24, -16), new Vector2(600, 24), TextAnchor.UpperRight);

            // 打つ文字が商品名そのものなので、上には値札だけを出す
            priceTagText = UiFactory.MakeText(root, "", 26, UiFactory.Warn, new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(700, 34), TextAnchor.MiddleCenter);

            view = new TypingView(root);

            // 結果は時間バーと目盛りの下に置き、上の文字と重ならないようにする
            resultText = UiFactory.MakeText(root, "", 24, UiFactory.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(900, 130), TextAnchor.UpperCenter);
            resultText.rectTransform.pivot = new Vector2(0.5f, 1f);
            resultText.lineSpacing = 1.1f;
            messageText = UiFactory.MakeText(root, "", 18, UiFactory.Quiet, new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(900, 28), TextAnchor.MiddleCenter);
            errorText = UiFactory.MakeText(root, "", 14, UiFactory.Bad, new Vector2(0, 0), new Vector2(24, 80), new Vector2(600, 200), TextAnchor.LowerLeft);
        }
    }
}
