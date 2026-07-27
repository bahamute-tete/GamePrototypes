using UnityEngine;

namespace LiangZhu.Geometry.Curves
{
    /// <summary>
    /// 任意 ICurveEvaluator 的弧长重参数化表。
    /// Build / ArcLengthToT 与原 CatmullRomSpline.BuildArcLengthLut / ArcLengthToT 逐式一致。
    /// </summary>
    public sealed class ArcLengthTable
    {
        float[] _lut;
        float _totalLength;

        public float TotalLength => _totalLength;
        public bool IsValid => _lut != null && _lut.Length >= 2;

        public void Build(ICurveEvaluator curve, int resolution)
        {
            if (curve == null || curve.SegmentCount < 1)
            {
                _lut = new float[] { 0f, 0f };
                _totalLength = 0f;
                return;
            }

            int res = Mathf.Max(32, resolution);
            if (_lut == null || _lut.Length != res + 1) _lut = new float[res + 1];

            _lut[0] = 0f;
            Vector3 prev = curve.Evaluate(0f);
            float accum = 0f;
            for (int i = 1; i <= res; i++)
            {
                float t = (float)i / res;
                Vector3 cur = curve.Evaluate(t);
                accum += Vector3.Distance(prev, cur);
                _lut[i] = accum;
                prev = cur;
            }
            _totalLength = accum;
        }

        /// <summary>弧长比例 s∈[0,1] -> 曲线参数 t∈[0,1]。</summary>
        public float ArcLengthToT(float s01)
        {
            if (_lut == null || _lut.Length < 2 || _totalLength <= 1e-6f) return 0f;

            s01 = Mathf.Clamp01(s01);
            float target = s01 * _totalLength;

            int lo = 0, hi = _lut.Length - 1;
            while (lo < hi - 1)
            {
                int mid = (lo + hi) >> 1;
                if (_lut[mid] < target) lo = mid; else hi = mid;
            }

            float seg = _lut[hi] - _lut[lo];
            float frac = seg > 1e-6f ? (target - _lut[lo]) / seg : 0f;
            int res = _lut.Length - 1;
            return (lo + frac) / res;
        }
    }
}
