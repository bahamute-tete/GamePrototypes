using System;
using UnityEngine;

namespace LiangZhu.Geometry
{
    /// <summary>
    /// 有界体层次结构 (BVH)。中点分割构建，数组化节点，迭代遍历。
    /// bake 时构建一次，raycast / closest-point 都很快。实现 IRayAccelerator。
    /// </summary>
    public class Bvh : IRayAccelerator
    {
        struct Node
        {
            public Vector3 bmin, bmax;
            public int leftFirst; // internal: 左子节点下标; leaf: triIdx 起始
            public int count;     // 0 = internal; >0 = leaf 三角形数
            public bool IsLeaf => count > 0;
        }

        const int LeafThreshold = 4;
        const int MaxStack = 128;

        Vector3[] _v;
        int[] _tris;
        int[] _triIdx;
        Vector3[] _centroid, _triMin, _triMax;
        Node[] _nodes;
        int _nodeCount;

        public void Build(Vector3[] vertices, int[] triangles)
        {
            _v = vertices;
            _tris = triangles;
            int triCount = triangles.Length / 3;

            _triIdx = new int[triCount];
            _centroid = new Vector3[triCount];
            _triMin = new Vector3[triCount];
            _triMax = new Vector3[triCount];

            for (int i = 0; i < triCount; i++)
            {
                _triIdx[i] = i;
                Vector3 a = _v[_tris[i * 3 + 0]];
                Vector3 b = _v[_tris[i * 3 + 1]];
                Vector3 c = _v[_tris[i * 3 + 2]];
                _centroid[i] = (a + b + c) / 3f;
                _triMin[i] = Vector3.Min(Vector3.Min(a, b), c);
                _triMax[i] = Vector3.Max(Vector3.Max(a, b), c);
            }

            _nodes = new Node[Mathf.Max(2, triCount * 2)];
            _nodeCount = 0;

            int root = NewNode();
            _nodes[root].leftFirst = 0;
            _nodes[root].count = triCount;
            UpdateBounds(root);
            if (triCount > 0) Subdivide(root);
        }

        int NewNode()
        {
            if (_nodeCount >= _nodes.Length) Array.Resize(ref _nodes, _nodes.Length * 2);
            return _nodeCount++;
        }

        void UpdateBounds(int ni)
        {
            Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 mx = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            int first = _nodes[ni].leftFirst, count = _nodes[ni].count;
            for (int i = 0; i < count; i++)
            {
                int t = _triIdx[first + i];
                mn = Vector3.Min(mn, _triMin[t]);
                mx = Vector3.Max(mx, _triMax[t]);
            }
            _nodes[ni].bmin = mn;
            _nodes[ni].bmax = mx;
        }

        void Subdivide(int ni)
        {
            int count = _nodes[ni].count;
            if (count <= LeafThreshold) return;
            int first = _nodes[ni].leftFirst;

            // 质心包围盒最长轴的中点
            Vector3 cmin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 cmax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < count; i++)
            {
                Vector3 c = _centroid[_triIdx[first + i]];
                cmin = Vector3.Min(cmin, c);
                cmax = Vector3.Max(cmax, c);
            }
            Vector3 ext = cmax - cmin;
            int axis = ext.x > ext.y ? (ext.x > ext.z ? 0 : 2) : (ext.y > ext.z ? 1 : 2);
            float split = (Comp(cmin, axis) + Comp(cmax, axis)) * 0.5f;

            int i2 = first, j = first + count - 1;
            while (i2 <= j)
            {
                if (Comp(_centroid[_triIdx[i2]], axis) < split) i2++;
                else { int tmp = _triIdx[i2]; _triIdx[i2] = _triIdx[j]; _triIdx[j] = tmp; j--; }
            }
            int leftCount = i2 - first;
            if (leftCount == 0 || leftCount == count) leftCount = count / 2; // 退化：等数量

            int left = NewNode();
            int right = NewNode();
            _nodes[left].leftFirst = first;
            _nodes[left].count = leftCount;
            _nodes[right].leftFirst = first + leftCount;
            _nodes[right].count = count - leftCount;

            _nodes[ni].leftFirst = left; // 转内部节点
            _nodes[ni].count = 0;

            UpdateBounds(left);
            UpdateBounds(right);
            Subdivide(left);
            Subdivide(right);
        }

        static float Comp(Vector3 v, int a) => a == 0 ? v.x : (a == 1 ? v.y : v.z);

        public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, bool cullBackface, out RayHit hit)
        {
            hit = default;
            if (_nodes == null || _nodeCount == 0) return false;

            Vector3 dir = direction.normalized;
            Vector3 invDir = new Vector3(
                1f / (Mathf.Abs(dir.x) < 1e-20f ? 1e-20f : dir.x),
                1f / (Mathf.Abs(dir.y) < 1e-20f ? 1e-20f : dir.y),
                1f / (Mathf.Abs(dir.z) < 1e-20f ? 1e-20f : dir.z));

            float bestT = maxDistance;
            int bestTri = -1; float bestU = 0f, bestV = 0f;
            bool found = false;

            Span<int> stack = stackalloc int[MaxStack];
            int sp = 0;
            stack[sp++] = 0;

            while (sp > 0)
            {
                Node node = _nodes[stack[--sp]];
                if (!Intersection.RayAabb(origin, invDir, node.bmin, node.bmax, bestT, out float tnear)) continue;
                if (tnear > bestT) continue;

                if (node.IsLeaf)
                {
                    for (int i = 0; i < node.count; i++)
                    {
                        int t = _triIdx[node.leftFirst + i];
                        Vector3 v0 = _v[_tris[t * 3 + 0]];
                        Vector3 v1 = _v[_tris[t * 3 + 1]];
                        Vector3 v2 = _v[_tris[t * 3 + 2]];
                        if (Intersection.RayTriangle(origin, dir, v0, v1, v2, out float tt, out float uu, out float vv, cullBackface)
                            && tt < bestT)
                        {
                            bestT = tt; bestTri = t; bestU = uu; bestV = vv; found = true;
                        }
                    }
                }
                else if (sp + 2 <= MaxStack)
                {
                    stack[sp++] = node.leftFirst;
                    stack[sp++] = node.leftFirst + 1;
                }
            }

            if (found)
            {
                hit.hasHit = true;
                hit.distance = bestT;
                hit.point = origin + dir * bestT;
                hit.triangleIndex = bestTri;
                hit.barycentric = new Vector3(1f - bestU - bestV, bestU, bestV);
                Vector3 a = _v[_tris[bestTri * 3 + 0]], b = _v[_tris[bestTri * 3 + 1]], c = _v[_tris[bestTri * 3 + 2]];
                hit.normal = Vector3.Cross(b - a, c - a).normalized;
            }
            return found;
        }

        public bool ClosestPoint(Vector3 point, float maxDistance, out RayHit hit)
        {
            hit = default;
            if (_nodes == null || _nodeCount == 0) return false;

            float bestSq = maxDistance * maxDistance;
            int bestTri = -1; Vector3 bestPt = point;
            bool found = false;

            Span<int> stack = stackalloc int[MaxStack];
            int sp = 0;
            stack[sp++] = 0;

            while (sp > 0)
            {
                Node node = _nodes[stack[--sp]];
                if (AabbDistanceSq(point, node.bmin, node.bmax) > bestSq) continue;

                if (node.IsLeaf)
                {
                    for (int i = 0; i < node.count; i++)
                    {
                        int t = _triIdx[node.leftFirst + i];
                        Vector3 a = _v[_tris[t * 3 + 0]], b = _v[_tris[t * 3 + 1]], c = _v[_tris[t * 3 + 2]];
                        Vector3 cp = Intersection.ClosestPointOnTriangle(point, a, b, c);
                        float dsq = (cp - point).sqrMagnitude;
                        if (dsq < bestSq) { bestSq = dsq; bestTri = t; bestPt = cp; found = true; }
                    }
                }
                else if (sp + 2 <= MaxStack)
                {
                    stack[sp++] = node.leftFirst;
                    stack[sp++] = node.leftFirst + 1;
                }
            }

            if (found)
            {
                hit.hasHit = true;
                hit.point = bestPt;
                hit.distance = Mathf.Sqrt(bestSq);
                hit.triangleIndex = bestTri;
                Vector3 a = _v[_tris[bestTri * 3 + 0]], b = _v[_tris[bestTri * 3 + 1]], c = _v[_tris[bestTri * 3 + 2]];
                hit.normal = Vector3.Cross(b - a, c - a).normalized;
                hit.barycentric = Intersection.Barycentric(bestPt, a, b, c);
            }
            return found;
        }

        static float AabbDistanceSq(Vector3 p, Vector3 mn, Vector3 mx)
        {
            float dx = Mathf.Max(Mathf.Max(mn.x - p.x, 0f), p.x - mx.x);
            float dy = Mathf.Max(Mathf.Max(mn.y - p.y, 0f), p.y - mx.y);
            float dz = Mathf.Max(Mathf.Max(mn.z - p.z, 0f), p.z - mx.z);
            return dx * dx + dy * dy + dz * dz;
        }
    }
}
