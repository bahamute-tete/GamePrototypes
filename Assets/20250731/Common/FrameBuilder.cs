using UnityEngine;

namespace LiangZhu.Geometry.Curves
{
    /// <summary>沿曲线的正交框架。约定 binormal = Cross(tangent, normal)（与 SweepBuilder 绕序一致）。</summary>
    [System.Serializable]
    public struct Frame
    {
        public Vector3 tangent;
        public Vector3 normal;
        public Vector3 binormal;
    }

    /// <summary>
    /// 双反射法 (double reflection, Wang et al. 2008) 的 RMF（rotation-minimizing frame）。
    /// 作用于离散点序列（位置 + 切线），与解析曲线无关——适合重采样点、Ray 投影后的表面点等
    /// （这些点背后没有 ICurveEvaluator）。与 ParallelTransportFrames 并列，是另一种帧策略。
    /// closed 曲线做扭转闭合，消除接缝处法线错位。
    /// </summary>
    public static class FrameBuilder
    {
        /// <summary>
        /// 离散点序列 -> 帧。u 仅用于闭环扭转的线性分配（归一化弧长，∈[0,1)）；
        /// 传 null 或长度不符则退化为 i/(n-1)。
        /// </summary>
        public static Frame[] Build(Vector3[] positions, Vector3[] tangents, float[] u,
            bool closed, float rollDegrees = 0f, Vector3? initialUpHint = null)
        {
            int n = (positions == null || tangents == null)
                ? 0 : Mathf.Min(positions.Length, tangents.Length);
            var frames = new Frame[Mathf.Max(n, 0)];
            if (n == 0) return frames;

            // 初始参考法线：取一个与切线不平行的 up，投影到切线正交平面
            Vector3 t0 = tangents[0];
            Vector3 up = initialUpHint ?? Vector3.up;
            if (Mathf.Abs(Vector3.Dot(t0, up)) > 0.99f) up = Vector3.right;
            Vector3 n0 = up - Vector3.Dot(up, t0) * t0;
            n0 = n0.sqrMagnitude > 1e-10f ? n0.normalized : Vector3.right;

            frames[0].tangent  = t0;
            frames[0].normal   = n0;
            frames[0].binormal = Vector3.Cross(t0, n0).normalized;

            // RMF 逐点传播
            for (int i = 0; i < n - 1; i++)
                frames[i + 1] = Propagate(positions[i], frames[i], positions[i + 1], tangents[i + 1]);

            // closed：再传播一步回起点，测量扭转缺陷，按归一化弧长线性分配抵消
            if (closed && n > 2)
            {
                Frame closing = Propagate(positions[n - 1], frames[n - 1], positions[0], tangents[0]);
                float defect = Vector3.SignedAngle(closing.normal, frames[0].normal, frames[0].tangent);
                bool useU = u != null && u.Length == n;
                for (int i = 0; i < n; i++)
                {
                    float ratio = useU ? u[i] : (float)i / Mathf.Max(n - 1, 1);
                    RotateAroundTangent(ref frames[i], -defect * ratio); // 起点(u=0)不动
                }
            }

            // 全局 roll
            if (Mathf.Abs(rollDegrees) > 1e-4f)
                for (int i = 0; i < n; i++)
                    RotateAroundTangent(ref frames[i], rollDegrees);

            return frames;
        }

        /// <summary>
        /// 便捷：从解析曲线采样后跑双反射，得到向量帧（RMF）。
        /// 与 ParallelTransportFrames（四元数 PTF）二选一——注意两者算法不同、结果不完全一致。
        /// </summary>
        public static Frame[] FromEvaluator(ICurveEvaluator curve, int sampleCount,
            bool closed, float rollDegrees = 0f, Vector3? initialUpHint = null)
        {
            if (curve == null || sampleCount < 2) return new Frame[0];

            var pos = new Vector3[sampleCount];
            var tan = new Vector3[sampleCount];
            var u   = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = closed ? (float)i / sampleCount : (float)i / (sampleCount - 1);
                pos[i] = curve.Evaluate(t);
                Vector3 d = curve.EvaluateDerivative(t);
                tan[i] = d.sqrMagnitude > 1e-10f ? d.normalized : Vector3.forward;
                u[i] = t;
            }
            return Build(pos, tan, u, closed, rollDegrees, initialUpHint);
        }

        static Frame Propagate(Vector3 xi, Frame fi, Vector3 xi1, Vector3 ti1)
        {
            Vector3 v1 = xi1 - xi;
            float c1 = Vector3.Dot(v1, v1);
            if (c1 < 1e-12f)
                return new Frame { tangent = ti1, normal = fi.normal,
                                   binormal = Vector3.Cross(ti1, fi.normal).normalized };

            Vector3 rL = fi.normal  - (2f / c1) * Vector3.Dot(v1, fi.normal)  * v1; // 反射参考法线
            Vector3 tL = fi.tangent - (2f / c1) * Vector3.Dot(v1, fi.tangent) * v1; // 反射切线

            Vector3 v2 = ti1 - tL;
            float c2 = Vector3.Dot(v2, v2);
            Vector3 nNext = c2 < 1e-12f ? rL : rL - (2f / c2) * Vector3.Dot(v2, rL) * v2;
            nNext = nNext.normalized;

            return new Frame
            {
                tangent  = ti1,
                normal   = nNext,
                binormal = Vector3.Cross(ti1, nNext).normalized
            };
        }

        static void RotateAroundTangent(ref Frame f, float degrees)
        {
            Quaternion q = Quaternion.AngleAxis(degrees, f.tangent);
            f.normal   = q * f.normal;
            f.binormal = q * f.binormal;
        }
    }
}
