using System.Collections.Generic;
using UnityEngine;

namespace LiangZhu.Geometry
{
    /// <summary>
    /// 在 MeshGraph 上求最短路,支持 4 种配对模式(语义对齐 Houdini Find Shortest Path)。
    /// 图与 Dijkstra scratch 在构造时分配并复用;mesh/transform 变化时由外部重建本对象。
    ///
    /// 趟数:Any→Any / Each→Any / Any→Each 均为 1 趟多源;Each→Each 为 min(N_start, N_end) 趟。
    /// 所有输出 PathResult.Points / Nodes 均为 start→end 顺序。
    /// </summary>
    public sealed class ShortestPathSolver
    {
        private readonly MeshGraph _graph;
        private readonly MultiSourceDijkstra _dijkstra;

        public MeshGraph Graph => _graph;

        public ShortestPathSolver(MeshGraph graph)
        {
            _graph = graph;
            _dijkstra = new MultiSourceDijkstra(graph);
        }

        public static ShortestPathSolver FromMesh(Mesh mesh, float weldEpsilon = 1e-4f,
                                                  Matrix4x4? localToWorld = null)
            => new ShortestPathSolver(MeshGraph.Build(mesh, weldEpsilon, localToWorld));

        public List<PathResult> Solve(ShortestPathRequest req)
        {
            var results = new List<PathResult>();

            var starts = SnapAll(req.StartPositions, req.SnapMaxDistance, "start");
            var ends = SnapAll(req.EndPositions, req.SnapMaxDistance, "end");
            if (starts.Count == 0 || ends.Count == 0) return results;

            switch (req.Pairing)
            {
                case PathPairing.AnyStartToEachEnd: AnyToEach(starts, ends, req, results); break;
                case PathPairing.EachStartToAnyEnd: EachToAny(starts, ends, req, results); break;
                case PathPairing.AnyStartToAnyEnd: AnyToAny(starts, ends, req, results); break;
                case PathPairing.EachStartToEachEnd: EachToEach(starts, ends, req, results); break;
            }
            return results;
        }

        // ---- 每个 end 连最近 start:种子=starts,一趟 ----
        private void AnyToEach(List<int> starts, List<int> ends,
                               ShortestPathRequest req, List<PathResult> outp)
        {
            _dijkstra.Run(starts, cost: req.Cost);
            var endSet = ToSet(ends);
            foreach (int e in endSet)
            {
                var chain = _dijkstra.TraceFrom(e, out int root, out float c);
                outp.Add(chain == null
                    ? PathResult.Unreachable(-1, e)
                    : Make(root, e, chain, c, reverse: true)); // [e..start] -> start..end
            }
        }

        // ---- 每个 start 连最近 end:种子=ends,一趟(无向图距离对称)----
        private void EachToAny(List<int> starts, List<int> ends,
                               ShortestPathRequest req, List<PathResult> outp)
        {
            _dijkstra.Run(ends, cost: req.Cost);
            var startSet = ToSet(starts);
            foreach (int s in startSet)
            {
                var chain = _dijkstra.TraceFrom(s, out int root, out float c);
                outp.Add(chain == null
                    ? PathResult.Unreachable(s, -1)
                    : Make(s, root, chain, c, reverse: false)); // [s..end] 已是 start..end
            }
        }

        // ---- 全局最短的一对:种子=starts,targets=ends,首个 end 出堆即停 ----
        private void AnyToAny(List<int> starts, List<int> ends,
                              ShortestPathRequest req, List<PathResult> outp)
        {
            var endSet = ToSet(ends);
            _dijkstra.Run(starts, endSet, stopAtFirstTarget: true, cost: req.Cost);

            int bestEnd = -1; float best = float.PositiveInfinity;
            foreach (int e in endSet)
            {
                float d = _dijkstra.Dist(e);
                if (d < best) { best = d; bestEnd = e; }
            }
            if (bestEnd < 0) return;

            var chain = _dijkstra.TraceFrom(bestEnd, out int root, out float c);
            if (chain != null) outp.Add(Make(root, bestEnd, chain, c, reverse: true));
        }

        // ---- 全配对:从点数较少一侧逐个发起,min(N_s,N_e) 趟 ----
        private void EachToEach(List<int> starts, List<int> ends,
                                ShortestPathRequest req, List<PathResult> outp)
        {
            var startSet = ToSet(starts);
            var endSet = ToSet(ends);

            if (startSet.Count <= endSet.Count)
            {
                var single = new int[1];
                foreach (int s in startSet)
                {
                    single[0] = s;
                    _dijkstra.Run(single, endSet, cost: req.Cost); // 全 end settle 即早退
                    foreach (int e in endSet)
                    {
                        var chain = _dijkstra.TraceFrom(e, out _, out float c);
                        outp.Add(chain == null
                            ? PathResult.Unreachable(s, e)
                            : Make(s, e, chain, c, reverse: true)); // [e..s] -> s..e
                    }
                }
            }
            else
            {
                var single = new int[1];
                foreach (int e in endSet)
                {
                    single[0] = e;
                    _dijkstra.Run(single, startSet, cost: req.Cost);
                    foreach (int s in startSet)
                    {
                        var chain = _dijkstra.TraceFrom(s, out _, out float c);
                        outp.Add(chain == null
                            ? PathResult.Unreachable(s, e)
                            : Make(s, e, chain, c, reverse: false)); // [s..e] 已正序
                    }
                }
            }
        }

        // chain 是 [fromNode..root];reverse=true 时翻成 start→end。
        private PathResult Make(int startNode, int endNode, int[] chain,
                                float cost, bool reverse)
        {
            if (reverse) System.Array.Reverse(chain);
            var pts = _graph.PathPositions(chain);
            return new PathResult(startNode, endNode, chain, pts, cost, true);
        }

        private List<int> SnapAll(List<Vector3> positions, float maxDist, string label)
        {
            var nodes = new List<int>(positions.Count);
            for (int i = 0; i < positions.Count; i++)
            {
                int n = _graph.Snap(positions[i], maxDist);
                if (n < 0)
                    Debug.LogWarning($"[ShortestPath] {label}[{i}] {positions[i]} " +
                                     $"超出吸附距离 {maxDist},已忽略。");
                else
                    nodes.Add(n);
            }
            return nodes;
        }

        private static HashSet<int> ToSet(List<int> src)
        {
            var set = new HashSet<int>();
            for (int i = 0; i < src.Count; i++) set.Add(src[i]);
            return set;
        }
    }
}
