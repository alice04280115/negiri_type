using System;

namespace Negiri.Core.Economy
{
    /// <summary>
    /// 財布とロック貯金。お金はかならずこのクラスを通して動かし、
    /// 「収入合計 ＝ 支出合計 ＋ 財布 ＋ ロック貯金」が常に成り立つようにする。
    /// 節約額は統計専用なので、ここには入れない。
    /// </summary>
    public sealed class Wallet
    {
        public int Cash { get; private set; }
        public int Locked { get; private set; }
        public int TotalIncome { get; private set; }
        public int TotalSpent { get; private set; }

        public Wallet(int initialCash = 0)
        {
            if (initialCash > 0) Receive(initialCash);
            else if (initialCash < 0) throw new ArgumentException("最初の金額は0以上にしてください");
        }

        /// <summary>お金の式が成り立っているか（テストと開発中のチェック用）。</summary>
        public bool IsBalanced => TotalIncome == TotalSpent + Cash + Locked;

        public bool CanPay(int amount) => amount >= 0 && amount <= Cash;

        /// <summary>収入（給料など）。</summary>
        public void Receive(int amount)
        {
            if (amount < 0) throw new ArgumentException("収入は0以上にしてください");
            TotalIncome += amount;
            Cash += amount;
        }

        /// <summary>支払い。財布が足りなければ例外にする（借金は段階3以降）。</summary>
        public void Pay(int amount)
        {
            if (amount < 0) throw new ArgumentException("支払いは0以上にしてください");
            if (amount > Cash) throw new InvalidOperationException($"財布が{amount - Cash}円足りません");
            Cash -= amount;
            TotalSpent += amount;
        }

        /// <summary>財布からロック貯金へ移す。</summary>
        public void Lock(int amount)
        {
            if (amount < 0 || amount > Cash) throw new ArgumentException("ロックできる金額を超えています");
            Cash -= amount;
            Locked += amount;
        }

        /// <summary>ロック貯金から財布へ戻す。</summary>
        public void Unlock(int amount)
        {
            if (amount < 0 || amount > Locked) throw new ArgumentException("ロック貯金が足りません");
            Locked -= amount;
            Cash += amount;
        }
    }
}
