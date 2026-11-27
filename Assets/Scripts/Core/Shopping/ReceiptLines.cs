using System.Collections.Generic;
using Negiri.Core.Typing;

namespace Negiri.Core.Shopping
{
    public enum ReceiptRowKind
    {
        /// <summary>品名と価格（特売なら特売後の価格）。</summary>
        Item,
        /// <summary>品名の下の、タイピング評価と値引き額。</summary>
        Discount,
        /// <summary>品名の下の、値引きのない評価（MISS）。</summary>
        Note,
        Separator,
        /// <summary>小計・値引の合計など。</summary>
        Subtotal,
        /// <summary>合計。</summary>
        Total,
        /// <summary>返品などの見出し。</summary>
        Section,
        /// <summary>返品した商品。</summary>
        Returned,
        /// <summary>借りて払った分（例：カードローン ¥480）。</summary>
        Loan,
    }

    /// <summary>レシートの1行。左（品名など）、印（特）、右（金額）。</summary>
    public readonly struct ReceiptRow
    {
        public readonly ReceiptRowKind Kind;
        public readonly string Left;
        public readonly string Mark;
        public readonly string Right;
        /// <summary>Discount / Note の行の評価。</summary>
        public readonly Rank Rank;

        public ReceiptRow(ReceiptRowKind kind, string left, string mark, string right, Rank rank = Rank.Miss)
        {
            Kind = kind;
            Left = left;
            Mark = mark;
            Right = right;
            Rank = rank;
        }
    }

    /// <summary>
    /// 本物のレシートのような行を作る。
    /// 特売の商品は「特」と特売後の価格を書き、その下にタイピングの値引きを別の行で書く。
    /// 1品ごとに「特売後の価格 － タイピング値引 ＝ 支払額」となり、合計は支払額の合計と一致する。
    /// 借りて払ったときは、支払いを「現金」と借入先（カードローン）の2行に分ける。
    /// </summary>
    public static class ReceiptLines
    {
        public static string Yen(int amount) => amount < 0 ? $"-¥{-amount:N0}" : $"¥{amount:N0}";

        public static string RankName(Rank rank) => rank.ToString().ToUpperInvariant();

        /// <summary>特売を反映した、タイピング割引なしの価格（レシートの品名の行に書く値段）。</summary>
        public static int ShelfPrice(CheckoutLine line) => line.Cart.PriceWithTyping(0);

        /// <summary>タイピング評価による値引き額（上限で止まった分は含まない）。</summary>
        public static int TypingDiscount(CheckoutLine line) => ShelfPrice(line) - line.Price;

        public static List<ReceiptRow> Build(Receipt receipt)
        {
            var rows = new List<ReceiptRow>();
            int count = 0, subtotal = 0, typingTotal = 0;

            foreach (var l in receipt.Lines)
            {
                if (l.Returned) continue;
                int shelf = ShelfPrice(l);
                int discount = TypingDiscount(l);
                count++;
                subtotal += shelf;
                typingTotal += discount;

                rows.Add(new ReceiptRow(ReceiptRowKind.Item, l.Cart.Item.name, l.Cart.SaleBp > 0 ? "特" : "", Yen(shelf)));
                if (discount > 0) rows.Add(new ReceiptRow(ReceiptRowKind.Discount, $"　{RankName(l.Rank)}値引", "", Yen(-discount), l.Rank));
                else rows.Add(new ReceiptRow(ReceiptRowKind.Note, $"　{RankName(l.Rank)}", "", "", l.Rank));
            }

            rows.Add(new ReceiptRow(ReceiptRowKind.Separator, "", "", ""));
            rows.Add(new ReceiptRow(ReceiptRowKind.Subtotal, "小計", $"{count}点", Yen(subtotal)));
            rows.Add(new ReceiptRow(ReceiptRowKind.Subtotal, "タイピング値引", "", Yen(-typingTotal)));
            rows.Add(new ReceiptRow(ReceiptRowKind.Total, "合計", "", Yen(subtotal - typingTotal)));
            if (receipt.Borrowed > 0)
            {
                // 借りたときは支払いを「現金」と借入先の2行に分ける
                rows.Add(new ReceiptRow(ReceiptRowKind.Subtotal, "現金", "", Yen(receipt.Paid - receipt.Borrowed)));
                rows.Add(new ReceiptRow(ReceiptRowKind.Loan, receipt.LenderName, "", Yen(receipt.Borrowed)));
            }
            else
            {
                rows.Add(new ReceiptRow(ReceiptRowKind.Subtotal, "お預り（財布）", "", Yen(receipt.CashBefore)));
            }
            rows.Add(new ReceiptRow(ReceiptRowKind.Subtotal, "財布の残り", "", Yen(receipt.CashAfter)));

            if (receipt.ReturnCount > 0)
            {
                rows.Add(new ReceiptRow(ReceiptRowKind.Separator, "", "", ""));
                rows.Add(new ReceiptRow(ReceiptRowKind.Section, "＜返品＞", $"{receipt.ReturnCount}点", ""));
                foreach (var l in receipt.Lines)
                {
                    if (!l.Returned) continue;
                    rows.Add(new ReceiptRow(ReceiptRowKind.Returned, l.Cart.Item.name, l.Cart.SaleBp > 0 ? "特" : "", Yen(ShelfPrice(l))));
                }
            }
            return rows;
        }
    }
}
