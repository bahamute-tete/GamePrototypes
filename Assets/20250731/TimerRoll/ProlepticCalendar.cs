// LiangZhu - 时间回溯日历 / 历法换算
// 先公历(proleptic Gregorian) <-> 儒略日序数(JDN) 互转。
// 用天文纪年法(存在第 0 年 = 公元前 1 年),因此可表示公元前日期,
// 摆脱 System.DateTime 只能表示公元 1~9999 年的限制。
//
// 有效范围:年份 >~ -4800(JDN >= 0)。本项目用到的 2026 ~ 公元前 3500 全部落在此范围内,
// 所有中间量为非负,C# 的截断除法此时等价于向下取整,公式成立。

namespace LiangZhu.TimeRoll
{
    public static class ProlepticCalendar
    {
        /// <summary>公历(天文纪年) -> 儒略日序数。month:1~12, day:1~31。</summary>
        public static long ToJdn(int year, int month, int day)
        {
            long a = (14 - month) / 12;          // 1~2 月归到上一年处理
            long y = year + 4800 - a;            // 在有效范围内恒为正
            long m = month + 12 * a - 3;
            return day
                 + (153 * m + 2) / 5
                 + 365 * y
                 + y / 4 - y / 100 + y / 400
                 - 32045;
        }

        /// <summary>儒略日序数 -> 公历(天文纪年)。year 可为 0 或负(= 公元前)。</summary>
        public static void FromJdn(long jdn, out int year, out int month, out int day)
        {
            long a = jdn + 32044;
            long b = (4 * a + 3) / 146097;
            long c = a - (146097 * b) / 4;
            long d = (4 * c + 3) / 1461;
            long e = c - (1461 * d) / 4;
            long m = (5 * e + 2) / 153;

            day   = (int)(e - (153 * m + 2) / 5 + 1);
            month = (int)(m + 3 - 12 * (m / 10));
            year  = (int)(100 * b + d - 4800 + m / 10);
        }

        /// <summary>"年 + 是否公元前" -> 天文纪年标量。公元前 N 年 = 1 - N。</summary>
        public static int ToAstronomicalYear(int year, bool isBCE)
        {
            return isBCE ? (1 - year) : year;
        }
    }
}
