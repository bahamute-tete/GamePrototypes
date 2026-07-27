using System.Collections.Generic;
using UnityEngine;

namespace LiangZhu.Geometry
{
    /// <summary>
    /// 由 Mesh 构建的无向加权图:
    ///   节点 = 按位置焊接(weld)后的顶点(消除 UV/法线接缝造成的重复,否则接缝处断路);
    ///   边   = 三角形的边(去重);
    ///   权   = 端点欧氏距离。
    /// 邻接采用 CSR(compressed sparse row)布局,Dijkstra 内循环 cache 友好。
    /// 构建一次后缓存复用;由调用方在 mesh/transform 变化时重建(参照 IRayAccelerator 失效策略)。
    /// </summary>
    public sealed class MeshGraph
    {
        public int NodeCount { get; private set; }

        // CSR
        private int[] _neighborStart;   // 长度 NodeCount+1
        private int[] _neighbors;       // 长度 2*edgeCount
        private float[] _weights;       // 与 _neighbors 对齐
        private Vector3[] _positions;   // 节点位置(与构建空间一致)

        // 吸附用空间哈希:cellKey -> 落在该 cell 的所有节点(同时服务 weld 与 Snap)
        private Dictionary<Vector3Int, List<int>> _cellToNodes;
        private float _cellSize;

        public Vector3 Position(int node) => _positions[node];
        public int NeighborStart(int node) => _neighborStart[node];
        public int NeighborEnd(int node) => _neighborStart[node + 1];
        public int NeighborAt(int i) => _neighbors[i];
        public float WeightAt(int i) => _weights[i];

        /// <summary>
        /// 由 Mesh 构建。localToWorld 给定时节点存世界坐标,吸附/输出也在世界空间;
        /// 默认 identity 即 mesh 局部空间。weldEpsilon 为焊接容差。
        /// </summary>
        public static MeshGraph Build(Mesh mesh, float weldEpsilon = 1e-4f,
                                      Matrix4x4? localToWorld = null)
        {
            var g = new MeshGraph();
            g.BuildInternal(mesh, weldEpsilon, localToWorld ?? Matrix4x4.identity);
            return g;
        }

        private void BuildInternal(Mesh mesh, float weld, Matrix4x4 m)
        {
            if (weld <= 0f) weld = 1e-4f;
            _cellSize = weld;

            var verts = mesh.vertices;            // 局部坐标
            var tris = mesh.triangles;            // 已展平所有 submesh
            int vCount = verts.Length;

            // ---- 1) 焊接:原始顶点 index -> welded node index ----
            _cellToNodes = new Dictionary<Vector3Int, List<int>>(vCount);
            var remap = new int[vCount];
            var nodePosList = new List<Vector3>(vCount);

            for (int i = 0; i < vCount; i++)
            {
                Vector3 p = m.MultiplyPoint3x4(verts[i]);
                int node = FindOrAddNode(p, nodePosList);
                remap[i] = node;
            }

            NodeCount = nodePosList.Count;
            _positions = nodePosList.ToArray();

            // ---- 2) 收集无向边(去重),统计度数 ----
            var edgeSet = new HashSet<long>();
            var degree = new int[NodeCount];

            void TryAddEdge(int a, int b)
            {
                if (a == b) return;
                int lo = a < b ? a : b;
                int hi = a < b ? b : a;
                long key = ((long)lo << 32) | (uint)hi;
                if (edgeSet.Add(key)) { degree[a]++; degree[b]++; }
            }

            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int w0 = remap[tris[t]], w1 = remap[tris[t + 1]], w2 = remap[tris[t + 2]];
                TryAddEdge(w0, w1);
                TryAddEdge(w1, w2);
                TryAddEdge(w2, w0);
            }

            // ---- 3) 前缀和 -> CSR 起点,再填充邻居 ----
            _neighborStart = new int[NodeCount + 1];
            for (int n = 0; n < NodeCount; n++)
                _neighborStart[n + 1] = _neighborStart[n] + degree[n];

            int totalDir = _neighborStart[NodeCount]; // = 2 * edgeCount
            _neighbors = new int[totalDir];
            _weights = new float[totalDir];

            var cursor = new int[NodeCount];
            System.Array.Copy(_neighborStart, cursor, NodeCount);

            foreach (long key in edgeSet)
            {
                int a = (int)(key >> 32);
                int b = (int)(key & 0xffffffff);
                float w = Vector3.Distance(_positions[a], _positions[b]);

                int ia = cursor[a]++; _neighbors[ia] = b; _weights[ia] = w;
                int ib = cursor[b]++; _neighbors[ib] = a; _weights[ib] = w;
            }
        }

        private Vector3Int Key(in Vector3 p) => new Vector3Int(
            Mathf.RoundToInt(p.x / _cellSize),
            Mathf.RoundToInt(p.y / _cellSize),
            Mathf.RoundToInt(p.z / _cellSize));

        // weld:查 27 个邻接 cell,命中容差内的已有节点则复用,否则新建。
        private int FindOrAddNode(in Vector3 p, List<Vector3> posList)
        {
            Vector3Int k = Key(p);
            float sqEps = _cellSize * _cellSize;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        var nk = new Vector3Int(k.x + dx, k.y + dy, k.z + dz);
                        if (!_cellToNodes.TryGetValue(nk, out var list)) continue;
                        for (int j = 0; j < list.Count; j++)
                        {
                            int node = list[j];
                            if ((posList[node] - p).sqrMagnitude <= sqEps) return node;
                        }
                    }
            int idx = posList.Count;
            posList.Add(p);
            if (!_cellToNodes.TryGetValue(k, out var cell))
            {
                cell = new List<int>(1);
                _cellToNodes[k] = cell;
            }
            cell.Add(idx);
            return idx;
        }

        /// <summary>
        /// 把世界/局部坐标吸附到最近图节点;超出 maxDistance 返回 -1。
        /// anchor 通常只有寥寥几个,直接线性扫全部节点即可(10^4 级别是微秒量级)。
        /// 切勿用 weld 网格(cell = weldEpsilon,极小)按 maxDistance 折算搜索半径——
        /// 那会让 cell 半径炸到上千、三重循环到数十亿次而卡死。
        /// </summary>
        public int Snap(in Vector3 p, float maxDistance)
        {
            float bestSq = maxDistance * maxDistance;
            int best = -1;
            for (int i = 0; i < NodeCount; i++)
            {
                float sq = (_positions[i] - p).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = i; }
            }
            return best;
        }

        public Vector3[] PathPositions(int[] nodes)
        {
            var pts = new Vector3[nodes.Length];
            for (int i = 0; i < nodes.Length; i++) pts[i] = _positions[nodes[i]];
            return pts;
        }
    }
}
