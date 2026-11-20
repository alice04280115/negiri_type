using System;

namespace Negiri.Core.Economy
{
    /// <summary>
    /// 支払額の計算。割引はベーシスポイント（1% = 100）で扱う。
    /// 支払額 = 通常価格 × (1 − min(タイピング割引 ＋ セール割引, 割引上限))、円未満切り捨て。
    /// </summary>
    public static class Pricing
    {
        public const int FullBp = 10000;

        /// <summary>割引を足し算で合算し、上限で止める。</summary>
        public static int CombinedDiscountBp(int typingBp, int saleBp, int capBp)
        {
            if (typingBp < 0 || saleBp < 0 || capBp < 0) throw new ArgumentException("割引は0以上にしてください");
            return Math.Min(typingBp + saleBp, Math.Min(capBp, FullBp));
        }

        public static int Price(int basePrice, int typingBp, int saleBp, int capBp)
        {
            if (basePrice < 0) throw new ArgumentException("価格は0以上にしてください");
            int bp = CombinedDiscountBp(typingBp, saleBp, capBp);
            // 整数で計算して切り捨てる（例：238 × 9500 / 10000 = 226）
            return (int)((long)basePrice * (FullBp - bp) / FullBp);
        }

        /// <summary>節約額（統計表示用。残高には加えない）。</summary>
        public static int Saved(int basePrice, int paid)
        {
            return basePrice - paid;
        }
    }
}
