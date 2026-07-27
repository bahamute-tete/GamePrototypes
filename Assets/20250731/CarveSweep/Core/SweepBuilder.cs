using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using LiangZhu.Geometry.Curves;

namespace LiangZhu.ProcMesh
{
    public enum SweepProfile { Ribbon, RoundTube }

    /// <summary>UV 沿长度方向 (V) 的映射方式。</summary>
    public enum UVMode
    {
        /// <summary>V = u * vTilingNormalized，整条线 0..1。</summary>
        Normalized,
        /// <summary>V = distance * tilesPerMeter，按世界距离平铺，纹理密度与线长无关。</summary>
        DistanceTiled,
    }

    [System.Serializable]
    public struct SweepConfig
    {
        public SweepProfile profile;

        [Header("Ribbon")] public float width;
        [Header("Round Tube")] public float radius;
        [Min(3)] public int tubeSegments;
        public bool caps;

        [Header("UV")]
        public UVMode uvMode;
        public float tilesPerMeter;     // DistanceTiled 用
        public float vTilingNormalized; // Normalized 用
        public float uTiling;

        [Header("Framing")]
        public float rollDegrees;

        public static SweepConfig DefaultRibbon => new SweepConfig
        {
            profile = SweepProfile.Ribbon, width = 0.5f, radius = 0.1f, tubeSegments = 12,
            caps = true, uvMode = UVMode.Normalized, tilesPerMeter = 1f,
            vTilingNormalized = 1f, uTiling = 1f, rollDegrees = 0f
        };

        public static SweepConfig DefaultTube => new SweepConfig
        {
            profile = SweepProfile.RoundTube, width = 0.5f, radius = 0.1f, tubeSegments = 12,
            caps = true, uvMode = UVMode.Normalized, tilesPerMeter = 1f,
            vTilingNormalized = 1f, uTiling = 1f, rollDegrees = 0f
        };
    }

    /// <summary>
    /// 顶点缓冲累加器:把一条或多条曲线 Append 进同一份缓冲,最后 Flush 成单个 Mesh。
    /// 不变量:每次 Append 结束后,各通道 List 的长度都与 verts 对齐。
    /// </summary>
    public sealed class MeshBuffers
    {
        public readonly List<Vector3> verts;
        public readonly List<Vector3> norms;
        public readonly List<Vector2> uv0;  // 贴图坐标(按 UVMode)
        public readonly List<Vector2> uv2;  // (u, distance) 生长 mask,逐条曲线自归一化
        public readonly List<Vector2> uv3;  // (pathId, pathLength) 逐条标识 + 弧长,供错峰生长
        public readonly List<Vector4> tans;
        public readonly List<int> tris;

        public MeshBuffers(int curveHint = 1)
        {
            int v = Mathf.Max(8, curveHint * 64);
            verts = new List<Vector3>(v);
            norms = new List<Vector3>(v);
            uv0   = new List<Vector2>(v);
            uv2   = new List<Vector2>(v);
            uv3   = new List<Vector2>(v);
            tans  = new List<Vector4>(v);
            tris  = new List<int>(v * 3);
        }
    }

    /// <summary>
    /// 把重采样曲线扫掠成 ribbon 或 round tube 网格。
    /// UV0 = 贴图坐标(按 UVMode);UV2 = (u, distance),专供生长 shader 的 reveal mask
    /// (与 UV0 解耦);UV3 = (pathId, 0),批处理时逐条标识,单曲线时恒为 0。
    /// </summary>
    public static class SweepBuilder
    {
        /// <summary>单曲线:行为与签名与改造前一致(内部走 Append + Flush),现有调用方零改动。</summary>
        public static void Build(ResampleResult curve, SweepConfig cfg, Mesh mesh,
                                 Frame[] framesOverride = null)
        {
            var buf = new MeshBuffers(1);
            Append(curve, cfg, buf, 0, framesOverride);
            Flush(buf, mesh);
        }

        /// <summary>把一条曲线扫掠后追加进 buf(不清空,可连续多条)。pathId 写入 UV3.x。</summary>
        public static void Append(ResampleResult curve, SweepConfig cfg, MeshBuffers buf,
                                  int pathId = 0, Frame[] framesOverride = null)
        {
            if (!curve.IsValid) return;

            var s = curve.samples;
            int n = s.Length;
            bool closed = curve.closed;

            Frame[] f = (framesOverride != null && framesOverride.Length == n)
                ? framesOverride
                : CurveFrames.Compute(s, closed, cfg.rollDegrees);

            int startV = buf.verts.Count;
            float pathLen = curve.totalLength;

            if (cfg.profile == SweepProfile.Ribbon)
                BuildRibbon(s, f, n, closed, cfg, buf);
            else
                BuildTube(s, f, n, closed, cfg, buf);

            // 本条新增顶点(含 caps)写入 (pathId, pathLength):
            // pathLength 供距离生长模式把 [0,1] 错峰进度换算回米;保持 uv3 与 verts 对齐
            for (int i = startV; i < buf.verts.Count; i++)
                buf.uv3.Add(new Vector2(pathId, pathLen));
        }

        /// <summary>把累加好的缓冲一次性写入 mesh。</summary>
        public static void Flush(MeshBuffers buf, Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = buf.verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(buf.verts);
            mesh.SetNormals(buf.norms);
            mesh.SetUVs(0, buf.uv0);
            mesh.SetUVs(2, buf.uv2);
            mesh.SetUVs(3, buf.uv3);
            mesh.SetTangents(buf.tans);
            mesh.SetTriangles(buf.tris, 0);
            mesh.RecalculateBounds();
        }

        // ---------- Ribbon ----------

        static void BuildRibbon(CurveSample[] s, Frame[] f, int n, bool closed, SweepConfig cfg,
                                MeshBuffers buf)
        {
            int bv = buf.verts.Count;          // 本条 ribbon 的顶点基址
            float hw = cfg.width * 0.5f;
            for (int i = 0; i < n; i++)
            {
                Vector3 B = f[i].binormal, N = f[i].normal, T = f[i].tangent;
                float v = VCoord(s[i], cfg);

                buf.verts.Add(s[i].position - B * hw);  // +0:U=0 一侧
                buf.verts.Add(s[i].position + B * hw);  // +1:U=uTiling 一侧
                buf.norms.Add(N); buf.norms.Add(N);
                buf.uv0.Add(new Vector2(0f, v));
                buf.uv0.Add(new Vector2(cfg.uTiling, v));
                buf.uv2.Add(new Vector2(s[i].u, s[i].distance));
                buf.uv2.Add(new Vector2(s[i].u, s[i].distance));

                Vector4 tan = TangentVec(B, N, T);
                buf.tans.Add(tan); buf.tans.Add(tan);
            }

            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                int i0 = bv + i * 2;
                int i1 = bv + ((i + 1) % n) * 2;
                // 正面朝 +normal
                buf.tris.Add(i0 + 0); buf.tris.Add(i0 + 1); buf.tris.Add(i1 + 1);
                buf.tris.Add(i0 + 0); buf.tris.Add(i1 + 1); buf.tris.Add(i1 + 0);
            }
        }

        // ---------- Round Tube ----------

        static void BuildTube(CurveSample[] s, Frame[] f, int n, bool closed, SweepConfig cfg,
                              MeshBuffers buf)
        {
            int bv = buf.verts.Count;          // 本条 tube 的顶点基址
            int seg = Mathf.Max(3, cfg.tubeSegments);
            int stride = seg + 1;              // 环向接缝复制顶点,UV 才能正确平铺
            float r = cfg.radius;

            for (int i = 0; i < n; i++)
            {
                Vector3 N = f[i].normal, B = f[i].binormal, T = f[i].tangent;
                Vector3 c = s[i].position;
                float v = VCoord(s[i], cfg);

                for (int j = 0; j <= seg; j++)
                {
                    float ang = Mathf.PI * 2f * j / seg;
                    float cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);
                    Vector3 dir  = cos * N + sin * B;     // 径向 = 外法线
                    Vector3 uDir = -sin * N + cos * B;    // 环向 = +U 方向

                    buf.verts.Add(c + dir * r);
                    buf.norms.Add(dir);
                    buf.uv0.Add(new Vector2((float)j / seg * cfg.uTiling, v));
                    buf.uv2.Add(new Vector2(s[i].u, s[i].distance));
                    buf.tans.Add(TangentVec(uDir, dir, T));
                }
            }

            int rings = closed ? n : n - 1;
            for (int i = 0; i < rings; i++)
            {
                int r0 = bv + i * stride;
                int r1 = bv + ((i + 1) % n) * stride;
                for (int j = 0; j < seg; j++)
                {
                    int a = r0 + j, b = r0 + j + 1, cc = r1 + j + 1, d = r1 + j;
                    buf.tris.Add(a); buf.tris.Add(b); buf.tris.Add(cc);  // 外法线朝外
                    buf.tris.Add(a); buf.tris.Add(cc); buf.tris.Add(d);
                }
            }

            if (cfg.caps && !closed)
            {
                AddCap(s[0],     f[0],     cfg, true,  buf);
                AddCap(s[n - 1], f[n - 1], cfg, false, buf);
            }
        }

        static void AddCap(CurveSample s, Frame f, SweepConfig cfg, bool start, MeshBuffers buf)
        {
            int seg = Mathf.Max(3, cfg.tubeSegments);
            float r = cfg.radius;
            Vector3 N = f.normal, B = f.binormal, c = s.position;
            Vector3 capN = start ? -f.tangent : f.tangent;
            Vector4 capTan = TangentVec(B, capN, N);

            // center / rim 用 buf.verts.Count 取绝对索引,批处理下天然正确
            int center = buf.verts.Count;
            buf.verts.Add(c); buf.norms.Add(capN); buf.uv0.Add(new Vector2(0.5f, 0.5f));
            buf.uv2.Add(new Vector2(s.u, s.distance)); buf.tans.Add(capTan);

            int rim = buf.verts.Count;
            for (int j = 0; j <= seg; j++)
            {
                float ang = Mathf.PI * 2f * j / seg;
                float cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);
                Vector3 dir = cos * N + sin * B;
                buf.verts.Add(c + dir * r);
                buf.norms.Add(capN);
                buf.uv0.Add(new Vector2(0.5f + 0.5f * cos, 0.5f + 0.5f * sin));
                buf.uv2.Add(new Vector2(s.u, s.distance));
                buf.tans.Add(capTan);
            }

            for (int j = 0; j < seg; j++)
            {
                int v0 = rim + j, v1 = rim + j + 1;
                if (start) { buf.tris.Add(center); buf.tris.Add(v1); buf.tris.Add(v0); } // 朝 -tangent
                else       { buf.tris.Add(center); buf.tris.Add(v0); buf.tris.Add(v1); } // 朝 +tangent
            }
        }

        // ---------- helpers ----------

        static float VCoord(CurveSample s, SweepConfig cfg)
            => cfg.uvMode == UVMode.DistanceTiled
                ? s.distance * cfg.tilesPerMeter
                : s.u * cfg.vTilingNormalized;

        /// <summary>tangent.xyz = +U 方向;w 取符号使 bitangent = cross(normal, tangent)*w 指向曲线前进方向。</summary>
        static Vector4 TangentVec(Vector3 uDir, Vector3 normal, Vector3 curveTangent)
        {
            uDir = uDir.sqrMagnitude > 1e-10f ? uDir.normalized : Vector3.right;
            Vector3 bit = Vector3.Cross(normal, uDir);
            float w = Vector3.Dot(bit, curveTangent) >= 0f ? 1f : -1f;
            return new Vector4(uDir.x, uDir.y, uDir.z, w);
        }
    }
}
