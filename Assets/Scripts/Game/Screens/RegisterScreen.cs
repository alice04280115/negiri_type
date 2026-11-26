using Negiri.Core.Data;
using Negiri.Core.Shopping;
using Negiri.Core.Typing;
using Negiri.Game.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Negiri.Game.Screens
{
    /// <summary>
    /// レジ。カートの商品を1品ずつ打つ（段階1のタイピング画面と同じ表示）。
    /// 上に「○／○品目」と支払いの見込みを出す。判定は全品を打ち終えてから。
    /// </summary>
    public sealed class RegisterScreen : ScreenBase
    {
        enum Phase { Ready, Running, Result, ImeWarning }

        readonly Text statusText, priceTagText, resultText;
        readonly TypingView view;

        Phase phase;
        int index;
        SentenceData sentence;
        TypingSession session;
        float missFlash;

        public RegisterScreen(ShoppingFlow flow, RectTransform parent) : base(flow, parent, "Register")
        {
            statusText = UiFactory.MakeText(Root, "", 18, UiFactory.Ink, new Vector2(0.5f, 1), new Vector2(0, -48), new Vector2(900, 28), TextAnchor.MiddleCenter);
            priceTagText = UiFactory.MakeText(Root, "", 24, UiFactory.Warn, new Vector2(0.5f, 1), new Vector2(0, -84), new Vector2(700, 32), TextAnchor.MiddleCenter);
            view = new TypingView(Root);
            // 結果は時間バーと目盛りの下に置き、上の文字と重ならないようにする
            resultText = UiFactory.MakeText(Root, "", 24, UiFactory.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(900, 130), TextAnchor.UpperCenter);
            resultText.rectTransform.pivot = new Vector2(0.5f, 1f);
            resultText.lineSpacing = 1.1f;
        }

        CheckoutLine Line => Flow.Checkout.Lines[index];

        public override void Enter()
        {
            NextItem();
        }

        void NextItem()
        {
            index = Flow.Checkout.NextToScan;
            if (index < 0)
            {
                Flow.FinishScanning();
                return;
            }
            var item = Line.Cart.Item;
            // 打つ商品名は、レジに着いた時点で難易度に合う名前から選ぶ
            sentence = Flow.Db.PickSentence(item.id, Flow.Def.Level) ?? new SentenceData { id = item.id, itemIds = new[] { item.id }, text = item.name, reading = item.name, level = 1 };
            session = new TypingSession(sentence.reading, Flow.TypingBalance);
            phase = Phase.Ready;
            missFlash = 0f;
        }

        public override void Tick(Keyboard kb, float deltaTime)
        {
            switch (phase)
            {
                case Phase.Ready:
                    if (Pressed(kb.rKey) && Flow.Def.Reading != ReadingMode.Always)
                    {
                        Flow.ReadingOn[Flow.Difficulty] = !Flow.ReadingOn[Flow.Difficulty];
                    }
                    if (Pressed(kb.spaceKey))
                    {
                        session.Start();
                        phase = Phase.Running;
                    }
                    break;

                case Phase.Running:
                    session.Tick(deltaTime);
                    missFlash = Mathf.Max(0f, missFlash - deltaTime);
                    if (session.State == SessionState.Finished) OnFinished();
                    break;

                case Phase.Result:
                    if (EnterPressed(kb)) NextItem();
                    break;

                case Phase.ImeWarning:
                    if (Pressed(kb.spaceKey))
                    {
                        session = session.Restart();
                        phase = Phase.Ready;
                    }
                    break;
            }
        }

        public override void OnText(char c)
        {
            if (phase != Phase.Running) return;
            // 英数字以外（全角文字）が来たら IME がオンになっている
            if (c > 0x7f)
            {
                phase = Phase.ImeWarning;
                return;
            }
            if (char.IsControl(c)) return;

            if (session.Input(c) == KeyResult.Miss) missFlash = 0.2f;
            if (session.State == SessionState.Finished) OnFinished();
        }

        void OnFinished()
        {
            if (phase == Phase.Result) return;
            Flow.Checkout.Record(index, session.Result.Value);
            phase = Phase.Result;
        }

        public override void Refresh()
        {
            if (index < 0 || session == null) return;
            var checkout = Flow.Checkout;
            int cash = Flow.Wallet.Cash;
            int total = checkout.Total;
            var (best, worst) = checkout.RemainingRange(Flow.PerfectBp);

            // 上部：○／○品目、ここまでの支払い、この先の支払いの見込み
            int number = phase == Phase.Result ? checkout.ScannedCount : checkout.ScannedCount + 1;
            var forecast = best == worst
                ? UiFactory.Yen(total + best)
                : $"{UiFactory.Yen(total + best)}〜{UiFactory.Yen(total + worst)}";
            var forecastColor = total + worst > cash ? (total + best > cash ? UiFactory.Bad : UiFactory.Warn) : UiFactory.Ink;
            statusText.text = $"{number}／{checkout.Lines.Count}品目　｜　ここまで {UiFactory.Yen(total)}　｜　支払いの見込み " + UiFactory.Paint(forecast, forecastColor);

            var line = Line;
            var tag = $"定価 {UiFactory.Yen(line.BasePrice)}";
            if (line.Cart.SaleBp > 0) tag += $"　特売-{line.Cart.SaleBp / 100}%";
            priceTagText.text = tag;

            view.Render(session, sentence, Flow.Def, Flow.ReadingOn[Flow.Difficulty], Flow.TypingBalance, missFlash);

            switch (phase)
            {
                case Phase.Ready:
                    var readingHelp = Flow.Def.Reading == ReadingMode.Always
                        ? ""
                        : $"　読みがな：{(Flow.ReadingOn[Flow.Difficulty] ? "あり" : "なし")}（Rで切り替え）";
                    Message.text = "スペースキーで開始" + readingHelp;
                    Message.color = UiFactory.Quiet;
                    resultText.text = "";
                    break;
                case Phase.Running:
                    Message.text = "半角英数で入力";
                    Message.color = UiFactory.Quiet;
                    resultText.text = "";
                    break;
                case Phase.Result:
                    var r = session.Result.Value;
                    var color = r.Rank == Rank.Miss ? UiFactory.Bad : UiFactory.Good;
                    string after;
                    if (line.Price == line.BasePrice) after = "定価でお買い上げ";
                    else after = $"{UiFactory.Yen(line.BasePrice)} → <b>{UiFactory.Yen(line.Price)}</b>（{UiFactory.Yen(line.BasePrice - line.Price)}おトク" + (line.Cart.SaleBp > 0 ? "・特売込み" : "") + "）";
                    resultText.text = UiFactory.Paint($"<size=44>{UiFactory.RankLabel(r.Rank)}</size>", color) + $"\n{after}\n<size=16>正確率 {r.Accuracy * 100:0}%　ミス {r.MissKeys}（-{r.MissKeys * Flow.TypingBalance.missPenaltySeconds:0.0}秒）　残り時間 {r.RemainingRatio * 100:0}%</size>" + TypingView.MissedHint(r, Flow.TypingBalance);
                    Message.text = checkout.AllScanned ? "Enterで会計へ" : "Enterで次の商品";
                    Message.color = UiFactory.Ink;
                    break;
                case Phase.ImeWarning:
                    Message.text = "日本語入力がオンになっています。半角英数に切り替えて、スペースキーでやり直してください";
                    Message.color = UiFactory.Warn;
                    resultText.text = "";
                    break;
            }
        }
    }
}
