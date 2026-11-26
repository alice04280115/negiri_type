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

        public void SetVisible(bool visible) => Root.gameObject.SetActive(visible);

        /// <summary>この画面に切り替わったとき。</summary>
        public virtual void Enter() { }

        /// <summary>毎フレームのキー入力と時間の処理。</summary>
        public virtual void Tick(Keyboard kb, float deltaTime) { }

        /// <summary>文字の入力（タイピング用）。</summary>
        public virtual void OnText(char c) { }

        public abstract void Refresh();

        protected static bool Pressed(KeyControl key) => key != null && key.wasPressedThisFrame;

        protected static bool EnterPressed(Keyboard kb) => Pressed(kb.enterKey) || Pressed(kb.numpadEnterKey);
    }
}
