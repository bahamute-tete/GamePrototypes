using UnityEngine;

namespace LiangZhu.Geometry
{
    /// <summary>射线 / 最近点查询的命中结果，处于被查询几何所在空间。</summary>
    public struct RayHit
    {
        public bool hasHit;
        public float distance;       // raycast: 沿射线参数 t；closest-point: 到表面距离
        public Vector3 point;        // 命中 / 最近点
        public Vector3 normal;       // RayMesh 填插值法线；裸求交填几何法线
        public int triangleIndex;    // 三角形序号（triangles 数组里的第几组）
        public Vector3 barycentric;  // (w,u,v) 对应 v0,v1,v2
    }

    /// <summary>底层几何求交原语。纯静态、无 Collider 依赖，可被任意功能复用。</summary>
    public static class Intersection
    {
        public const float Epsilon = 1e-8f;

        /// <summary>
        /// Möller–Trumbore ray-triangle。命中返回 true，写回 t 与重心 (u,v)。
        /// cull=true 仅正面；false 双面。
        /// </summary>
        public static bool RayTriangle(
            Vector3 ro, Vector3 rd,
            Vector3 v0, Vector3 v1, Vector3 v2,
            out float t, out float u, out float v, bool cull = false)
        {
            t = 0f; u = 0f; v = 0f;
            Vector3 e1 = v1 - v0;
            Vector3 e2 = v2 - v0;
            Vector3 p = Vector3.Cross(rd, e2);
            float det = Vector3.Dot(e1, p);

            if (cull) { if (det < Epsilon) return false; }
            else if (det > -Epsilon && det < Epsilon) return false;

            float inv = 1f / det;
            Vector3 tvec = ro - v0;
            u = Vector3.Dot(tvec, p) * inv;
            if (u < 0f || u > 1f) return false;

            Vector3 q = Vector3.Cross(tvec, e1);
            v = Vector3.Dot(rd, q) * inv;
            if (v < 0f || u + v > 1f) return false;

            t = Vector3.Dot(e2, q) * inv;
            return t > Epsilon;
        }

        /// <summary>Ray-AABB slab。invDir = 1/dir（分量为 0 时用极小值）。tmin 写回近交参数。</summary>
        public static bool RayAabb(Vector3 ro, Vector3 invDir, Vector3 bmin, Vector3 bmax, float maxT, out float tmin)
        {
            float t1 = (bmin.x - ro.x) * invDir.x;
            float t2 = (bmax.x - ro.x) * invDir.x;
            float tlo = Mathf.Min(t1, t2), thi = Mathf.Max(t1, t2);

            t1 = (bmin.y - ro.y) * invDir.y; t2 = (bmax.y - ro.y) * invDir.y;
            tlo = Mathf.Max(tlo, Mathf.Min(t1, t2)); thi = Mathf.Min(thi, Mathf.Max(t1, t2));

            t1 = (bmin.z - ro.z) * invDir.z; t2 = (bmax.z - ro.z) * invDir.z;
            tlo = Mathf.Max(tlo, Mathf.Min(t1, t2)); thi = Mathf.Min(thi, Mathf.Max(t1, t2));

            tmin = tlo;
            return thi >= Mathf.Max(tlo, 0f) && tlo <= maxT;
        }

        /// <summary>点到三角形的最近点（Ericson, Real-Time Collision Detection）。</summary>
        public static Vector3 ClosestPointOnTriangle(Vector3 pt, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = pt - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;

            Vector3 bp = pt - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + (d1 / (d1 - d3)) * ab;

            Vector3 cp = pt - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + (d2 / (d2 - d6)) * ac;

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
                return b + ((d4 - d3) / ((d4 - d3) + (d5 - d6))) * (c - b);

            float denom = 1f / (va + vb + vc);
            return a + ab * (vb * denom) + ac * (vc * denom);
        }

        /// <summary>点 p 在三角形内的重心坐标（用于最近点后的法线插值）。</summary>
        public static Vector3 Barycentric(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 v0 = b - a, v1 = c - a, v2 = p - a;
            float d00 = Vector3.Dot(v0, v0), d01 = Vector3.Dot(v0, v1), d11 = Vector3.Dot(v1, v1);
            float d20 = Vector3.Dot(v2, v0), d21 = Vector3.Dot(v2, v1);
            float denom = d00 * d11 - d01 * d01;
            if (Mathf.Abs(denom) < 1e-12f) return new Vector3(1f, 0f, 0f);
            float vv = (d11 * d20 - d01 * d21) / denom;
            float ww = (d00 * d21 - d01 * d20) / denom;
            return new Vector3(1f - vv - ww, vv, ww);
        }
    }
}
