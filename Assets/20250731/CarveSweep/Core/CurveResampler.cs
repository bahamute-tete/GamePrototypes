using System.Collections.Generic;
using UnityEngine;

namespace LiangZhu.ProcMesh
{
    /// <summary>重采样模式（对应 Houdini Resample 的处理方式）。</summary>
    public enum ResampleMode
    {
        /// <summary>沿原始 polyline 重采样，保形不圆滑。</summary>
        StraightEdge,
        /// <summary>Catmull-Rom 平滑后重采样（interpolating，穿过路点）。</summary>
        SubdivisionCurve,
    }

    /// <summary>重采样规格（对应 Houdini 的 Segments / Length）。</summary>
    public enum ResampleSpec
    {
        /// <summary>固定输出点数。</summary>
        ByCount,
        /// <summary>按最大段长，自动决定点数。</summary>
        ByMaxLength,
    }

    /// <summary>
    /// 单个重采样点。position/tangent 处于输入路点所在空间（通常是组件 local space）。
    /// distance = 从起点累积的弧长；u = distance / totalLength ∈ [0,1]，即 Carve 的 curveU。
    /// </summary>
    [System.Serializable]
    public struct CurveSample
    {
        public Vector3 position;
        public Vector3 tangent;   // 已归一化
        public float distance;
        public float u;
    }

    public struct ResampleResult
    {
        public CurveSample[] samples;
        public float totalLength;
        public bool closed;

        public bool IsValid => samples != null && samples.Length >= 2;
    }

    /// <summary>
    /// 纯静态曲线重采样核心。无 Unity 生命周期依赖，编辑器与 runtime 均可调用。
    /// 两种 mode 统一走「先生成 dense polyline → 单一弧长均匀重采样」的管线。
    /// </summary>
    public static class CurveResampler
    {
        /// <param name="waypoints">控制点（>= 2）。</param>
        /// <param name="specValue">ByCount 时为点数；ByMaxLength 时为最大段长。</param>
        /// <param name="catmullAlpha">0=uniform / 0.5=centripetal / 1=chordal。</param>
        /// <param name="subdivisionSamplesPerSegment">每段 Catmull-Rom 的稠密采样数（仅 SubdivisionCurve 用）。</param>
        public static ResampleResult Resample(
            IReadOnlyList<Vector3> waypoints,
            ResampleMode mode,
            ResampleSpec spec,
            float specValue,
            bool closed,
            float catmullAlpha = 0.5f,
            int subdivisionSamplesPerSegment = 16)
        {
            var result = new ResampleResult { closed = closed };
            if (waypoints == null || waypoints.Count < 2) return result;

            // 1) 生成 dense polyline（StraightEdge 直接用路点；Subdivision 用 Catmull-Rom 稠密采样）
            List<Vector3> dense;
            if (mode == ResampleMode.StraightEdge)
            {
                dense = new List<Vector3>(waypoints.Count);
                for (int i = 0; i < waypoints.Count; i++) dense.Add(waypoints[i]);
            }
            else
            {
                dense = SampleCatmullRom(waypoints, closed, catmullAlpha, subdivisionSamplesPerSegment);
            }
            int dn = dense.Count;
            if (dn < 2) return result;

            // 2) 稠密折线的累积弧长（closed 时额外加闭合段 dense[dn-1] -> dense[0]）
            float[] cum = new float[closed ? dn + 1 : dn];
            cum[0] = 0f;
            for (int i = 1; i < dn; i++)
                cum[i] = cum[i - 1] + Vector3.Distance(dense[i - 1], dense[i]);
            if (closed)
                cum[dn] = cum[dn - 1] + Vector3.Distance(dense[dn - 1], dense[0]);

            float total = cum[cum.Length - 1];
            if (total < 1e-6f) return result;

            // 3) 目标点数
            int count;
            if (spec == ResampleSpec.ByCount)
                count = Mathf.Max(2, Mathf.RoundToInt(specValue));
            else
                count = Mathf.Max(2, Mathf.CeilToInt(total / Mathf.Max(specValue, 1e-4f)) + 1);

            // 4) 按弧长均匀重采样。closed 不重复首尾点（count 个点 = count 段）；open 含两端点。
            var samples = new CurveSample[count];
            for (int i = 0; i < count; i++)
            {
                float d = closed ? total * i / count
                                 : total * i / (count - 1);
                samples[i].position = SampleAtDistance(dense, cum, d, total);
                samples[i].distance = d;
                samples[i].u = d / total;
            }

            // 5) 切线：对重采样点做中心差分（closed 环绕；open 端点单侧差分）
            for (int i = 0; i < count; i++)
            {
                Vector3 prev, next;
                if (closed)
                {
                    prev = samples[(i - 1 + count) % count].position;
                    next = samples[(i + 1) % count].position;
                }
                else
                {
                    prev = samples[Mathf.Max(i - 1, 0)].position;
                    next = samples[Mathf.Min(i + 1, count - 1)].position;
                }
                Vector3 t = next - prev;
                samples[i].tangent = t.sqrMagnitude > 1e-10f ? t.normalized : Vector3.forward;
            }

            result.samples = samples;
            result.totalLength = total;
            return result;
        }

        // ---- Catmull-Rom (参数化 / Barry–Goldman，支持 centripetal) ----

        static List<Vector3> SampleCatmullRom(
            IReadOnlyList<Vector3> pts, bool closed, float alpha, int samplesPerSeg)
        {
            int n = pts.Count;
            int subdivs = Mathf.Max(1, samplesPerSeg);
            int segCount = closed ? n : n - 1;

            var dense = new List<Vector3>(segCount * subdivs + 1);
            for (int i = 0; i < segCount; i++)
            {
                Vector3 p0 = pts[ClampIndex(i - 1, n, closed)];
                Vector3 p1 = pts[ClampIndex(i,     n, closed)];
                Vector3 p2 = pts[ClampIndex(i + 1, n, closed)];
                Vector3 p3 = pts[ClampIndex(i + 2, n, closed)];

                // 每段贡献 [0,1) 的采样，避免接缝重复点
                for (int s = 0; s < subdivs; s++)
                {
                    float t = (float)s / subdivs;
                    dense.Add(EvalCatmullRom(p0, p1, p2, p3, t, alpha));
                }
            }
            if (!closed) dense.Add(pts[n - 1]); // open 曲线补上终点
            return dense;
        }

        static int ClampIndex(int i, int n, bool closed)
        {
            if (closed) return ((i % n) + n) % n;
            return Mathf.Clamp(i, 0, n - 1);
        }

        static Vector3 EvalCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t, float alpha)
        {
            float t0 = 0f;
            float t1 = t0 + KnotInterval(p0, p1, alpha);
            float t2 = t1 + KnotInterval(p1, p2, alpha);
            float t3 = t2 + KnotInterval(p2, p3, alpha);

            float tt = Mathf.Lerp(t1, t2, t); // 把 t(0..1) 映射进 [t1,t2] 节点域

            Vector3 a1 = LerpKnot(p0, p1, t0, t1, tt);
            Vector3 a2 = LerpKnot(p1, p2, t1, t2, tt);
            Vector3 a3 = LerpKnot(p2, p3, t2, t3, tt);
            Vector3 b1 = LerpKnot(a1, a2, t0, t2, tt);
            Vector3 b2 = LerpKnot(a2, a3, t1, t3, tt);
            return LerpKnot(b1, b2, t1, t2, tt);
        }

        static float KnotInterval(Vector3 a, Vector3 b, float alpha)
        {
            float dist = Mathf.Max((b - a).magnitude, 1e-5f); // 防重合点除零
            return Mathf.Pow(dist, alpha); // alpha=0 -> 1 (uniform)
        }

        static Vector3 LerpKnot(Vector3 a, Vector3 b, float ta, float tb, float t)
        {
            float denom = tb - ta;
            if (Mathf.Abs(denom) < 1e-9f) return a;
            return a + (b - a) * ((t - ta) / denom);
        }

        // ---- 弧长定位：二分查找稠密折线上距离 d 处的位置 ----

        static Vector3 SampleAtDistance(List<Vector3> dense, float[] cum, float d, float total)
        {
            int dn = dense.Count;
            d = Mathf.Clamp(d, 0f, total);

            int lo = 0, hi = cum.Length - 1;
            while (lo < hi - 1)
            {
                int mid = (lo + hi) >> 1;
                if (cum[mid] <= d) lo = mid; else hi = mid;
            }

            float segLen = cum[hi] - cum[lo];
            float w = segLen > 1e-6f ? (d - cum[lo]) / segLen : 0f;
            Vector3 a = dense[lo % dn];
            Vector3 b = dense[hi % dn]; // closed 时 hi 可能 == dn -> 环绕到 dense[0]
            return Vector3.Lerp(a, b, w);
        }
    }
}
