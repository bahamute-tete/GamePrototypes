using UnityEngine;

namespace LiangZhu.Geometry
{
    /// <summary>
    /// 把一个 Mesh 包成可射线 / 最近点查询的对象：持有几何 + 选定加速结构（默认 BVH）。
    /// 命中时用重心坐标插值顶点法线得到平滑法线。无 Collider 依赖。
    /// 几何处于构建时传入的空间——一般用 FromMesh 把顶点烘到世界空间，查询也用世界空间。
    /// </summary>
    public class RayMesh
    {
        readonly Vector3[] _vertices;
        readonly Vector3[] _normals; // 可空；空则回退几何法线
        readonly int[] _triangles;
        readonly IRayAccelerator _accel;

        public IRayAccelerator Accelerator => _accel;

        public RayMesh(Vector3[] vertices, int[] triangles, Vector3[] normals = null, IRayAccelerator accelerator = null)
        {
            _vertices = vertices;
            _triangles = triangles;
            _normals = (normals != null && normals.Length == vertices.Length) ? normals : null;
            _accel = accelerator ?? new Bvh();
            _accel.Build(vertices, triangles);
        }

        /// <summary>
        /// 从 Mesh + Transform 构建，顶点 / 法线烘到世界空间。
        /// 目标 mesh 需开启 Read/Write Enabled（runtime 读 mesh.vertices 的前提；程序生成的 mesh 默认可读）。
        /// 可传入其他 IRayAccelerator 实现替换 BVH。
        /// </summary>
        public static RayMesh FromMesh(Mesh mesh, Transform transform, IRayAccelerator accelerator = null)
        {
            var lv = mesh.vertices;
            var ln = mesh.normals;
            var tris = mesh.triangles;

            var wv = new Vector3[lv.Length];
            var wn = (ln != null && ln.Length == lv.Length) ? new Vector3[ln.Length] : null;

            Matrix4x4 l2w = transform != null ? transform.localToWorldMatrix : Matrix4x4.identity;
            Matrix4x4 nMat = transform != null ? transform.worldToLocalMatrix.transpose : Matrix4x4.identity; // 法线用逆转置

            for (int i = 0; i < lv.Length; i++)
            {
                wv[i] = l2w.MultiplyPoint3x4(lv[i]);
                if (wn != null) wn[i] = nMat.MultiplyVector(ln[i]).normalized;
            }
            return new RayMesh(wv, tris, wn, accelerator);
        }

        public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out RayHit hit, bool cullBackface = false)
        {
            if (_accel.Raycast(origin, direction, maxDistance, cullBackface, out hit))
            {
                hit.normal = SmoothNormal(hit);
                return true;
            }
            return false;
        }

        public bool ClosestPoint(Vector3 point, float maxDistance, out RayHit hit)
        {
            if (_accel.ClosestPoint(point, maxDistance, out hit))
            {
                hit.normal = SmoothNormal(hit);
                return true;
            }
            return false;
        }

        Vector3 SmoothNormal(RayHit hit)
        {
            if (_normals == null) return hit.normal;
            int t = hit.triangleIndex;
            Vector3 n = _normals[_triangles[t * 3 + 0]] * hit.barycentric.x
                      + _normals[_triangles[t * 3 + 1]] * hit.barycentric.y
                      + _normals[_triangles[t * 3 + 2]] * hit.barycentric.z;
            return n.sqrMagnitude > 1e-10f ? n.normalized : hit.normal;
        }
    }
}
