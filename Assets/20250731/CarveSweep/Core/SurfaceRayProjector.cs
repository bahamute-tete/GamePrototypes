using System.Collections.Generic;
using UnityEngine;
using LiangZhu.Geometry;
using LiangZhu.Geometry.Curves;

namespace LiangZhu.ProcMesh
{
    public enum ProjectionMode
    {
        /// <summary>沿固定方向投射（地形：俯射 -Y）。</summary>
        Direction,
        /// <summary>吸附到最近表面点（雕像 / 任意模型 shrinkwrap）。</summary>
        MinimumDistance,
    }

    [System.Serializable]
    public struct ProjectionConfig
    {
        public ProjectionMode mode;
        public Vector3 direction;       // Direction 模式：世界空间投影方向
        public float castBackDistance;  // 射线起点沿 -direction 回退量（确保从表面上方开始）
        public float maxDistance;
        public float surfaceOffset;     // 命中后沿命中法线推离，防 z-fighting
        public bool alignToSurface;     // 用命中法线对齐 frame（面片贴平）
        public bool dropMisses;         // true 丢弃未命中点；false 保留原位

        public static ProjectionConfig Default => new ProjectionConfig
        {
            mode = ProjectionMode.Direction,
            direction = Vector3.down,
            castBackDistance = 100f,
            maxDistance = 1000f,
            surfaceOffset = 0.01f,
            alignToSurface = true,
            dropMisses = false,
        };
    }

    public struct ProjectionResult
    {
        public ResampleResult curve;      // 投影 + 重参数化后的曲线
        public Frame[] frames;            // 表面对齐框架（alignToSurface 时非空）
        public Vector3[] surfaceNormals;  // 逐点命中法线
        public bool[] hitMask;            // 逐点是否命中
        public bool IsValid => curve.IsValid;
    }

    /// <summary>
    /// Houdini Ray 节点的 Unity 等价物。把曲线点投影到目标 RayMesh 表面，
    /// 重算弧长 / u（投影后位置变了，UV 才不会被坡度拉伸），并按命中法线生成贴平框架。
    /// 输入 samples 与 RayMesh 必须处于同一空间（一般都用世界空间）。
    /// </summary>
    public static class SurfaceRayProjector
    {
        public static ProjectionResult Project(ResampleResult curve, RayMesh target, ProjectionConfig cfg)
        {
            var res = new ProjectionResult();
            if (!curve.IsValid || target == null) return res;

            int nIn = curve.samples.Length;
            var pos = new List<Vector3>(nIn);
            var nrm = new List<Vector3>(nIn);
            var hits = new List<bool>(nIn);

            Vector3 dir = cfg.direction.sqrMagnitude > 1e-10f ? cfg.direction.normalized : Vector3.down;

            for (int i = 0; i < nIn; i++)
            {
                Vector3 p = curve.samples[i].position;
                bool ok; RayHit rh;

                if (cfg.mode == ProjectionMode.Direction)
                {
                    Vector3 origin = p - dir * cfg.castBackDistance;
                    ok = target.Raycast(origin, dir, cfg.maxDistance + cfg.castBackDistance, out rh);
                }
                else
                {
                    ok = target.ClosestPoint(p, cfg.maxDistance, out rh);
                }

                if (ok)
                {
                    pos.Add(rh.point + rh.normal * cfg.surfaceOffset);
                    nrm.Add(rh.normal);
                    hits.Add(true);
                }
                else if (!cfg.dropMisses)
                {
                    pos.Add(p);
                    nrm.Add(Vector3.up);
                    hits.Add(false);
                }
                // dropMisses && miss -> 跳过
            }

            int m = pos.Count;
            if (m < 2) return res;

            // 丢点会破坏闭合性，按开曲线更安全
            bool closed = curve.closed && m == nIn;

            // 重参数化：投影后位置重算累积弧长
            var samples = new CurveSample[m];
            float total = 0f;
            samples[0].position = pos[0];
            samples[0].distance = 0f;
            for (int i = 1; i < m; i++)
            {
                total += Vector3.Distance(pos[i - 1], pos[i]);
                samples[i].position = pos[i];
                samples[i].distance = total;
            }
            float loopLen = total + (closed ? Vector3.Distance(pos[m - 1], pos[0]) : 0f);
            for (int i = 0; i < m; i++)
                samples[i].u = loopLen > 1e-6f ? samples[i].distance / loopLen : 0f;

            // 切线：投影后位置的中心差分
            for (int i = 0; i < m; i++)
            {
                Vector3 prev = closed ? pos[(i - 1 + m) % m] : pos[Mathf.Max(i - 1, 0)];
                Vector3 next = closed ? pos[(i + 1) % m]     : pos[Mathf.Min(i + 1, m - 1)];
                Vector3 t = next - prev;
                samples[i].tangent = t.sqrMagnitude > 1e-10f ? t.normalized : Vector3.forward;
            }

            res.curve = new ResampleResult { samples = samples, totalLength = loopLen, closed = closed };
            res.surfaceNormals = nrm.ToArray();
            res.hitMask = hits.ToArray();

            // 表面对齐框架：normal = 命中法线，tangent 投影到表面切平面，binormal 横展坡面
            if (cfg.alignToSurface)
            {
                var frames = new Frame[m];
                for (int i = 0; i < m; i++)
                {
                    Vector3 N = nrm[i].sqrMagnitude > 1e-10f ? nrm[i].normalized : Vector3.up;
                    Vector3 T = samples[i].tangent;
                    Vector3 Tproj = T - Vector3.Dot(T, N) * N;
                    Tproj = Tproj.sqrMagnitude > 1e-10f ? Tproj.normalized
                                                        : Vector3.Cross(N, Vector3.right).normalized;
                    frames[i].tangent  = Tproj;
                    frames[i].normal   = N;
                    // 必须与 CurveFrames 一致: B = T×N。用 Cross(N,T) 会翻转截面 winding，法线反向
                    frames[i].binormal = Vector3.Cross(Tproj, N).normalized;
                }
                res.frames = frames;
            }

            return res;
        }

        /// <summary>把曲线整体变换到另一空间（如 local -> world 给投影器，再 world -> local 给 Sweep）。</summary>
        public static ResampleResult TransformCurve(ResampleResult c, Matrix4x4 m)
        {
            if (!c.IsValid) return c;
            var s = new CurveSample[c.samples.Length];
            for (int i = 0; i < s.Length; i++)
            {
                s[i] = c.samples[i];
                s[i].position = m.MultiplyPoint3x4(c.samples[i].position);
                s[i].tangent  = m.MultiplyVector(c.samples[i].tangent).normalized;
            }
            return new ResampleResult { samples = s, totalLength = c.totalLength, closed = c.closed };
        }

        /// <summary>把表面对齐框架整体变换到另一空间（配合 TransformCurve 用）。</summary>
        public static Frame[] TransformFrames(Frame[] frames, Matrix4x4 m)
        {
            if (frames == null) return null;
            var outF = new Frame[frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                outF[i].tangent  = m.MultiplyVector(frames[i].tangent).normalized;
                outF[i].normal   = m.MultiplyVector(frames[i].normal).normalized;
                outF[i].binormal = m.MultiplyVector(frames[i].binormal).normalized;
            }
            return outF;
        }
    }
}
