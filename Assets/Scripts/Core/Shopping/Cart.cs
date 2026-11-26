using System;
using System.Collections.Generic;
using Negiri.Core.Data;
using Negiri.Core.Economy;

namespace Negiri.Core.Shopping
{
    /// <summary>カートの1行（同じ商品を2つ入れたら2行になる）。</summary>
    public sealed class CartLine
    {
        public ItemData Item { get; }
        /// <summary>カートに入れたときの特売割引。</summary>
        public int SaleBp { get; }
        /// <summary>カートに入れた順番（0から）。自動返品で同じ金額のときに使う。</summary>
        public int Order { get; }

        public CartLine(ItemData item, int saleBp, int order)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            if (saleBp < 0) throw new ArgumentException("特売割引は0以上にしてください");
            SaleBp = saleBp;
            Order = order;
        }

        public int PriceWithTyping(int typingBp) => Pricing.Price(Item.basePrice, typingBp, SaleBp, Item.maxDiscountBp);
    }

    /// <summary>
    /// 店内のカート。合計は財布で制限しない（足りなければ会計で返品する）。品数だけ難易度で制限する。
    /// </summary>
    public sealed class Cart
    {
        readonly List<CartLine> lines = new List<CartLine>();
        int nextOrder;

        public int MaxItems { get; }
        public IReadOnlyList<CartLine> Lines => lines;
        public int Count => lines.Count;
        public bool IsFull => lines.Count >= MaxItems;

        public Cart(int maxItems)
        {
            if (maxItems <= 0) throw new ArgumentException("品数の上限は1以上にしてください");
            MaxItems = maxItems;
        }

        /// <summary>カートに入れる。品数の上限なら入れずに false。</summary>
        public bool Add(ItemData item, int saleBp)
        {
            if (IsFull) return false;
            lines.Add(new CartLine(item, saleBp, nextOrder++));
            return true;
        }

        public void RemoveAt(int index)
        {
            lines.RemoveAt(index);
        }

        /// <summary>通常価格の合計。</summary>
        public int BaseTotal
        {
            get
            {
                int sum = 0;
                foreach (var l in lines) sum += l.Item.basePrice;
                return sum;
            }
        }

        /// <summary>全品同じ割引で打てたときの支払額の合計（特売は込み）。</summary>
        public int TotalWithTyping(int typingBp)
        {
            int sum = 0;
            foreach (var l in lines) sum += l.PriceWithTyping(typingBp);
            return sum;
        }

        /// <summary>全部 MISS（タイピング割引なし）のときの支払額。いちばん高くなる。</summary>
        public int WorstTotal => TotalWithTyping(0);

        /// <summary>全部 PERFECT のときの支払額。いちばん安くなる。</summary>
        public int BestTotal(int perfectBp) => TotalWithTyping(perfectBp);

        /// <summary>全部 MISS で財布を超える金額。超えなければ 0。</summary>
        public int WorstShortage(int cash) => Math.Max(0, WorstTotal - cash);
    }
}
