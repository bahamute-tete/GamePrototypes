using System.Collections.Generic;
using UnityEngine;

namespace LiangZhu.Geometry
{
    /// <summary>
    /// 多源 Dijkstra。一次 Run 以一组源点(cost=0)出发,求每个节点到最近源点的最短代价,
    /// 并记录 prev(前驱)与 root(所属源点)。scratch 数组一次性分配、跨 query 复用,
    /// 用递增 epoch 做懒清零,避免每趟 O(V) memset。
    /// </summary>
    public sealed class MultiSourceDijkstra
    {
        private readonly MeshGraph _g;
        private readonly BinaryMinHeap _heap;

        private readonly float[] _dist;
        private readonly int[] _prev;
        private readonly int[] _root;
        private readonly int[] _epoch;     // _epoch[n]==_cur 表示本趟已触及
        private int _cur;

        public MultiSourceDijkstra(MeshGraph graph)
        {
            _g = graph;
            int n = graph.NodeCount;
            _dist = new float[n];
            _prev = new int[n];
            _root = new int[n];
            _epoch = new int[n];      // 初值 0,故 _cur 从 1 开始
            _heap = new BinaryMinHeap(Mathf.Max(16, n / 4));
            _cur = 0;
        }

        public bool Visited(int node) => _epoch[node] == _cur;
        public float Dist(int node) => _epoch[node] == _cur ? _dist[node] : float.PositiveInfinity;
        public int Root(int node) => _epoch[node] == _cur ? _root[node] : -1;

        /// <summary>
        /// 跑一趟。targets 非 null 时:stopAtFirstTarget=true 第一个 target 出堆即停(Any→Any);
        /// 否则等所有 target 出堆即停(Each→Each 单趟早退)。targets 为 null 则跑满整图。
        /// </summary>
        public void Run(IReadOnlyList<int> sources,
                        HashSet<int> targets = null,
                        bool stopAtFirstTarget = false,
                        IEdgeCostFunction cost = null)
        {
            _cur++;
            _heap.Clear();

            for (int i = 0; i < sources.Count; i++)
            {
                int s = sources[i];
                if (s < 0) continue;
                if (_epoch[s] == _cur && _dist[s] <= 0f) continue; // 重复源点
                _epoch[s] = _cur;
                _dist[s] = 0f;
                _prev[s] = -1;
                _root[s] = s;
                _heap.Push(s, 0f);
            }

            int remaining = targets?.Count ?? -1;

            while (_heap.TryPop(out int u, out float du))
            {
                if (du > _dist[u]) continue; // stale(lazy deletion)

                if (targets != null && targets.Contains(u))
                {
                    if (stopAtFirstTarget) return;
                    if (--remaining == 0) return;
                }

                // 进入 u 的方向,供 turn cost 使用(基础模式忽略)
                bool hasIn = _prev[u] >= 0;
                Vector3 uPos = _g.Position(u);
                Vector3 inDir = hasIn ? (uPos - _g.Position(_prev[u])).normalized : Vector3.zero;

                int end = _g.NeighborEnd(u);
                for (int e = _g.NeighborStart(u); e < end; e++)
                {
                    int v = _g.NeighborAt(e);
                    float w = _g.WeightAt(e);
                    float step = cost == null
                        ? w
                        : cost.Evaluate(u, v, w, uPos, _g.Position(v), inDir, hasIn);
                    float nd = du + step;

                    if (_epoch[v] != _cur || nd < _dist[v])
                    {
                        _epoch[v] = _cur;
                        _dist[v] = nd;
                        _prev[v] = u;
                        _root[v] = _root[u];
                        _heap.Push(v, nd);
                    }
                }
            }
        }

        /// <summary>
        /// 从 fromNode 沿 prev 回溯到其 root,返回 [fromNode, ..., root] 的节点链。
        /// 不可达返回 null。
        /// </summary>
        public int[] TraceFrom(int fromNode, out int root, out float cost)
        {
            root = -1; cost = float.PositiveInfinity;
            if (_epoch[fromNode] != _cur) return null;

            cost = _dist[fromNode];
            var chain = new List<int>(16);
            int n = fromNode;
            while (n >= 0)
            {
                chain.Add(n);
                if (_prev[n] < 0) { root = n; break; }
                n = _prev[n];
            }
            return chain.ToArray();
        }
    }
}
