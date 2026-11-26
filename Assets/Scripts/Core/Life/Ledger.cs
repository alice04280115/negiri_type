using System;
using System.Collections.Generic;

namespace Negiri.Core.Life
{
    /// <summary>家計簿の1行（買った商品1つ分）。セーブにもこの形で入る。</summary>
    [Serializable]
    public class LedgerEntry
    {
        public int day;
        public string itemId;
        public string name;
        public int basePrice;
        public int paid;
        /// <summary>タイピングの評価（PERFECT など）。</summary>
        public string rank;
        /// <summary>タイピング割引（ベーシスポイント）。平均割引の計算に使う。</summary>
        public int typingBp;
    }

    /// <summary>家計簿。買った商品を日付順に持つ。</summary>
    public sealed class Ledger
    {
        readonly List<LedgerEntry> entries = new List<LedgerEntry>();

        public IReadOnlyList<LedgerEntry> Entries => entries;

        public void Add(LedgerEntry e) => entries.Add(e);

        public int TotalPaid
        {
            get
            {
                int sum = 0;
                foreach (var e in entries) sum += e.paid;
                return sum;
            }
        }

        /// <summary>今月の節約額（通常価格 － 支払額 の合計）。統計専用。</summary>
        public int TotalSaved
        {
            get
            {
                int sum = 0;
                foreach (var e in entries) sum += e.basePrice - e.paid;
                return sum;
            }
        }

        public int PaidOn(int day)
        {
            int sum = 0;
            foreach (var e in entries) if (e.day == day) sum += e.paid;
            return sum;
        }

        /// <summary>タイピング割引の平均（ベーシスポイント）。まだ1品も買っていなければ 0。</summary>
        public int AverageTypingBp
        {
            get
            {
                if (entries.Count == 0) return 0;
                long sum = 0;
                foreach (var e in entries) sum += e.typingBp;
                return (int)(sum / entries.Count);
            }
        }

        public int PerfectCount
        {
            get
            {
                int n = 0;
                foreach (var e in entries) if (e.rank == "PERFECT") n++;
                return n;
            }
        }
    }
}
