using UnityEngine;
using LiangZhu.Geometry.Curves;

namespace LiangZhu.ProcMesh
{
    /// <summary>
    /// CurveSample[] -> Frame[] 的薄适配器，转调底层 LiangZhu.Geometry.Curves.FrameBuilder（双反射 RMF）。
    /// Frame 类型现位于 LiangZhu.Geometry.Curves；本类签名不变，调用点（SweepBuilder 等）无需改动。
    /// </summary>
    public static class CurveFrames
    {
        public static Frame[] Compute(CurveSample[] samples, bool closed,
            float rollDegrees = 0f, Vector3? initialUpHint = null)
        {
            int n = samples == null ? 0 : samples.Length;
            if (n == 0) return new Frame[0];

            var pos = new Vector3[n];
            var tan = new Vector3[n];
            var u   = new float[n];
            for (int i = 0; i < n; i++)
            {
                pos[i] = samples[i].position;
                tan[i] = samples[i].tangent;
                u[i]   = samples[i].u;
            }
            return FrameBuilder.Build(pos, tan, u, closed, rollDegrees, initialUpHint);
        }
    }
}
