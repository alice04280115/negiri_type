using System;
using Negiri.Core.Economy;
using Negiri.Core.Life;

namespace Negiri.Core.Save
{
    /// <summary>
    /// セーブの中身。JSON にしてブラウザ（PlayerPrefs）に1つだけ保存する。
    /// 項目を増やすときは version を上げ、古いセーブも読めるようにする。
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>1：段階3-1。2：借金（借入合計・返済合計・借入先ごとの借金・借金の記録）を足した。</summary>
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        /// <summary>0：初級 … 3：達人。</summary>
        public int difficulty;
        public int day = 1;
        public bool isOver;
        public int lastShopDay;

        public int totalIncome;
        public int totalSpent;
        public int cash;
        public int locked;
        /// <summary>今日、家計簿から貯金箱に入れた額（1日の上限の判定用）。古いセーブにはないので0。</summary>
        public int lockedToday;
        /// <summary>借りて財布に入れた合計と、財布から返した合計（version 2 から。古いセーブにはないので0）。</summary>
        public int totalBorrowed;
        public int totalRepaid;

        public int food;
        public int daily;
        public int foodOutDays;
        public int dailyOutDays;
        public int satisfaction;

        public LedgerEntry[] ledger = Array.Empty<LedgerEntry>();
        /// <summary>借入先ごとの借金（version 2 から）。</summary>
        public DebtAccount[] debts = Array.Empty<DebtAccount>();
        /// <summary>借りた・返した・利息の記録（version 2 から）。</summary>
        public DebtEvent[] debtLog = Array.Empty<DebtEvent>();
    }
}
