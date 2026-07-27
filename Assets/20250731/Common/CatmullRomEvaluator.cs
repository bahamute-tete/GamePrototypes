using System.Collections.Generic;
using UnityEngine;

namespace LiangZhu.Geometry.Curves
{
    public enum CatmullRomAlpha { Uniform = 0, Centripetal = 1, Chordal = 2 }

    /// <summary>
    /// Catmull-Rom 求值器（interpolating，穿过控制点）。uniform / centripetal / chordal 参数化 + 闭环。
    /// 求值数学与原 CatmullRomSpline.ResolveSegment / EvaluateSegment / GetCachedPoint 逐式一致。
    /// </summary>
    public sealed class CatmullRomEvaluator : ICurveEvaluator
    {
        Vector3[] _points = System.Array.Empty<Vector3>();
        bool _loop;
        CatmullRomAlpha _alpha = CatmullRomAlpha.Centripetal;

        public bool IsLoop => _loop;
        public int PointCount => _points.Length;
        public int SegmentCount => _loop ? _points.Length : Mathf.Max(0, _points.Length - 1);

        /// <summary>用控制点配置（内部拷贝，调用方后续改动不影响）。</summary>
        public void SetControlPoints(IReadOnlyList<Vector3> points, bool loop, CatmullRomAlpha alpha)
        {
            int n = points?.Count ?? 0;
            if (_points.Length != n) _points = new Vector3[n];
            for (int i = 0; i < n; i++) _points[i] = points[i];
            _loop = loop;
            _alpha = alpha;
        }

        public Vector3 Evaluate(float t)
        {
            int n = _points.Length;
            if (n == 0) return Vector3.zero;
            if (n == 1) return _points[0];
            ResolveSegment(t, out int seg, out float localT);
            return EvaluateSegment(seg, localT, derivative: false);
        }

        public Vector3 EvaluateDerivative(float t)
        {
            int n = _points.Length;
            if (n < 2) return Vector3.forward;
            ResolveSegment(t, out int seg, out float localT);
            return EvaluateSegment(seg, localT, derivative: true);
        }

        void ResolveSegment(float t, out int segmentIndex, out float localT)
        {
            int segCount = _loop ? _points.Length : _points.Length - 1;
            if (segCount <= 0) { segmentIndex = 0; localT = 0f; return; }

            if (_loop)
            {
                t = t - Mathf.Floor(t);
                float scaled = t * segCount;
                segmentIndex = Mathf.FloorToInt(scaled);
                if (segmentIndex >= segCount) segmentIndex = segCount - 1;
                localT = scaled - segmentIndex;
            }
            else
            {
                t = Mathf.Clamp01(t);
                float scaled = t * segCount;
                segmentIndex = Mathf.FloorToInt(scaled);
                if (segmentIndex >= segCount) { segmentIndex = segCount - 1; localT = 1f; }
                else localT = scaled - segmentIndex;
            }
        }

        Vector3 EvaluateSegment(int segmentIndex, float localT, bool derivative)
        {
            Vector3 p0 = GetCachedPoint(segmentIndex - 1);
            Vector3 p1 = GetCachedPoint(segmentIndex);
            Vector3 p2 = GetCachedPoint(segmentIndex + 1);
            Vector3 p3 = GetCachedPoint(segmentIndex + 2);

            if (_alpha == CatmullRomAlpha.Uniform)
            {
                float t = localT;
                float t2 = t * t;
                if (!derivative)
                {
                    float t3 = t2 * t;
                    return 0.5f * (
                        (2f * p1) +
                        (-p0 + p2) * t +
                        (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                        (-p0 + 3f * p1 - 3f * p2 + p3) * t3
                    );
                }
                return 0.5f * (
                    (-p0 + p2) +
                    2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t +
                    3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2
                );
            }

            float exp = (_alpha == CatmullRomAlpha.Centripetal) ? 0.5f : 1f;
            float dt0 = Mathf.Pow(Mathf.Max((p1 - p0).sqrMagnitude, 1e-8f), exp * 0.5f);
            float dt1 = Mathf.Pow(Mathf.Max((p2 - p1).sqrMagnitude, 1e-8f), exp * 0.5f);
            float dt2 = Mathf.Pow(Mathf.Max((p3 - p2).sqrMagnitude, 1e-8f), exp * 0.5f);

            if (dt1 < 1e-5f) dt1 = 1f;
            if (dt0 < 1e-5f) dt0 = dt1;
            if (dt2 < 1e-5f) dt2 = dt1;

            Vector3 m1 = ((p1 - p0) / dt0 - (p2 - p0) / (dt0 + dt1) + (p2 - p1) / dt1) * dt1;
            Vector3 m2 = ((p2 - p1) / dt1 - (p3 - p1) / (dt1 + dt2) + (p3 - p2) / dt2) * dt1;

            float u = localT;
            float u2 = u * u;
            float u3 = u2 * u;

            if (!derivative)
            {
                float h00 = 2f * u3 - 3f * u2 + 1f;
                float h10 = u3 - 2f * u2 + u;
                float h01 = -2f * u3 + 3f * u2;
                float h11 = u3 - u2;
                return h00 * p1 + h10 * m1 + h01 * p2 + h11 * m2;
            }
            else
            {
                float dh00 = 6f * u2 - 6f * u;
                float dh10 = 3f * u2 - 4f * u + 1f;
                float dh01 = -6f * u2 + 6f * u;
                float dh11 = 3f * u2 - 2f * u;
                return dh00 * p1 + dh10 * m1 + dh01 * p2 + dh11 * m2;
            }
        }

        Vector3 GetCachedPoint(int index)
        {
            int n = _points.Length;
            if (n == 0) return Vector3.zero;

            if (_loop)
            {
                index = ((index % n) + n) % n;
                return _points[index];
            }

            if (index < 0)
                return n > 1 ? _points[0] + (_points[0] - _points[1]) : _points[0];
            if (index >= n)
            {
                int last = n - 1;
                return last > 0 ? _points[last] + (_points[last] - _points[last - 1]) : _points[last];
            }
            return _points[index];
        }
    }
}
