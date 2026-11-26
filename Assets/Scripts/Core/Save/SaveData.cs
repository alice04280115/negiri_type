using System;
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
        public const int CurrentVersion = 1;

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

        public int food;
        public int daily;
        public int foodOutDays;
        public int dailyOutDays;
        public int satisfaction;

        public LedgerEntry[] ledger = Array.Empty<LedgerEntry>();
    }
}
