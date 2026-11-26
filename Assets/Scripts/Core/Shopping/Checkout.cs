using System;
using System.Collections.Generic;
using Negiri.Core.Economy;
using Negiri.Core.Typing;

namespace Negiri.Core.Shopping
{
    /// <summary>レジで1品打った結果と、返品したかどうか。</summary>
    public sealed class CheckoutLine
    {
        public CartLine Cart { get; }
        public bool Scanned { get; private set; }
        public Rank Rank { get; private set; } = Rank.Miss;
        public int TypingBp { get; private set; }
        public bool Returned { get; internal set; }

        internal CheckoutLine(CartLine cart)
        {
            Cart = cart;
        }

        internal void Record(Rank rank, int typingBp)
        {
            if (typingBp < 0) throw new ArgumentException("割引は0以上にしてください");
            Rank = rank;
            TypingBp = typingBp;
            Scanned = true;
        }

        public int BasePrice => Cart.Item.basePrice;
        /// <summary>タイピング割引と特売を合わせて、実際にかかった割引（上限込み）。</summary>
        public int DiscountBp => Pricing.CombinedDiscountBp(TypingBp, Cart.SaleBp, Cart.Item.maxDiscountBp);
        /// <summary>この商品の支払額（返品しても値は残る。合計には入らない）。</summary>
        public int Price => Cart.PriceWithTyping(TypingBp);
        /// <summary>この商品で浮いた金額。返品した商品は数えない。</summary>
        public int Saved => Returned ? 0 : Pricing.Saved(BasePrice, Price);
    }

    /// <summary>会計の結果（レシート）。</summary>
    public sealed class Receipt
    {
        public IReadOnlyList<CheckoutLine> Lines { get; }
        public int CashBefore { get; }
        public int CashAfter { get; }
        public int Paid { get; }
        public int BaseTotal { get; }
        public int Saved { get; }
        public int ReturnCount { get; }
        public int SatisfactionLoss { get; }

        internal Receipt(IReadOnlyList<CheckoutLine> lines, int cashBefore, int cashAfter, int paid, int baseTotal, int saved, int returnCount, int satisfactionLoss)
        {
            Lines = lines;
            CashBefore = cashBefore;
            CashAfter = cashAfter;
            Paid = paid;
            BaseTotal = baseTotal;
            Saved = saved;
            ReturnCount = returnCount;
            SatisfactionLoss = satisfactionLoss;
        }
    }

    /// <summary>
    /// レジから会計まで。全品を打ち終えてから、財布が足りるかを判定する。
    /// 足りなければ、難易度の決まりに従って返品する（自分で選ぶ／高い順／安い順）。
    /// </summary>
    public sealed class Checkout
    {
        readonly List<CheckoutLine> lines = new List<CheckoutLine>();

        public ShoppingRule Rule { get; }
        public IReadOnlyList<CheckoutLine> Lines => lines;
        public bool IsPaid { get; private set; }

        public Checkout(IEnumerable<CartLine> cart, ShoppingRule rule)
        {
            Rule = rule ?? throw new ArgumentNullException(nameof(rule));
            foreach (var c in cart) lines.Add(new CheckoutLine(c));
        }

        /// <summary>まだ打っていない最初の商品の番号。全部打ったら -1。</summary>
        public int NextToScan
        {
            get
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    if (!lines[i].Scanned) return i;
                }
                return -1;
            }
        }

        public bool AllScanned => NextToScan < 0;

        public int ScannedCount
        {
            get
            {
                int n = 0;
                foreach (var l in lines) if (l.Scanned) n++;
                return n;
            }
        }

        public void Record(int index, Rank rank, int typingBp)
        {
            if (IsPaid) throw new InvalidOperationException("会計は終わっています");
            lines[index].Record(rank, typingBp);
        }

        public void Record(int index, TypingResult result) => Record(index, result.Rank, result.DiscountBp);

        /// <summary>打ち終えた商品の支払額の合計（返品した商品を除く）。</summary>
        public int Total
        {
            get
            {
                int sum = 0;
                foreach (var l in lines) if (l.Scanned && !l.Returned) sum += l.Price;
                return sum;
            }
        }

        /// <summary>まだ打っていない商品の支払額の幅（全部PERFECT～全部MISS）。レジ画面の見込み表示用。</summary>
        public (int best, int worst) RemainingRange(int perfectBp)
        {
            int best = 0, worst = 0;
            foreach (var l in lines)
            {
                if (l.Scanned) continue;
                best += l.Cart.PriceWithTyping(perfectBp);
                worst += l.Cart.PriceWithTyping(0);
            }
            return (best, worst);
        }

        public int Shortage(int cash) => Math.Max(0, Total - cash);

        public int ReturnCount
        {
            get
            {
                int n = 0;
                foreach (var l in lines) if (l.Returned) n++;
                return n;
            }
        }

        public int SatisfactionLoss => ReturnCount * Rule.satisfactionPerReturn;

        /// <summary>自分で返品を選ぶ（初級・中級）。もう一度呼ぶと返品を取り消す。</summary>
        public void ToggleReturn(int index)
        {
            if (IsPaid) throw new InvalidOperationException("会計は終わっています");
            if (!AllScanned) throw new InvalidOperationException("全品を打ち終えてから返品してください");
            if (Rule.IsAutoReturn) throw new InvalidOperationException("この難易度では返品は自動で決まります");
            lines[index].Returned = !lines[index].Returned;
        }

        /// <summary>
        /// 自動返品で返品される商品の番号を、返品する順に返す（まだ返品はしない）。
        /// 上級は支払額の高い順、達人は安い順。同じ金額なら後からカートに入れた商品から。
        /// 合計が財布以内になったところで止める。
        /// </summary>
        public List<int> PlanAutoReturn(int cash)
        {
            var order = new List<int>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Scanned && !lines[i].Returned) order.Add(i);
            }
            bool highestFirst = Rule.ShortageValue == ShortageRule.AutoReturnHighestFirst;
            order.Sort((a, b) =>
            {
                int byPrice = highestFirst
                    ? lines[b].Price.CompareTo(lines[a].Price)
                    : lines[a].Price.CompareTo(lines[b].Price);
                if (byPrice != 0) return byPrice;
                return lines[b].Cart.Order.CompareTo(lines[a].Cart.Order);
            });

            var plan = new List<int>();
            int total = Total;
            foreach (var i in order)
            {
                if (total <= cash) break;
                plan.Add(i);
                total -= lines[i].Price;
            }
            return plan;
        }

        /// <summary>自動返品を行う。返品した商品の番号を返す。</summary>
        public List<int> ApplyAutoReturn(int cash)
        {
            if (IsPaid) throw new InvalidOperationException("会計は終わっています");
            if (!AllScanned) throw new InvalidOperationException("全品を打ち終えてから返品してください");
            if (!Rule.IsAutoReturn) throw new InvalidOperationException("この難易度では返品は自分で選びます");
            var plan = PlanAutoReturn(cash);
            foreach (var i in plan) lines[i].Returned = true;
            return plan;
        }

        /// <summary>支払う。全品を打ち終え、合計が財布以内でなければ払えない。</summary>
        public Receipt Pay(Wallet wallet)
        {
            if (wallet == null) throw new ArgumentNullException(nameof(wallet));
            if (IsPaid) throw new InvalidOperationException("会計は終わっています");
            if (!AllScanned) throw new InvalidOperationException("まだ打っていない商品があります");
            int total = Total;
            if (!wallet.CanPay(total)) throw new InvalidOperationException($"財布が{total - wallet.Cash}円足りません。返品してください");

            int before = wallet.Cash;
            wallet.Pay(total);
            IsPaid = true;

            int baseTotal = 0, saved = 0;
            foreach (var l in lines)
            {
                if (l.Returned) continue;
                baseTotal += l.BasePrice;
                saved += l.Saved;
            }
            return new Receipt(lines, before, wallet.Cash, total, baseTotal, saved, ReturnCount, SatisfactionLoss);
        }
    }
}
