using System;

namespace Negiri.Core.Life
{
    /// <summary>
    /// 必需品の在庫。食料と日用品を「何日分あるか」で持つ。
    /// 上限を超えて買った分は捨てる（ムダになる）。毎晩それぞれ減り、0の夜は「在庫切れ」の日として数える。
    /// </summary>
    public sealed class Stock
    {
        public int Food { get; private set; }
        public int Daily { get; private set; }
        /// <summary>食料の上限（難易度ごと）。0なら上限なし。</summary>
        public int FoodCap { get; }
        /// <summary>日用品の上限。0なら上限なし。</summary>
        public int DailyCap { get; }
        public int FoodOutDays { get; private set; }
        public int DailyOutDays { get; private set; }

        public Stock(int food, int daily, int foodCap, int dailyCap, int foodOutDays = 0, int dailyOutDays = 0)
        {
            if (food < 0 || daily < 0) throw new ArgumentException("在庫は0以上にしてください");
            if (foodCap < 0 || dailyCap < 0) throw new ArgumentException("上限は0以上にしてください");
            FoodCap = foodCap;
            DailyCap = dailyCap;
            Food = Clamp(food, foodCap);
            Daily = Clamp(daily, dailyCap);
            FoodOutDays = foodOutDays;
            DailyOutDays = dailyOutDays;
        }

        static int Clamp(int value, int cap) => cap > 0 ? Math.Min(value, cap) : value;

        /// <summary>今の在庫に amount 日分足したらどうなるか（after と、上限を超えてムダになる量）。</summary>
        public static (int after, int wasted) Preview(int current, int amount, int cap)
        {
            int raw = current + Math.Max(0, amount);
            if (cap <= 0 || raw <= cap) return (raw, 0);
            return (cap, raw - cap);
        }

        /// <summary>食料を足す。上限を超えた分は捨て、捨てた量を返す。</summary>
        public int AddFood(int amount)
        {
            var (after, wasted) = Preview(Food, amount, FoodCap);
            Food = after;
            return wasted;
        }

        public int AddDaily(int amount)
        {
            var (after, wasted) = Preview(Daily, amount, DailyCap);
            Daily = after;
            return wasted;
        }

        /// <summary>夜の消費。在庫が足りなければ「切れた」として日数を数え、true を返す。</summary>
        public (bool foodOut, bool dailyOut) ConsumeNight(int foodPerDay, int dailyPerDay)
        {
            bool foodOut = Food <= 0;
            bool dailyOut = Daily <= 0;
            Food = Math.Max(0, Food - foodPerDay);
            Daily = Math.Max(0, Daily - dailyPerDay);
            if (foodOut) FoodOutDays++;
            if (dailyOut) DailyOutDays++;
            return (foodOut, dailyOut);
        }
    }
}
