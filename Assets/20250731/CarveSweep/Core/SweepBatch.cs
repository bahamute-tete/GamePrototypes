using System.Collections.Generic;
using UnityEngine;
using LiangZhu.Geometry.Curves;

namespace LiangZhu.ProcMesh
{
    /// <summary>
    /// 批处理产物里单条路径的占位信息。VertexStart/Count 与 TriangleStart/Count
    /// 给 Timeline 用来定位/驱动逐条生长;Length 为该曲线弧长。
    /// </summary>
    public readonly struct PathInfo
    {
        public readonly int PathId;
        public readonly int VertexStart;
        public readonly int VertexCount;
        public readonly int TriangleStart;   // tris 数组下标(3 的倍数)
        public readonly int TriangleCount;
        public readonly float Length;

        public PathInfo(int pathId, int vStart, int vCount,
                        int tStart, int tCount, float length)
        {
            PathId = pathId;
            VertexStart = vStart; VertexCount = vCount;
            TriangleStart = tStart; TriangleCount = tCount;
            Length = length;
        }
    }

    /// <summary>
    /// 把多条 ResampleResult 扫掠进同一个 Mesh。每条曲线自带 UV2 生长参数(自归一化),
    /// 并被写入 UV3 的 path id,因此可逐条独立生长。返回 PathInfo[] 供 Timeline 驱动。
    /// </summary>
    public static class SweepBatch
    {
        /// <param name="framesPerPath">可选:每条曲线的 surface-aligned 框架(如来自 SurfaceRayProjector);
        /// 为 null 或某条缺省时,该条走 RMF。</param>
        public static PathInfo[] Build(IReadOnlyList<ResampleResult> curves, SweepConfig cfg,
                                       Mesh mesh, Frame[][] framesPerPath = null)
        {
            var buf = new MeshBuffers(curves != null ? curves.Count : 1);
            var infos = new List<PathInfo>(curves != null ? curves.Count : 0);

            if (curves != null)
            {
                for (int i = 0; i < curves.Count; i++)
                {
                    var c = curves[i];
                    if (!c.IsValid) continue;

                    int vStart = buf.verts.Count;
                    int tStart = buf.tris.Count;

                    Frame[] fo = (framesPerPath != null && i < framesPerPath.Length)
                        ? framesPerPath[i] : null;

                    SweepBuilder.Append(c, cfg, buf, pathId: i, framesOverride: fo);

                    infos.Add(new PathInfo(
                        i,
                        vStart, buf.verts.Count - vStart,
                        tStart, buf.tris.Count - tStart,
                        c.totalLength));
                }
            }

            SweepBuilder.Flush(buf, mesh);
            return infos.ToArray();
        }

        /// <summary>便捷重载:直接吃任意曲线来源(路点 / 最短路 / …)。</summary>
        public static PathInfo[] Build(ICurveSource source, SweepConfig cfg,
                                       Mesh mesh, Frame[][] framesPerPath = null)
            => Build(source != null ? source.GetCurves() : null, cfg, mesh, framesPerPath);
    }
}
