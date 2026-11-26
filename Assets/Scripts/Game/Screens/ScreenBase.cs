using Negiri.Game.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace Negiri.Game.Screens
{
    /// <summary>
    /// 買い物の画面1枚分。状態は ShoppingFlow が持ち、画面は表示とキー入力だけを受け持つ。
    /// </summary>
    public abstract class ScreenBase
    {
        protected readonly ShoppingFlow Flow;
        public RectTransform Root { get; }

        /// <summary>画面の下に出す操作の案内。</summary>
        protected readonly Text Message;

        protected ScreenBase(ShoppingFlow flow, RectTransform parent, string name)
        {
            Flow = flow;
            Root = UiFactory.Stretch(name, parent);
            Message = UiFactory.MakeText(Root, "", 18, UiFactory.Quiet, new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(900, 28), TextAnchor.MiddleCenter);
            Root.gameObject.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            Root.gameObject.SetActive(visible);
            // 操作の案内はパネルより手前に出す（パネルの裏に隠れないように）
            if (visible) Message.rectTransform.SetAsLastSibling();
        }

        /// <summary>この画面に切り替わったとき。</summary>
        public virtual void Enter() { }

        /// <summary>毎フレームのキー入力と時間の処理。</summary>
        public virtual void Tick(Keyboard kb, float deltaTime) { }

        /// <summary>文字の入力（タイピング用）。</summary>
        public virtual void OnText(char c) { }

        public abstract void Refresh();

        protected static bool Pressed(KeyControl key) => key != null && key.wasPressedThisFrame;

        /// <summary>決定はスペースキーに統一する。</summary>
        protected static bool Decide(Keyboard kb) => Pressed(kb.spaceKey);

        /// <summary>上下の選択を動かす（端から端へ回る）。</summary>
        protected static int MoveCursor(Keyboard kb, int cursor, int count)
        {
            if (count <= 0) return 0;
            if (Pressed(kb.upArrowKey)) cursor = (cursor + count - 1) % count;
            if (Pressed(kb.downArrowKey)) cursor = (cursor + 1) % count;
            return Mathf.Clamp(cursor, 0, count - 1);
        }
    }
}
