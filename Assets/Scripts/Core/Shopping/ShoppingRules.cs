using System;

namespace Negiri.Core.Shopping
{
    /// <summary>会計で財布が足りないときの扱い。</summary>
    public enum ShortageRule
    {
        /// <summary>返品する商品を自分で選ぶ（初級・中級）。</summary>
        ChooseReturn,
        /// <summary>支払額の高い順に自動で返品する（上級。借金ができるまでの代用）。</summary>
        AutoReturnHighestFirst,
        /// <summary>支払額の安い順に自動で返品する（達人。借金ができるまでの代用）。</summary>
        AutoReturnLowestFirst,
    }

    /// <summary>難易度ごとの買い物の決まり。balance.json の shopping.difficulties に1つずつ並べる。</summary>
    [Serializable]
    public class ShoppingRule
    {
        public string label;
        /// <summary>1回の買い物でカートに入れられる品数。</summary>
        public int maxItems;
        /// <summary>ShortageRule の名前（ChooseReturn / AutoReturnHighestFirst / AutoReturnLowestFirst）。</summary>
        public string shortage = nameof(ShortageRule.ChooseReturn);
        /// <summary>1品返品するごとに減る満足度。</summary>
        public int satisfactionPerReturn;
        /// <summary>買い物に行ける間隔（日）。1なら毎日、3なら3日に1回。</summary>
        public int shopIntervalDays = 1;
        /// <summary>食料の在庫の上限（日分）。</summary>
        public int foodCap = 7;

        public ShortageRule ShortageValue => (ShortageRule)Enum.Parse(typeof(ShortageRule), shortage, true);
        public bool IsAutoReturn => ShortageValue != ShortageRule.ChooseReturn;
    }

    /// <summary>買い物の数値。すべて（仮）で、balance.json から上書きする。</summary>
    [Serializable]
    public class ShoppingBalance
    {
        /// <summary>段階2の「1回の買い物」の試作で財布に入れていた金額。段階3からは月の予算（life.month.budget）を使うので、今は使っていない。</summary>
        public int startingCash = 3000;

        /// <summary>レジで1品打ち終えたあと、結果を出しておく秒数。過ぎたら自動で次の商品へ進む。</summary>
        public float resultSeconds = 5f;

        /// <summary>初級・中級・上級・達人の順。</summary>
        public ShoppingRule[] difficulties = DefaultRules();

        public static ShoppingRule[] DefaultRules()
        {
            return new[]
            {
                new ShoppingRule { label = "初級", maxItems = 4, shortage = nameof(ShortageRule.ChooseReturn), satisfactionPerReturn = 0, shopIntervalDays = 1, foodCap = 7 },
                new ShoppingRule { label = "中級", maxItems = 7, shortage = nameof(ShortageRule.ChooseReturn), satisfactionPerReturn = 3, shopIntervalDays = 3, foodCap = 7 },
                new ShoppingRule { label = "上級", maxItems = 12, shortage = nameof(ShortageRule.AutoReturnHighestFirst), satisfactionPerReturn = 3, shopIntervalDays = 7, foodCap = 10 },
                new ShoppingRule { label = "達人", maxItems = 12, shortage = nameof(ShortageRule.AutoReturnLowestFirst), satisfactionPerReturn = 3, shopIntervalDays = 7, foodCap = 10 },
            };
        }
    }
}
