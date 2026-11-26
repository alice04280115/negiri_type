using System;

namespace Negiri.Core.Life
{
    /// <summary>1〜30日の日付と曜日。</summary>
    public static class Calendar
    {
        public static readonly string[] WeekdayNames = { "月", "火", "水", "木", "金", "土", "日" };

        /// <summary>day 日目の曜日（0＝月曜 … 6＝日曜）。</summary>
        public static int Weekday(int day, int firstWeekday)
        {
            if (day < 1) throw new ArgumentException("日付は1日目からです");
            return ((firstWeekday + day - 1) % 7 + 7) % 7;
        }

        public static string WeekdayName(int day, int firstWeekday) => WeekdayNames[Weekday(day, firstWeekday)];

        /// <summary>「4月5日(金)」の形。</summary>
        public static string DateLabel(int day, MonthBalance month) => $"{month.month}月{day}日({WeekdayName(day, month.firstWeekday)})";

        /// <summary>次に買い物に行ける日。まだ一度も行っていなければ今日。</summary>
        public static int NextShopDay(int today, int lastShopDay, int intervalDays)
        {
            if (lastShopDay <= 0) return today;
            return Math.Max(today, lastShopDay + Math.Max(1, intervalDays));
        }

        public static bool CanShop(int today, int lastShopDay, int intervalDays) => NextShopDay(today, lastShopDay, intervalDays) == today;
    }
}
