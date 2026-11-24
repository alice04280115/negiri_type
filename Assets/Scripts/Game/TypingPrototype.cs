using System.Collections.Generic;
using System.Text;
using Negiri.Core.Data;
using Negiri.Core.Economy;
using Negiri.Core.Typing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Negiri.Game
{
    /// <summary>
    /// 段階1のプロトタイプ：商品の文章を打ち、評価と値引き後の価格を見るだけの1画面。
    /// 画面はコードで組み立てるので、空のシーンで再生するだけで動く。
    /// 確認したいこと：制限時間と評価の境目が厳しすぎないか。値切れると嬉しいか。
    /// </summary>
    public sealed class TypingPrototype : MonoBehaviour
    {
        enum Phase { Ready, Running, Result, ImeWarning }

        /// <summary>読みがな（とローマ字の見本）の出し方。</summary>
        enum ReadingMode
        {
            /// <summary>常に出す。</summary>
            Always,
            /// <summary>Rキーで隠せる。隠しても、残り時間が EXCELLENT の線を切ると出てくる。</summary>
            OptionalWithReveal,
            /// <summary>Rキーで隠せる。隠したら最後まで出ない。</summary>
            Optional,
        }

        // 難易度ごとの打鍵速度・猶予秒・ミス1回で減る秒・出てくる商品名の level・読みがなの出し方。数値はすべて（仮）。
        // level 1：かんたんな単語 / 2：漢字多め / 3：見慣れない漢字・カタカナ多め / 4：英語とサブタイトルつき
        static readonly (string label, float kps, float grace, float missPenalty, int level, ReadingMode reading)[] Difficulties =
        {
            ("初級", 2.0f, 2.0f, 0.5f, 1, ReadingMode.Always),
            ("中級", 2.5f, 2.0f, 0.5f, 2, ReadingMode.Always),
            ("上級", 3.5f, 1.5f, 1.0f, 3, ReadingMode.OptionalWithReveal),
            ("達人", 5.0f, 1.0f, 2.0f, 4, ReadingMode.Optional),
        };

        // 上級・達人で読みがなを出すかどうか（難易度ごとに覚えておく）
        readonly bool[] readingOn = { true, true, true, true };
        float missFlash;

        // 色（ドット絵UIの仮の配色）
        static readonly Color Bg = new Color32(0x1d, 0x1f, 0x3a, 0xff);
        static readonly Color Ink = new Color32(0xf4, 0xf1, 0xe8, 0xff);
        static readonly Color Quiet = new Color32(0x9a, 0x9c, 0xb8, 0xff);
        static readonly Color Good = new Color32(0x6f, 0xd0, 0x8c, 0xff);
        static readonly Color Bad = new Color32(0xe8, 0x6a, 0x6a, 0xff);
        static readonly Color Warn = new Color32(0xf0, 0xc0, 0x50, 0xff);

        ItemList items;
        SentenceList sentences;
        TypingBalance balance;
        readonly Dictionary<string, ItemData> itemById = new Dictionary<string, ItemData>();
        readonly Queue<string> recent = new Queue<string>();

        int difficulty = 1;
        Phase phase;
        TypingSession session;
        SentenceData sentence;
        ItemData item;
        int lastPrice;

        // 統計（調整の参考に表示する）
        int challenges;
        int discountBpSum;
        int perfects;
        int savedTotal;

        Font font;
        Text priceTagText, sentenceText, readingText, guideText, previewText, barRankText, messageText, resultText, statsText, errorText;
        RectTransform barFill;
        Image barFillImage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            // 段階1では、どのシーンで再生してもこの画面が出るようにする
            if (FindAnyObjectByType<TypingPrototype>() != null) return;
            new GameObject("TypingPrototype").AddComponent<TypingPrototype>();
        }

        void Awake()
        {
            LoadData();
            BuildUi();
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

        // ---------------- データ ----------------

        void LoadData()
        {
            items = JsonUtility.FromJson<ItemList>(Resources.Load<TextAsset>("Data/items").text);
            sentences = JsonUtility.FromJson<SentenceList>(Resources.Load<TextAsset>("Data/sentences").text);
            var balanceData = JsonUtility.FromJson<BalanceData>(Resources.Load<TextAsset>("Data/balance").text);
            balance = balanceData.typing;
            ApplyDifficulty(1);

            foreach (var i in items.items) itemById[i.id] = i;

            var errors = DataValidator.Validate(items, sentences);
            foreach (var e in errors) Debug.LogError("[データ] " + e);
            pendingErrors = errors.Count > 0 ? string.Join("\n", errors) : "";
        }

        string pendingErrors = "";

        void NextSentence()
        {
            var pool = new List<SentenceData>();
            var level = Difficulties[difficulty].level;
            var allowed = new List<SentenceData>();
            foreach (var s in sentences.sentences)
            {
                if (s.level == level) allowed.Add(s);
            }
            foreach (var s in allowed)
            {
                if (!recent.Contains(s.id)) pool.Add(s);
            }
            if (pool.Count == 0) pool.AddRange(allowed);

            sentence = pool[Random.Range(0, pool.Count)];
            recent.Enqueue(sentence.id);
            while (recent.Count > 3) recent.Dequeue();

            item = itemById[sentence.itemIds[0]];
            session = new TypingSession(sentence.reading, balance);
            phase = Phase.Ready;
            Refresh();
        }

        // ---------------- 入力 ----------------

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            switch (phase)
            {
                case Phase.Ready:
                    if (kb.digit1Key.wasPressedThisFrame) SetDifficulty(0);
                    if (kb.digit2Key.wasPressedThisFrame) SetDifficulty(1);
                    if (kb.digit3Key.wasPressedThisFrame) SetDifficulty(2);
                    if (kb.digit4Key.wasPressedThisFrame) SetDifficulty(3);
                    if (kb.rKey.wasPressedThisFrame && Difficulties[difficulty].reading != ReadingMode.Always)
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
                    if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) NextSentence();
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
            if (phase != Phase.Running) return;
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
            balance.keysPerSecond = Difficulties[index].kps;
            balance.graceSeconds = Difficulties[index].grace;
            balance.missPenaltySeconds = Difficulties[index].missPenalty;
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
            sentenceText.text = sentence.text;
            // 達人のサブタイトルつきの長い商品名でも画面からはみ出さないよう、文字を小さくする
            sentenceText.fontSize = sentence.text.Length <= 14 ? 40 : sentence.text.Length <= 22 ? 32 : 26;
            var (typed, remaining) = session.Matcher.Guide();
            if (ShowReading())
            {
                readingText.text = sentence.reading;
                guideText.text = $"<color=#{Hex(Quiet)}>{typed}</color><color=#{Hex(Ink)}>{remaining}</color>";
            }
            else
            {
                // 読みがなを隠しているときは、打った分のローマ字だけを出す
                readingText.text = Difficulties[difficulty].reading == ReadingMode.OptionalWithReveal
                    ? "（読みがなは残り時間がEXCELLENTの線を切ると出ます）"
                    : "（読みがななし）";
                guideText.text = $"<color=#{Hex(Quiet)}>{typed}</color><color=#{Hex(Ink)}>_</color>";
            }

            // バーは実際の評価：時間で減り、ミスでも減る。色と右の文字は、いま打ち終えたときの評価
            var p = session.Preview();
            float ratio = phase == Phase.Ready ? 1f : session.RemainingRatio;
            barFill.anchorMax = new Vector2(ratio, 1f);
            barFillImage.color = missFlash > 0f ? Ink : RankColor(phase == Phase.Result ? session.Result.Value.Rank : p.Rank);
            barRankText.text = phase == Phase.Running
                ? $"{RankLabel(p.Rank)}  -{p.DiscountBp / 100}%"
                : "";
            barRankText.color = RankColor(p.Rank);

            // 数字は右下に
            previewText.text = phase == Phase.Ready
                ? $"制限時間 {session.TimeLimit:0.0}秒\nミス1回で -{balance.missPenaltySeconds:0.0}秒"
                : $"残り {session.Remaining:0.0}秒\n正確率 {session.Preview().Accuracy * 100:0}%　ミス {session.Matcher.MissKeys}";

            switch (phase)
            {
                case Phase.Ready:
                    var readingHelp = Difficulties[difficulty].reading == ReadingMode.Always
                        ? ""
                        : $"　読みがな：{(readingOn[difficulty] ? "あり" : "なし")}（Rで切り替え）";
                    messageText.text = $"スペースキーで開始　（難易度：{Difficulties[difficulty].label}　1〜4で切り替え）{readingHelp}";
                    messageText.color = Quiet;
                    resultText.text = "";
                    break;
                case Phase.Running:
                    messageText.text = "半角英数で入力　Escで飛ばす";
                    messageText.color = Quiet;
                    resultText.text = "";
                    break;
                case Phase.Result:
                    var r = session.Result.Value;
                    var color = r.Rank == Rank.Miss ? Bad : Good;
                    var after = r.Rank == Rank.Miss
                        ? "定価でお買い上げ"
                        : $"{item.basePrice:N0}円 → <b>{lastPrice:N0}円</b>（{Pricing.Saved(item.basePrice, lastPrice):N0}円おトク）";
                    resultText.text = $"<color=#{Hex(color)}><size=44>{RankLabel(r.Rank)}</size></color>\n{after}\n<size=16>正確率 {r.Accuracy * 100:0}%　ミス {r.MissKeys}（-{r.MissKeys * balance.missPenaltySeconds:0.0}秒）　残り時間 {r.RemainingRatio * 100:0}%</size>{MissedHint(r)}";
                    messageText.text = "Enterで次の商品";
                    messageText.color = Ink;
                    break;
                case Phase.ImeWarning:
                    messageText.text = "日本語入力がオンになっています。半角英数に切り替えて、スペースキーでやり直してください";
                    messageText.color = Warn;
                    resultText.text = "";
                    break;
            }

            var avg = challenges == 0 ? 0f : discountBpSum / 100f / challenges;
            statsText.text = $"挑戦 {challenges}回　平均割引 {avg:0.0}%　PERFECT {perfects}回　節約 {savedTotal:N0}円";
            errorText.text = pendingErrors;
        }

        // 時間は足りていたのに正確率で届かなかったとき、その理由を添える
        string MissedHint(TypingResult r)
        {
            var rule = TypingEvaluator.MissedByAccuracy(r, balance);
            if (rule == null) return "";
            string why;
            if (r.Accuracy + 1e-5f < rule.minAccuracy)
                why = $"正確率があと{System.Math.Ceiling((rule.minAccuracy - r.Accuracy) * 100 - 0.001):0}%あれば";
            else
                why = "ミスなしなら";
            return $"\n<size=16><color=#{Hex(Warn)}>時間は{RankLabel(rule.RankValue)}に届いています。{why}{RankLabel(rule.RankValue)}でした</color></size>";
        }

        bool ShowReading()
        {
            var mode = Difficulties[difficulty].reading;
            if (mode == ReadingMode.Always || readingOn[difficulty]) return true;
            if (mode == ReadingMode.Optional) return false;
            // 上級：隠していても、EXCELLENT の線（いちばん高い時間の条件）を切ったら出す
            if (phase == Phase.Ready) return false;
            float line = 0f;
            foreach (var rule in balance.ranks) line = Mathf.Max(line, rule.minRemainingRatio);
            return session.RemainingRatio < line;
        }

        static Color RankColor(Rank r)
        {
            switch (r)
            {
                case Rank.Perfect:
                case Rank.Excellent: return Good;
                case Rank.Great: return Warn;
                default: return Bad;
            }
        }

        static string RankLabel(Rank r)
        {
            switch (r)
            {
                case Rank.Perfect: return "PERFECT";
                case Rank.Excellent: return "EXCELLENT";
                case Rank.Great: return "GREAT";
                case Rank.Good: return "GOOD";
                default: return "MISS";
            }
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        // ---------------- 画面の組み立て ----------------

        void BuildUi()
        {
            font = Resources.Load<Font>("Fonts/main");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (Camera.main == null)
            {
                var cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.orthographic = true;
            }
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = Bg;

            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.GetComponent<RectTransform>();

            MakeText(root, "ネギリタイプ　段階1プロトタイプ", 16, Quiet, new Vector2(0, 1), new Vector2(24, -16), new Vector2(600, 24), TextAnchor.UpperLeft);
            statsText = MakeText(root, "", 16, Quiet, new Vector2(1, 1), new Vector2(-24, -16), new Vector2(600, 24), TextAnchor.UpperRight);

            // 打つ文字が商品名そのものなので、上には値札だけを出す
            priceTagText = MakeText(root, "", 26, Warn, new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(700, 34), TextAnchor.MiddleCenter);

            sentenceText = MakeText(root, "", 40, Ink, new Vector2(0.5f, 0.5f), new Vector2(0, 100), new Vector2(900, 52), TextAnchor.MiddleCenter);
            readingText = MakeText(root, "", 20, Quiet, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(900, 28), TextAnchor.MiddleCenter);
            guideText = MakeText(root, "", 28, Ink, new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(900, 40), TextAnchor.MiddleCenter);

            BuildTimeBar(root);

            previewText = MakeText(root, "", 18, Ink, new Vector2(1, 0), new Vector2(-24, 20), new Vector2(260, 50), TextAnchor.LowerRight);
            // 結果は時間バーと目盛りの下に置き、上の文字と重ならないようにする
            resultText = MakeText(root, "", 24, Ink, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(900, 130), TextAnchor.UpperCenter);
            resultText.rectTransform.pivot = new Vector2(0.5f, 1f);
            resultText.lineSpacing = 1.1f;
            messageText = MakeText(root, "", 18, Quiet, new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(900, 28), TextAnchor.MiddleCenter);
            errorText = MakeText(root, "", 14, Bad, new Vector2(0, 0), new Vector2(24, 80), new Vector2(600, 200), TextAnchor.LowerLeft);
        }

        void BuildTimeBar(RectTransform root)
        {
            var frame = NewRect("TimeBar", root);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(640, 18);
            frame.anchoredPosition = new Vector2(0, -20);
            frame.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.12f);

            barFill = NewRect("Fill", frame);
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = Vector2.one;
            barFill.offsetMin = barFill.offsetMax = Vector2.zero;
            barFillImage = barFill.gameObject.AddComponent<Image>();

            barRankText = MakeText(frame, "", 18, Ink, new Vector2(1, 0.5f), new Vector2(12, 0), new Vector2(180, 24), TextAnchor.MiddleLeft);
            barRankText.rectTransform.pivot = new Vector2(0, 0.5f);

            // 評価の境目（残り45%と25%）に目盛りを入れる
            AddMarker(frame, 0.45f, "EXCELLENT");
            AddMarker(frame, 0.25f, "GREAT");
        }

        void AddMarker(RectTransform frame, float ratio, string label)
        {
            var line = NewRect("Marker", frame);
            line.anchorMin = new Vector2(ratio, 0);
            line.anchorMax = new Vector2(ratio, 1);
            line.sizeDelta = new Vector2(2, 8);
            line.gameObject.AddComponent<Image>().color = Ink;
            var t = MakeText(frame, label, 12, Quiet, new Vector2(ratio, 0), new Vector2(0, -14), new Vector2(120, 16), TextAnchor.MiddleCenter);
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        Text MakeText(RectTransform parent, string s, int size, Color color, Vector2 anchor, Vector2 pos, Vector2 sizeDelta, TextAnchor align)
        {
            var rt = NewRect("Text", parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = s;
            return t;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
    }
}
