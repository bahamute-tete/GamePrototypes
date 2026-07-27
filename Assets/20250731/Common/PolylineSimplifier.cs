using System.Collections.Generic;
using UnityEngine;

namespace LiangZhu.Geometry
{
    /// <summary>
    /// Ramer–Douglas–Peucker 折线抽稀(迭代版,显式栈,避免长路径深递归爆栈)。
    /// 性质:简化后折线处处不偏离原折线超过 epsilon(误差有界),首尾点必保留。
    /// 用途:最短路沿 mesh 边走会有边尺度锯齿,抽稀后再喂 Catmull-Rom 才能真正平滑宏观形状。
    /// </summary>
    public static class PolylineSimplifier
    {
        struct Seg { public int s, e; public Seg(int s, int e) { this.s = s; this.e = e; } }

        /// <summary>
        /// 返回抽稀后的点(新数组)。epsilon &lt;= 0 或点数 &lt; 3 时原样返回拷贝。
        /// epsilon 单位与输入点一致(本工程里是 mesh local)。
        /// </summary>
        public static Vector3[] Decimate(IReadOnlyList<Vector3> pts, float epsilon)
        {
            int n = pts != null ? pts.Count : 0;
            if (n < 3 || epsilon <= 0f)
            {
                var copy = new Vector3[n];
                for (int i = 0; i < n; i++) copy[i] = pts[i];
                return copy;
            }

            float epsSq = epsilon * epsilon;
            var keep = new bool[n];
            keep[0] = true;
            keep[n - 1] = true;

            var stack = new Stack<Seg>();
            stack.Push(new Seg(0, n - 1));

            while (stack.Count > 0)
            {
                var seg = stack.Pop();
                int s = seg.s, e = seg.e;
                if (e - s < 2) continue; // 中间没有可判定的点

                Vector3 a = pts[s], b = pts[e];
                float maxSq = -1f;
                int maxK = -1;

                for (int i = s + 1; i < e; i++)
                {
                    float dSq = PerpDistSq(pts[i], a, b);
                    if (dSq > maxSq) { maxSq = dSq; maxK = i; }
                }

                if (maxK >= 0 && maxSq > epsSq)
                {
                    keep[maxK] = true;                // 真转折,保留并对两段递归
                    stack.Push(new Seg(s, maxK));
                    stack.Push(new Seg(maxK, e));
                }
                // 否则:s..e 之间所有点垂距都 <= epsilon,整段用弦近似,中间点丢弃
            }

            int count = 0;
            for (int i = 0; i < n; i++) if (keep[i]) count++;

            var outPts = new Vector3[count];
            int w = 0;
            for (int i = 0; i < n; i++) if (keep[i]) outPts[w++] = pts[i];
            return outPts;
        }

        // 点 p 到经过 a、b 的直线的垂距平方;a≈b 退化为点到点距离平方。
        // |(p-a) × (b-a)| / |b-a| = 高;两边平方即得 crossSq / baseSq。
        static float PerpDistSq(in Vector3 p, in Vector3 a, in Vector3 b)
        {
            Vector3 ab = b - a;
            float baseSq = ab.sqrMagnitude;
            if (baseSq < 1e-12f) return (p - a).sqrMagnitude;
            Vector3 cross = Vector3.Cross(p - a, ab);
            return cross.sqrMagnitude / baseSq;
        }
    }
}
