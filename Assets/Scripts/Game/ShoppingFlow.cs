using System.Collections.Generic;
using Negiri.Core.Economy;
using Negiri.Core.Life;
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
    /// ゲームの進行役（段階3：30日間）。
    /// タイトル → 月初 → マイルーム ⇄（店内 → レジ →（足りなければ）返品 → レシート）→ 夜 → … → 月末のまとめ。
    /// 今月の状態は Core の MonthState が持ち、ここは画面の切り替えとセーブだけを受け持つ。
    /// 画面はコードで組み立てるので、空のシーンで再生するだけで動く。
    /// </summary>
    public sealed class ShoppingFlow : MonoBehaviour
    {
        public GameDatabase Db { get; private set; }
        public int Difficulty { get; private set; } = 1;
        public DifficultyDef Def => Difficulties.All[Difficulty];
        public ShoppingRule Rule => Db.Balance.shopping.difficulties[Difficulty];
        public LifeBalance Life => Db.Balance.life;
        public TypingBalance TypingBalance { get; private set; }
        public int PerfectBp => Difficulties.BestDiscountBp(TypingBalance);

        /// <summary>上級・達人で読みがなを出すかどうか（難易度ごとに覚えておく）。</summary>
        public readonly bool[] ReadingOn = { true, true, true, true };

        /// <summary>今月の状態。タイトルでは null。</summary>
        public MonthState Month { get; private set; }
        public Wallet Wallet => Month?.Wallet;
        public Cart Cart { get; private set; }
        public Checkout Checkout { get; private set; }
        public Receipt Receipt { get; private set; }
        /// <summary>直前の買い物で在庫と満足度がどう変わったか（レシートに出す）。</summary>
        public LifeChange LastChange { get; private set; }
        /// <summary>直前に過ごした夜（夜のまとめに出す）。</summary>
        public List<NightReport> LastNights { get; private set; } = new List<NightReport>();

        RectTransform canvas;
        Text errorText;
        StatusBar statusBar;
        TitleScreen title;
        MonthStartScreen monthStart;
        MyRoomScreen myRoom;
        StoreScreen store;
        RegisterScreen register;
        ReturnScreen returns;
        ReceiptScreen receipt;
        NightScreen night;
        LedgerScreen ledger;
        MonthEndScreen monthEnd;
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
            statusBar = new StatusBar(stage);

            title = new TitleScreen(this, stage);
            monthStart = new MonthStartScreen(this, stage);
            myRoom = new MyRoomScreen(this, stage);
            store = new StoreScreen(this, stage);
            register = new RegisterScreen(this, stage);
            returns = new ReturnScreen(this, stage);
            receipt = new ReceiptScreen(this, stage);
            night = new NightScreen(this, stage);
            ledger = new LedgerScreen(this, stage);
            monthEnd = new MonthEndScreen(this, stage);

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
            Month = null;
            ClearShopping();
            Switch(title);
        }

        void ClearShopping()
        {
            Cart = null;
            Checkout = null;
            Receipt = null;
            LastChange = null;
        }

        /// <summary>はじめから：今の難易度で新しい月を始め、月初の画面へ（セーブは月初を確定したときに上書きする）。</summary>
        public void NewGame()
        {
            Month = MonthState.Start(Life, Rule, Difficulty);
            ClearShopping();
            Switch(monthStart);
        }

        /// <summary>つづきから：セーブの日の朝（月末ならまとめ）から再開する。</summary>
        public bool ContinueGame()
        {
            var save = SaveStore.Load();
            if (save == null || save.difficulty < 0 || save.difficulty >= Difficulties.All.Length) return false;
            try
            {
                SetDifficulty(save.difficulty);
                Month = MonthState.FromSave(save, Life, Rule);
            }
            catch (System.ArgumentException e)
            {
                Debug.LogError("[セーブ] " + e.Message);
                Month = null;
                return false;
            }
            ClearShopping();
            Switch(Month.IsOver ? (ScreenBase)monthEnd : myRoom);
            return true;
        }

        /// <summary>月初のロック額を決めて、1日目の朝へ。</summary>
        public void ConfirmMonthStart(int lockAmount)
        {
            if (lockAmount > 0) Month.Lock(lockAmount);
            Save();
            Switch(myRoom);
        }

        /// <summary>家計簿からロックを追加する（解除はできない）。</summary>
        public void AddLock(int amount)
        {
            if (amount <= 0) return;
            Month.Lock(amount);
            Save();
        }

        public void Save()
        {
            if (Month != null) SaveStore.Write(Month.ToSave());
        }

        public void ShowMyRoom() => Switch(myRoom);
        public void OpenLedger() => Switch(ledger);

        /// <summary>今日の曜日の特売割引。</summary>
        public int SaleBp(string itemId) => Db.Store.SaleBp(itemId, Month != null ? Month.Weekday : -1);

        public void GoShopping()
        {
            if (Month == null || !Month.CanShop) return;
            ClearShopping();
            Cart = new Cart(Rule.maxItems);
            Switch(store);
        }

        /// <summary>何も買わずに家に帰る（買い物はしなかったことになる）。</summary>
        public void LeaveStore()
        {
            ClearShopping();
            Switch(myRoom);
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
            LastChange = Month.ApplyShopping(Receipt);
            if (!Wallet.IsBalanced) Debug.LogError("[お金] 収入合計 ＝ 支出合計 ＋ 財布 ＋ ロック貯金 が成り立っていません");
            Switch(receipt);
        }

        /// <summary>夜を迎えて次の日へ（家で過ごす・買い物の後）。</summary>
        public void EndDay()
        {
            ClearShopping();
            LastNights = new List<NightReport> { Month.EndDay() };
            Save();
            Switch(night);
        }

        /// <summary>数日進める：買い物できる日か、在庫が切れる日の朝か、月末まで。</summary>
        public void AdvanceDays(int nights)
        {
            ClearShopping();
            LastNights = Month.AdvanceDays(nights);
            Save();
            Switch(night);
        }

        /// <summary>夜のまとめを閉じたら、次の朝か月末のまとめへ。</summary>
        public void AfterNight()
        {
            Switch(Month.IsOver ? (ScreenBase)monthEnd : myRoom);
        }

        /// <summary>月末のまとめから、同じ難易度でもう一度。</summary>
        public void Restart()
        {
            SaveStore.Delete();
            NewGame();
        }

        /// <summary>中断してタイトルへ。セーブはその日の朝の状態が残っている。</summary>
        public void Quit()
        {
            ShowTitle();
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
            statusBar.Show(Month, Def.Label);
            errorText.text = Db.Errors;
            current?.Refresh();
        }

        void OnTextInput(char c)
        {
            if (practice != null) return;
            current?.OnText(c);
        }
    }
}
