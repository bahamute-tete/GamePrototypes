// LiangZhu - 时间回溯日历 / 纯计算核心
// 全部输出是 (p, pDot) 的无状态纯函数 —— 不读任何帧历史,Timeline 擦洗安全。
//   p    : 已被节奏曲线重映射后的进度,∈[0,1]
//   pDot : dp/dt(每秒),由驱动端按曲线斜率/时长解析给出(同样只依赖 p,不依赖真实帧间隔)
//
// 8 个数字轮,显示顺序: Y3 Y2 Y1 Y0 . M1 M0 . D1 D0  (索引 0..7)
//   每个轮输出: scroll(滚动位置 s,单位"格",整数选数字、小数滚动)
//               speed (v = ds/dt,格/秒,喂运动模糊与速度淡出)
//               alphaCeil(该轮透明度上限)

using System;

namespace LiangZhu.TimeRoll
{
    public enum TimeRollMode
    {
        Date, // 第一段:公历 年/月/日,按天插值
        Year, // 第二段:深时,只有"年 + 纪元"有意义,日月降级为装饰快转
    }

    public struct WheelOut
    {
        public float scroll;
        public float speed;
        public float alphaCeil;
    }

    [Serializable]
    public struct TimeRollConfig
    {
        public TimeRollMode mode;

        // 主标量端点:
        //   Date 模式 = 起止日期的 JDN(天)
        //   Year 模式 = 起止年份的天文纪年标量
        public double startBackbone;
        public double endBackbone;

        // 装饰日月(仅 Year 模式)
        public float decorativeRate;     // 基础转速 K
        public float dayMonthAlphaCeil;  // 日月透明度硬上限(如 0.15),保证停顿时不暴露错值
    }

    // 显示参数(模糊/透明),Driver 持全局默认,Clip 可逐段 override
    [Serializable]
    public struct TimeRollDisplayParams
    {
        public float shutter;     // 模糊长度系数(秒)
        public float maxBlur;     // 最大模糊(格)
        public float speedLo;     // 开始变透的速度
        public float speedHi;     // 最透的速度
        public float floorAlpha;  // 最透到

        public static TimeRollDisplayParams Default => new TimeRollDisplayParams
        {
            shutter = 0.01f, maxBlur = 2.5f, speedLo = 5f, speedHi = 60f, floorAlpha = 0f
        };
    }

    public static class TimeRollCore
    {
        // 复用缓冲,避免每帧分配(主线程调用)
        static readonly int[] _digA = new int[8];
        static readonly int[] _digB = new int[8];

        // 装饰轮转速比(对 M1, M0, D1, D0),取互质感的比例让它们不锁相
        static readonly float[] _decorRatio = { 1f / 13f, 1f / 7f, 1f / 3f, 1f };

        /// <summary>主入口。outW 长度需为 8。</summary>
        public static void Evaluate(in TimeRollConfig cfg, double p, double pDot,
                                    WheelOut[] outW, out bool isBCE)
        {
            if (cfg.mode == TimeRollMode.Date)
                EvaluateDate(cfg, p, pDot, outW, out isBCE);
            else
                EvaluateYear(cfg, p, pDot, outW, out isBCE);
        }

        // ---------- 第一段:日期模式 ----------
        static void EvaluateDate(in TimeRollConfig cfg, double p, double pDot,
                                 WheelOut[] outW, out bool isBCE)
        {
            double range = cfg.endBackbone - cfg.startBackbone;      // 倒带时为负(天数)
            double C = cfg.startBackbone + range * p;                // 当前连续天数
            double dCdt = range * pDot;                              // 天/秒

            long n = (long)Math.Floor(C);
            float f = (float)(C - n);

            // 相邻两个真实日期:A(较早) -> B(较晚),C 在两者间 f 处
            ProlepticCalendar.FromJdn(n,     out int yA, out int mA, out int dA);
            ProlepticCalendar.FromJdn(n + 1, out int yB, out int mB, out int dB);
            DigitsDate(yA, mA, dA, _digA);
            DigitsDate(yB, mB, dB, _digB);

            for (int w = 0; w < 8; w++)
            {
                int delta = ShortStep(_digA[w], _digB[w]); // 相邻日期每位变 0 或 ±1(月末重置时日十位可达 ±N,走最短)
                outW[w].scroll    = _digA[w] + f * delta;  // f=0 显示 A,f->1 显示 B
                outW[w].speed     = (float)(delta * dCdt); // 不变的位 delta=0 -> 速度0(高位只在进位瞬间动)
                outW[w].alphaCeil = 1f;
            }
            isBCE = false;
        }

        // ---------- 第二段:年份模式 ----------
        static void EvaluateYear(in TimeRollConfig cfg, double p, double pDot,
                                 WheelOut[] outW, out bool isBCE)
        {
            double range = cfg.endBackbone - cfg.startBackbone;      // 天文纪年差
            double a = cfg.startBackbone + range * p;                // 当前天文年(可负)

            // 显示幅值 M = max(a, 1-a):V 形,a=1 与 a=0 处都等于 1,谷底 0.5
            double M = Math.Max(a, 1.0 - a);
            double dMda = (a >= 0.5) ? 1.0 : -1.0;                   // 谷底处换向
            double dMdt = dMda * range * pDot;                      // 年/秒(已含 V 形换向)

            long m = (long)Math.Floor(M);
            float g = (float)(M - m);

            // 年份轮 Y3..Y0:在 M 上做标准十进制里程表,4 位零填充(0476)
            DigitsYear4(m,     _digA);
            DigitsYear4(m + 1, _digB);
            for (int w = 0; w < 4; w++)
            {
                int delta = ShortStep(_digA[w], _digB[w]);
                outW[w].scroll    = _digA[w] + g * delta;
                outW[w].speed     = (float)(delta * dMdt);
                outW[w].alphaCeil = 1f;
            }

            // 装饰日月轮 M1 M0 D1 D0(索引 4..7):独立快转 + 低透明上限
            for (int i = 0; i < 4; i++)
            {
                int w = 4 + i;
                double Kw = cfg.decorativeRate * _decorRatio[i];
                outW[w].scroll    = (float)Mod(Kw * M, 10.0); // 取模保持浮点精度
                outW[w].speed     = (float)(Kw * dMdt);
                outW[w].alphaCeil = cfg.dayMonthAlphaCeil;
            }

            // 纪元在 M 谷底(a=0.5)翻转,此刻年读数恰为 0001
            isBCE = a < 0.5;
        }

        // ---------- 工具 ----------

        // 0-9 环上 a->b 的最短带符号步:9->0 给 +1,3->0 给 -3,0->9 给 -1
        static int ShortStep(int a, int b)
        {
            int d = b - a;
            return ((d + 5) % 10 + 10) % 10 - 5; // -> [-5, 4]
        }

        static void DigitsDate(int y, int m, int d, int[] dig)
        {
            int yy = y < 0 ? 0 : y; // 第一段年份恒为正,负值兜底
            dig[0] = (yy / 1000) % 10;
            dig[1] = (yy / 100) % 10;
            dig[2] = (yy / 10) % 10;
            dig[3] = yy % 10;
            dig[4] = (m / 10) % 10;
            dig[5] = m % 10;
            dig[6] = (d / 10) % 10;
            dig[7] = d % 10;
        }

        static void DigitsYear4(long yr, int[] dig)
        {
            long y = yr < 0 ? 0 : yr;
            dig[0] = (int)((y / 1000) % 10);
            dig[1] = (int)((y / 100) % 10);
            dig[2] = (int)((y / 10) % 10);
            dig[3] = (int)(y % 10);
        }

        static double Mod(double a, double m)
        {
            double r = a % m;
            return r < 0 ? r + m : r;
        }
    }
}
