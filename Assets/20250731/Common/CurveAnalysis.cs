using UnityEngine;

namespace LiangZhu.Geometry.Curves
{
    /// <summary>曲线分析工具（通用，作用于任意 ICurveEvaluator）。</summary>
    public static class CurveAnalysis
    {
        /// <summary>
        /// t 处的 signed curvature（单位 1/米）。需传入对应的 PTF 帧 LUT 取参考 right 方向定符号。
        /// 符号约定与原 CatmullRomSpline.GetSignedCurvatureAtT 一致：圆心在 -right 一侧时 > 0。
        /// </summary>
        public static float SignedCurvature(ICurveEvaluator curve, Quaternion[] frameLut, float t, bool loop)
        {
            if (curve == null || curve.SegmentCount < 1) return 0f;

            const float h = 1e-3f;
            float t1 = loop ? Mod1(t) : Mathf.Clamp01(t);
            float t2 = loop ? Mod1(t + h) : Mathf.Clamp01(t + h);

            Vector3 tan1 = TangentAt(curve, t1);
            Vector3 tan2 = TangentAt(curve, t2);
            Vector3 dT = tan2 - tan1;
            float dTMag = dT.magnitude;
            if (dTMag < 1e-8f) return 0f;

            Vector3 p1 = curve.Evaluate(t1);
            Vector3 p2 = curve.Evaluate(t2);
            float ds = Vector3.Distance(p1, p2);
            if (ds < 1e-8f) return 0f;

            float k = dTMag / ds;

            Quaternion ptf = ParallelTransportFrames.Sample(frameLut, t1, loop);
            Vector3 right = ptf * Vector3.right;
            float lateralSign = -Mathf.Sign(Vector3.Dot(dT, right));
            if (lateralSign == 0f) lateralSign = 1f;
            return lateralSign * k;
        }

        static float Mod1(float t) => t - Mathf.Floor(t);

        static Vector3 TangentAt(ICurveEvaluator curve, float t)
        {
            Vector3 tan = curve.EvaluateDerivative(t);
            float mag = tan.magnitude;
            return mag > 1e-6f ? tan / mag : Vector3.forward;
        }
    }
}
