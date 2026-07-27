using System.Collections.Generic;
using UnityEngine;

namespace LiangZhu.Geometry
{
    /// <summary>
    /// 配对模式,语义与 Houdini Find Shortest Path 的 Search 一致。
    /// </summary>
    public enum PathPairing
    {
        /// <summary>所有 start / end 里全局最短的一对,产出 1 条。</summary>
        AnyStartToAnyEnd,
        /// <summary>每个 start 连到它最近的 end,产出 N_start 条。</summary>
        EachStartToAnyEnd,
        /// <summary>每个 end 连到它最近的 start,产出 N_end 条。</summary>
        AnyStartToEachEnd,
        /// <summary>全配对,产出 N_start × N_end 条。</summary>
        EachStartToEachEnd
    }

    /// <summary>
    /// 边代价函数。基础实现只返回边长;turn cost 等以后通过 incomingDir 接入,
    /// 接入时无需改动求解器调用方。求解器在此为 null 时走快路径(直接用边长)。
    /// </summary>
    public interface IEdgeCostFunction
    {
        /// <param name="fromNode">当前节点 u</param>
        /// <param name="toNode">邻居节点 v</param>
        /// <param name="edgeLength">u-v 的欧氏边长(base weight)</param>
        /// <param name="fromPos">u 的位置</param>
        /// <param name="toPos">v 的位置</param>
        /// <param name="incomingDir">进入 u 的单位方向;hasIncoming 为 false 时无意义(u 是源点)</param>
        /// <param name="hasIncoming">u 是否有来路</param>
        float Evaluate(int fromNode, int toNode, float edgeLength,
                       in Vector3 fromPos, in Vector3 toPos,
                       in Vector3 incomingDir, bool hasIncoming);
    }

    /// <summary>纯距离代价。基础模式直接用这个,或干脆传 null 走快路径。</summary>
    public sealed class DistanceCost : IEdgeCostFunction
    {
        public static readonly DistanceCost Instance = new DistanceCost();
        public float Evaluate(int fromNode, int toNode, float edgeLength,
                              in Vector3 fromPos, in Vector3 toPos,
                              in Vector3 incomingDir, bool hasIncoming) => edgeLength;
    }

    /// <summary>一次求解请求。坐标空间需与 MeshGraph 构建时一致(默认 mesh 局部空间)。</summary>
    public sealed class ShortestPathRequest
    {
        public readonly List<Vector3> StartPositions = new List<Vector3>();
        public readonly List<Vector3> EndPositions = new List<Vector3>();
        public PathPairing Pairing = PathPairing.AnyStartToEachEnd;

        /// <summary>吸附到最近图节点时允许的最大距离;超出则该锚点被丢弃并告警。</summary>
        public float SnapMaxDistance = 0.1f;

        /// <summary>null 时走快路径(纯边长)。</summary>
        public IEdgeCostFunction Cost = null;
    }

    /// <summary>一条解出的路径。Points 已是 start→end 顺序,可直接喂 resample/Sweep。</summary>
    public readonly struct PathResult
    {
        public readonly int StartNode;
        public readonly int EndNode;
        public readonly int[] Nodes;      // welded node index,start→end
        public readonly Vector3[] Points; // 对应世界/局部坐标,start→end
        public readonly float Cost;       // 累计代价
        public readonly bool Reachable;

        public PathResult(int startNode, int endNode, int[] nodes,
                          Vector3[] points, float cost, bool reachable)
        {
            StartNode = startNode; EndNode = endNode;
            Nodes = nodes; Points = points; Cost = cost; Reachable = reachable;
        }

        public static PathResult Unreachable(int s, int e) =>
            new PathResult(s, e, System.Array.Empty<int>(),
                           System.Array.Empty<Vector3>(), float.PositiveInfinity, false);
    }
}
