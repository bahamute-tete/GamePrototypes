using UnityEngine;

namespace LiangZhu.Geometry
{
    /// <summary>
    /// 空间加速结构统一接口。当前实现：Bvh。以后加 KD-Tree / BSP 等，
    /// 实现本接口即可被 RayMesh / 投影器直接替换，无需改上层。
    /// 顶点 / 查询均处于构建时传入的同一空间（通常是世界空间）。
    /// 注意：单个实例非线程安全（内部遍历状态），bake 时单线程使用。
    /// </summary>
    public interface IRayAccelerator
    {
        /// <summary>用三角形软体构建。triangles 每 3 个 int 一组（顶点索引）。</summary>
        void Build(Vector3[] vertices, int[] triangles);

        /// <summary>最近命中。返回 t / 重心 / triangleIndex（几何法线已填）。</summary>
        bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, bool cullBackface, out RayHit hit);

        /// <summary>网格上离 point 最近的点（minimum-distance / shrinkwrap 投影）。</summary>
        bool ClosestPoint(Vector3 point, float maxDistance, out RayHit hit);
    }
}
