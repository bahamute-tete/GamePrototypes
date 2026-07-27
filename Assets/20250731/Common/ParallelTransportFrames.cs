using UnityEngine;

namespace LiangZhu.Geometry.Curves
{
    /// <summary>
    /// 平行传输帧 (PTF) LUT 的构建与采样，作用于任意 ICurveEvaluator。
    /// 算法与原 CatmullRomSpline.BuildPTFFrameLut / SamplePTFFrame 逐式一致，
    /// 含闭环 holonomy 校正与"强制末帧 = 首帧"。LUT 元素为四元数帧（forward = 切线）。
    /// </summary>
    public static class ParallelTransportFrames
    {
        public static Quaternion[] Build(ICurveEvaluator curve, int resolution, bool loop)
        {
            if (curve == null || curve.SegmentCount < 1)
                return new Quaternion[] { Quaternion.identity, Quaternion.identity };

            int res = Mathf.Max(32, resolution);
            int count = res + 1;
            var lut = new Quaternion[count];

            Vector3 t0 = TangentAt(curve, 0f);
            Vector3 up0 = ChooseInitialUp(t0);
            Quaternion frame = LookRotationSafe(t0, up0);
            lut[0] = frame;

            Vector3 prevTan = t0;
            for (int i = 1; i < count; i++)
            {
                float t = (float)i / res;
                Vector3 curTan = TangentAt(curve, t);
                Quaternion delta = FromToRotationSafe(prevTan, curTan);
                frame = delta * frame;
                Vector3 currentUp = frame * Vector3.up;
                frame = LookRotationSafe(curTan, currentUp);
                lut[i] = frame;
                prevTan = curTan;
            }

            // 闭环 holonomy 校正
            if (loop && count > 1)
            {
                Vector3 startTan = TangentAt(curve, 0f);
                Vector3 endTan = TangentAt(curve, 1f);
                Quaternion startFrame = lut[0];
                Quaternion endFrame = lut[count - 1];

                Quaternion alignToStart = FromToRotationSafe(endTan, startTan);
                Quaternion endInStartFrame = alignToStart * endFrame;

                Vector3 endUpInStart = Vector3.ProjectOnPlane(endInStartFrame * Vector3.up, startTan).normalized;
                Vector3 startUp = Vector3.ProjectOnPlane(startFrame * Vector3.up, startTan).normalized;
                float twistError = Vector3.SignedAngle(endUpInStart, startUp, startTan);

                for (int i = 0; i < count; i++)
                {
                    float ratio = (float)i / (count - 1);
                    float ti = (float)i / res;
                    Vector3 tanI = TangentAt(curve, ti);
                    Quaternion correction = Quaternion.AngleAxis(twistError * ratio, tanI);
                    lut[i] = correction * lut[i];
                }

                // 强制末帧严格等于首帧，消除浮点残差导致的接缝抖动
                lut[count - 1] = lut[0];
            }

            return lut;
        }

        public static Quaternion Sample(Quaternion[] lut, float t, bool loop)
        {
            if (lut == null || lut.Length < 2) return Quaternion.identity;

            if (loop) t = t - Mathf.Floor(t);
            else t = Mathf.Clamp01(t);

            int res = lut.Length - 1;
            float scaled = t * res;
            int lo = Mathf.FloorToInt(scaled);
            if (lo >= res) lo = res - 1;
            int hi = lo + 1;
            float frac = scaled - lo;
            return Quaternion.Slerp(lut[lo], lut[hi], frac);
        }

        // ---- 与原 CatmullRomSpline 同名私有方法逐式照搬 ----

        static Vector3 TangentAt(ICurveEvaluator curve, float t)
        {
            Vector3 tan = curve.EvaluateDerivative(t);
            float mag = tan.magnitude;
            return mag > 1e-6f ? tan / mag : Vector3.forward;
        }

        static Vector3 ChooseInitialUp(Vector3 tangent)
        {
            Vector3 up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(tangent, up)) > 0.95f) up = Vector3.right;
            return Vector3.ProjectOnPlane(up, tangent).normalized;
        }

        static Quaternion LookRotationSafe(Vector3 forward, Vector3 up)
        {
            if (forward.sqrMagnitude < 1e-8f) return Quaternion.identity;
            Vector3 fN = forward.normalized;
            if (up.sqrMagnitude < 1e-8f || Mathf.Abs(Vector3.Dot(fN, up.normalized)) > 0.999f)
                up = ChooseInitialUp(fN);
            return Quaternion.LookRotation(fN, up);
        }

        static Quaternion FromToRotationSafe(Vector3 from, Vector3 to)
        {
            if (from.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f) return Quaternion.identity;
            return Quaternion.FromToRotation(from.normalized, to.normalized);
        }
    }
}
