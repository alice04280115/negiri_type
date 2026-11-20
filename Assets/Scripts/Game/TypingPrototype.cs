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

        static readonly (string label, float kps)[] Difficulties =
        {
            ("初級", 2.0f),
            ("中級", 2.5f),
            ("上級", 3.0f),
        };

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
        Text itemText, priceTagText, sentenceText, readingText, guideText, previewText, messageText, resultText, statsText, errorText;
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
            difficulty = 1;
            balance.keysPerSecond = Difficulties[difficulty].kps;

            foreach (var i in items.items) itemById[i.id] = i;

            var errors = DataValidator.Validate(items, sentences);
            foreach (var e in errors) Debug.LogError("[データ] " + e);
            pendingErrors = errors.Count > 0 ? string.Join("\n", errors) : "";
        }

        string pendingErrors = "";

        void NextSentence()
        {
            var pool = new List<SentenceData>();
            foreach (var s in sentences.sentences)
            {
                if (!recent.Contains(s.id)) pool.Add(s);
            }
            if (pool.Count == 0) pool.AddRange(sentences.sentences);

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
            if (c == ' ' ) return;

            // 英数字以外（全角文字）が来たら IME がオンになっている
            if (c > 0x7f)
            {
                phase = Phase.ImeWarning;
                Refresh();
                return;
            }
            if (char.IsControl(c)) return;

            session.Input(c);
            if (session.State == SessionState.Finished) OnFinished();
        }

        void SetDifficulty(int index)
        {
            difficulty = index;
            balance.keysPerSecond = Difficulties[index].kps;
            session = new TypingSession(sentence.reading, balance);
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
            itemText.text = item.name;
            priceTagText.text = $"定価 {item.basePrice:N0}円";
            sentenceText.text = sentence.text;
            readingText.text = sentence.reading;

            var (typed, remaining) = session.Matcher.Guide();
            guideText.text = $"<color=#{Hex(Quiet)}>{typed}</color><color=#{Hex(Ink)}>{remaining}</color>";

            float ratio = phase == Phase.Ready ? 1f : session.RemainingRatio;
            barFill.anchorMax = new Vector2(ratio, 1f);
            barFillImage.color = ratio >= 0.45f ? Good : ratio >= 0.25f ? Warn : Bad;

            var p = session.Preview();
            previewText.text = phase == Phase.Running
                ? $"いまのペース\n{RankLabel(p.Rank)}  -{p.DiscountBp / 100}%\nミス {session.Matcher.MissKeys}"
                : $"制限時間 {session.TimeLimit:0.0}秒";

            switch (phase)
            {
                case Phase.Ready:
                    messageText.text = $"スペースキーで開始　（難易度：{Difficulties[difficulty].label}　1/2/3で切り替え）";
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
                    resultText.text = $"<color=#{Hex(color)}><size=44>{RankLabel(r.Rank)}</size></color>\n{after}\n<size=16>正確率 {r.Accuracy * 100:0}%　ミス {r.MissKeys}　残り時間 {r.RemainingRatio * 100:0}%</size>";
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

            itemText = MakeText(root, "", 36, Ink, new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(700, 48), TextAnchor.MiddleCenter);
            priceTagText = MakeText(root, "", 22, Warn, new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(700, 30), TextAnchor.MiddleCenter);

            sentenceText = MakeText(root, "", 34, Ink, new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(900, 48), TextAnchor.MiddleCenter);
            readingText = MakeText(root, "", 20, Quiet, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(900, 28), TextAnchor.MiddleCenter);
            guideText = MakeText(root, "", 28, Ink, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(900, 40), TextAnchor.MiddleCenter);

            BuildTimeBar(root);

            previewText = MakeText(root, "", 18, Ink, new Vector2(1, 0.5f), new Vector2(-24, -110), new Vector2(200, 80), TextAnchor.UpperRight);
            resultText = MakeText(root, "", 24, Ink, new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(900, 140), TextAnchor.MiddleCenter);
            messageText = MakeText(root, "", 18, Quiet, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(900, 28), TextAnchor.MiddleCenter);
            errorText = MakeText(root, "", 14, Bad, new Vector2(0, 0), new Vector2(24, 80), new Vector2(600, 200), TextAnchor.LowerLeft);
        }

        void BuildTimeBar(RectTransform root)
        {
            var frame = NewRect("TimeBar", root);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(640, 18);
            frame.anchoredPosition = new Vector2(0, -50);
            frame.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.12f);

            barFill = NewRect("Fill", frame);
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = Vector2.one;
            barFill.offsetMin = barFill.offsetMax = Vector2.zero;
            barFillImage = barFill.gameObject.AddComponent<Image>();

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
