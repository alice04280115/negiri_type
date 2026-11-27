using System;
using Negiri.Core.Economy;

namespace Negiri.Core.Life
{
    /// <summary>1か月の数値。すべて（仮）で、balance.json の life.month から上書きする。</summary>
    [Serializable]
    public class MonthBalance
    {
        /// <summary>1か月の日数。</summary>
        public int days = 30;
        /// <summary>1日目に財布に入る金額（給料日までの予算）。</summary>
        public int budget = 5000;
        /// <summary>月の途中でお金が入る日（給料日）。2日目以降。空なら月初の予算だけ。</summary>
        public PaydayData[] paydays = { new PaydayData() };
        /// <summary>1日目の曜日（0＝月曜 … 6＝日曜）。</summary>
        public int firstWeekday = 0;
        /// <summary>レシートに書く月（ゲーム内の月）。</summary>
        public int month = 4;
        /// <summary>貯金箱（ロック貯金）に入れる単位。</summary>
        public int lockStep = 1000;
        /// <summary>月初に貯金箱へ入れられる上限。0なら月初は入れられない。</summary>
        public int lockStartMax = 6000;
        /// <summary>2日目以降（家計簿から）1日に入れられる上限。0なら追加できない。</summary>
        public int lockDailyMax = 6000;
        /// <summary>貯金箱の合計の上限。0なら上限なし。</summary>
        public int lockTotalMax = 6000;
        /// <summary>月初の画面に出す「1か月の必需品の目安」。</summary>
        public int essentialsEstimate = 7000;
        /// <summary>月末に買いたい目標商品（段階4の決戦で買う。段階3では表示だけ）。</summary>
        public string targetName = "あこがれのバッグ";
        public int targetPrice = 12000;

        /// <summary>その日の朝に入るお金の合計（1日目の予算は含めない）。</summary>
        public int PaydayOn(int day)
        {
            int sum = 0;
            foreach (var p in paydays ?? Array.Empty<PaydayData>()) if (p != null && p.day == day) sum += p.amount;
            return sum;
        }

        /// <summary>今日より後の最初の給料日。なければ0。</summary>
        public int NextPaydayAfter(int day)
        {
            int next = 0;
            foreach (var p in paydays ?? Array.Empty<PaydayData>())
                if (p != null && p.day > day && p.day <= days && (next == 0 || p.day < next)) next = p.day;
            return next;
        }

        /// <summary>1か月に入るお金の合計（1日目の予算＋給料）。</summary>
        public int TotalBudget
        {
            get
            {
                int sum = budget;
                foreach (var p in paydays ?? Array.Empty<PaydayData>()) if (p != null && p.day <= days) sum += p.amount;
                return sum;
            }
        }
    }

    /// <summary>給料日1つ（その日の朝に財布へ入る）。</summary>
    [Serializable]
    public class PaydayData
    {
        public int day = 15;
        public int amount = 14000;
        /// <summary>家計簿と画面に出す名前。</summary>
        public string name = "給料";
    }

    /// <summary>必需品の在庫（何日分あるか）の数値。</summary>
    [Serializable]
    public class StockBalance
    {
        public int foodStart = 3;
        public int dailyStart = 7;
        /// <summary>毎晩減る量。</summary>
        public int foodPerDay = 1;
        public int dailyPerDay = 1;
        /// <summary>日用品の上限。0なら上限なし（食料の上限は難易度ごと）。</summary>
        public int dailyCap = 0;
        /// <summary>在庫が切れた夜に減る満足度。</summary>
        public int foodOutPenalty = 8;
        public int dailyOutPenalty = 4;
    }

    /// <summary>満足度の数値。</summary>
    [Serializable]
    public class SatisfactionBalance
    {
        public int start = 60;
        public int max = 100;
        /// <summary>月末に保ちたい値（段階4の採点で使う。段階3ではメーターに線を引くだけ）。</summary>
        public int passLine = 50;
        /// <summary>毎晩減る量。</summary>
        public int dailyDecay = 1;
    }

    [Serializable]
    public class LifeBalance
    {
        public MonthBalance month = new MonthBalance();
        public StockBalance stock = new StockBalance();
        public SatisfactionBalance satisfaction = new SatisfactionBalance();
        public DebtBalance debt = new DebtBalance();
    }
}
