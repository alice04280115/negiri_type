using UnityEngine;
using UnityEngine.UI;

namespace Negiri.Game.Ui
{
    /// <summary>
    /// 絵の差し込み口。Resources/Art/&lt;name&gt;.png があればそれを出し、なければ色つきの四角と置き場所の案内を出す。
    /// ドット絵がにじまないよう、絵は拡大しても点のまま（Point）で表示する。
    /// 絵を置くときは、Unity の Inspector で Texture Type を Default のままにしておけば読める。
    /// </summary>
    public static class ArtSlot
    {
        public static bool Has(string name) => Resources.Load<Texture2D>("Art/" + name) != null;

        public static RectTransform Make(RectTransform parent, string name, string label, Color placeholder, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var tex = Resources.Load<Texture2D>("Art/" + name);
            var rt = UiFactory.NewRect("Art_" + name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                var raw = rt.gameObject.AddComponent<RawImage>();
                raw.texture = tex;
                return rt;
            }

            var img = rt.gameObject.AddComponent<Image>();
            img.color = placeholder;
            var t = UiFactory.MakeText(rt, $"{label}\n<size=12>Art/{name}.png</size>", 14, UiFactory.Quiet, new Vector2(0.5f, 0.5f), Vector2.zero, size, TextAnchor.MiddleCenter);
            t.lineSpacing = 1.1f;
            return rt;
        }
    }
}
