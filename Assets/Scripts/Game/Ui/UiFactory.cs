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
            scaler.matchWidthOrHeight = 0.5f;
            return canvasGo.GetComponent<RectTransform>();
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
    }
}
