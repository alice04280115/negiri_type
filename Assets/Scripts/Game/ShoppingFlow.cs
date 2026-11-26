using Negiri.Core.Economy;
using Negiri.Core.Shopping;
using Negiri.Core.Typing;
using Negiri.Game.Screens;
using Negiri.Game.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Negiri.Game
{
    /// <summary>
    /// 段階2：1回の買い物の進行役。タイトル → 店内 → レジ →（足りなければ）返品 → レシート。
    /// 財布・カート・会計の状態はここで持ち、計算は Core に任せる。各画面は表示とキー入力だけを受け持つ。
    /// 画面はコードで組み立てるので、空のシーンで再生するだけで動く。
    /// </summary>
    public sealed class ShoppingFlow : MonoBehaviour
    {
        public GameDatabase Db { get; private set; }
        public int Difficulty { get; private set; } = 1;
        public DifficultyDef Def => Difficulties.All[Difficulty];
        public ShoppingRule Rule => Db.Balance.shopping.difficulties[Difficulty];
        public TypingBalance TypingBalance { get; private set; }
        public int PerfectBp => Difficulties.BestDiscountBp(TypingBalance);

        /// <summary>上級・達人で読みがなを出すかどうか（難易度ごとに覚えておく）。</summary>
        public readonly bool[] ReadingOn = { true, true, true, true };

        public Wallet Wallet { get; private set; }
        public Cart Cart { get; private set; }
        public Checkout Checkout { get; private set; }
        public Receipt Receipt { get; private set; }

        RectTransform canvas;
        Text headerText, walletText, errorText;
        TitleScreen title;
        StoreScreen store;
        RegisterScreen register;
        ReturnScreen returns;
        ReceiptScreen receipt;
        ScreenBase current;
        TypingPrototype practice;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            // どのシーンで再生してもこの画面が出るようにする
            if (FindAnyObjectByType<ShoppingFlow>() != null || FindAnyObjectByType<TypingPrototype>() != null) return;
            new GameObject("ShoppingFlow").AddComponent<ShoppingFlow>();
        }

        void Awake()
        {
            Db = new GameDatabase();
            SetDifficulty(1);
            BuildUi();
            ShowTitle();
        }

        void OnEnable()
        {
            if (Keyboard.current != null) Keyboard.current.onTextInput += OnTextInput;
        }

        void OnDisable()
        {
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
        }

        void BuildUi()
        {
            canvas = UiFactory.CreateCanvas("ShoppingCanvas");
            var stage = UiFactory.CreateStage(canvas);
            headerText = UiFactory.MakeText(stage, "", 16, UiFactory.Quiet, new Vector2(0, 1), new Vector2(24, -16), new Vector2(600, 24), TextAnchor.UpperLeft);
            walletText = UiFactory.MakeText(stage, "", 18, UiFactory.Warn, new Vector2(1, 1), new Vector2(-24, -14), new Vector2(400, 24), TextAnchor.UpperRight);

            title = new TitleScreen(this, stage);
            store = new StoreScreen(this, stage);
            register = new RegisterScreen(this, stage);
            returns = new ReturnScreen(this, stage);
            receipt = new ReceiptScreen(this, stage);

            errorText = UiFactory.MakeText(stage, Db.Errors, 14, UiFactory.Bad, new Vector2(0, 0), new Vector2(24, 80), new Vector2(600, 200), TextAnchor.LowerLeft);
        }

        // ---------------- 進行 ----------------

        public void SetDifficulty(int index)
        {
            Difficulty = index;
            TypingBalance = Difficulties.CreateBalance(Db.Balance.typing, index);
        }

        public void ShowTitle()
        {
            Wallet = null;
            Cart = null;
            Checkout = null;
            Receipt = null;
            Switch(title);
        }

        /// <summary>財布に試作用の金額を入れて、店内へ。</summary>
        public void StartShopping()
        {
            Wallet = new Wallet(Db.Balance.shopping.startingCash);
            Cart = new Cart(Rule.maxItems);
            Checkout = null;
            Receipt = null;
            Switch(store);
        }

        public void GoToRegister()
        {
            if (Cart == null || Cart.Count == 0) return;
            Checkout = new Checkout(Cart.Lines, Rule);
            Switch(register);
        }

        /// <summary>全品を打ち終えたら判定する。足りれば払い、足りなければ返品へ。</summary>
        public void FinishScanning()
        {
            if (Checkout.Shortage(Wallet.Cash) > 0) Switch(returns);
            else Pay();
        }

        public void Pay()
        {
            Receipt = Checkout.Pay(Wallet);
            if (!Wallet.IsBalanced) Debug.LogError("[お金] 収入合計 ＝ 支出合計 ＋ 財布 ＋ ロック貯金 が成り立っていません");
            Switch(receipt);
        }

        /// <summary>段階1の画面を「タイピング練習」として開く。抜けるとタイトルに戻る。</summary>
        public void StartPractice()
        {
            Switch(null);
            canvas.gameObject.SetActive(false);
            var go = new GameObject("TypingPractice");
            practice = go.AddComponent<TypingPrototype>();
            practice.Init(Db, Difficulty, ReadingOn, () =>
            {
                Destroy(go);
                practice = null;
                canvas.gameObject.SetActive(true);
                ShowTitle();
            });
        }

        void Switch(ScreenBase next)
        {
            current?.SetVisible(false);
            current = next;
            if (current == null) return;
            current.SetVisible(true);
            current.Enter();
            current.Refresh();
        }

        // ---------------- 入力と表示 ----------------

        void Update()
        {
            if (practice != null || current == null) return;
            var kb = Keyboard.current;
            if (kb != null) current.Tick(kb, Time.deltaTime);
            RefreshHeader();
            current?.Refresh();
        }

        void OnTextInput(char c)
        {
            if (practice != null) return;
            current?.OnText(c);
        }

        void RefreshHeader()
        {
            headerText.text = $"ネギリタイプ　段階2（1回の買い物）　難易度：{Def.Label}";
            walletText.text = Wallet == null ? "" : $"財布 {UiFactory.Yen(Wallet.Cash)}";
            errorText.text = Db.Errors;
        }
    }
}
