using UnityEngine;

namespace SpiralPlacer
{
    public enum SpiralOrientation { XY, YZ, ZX }
    public enum SpiralRadiusMode  { Linear, Exponential }

    [System.Serializable]
    public class SpiralParams
    {
        [Header("Shape")]
        public float revolutions   = 3f;
        public int   divisions     = 12;          // points per revolution (resolution)

        [Header("Radius")]
        public float startRadius   = 0.5f;
        public float endRadius     = 3f;
        public SpiralRadiusMode radiusMode = SpiralRadiusMode.Linear;

        [Header("Height (3-D)")]
        public float height        = 0f;          // 0 = flat spiral

        [Header("Angle")]
        public float startAngleDeg = 0f;

        [Header("Orientation & Position")]
        public SpiralOrientation orientation = SpiralOrientation.XY;
        public Vector3 center      = Vector3.zero;
    }

    public static class SpiralMath
    {
        /// <summary>
        /// 按 t ∈ [0,1] 采样螺旋线上的世界位置
        /// </summary>
        public static Vector3 Evaluate(SpiralParams p, float t)
        {
            float totalAngle = p.revolutions * 2f * Mathf.PI;
            float angle      = p.startAngleDeg * Mathf.Deg2Rad + t * totalAngle;

            // Radius
            float radius;
            if (p.radiusMode == SpiralRadiusMode.Linear)
            {
                radius = Mathf.Lerp(p.startRadius, p.endRadius, t);
            }
            else // Exponential
            {
                // log-space lerp: r = startRadius * (endRadius/startRadius)^t
                float sr = Mathf.Max(p.startRadius, 0.0001f);
                float er = Mathf.Max(p.endRadius,   0.0001f);
                radius = sr * Mathf.Pow(er / sr, t);
            }

            float h = t * p.height;

            // Local 2-D position on spiral plane
            float u = radius * Mathf.Cos(angle);
            float v = radius * Mathf.Sin(angle);

            Vector3 local = p.orientation switch
            {
                SpiralOrientation.XY => new Vector3(u, v, h),
                SpiralOrientation.YZ => new Vector3(h, u, v),
                SpiralOrientation.ZX => new Vector3(v, h, u),
                _                    => new Vector3(u, v, h),
            };

            return p.center + local;
        }

        /// <summary>
        /// 将 count 个点均匀分布在螺旋线上，返回 world-space 位置数组
        /// </summary>
        public static Vector3[] Sample(SpiralParams p, int count)
        {
            var pts = new Vector3[count];
            if (count == 0) return pts;
            if (count == 1) { pts[0] = Evaluate(p, 0f); return pts; }

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                pts[i] = Evaluate(p, t);
            }
            return pts;
        }

        /// <summary>
        /// 生成用于 Gizmo 预览的折线点（独立于目标点数，用更高分辨率）
        /// </summary>
        public static Vector3[] PreviewLine(SpiralParams p, int segments = 128)
        {
            var pts = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                pts[i] = Evaluate(p, t);
            }
            return pts;
        }
    }
}
