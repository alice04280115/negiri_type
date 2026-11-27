using Negiri.Core.Typing;
using UnityEngine;
using UnityEngine.UI;

namespace Negiri.Game.Ui
{
    /// <summary>
    /// 画面部品をコードで作るための共通処理（段階1の TypingPrototype から切り出し）。
    /// 画面は 960×540 を基準にして、画面サイズに合わせて拡大縮小する。
    /// </summary>
    public static class UiFactory
    {
        // 色（ドット絵UIの仮の配色）
        public static readonly Color Bg = new Color32(0x1d, 0x1f, 0x3a, 0xff);
        public static readonly Color Panel = new Color32(0x2a, 0x2d, 0x52, 0xff);
        public static readonly Color Ink = new Color32(0xf4, 0xf1, 0xe8, 0xff);
        public static readonly Color Quiet = new Color32(0x9a, 0x9c, 0xb8, 0xff);
        public static readonly Color Good = new Color32(0x6f, 0xd0, 0x8c, 0xff);
        public static readonly Color Bad = new Color32(0xe8, 0x6a, 0x6a, 0xff);
        public static readonly Color Warn = new Color32(0xf0, 0xc0, 0x50, 0xff);
        /// <summary>選べない項目の色。</summary>
        public static readonly Color Dim = new Color32(0x62, 0x64, 0x82, 0xff);
        /// <summary>ロック貯金の色（画面設計：ロック貯金は金色）。</summary>
        public static readonly Color Gold = new Color32(0xe6, 0xb4, 0x22, 0xff);
        /// <summary>選択中の帯など、パネルより少し明るい色。</summary>
        public static readonly Color PanelLight = new Color32(0x3a, 0x3e, 0x6c, 0xff);

        static Font font;

        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    font = Resources.Load<Font>("Fonts/main");
                    if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return font;
            }
        }

        public static void EnsureCamera()
        {
            if (Camera.main == null)
            {
                var cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.orthographic = true;
            }
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = Bg;
        }

        public static RectTransform CreateCanvas(string name)
        {
            EnsureCamera();
            var canvasGo = new GameObject(name);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            // 画面の縦横比が 16:9 でなくても 960×540 が必ず収まるように広げる（はみ出して見切れるのを防ぐ）
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            return canvasGo.GetComponent<RectTransform>();
        }

        /// <summary>画面の中央に置く 960×540 の舞台。画面の部品はすべてこの中に置く。</summary>
        public static RectTransform CreateStage(RectTransform canvas)
        {
            var stage = NewRect("Stage", canvas);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(0.5f, 0.5f);
            stage.anchoredPosition = Vector2.zero;
            stage.sizeDelta = new Vector2(960, 540);
            return stage;
        }

        /// <summary>親いっぱいに広がる空の入れ物（画面1枚分など）。</summary>
        public static RectTransform Stretch(string name, Transform parent)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>色つきの四角（パネルや選択中の行の帯）。</summary>
        public static Image MakeBox(RectTransform parent, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = NewRect("Box", parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text MakeText(RectTransform parent, string s, int size, Color color, Vector2 anchor, Vector2 pos, Vector2 sizeDelta, TextAnchor align)
        {
            var rt = NewRect("Text", parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = s;
            return t;
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        /// <summary>リッチテキストで色をつける。</summary>
        public static string Paint(string s, Color c) => $"<color=#{Hex(c)}>{s}</color>";

        public static Color RankColor(Rank r)
        {
            switch (r)
            {
                case Rank.Perfect:
                case Rank.Excellent: return Good;
                case Rank.Great: return Warn;
                default: return Bad;
            }
        }

        public static string RankLabel(Rank r)
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

        public static string Yen(int amount) => $"{amount:N0}円";

        /// <summary>「■■■□□□□」のような文字のメーター。</summary>
        public static string Meter(int value, int max, int width)
        {
            if (max <= 0) max = 1;
            int filled = Mathf.Clamp(Mathf.RoundToInt((float)value / max * width), 0, width);
            return new string('■', filled) + new string('□', width - filled);
        }

        /// <summary>上下の選択メニュー（選んでいる行に ▶ を付ける）。disabled の行は薄く出す。</summary>
        public static string Menu(string[] items, int cursor, bool[] disabled = null)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < items.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                bool off = disabled != null && i < disabled.Length && disabled[i];
                if (i == cursor) sb.Append(Paint("▶ " + items[i], off ? Quiet : Warn));
                else sb.Append(Paint("　" + items[i], off ? Dim : Quiet));
            }
            return sb.ToString();
        }
    }
}
