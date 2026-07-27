using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public static class GJKAlgorithm
{ 
    public static List<Vector3> simplex = new List<Vector3>();
    public static Vector3 currentDir = Vector3.forward;
    public static Vector3 lastSupportPoint = Vector3.zero;

    private const int GJK_ITERATIONS = 64;
    private const float EPA_TOLERANCE = 0.0001f;
    private const int EPA_MAX_ITERATIONS = 32;

    #region  Shape Definition
    private static Vector3 GetSupportPoint(Vector3 center,float radius,Vector3 dir)
    {
       if (dir.sqrMagnitude<0.001f) return center;
       return center + Vector3.Normalize(dir) * radius;
    }
    private static Vector3 GetSupportPoint(List<Vector3> vertices, Vector3 direction)
    {
        Vector3 furthestPoint = Vector3.zero;
        float maxDot = float.MinValue;
        bool isFirst = true;

        foreach (var v in vertices)
        {
            float dot = Vector3.Dot(v, direction);
            if (dot > maxDot || isFirst)
            {
                maxDot = dot;
                furthestPoint = v;
                isFirst = false;
            }
        }
        return furthestPoint;
    }
    public interface IGJKShape
    {
        Vector3 GetSupportPoint(Vector3 direction);
        Vector3 GetCenter();
    }


    public struct SphereShape : IGJKShape
    {
        public Vector3 center;
        public float radius;

        public SphereShape(Vector3 center, float radius)
        {
            this.center = center;
            this.radius = radius;
        }

        public Vector3 GetSupportPoint(Vector3 direction)
        {
            return GJKAlgorithm.GetSupportPoint(center, radius, direction);
        }

        public Vector3 GetCenter()
        {
            return center;
        }
    }

    public struct PolygonShape : IGJKShape
    {
        public List<Vector3> vertices;

        public PolygonShape(List<Vector3> vertices)
        {
            this.vertices = vertices;
        }

        public Vector3 GetSupportPoint(Vector3 direction)
        {
           return GJKAlgorithm.GetSupportPoint(vertices, direction);
        }

        public Vector3 GetCenter()
        {
            if (vertices.Count == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            foreach (var v in vertices)
            {
                sum += v;
            }
            return sum / vertices.Count;
        }
    }
    #endregion

    #region Minkowski Difference Visualization
    // 闵可夫斯基差
    public static List<Vector3> GetMinkowskiDifferenceVertices(List<Vector3> vertsA, List<Vector3> vertsB)
    {
       
        List<Vector3> diffPoints = new List<Vector3>();

        foreach (var va in vertsA)
        {
            foreach (var vb in vertsB)
            {
                diffPoints.Add(va - vb);
            }
        }
        // 计算凸包
        //计算所有顶点对的差（va - vb）时，会得到一大堆点（点云）。
        //只有最外圈的点构成了闵可夫斯基差的“形状”。
        return GetConvexHull(diffPoints);
    }

    // 2D凸包 (Monotone Chain Algorithm)
    private static List<Vector3> GetConvexHull(List<Vector3> points)
    {
        if (points.Count <= 2) return points;

        // 按 X 排序，如果 X 相同按 Y 排序
        points.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

        List<Vector3> hull = new List<Vector3>();

        // 下凸包
        List<Vector3> lower = new List<Vector3>();
        foreach (var p in points)
        {
            while (lower.Count >= 2 && CrossProduct2D(lower[lower.Count - 2], lower[lower.Count - 1], p) <= 0)
            {
                lower.RemoveAt(lower.Count - 1);
            }
            lower.Add(p);
        }

        // 上凸包
        List<Vector3> upper = new List<Vector3>();
        for (int i = points.Count - 1; i >= 0; i--)
        {
            var p = points[i];
            while (upper.Count >= 2 && CrossProduct2D(upper[upper.Count - 2], upper[upper.Count - 1], p) <= 0)
            {
                upper.RemoveAt(upper.Count - 1);
            }
            upper.Add(p);
        }

        // 合并（移除重复的起点/终点）
        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);

        hull.AddRange(lower);
        hull.AddRange(upper);

        return hull;
    }

    // 返回值大于0表示C在AB左侧（逆时针/左转）
    // 返回值小于0表示C在AB右侧（顺时针/右转）
    // 返回值等于0表示三点共线
    private static float CrossProduct2D(Vector3 o, Vector3 a, Vector3 b)
    {
        return (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
    }

    #endregion

    #region GJK Implementation
   
    public static Vector3 GetMinkowskiSupport(IGJKShape shapeA, IGJKShape shapeB, Vector3 direction)
    {
        Vector3 pointA = shapeA.GetSupportPoint(direction);
        Vector3 pointB = shapeB.GetSupportPoint(-direction);
        return pointA - pointB;
    }

    public static bool GJK(IGJKShape shapeA, IGJKShape shapeB, bool is2D = false)
    {
        simplex.Clear();

        // 2D 模式下，强制 Z 轴为 0
        if (is2D) currentDir.z = 0;

        // 简单的初始方向选择：取两个形状的第一个点之差
        if (shapeA.GetCenter() == Vector3.zero || shapeB.GetCenter() == Vector3.zero) return false;
        Vector3 startA = shapeA.GetCenter();
        Vector3 startB = shapeB.GetCenter();

        currentDir = Vector3.Normalize(startB - startA);
        
        if (currentDir == Vector3.zero) currentDir = Vector3.right;

        lastSupportPoint = GetMinkowskiSupport(shapeA, shapeB, currentDir);

        simplex.Add(lastSupportPoint);

        currentDir =Vector3.zero -lastSupportPoint;

        for (int iteration = 0; iteration < GJK_ITERATIONS; iteration++)
        {
            // 2D 模式下确保搜索方向在平面内
            if (is2D) currentDir.z = 0;
            
            lastSupportPoint = GetMinkowskiSupport(shapeA, shapeB, currentDir);

            // 如果新加入的点沿搜索方向没有越过原点，说明原点不可能在单纯形内
            if (Vector3.Dot(lastSupportPoint, currentDir) < 0)
            {
                return false;
            }

            simplex.Add(lastSupportPoint);

            if (HandleSimplex(ref currentDir, is2D))
            {
                return true;
            }
        }

        return false;
        
    }

    private static bool HandleSimplex(ref Vector3 currentDir, bool is2D = false)
    {
       switch (simplex.Count)
        {
            case 2:
                return HandleLine(ref currentDir);
            case 3:
                return HandleTriangle(ref currentDir,is2D);
            case 4:
                return HandleTetrahedron(ref currentDir);
            default:
                return false;
        }
    }

    private static bool HandleLine(ref Vector3 currentDir)
    {
       Vector3 b= simplex[0];
       Vector3 a = simplex[1];

        Vector3 ab = b - a;
        Vector3 ao = -a;

        // 需要一个垂直于 ab 且朝向原点的方向
        // 三重叉乘 (ab x ao) x ab 可以得到这个方向，但在 3D 中更简单的是直接判断投影
        // 在 3D 中可能得到零向量如果 ao 和 ab 平行。
        // 3D GJK 中，Line Case 的方向其实可以保留原来的三重叉乘逻辑，只要确保 ab 和 ao 不共线。

        Vector3 abPrep = Vector3.Cross(Vector3.Cross(ab, ao), ab);

        // 如果 abPrep 接近 0 (原点在线段上)，则任意取一个垂直方向
        if (abPrep.sqrMagnitude < 0.0001f)
        {
             // 原点就在线段上，视为碰撞（或者退化）
             return true; 
        }


        currentDir = abPrep;

        return false;
       
    }

    private static bool HandleTriangle(ref Vector3 currentDir,bool is2D =false)
    {
        Vector3 c = simplex[0];
        Vector3 b = simplex[1];
        Vector3 a = simplex[2];

        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 ao = -a;

        Vector3 abc = Vector3.Cross(ab, ac);
        // 边法线（在三角形平面内，垂直于边，向外）
        Vector3 abPrep = Vector3.Cross(ab, abc);
        Vector3 acPrep = Vector3.Cross(abc, ac);
        
        if (Vector3.Dot(abPrep, ao) > 0)
        {
            simplex.Remove(c);
            currentDir = Vector3.Cross(Vector3.Cross(ab, ao), ab);
            return false;
        }

        

        if (Vector3.Dot(acPrep, ao) > 0)
        {
            simplex.Remove(b);
            currentDir = Vector3.Cross(Vector3.Cross(ac, ao), ac);
            return false;
        }

        if (is2D)
        {
            //2D:原点在三角形内
            return true;
        }
        else
        {
            // 3D:我们需要构建四面体，返回 false 继续寻找
            // 如果都不在外侧，说明原点在三角形法线方向的上方或下方
            if (Vector3.Dot(abc, ao) > 0)
            {
                currentDir = abc;
            }
            else
            {
                // 原点在三角形的另一侧，翻转法线方向
                Vector3 temp = simplex[0];
                simplex[0] = simplex[1];
                simplex[1] = temp;
                currentDir = -abc;
            }
            return false;
        }
    }

    private static bool HandleTetrahedron(ref Vector3 currentDir)
    {
        Vector3 d = simplex[0];
        Vector3 c = simplex[1];
        Vector3 b = simplex[2];
        Vector3 a = simplex[3];// 最新加入的点

        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 ad = d - a;
        Vector3 ao = -a;

         // 计算三个新面的法线 (ABC, ACD, ADB) 法线指向四面体外部
        Vector3 abc = Vector3.Cross(ab, ac);
        Vector3 acd = Vector3.Cross(ac, ad);
        Vector3 adb = Vector3.Cross(ad, ab);

        // ABC 面
        if (Vector3.Dot(abc, ao) > 0)
        {
            simplex.Remove(d);
            currentDir = abc;
            return false;
        }

        // ACD 面
        if (Vector3.Dot(acd, ao) > 0)
        {
            simplex.Remove(b);
            currentDir = acd;
            return false;
        }

        // ADB 面
        if (Vector3.Dot(adb, ao) > 0)
        {
            simplex.Remove(c);
            currentDir = adb;
            return false;
        }

        return true;
    }

    #endregion

    #region EPA Implementation
    public static bool EPA(IGJKShape shapeA, IGJKShape shapeB, out Vector3 normal, out float depth, bool is2D = false)
    {
        if (is2D)
        {
            return EPA2D(shapeA, shapeB, out normal, out depth);
        }
        else
        {
            return EPA3D(shapeA, shapeB, out normal, out depth);
        }
    }

    // ----------------------------------------------------------------------------------
    // 2D EPA 实现
    // ----------------------------------------------------------------------------------
    private static bool EPA2D(IGJKShape shapeA, IGJKShape shapeB, out Vector3 normal, out float depth)
    {
        normal = Vector3.zero;
        depth = 0;

        // 1. 初始化多边形 (Polytope)
        // GJK 结束时，simplex 是一个包含原点的三角形（在 2D 情况下）
        List<Vector3> polytope = new List<Vector3>(simplex);

        // 确保 winding order (虽然下面的逻辑主要依赖法线计算，但保持一致是个好习惯)
        // 这里我们主要依赖计算出的边法线是否背离原点

        for (int i = 0; i < EPA_MAX_ITERATIONS; i++)
        {
            // 2. 寻找距离原点最近的边
            int closestEdgeIndex = -1;
            float minDistance = float.MaxValue;
            Vector3 minNormal = Vector3.zero;

            for (int j = 0; j < polytope.Count; j++)
            {
                Vector3 a = polytope[j];
                Vector3 b = polytope[(j + 1) % polytope.Count];

                Vector3 edge = b - a;
                
                // 计算指向多边形外部的法线
                // 在 2D (XY平面) 中，向量 (x, y) 的垂线是 (y, -x) 或 (-y, x)
                // 我们需要确保法线指向原点外侧。
                // 因为原点在多边形内部，所以 法线 dot 顶点 > 0
                
                Vector3 n = new Vector3(edge.y, -edge.x, 0).normalized;
                float dist = Vector3.Dot(n, a);

                // 如果距离是负的，说明法线指反了（指向了原点），翻转它
                if (dist < 0)
                {
                    n = -n;
                    dist = -dist;
                }

                if (dist < minDistance)
                {
                    minDistance = dist;
                    minNormal = n;
                    closestEdgeIndex = j;
                }
            }

            // 3. 沿最近边的法线方向寻找新的支撑点
            Vector3 support = GetMinkowskiSupport(shapeA, shapeB, minNormal);

            // 4. 计算新支撑点到原点的距离（沿法线方向）
            float supportDist = Vector3.Dot(support, minNormal);

            // 5. 终止条件：如果新点没有比当前边更远（在误差范围内），说明我们已经找到了最近的边界
            if (Mathf.Abs(supportDist - minDistance) < EPA_TOLERANCE)
            {
                normal = minNormal;
                depth = supportDist;
                return true;
            }

            // 6. 扩展多边形：将新点插入到最近边的两个顶点之间
            polytope.Insert(closestEdgeIndex + 1, support);
        }

        return false;
    }

     // ----------------------------------------------------------------------------------
    // 3D EPA 实现
    // ----------------------------------------------------------------------------------
    
    // 内部类：表示 3D 多胞体的一个面
    private class EPAFace
    {
        public Vector3 a, b, c;
        public Vector3 normal;
        public float distance;

        public EPAFace(Vector3 a, Vector3 b, Vector3 c)
        {
            this.a = a; this.b = b; this.c = c;
            // 计算法线 (右手定则)
            normal = Vector3.Cross(b - a, c - a).normalized;
            distance = Vector3.Dot(normal, a);

            // 确保法线背离原点 (原点在内部，所以距离应为正)
            if (distance < 0)
            {
                normal = -normal;
                distance = -distance;
                // 交换 b 和 c 以维持绕序（可选，但在某些渲染或逻辑中很重要）
                Vector3 temp = this.b; this.b = this.c; this.c = temp;
            }
        }
    }

    // 内部结构：表示一条边，用于构建地平线 (Horizon)
    private struct EPAEdge
    {
        public Vector3 a, b;
        public EPAEdge(Vector3 a, Vector3 b) { this.a = a; this.b = b; }
        
        // 重写 Equals 以便在 HashSet 或比较中使用
        // 我们认为 (A,B) 和 (B,A) 是同一条边（无向边），或者在构建地平线时通过计数来处理
        public override bool Equals(object obj)
        {
            if (!(obj is EPAEdge)) return false;
            EPAEdge other = (EPAEdge)obj;
            return (a == other.a && b == other.b) || (a == other.b && b == other.a);
        }
        public override int GetHashCode()
        {
            return a.GetHashCode() ^ b.GetHashCode();
        }
    }

    private static bool EPA3D(IGJKShape shapeA, IGJKShape shapeB, out Vector3 normal, out float depth)
    {
        normal = Vector3.zero;
        depth = 0;

        // 1. 初始化多胞体 (Polytope)
        // GJK 结束时，simplex 是一个四面体 (4个点)
        List<EPAFace> faces = new List<EPAFace>();
        
        // 构建初始的 4 个面
        // 顶点顺序：0,1,2,3
        faces.Add(new EPAFace(simplex[0], simplex[1], simplex[2]));
        faces.Add(new EPAFace(simplex[0], simplex[2], simplex[3]));
        faces.Add(new EPAFace(simplex[0], simplex[3], simplex[1]));
        faces.Add(new EPAFace(simplex[1], simplex[3], simplex[2]));

        for (int i = 0; i < EPA_MAX_ITERATIONS; i++)
        {
            // 2. 寻找距离原点最近的面
            EPAFace closestFace = null;
            float minDist = float.MaxValue;

            foreach (var face in faces)
            {
                if (face.distance < minDist)
                {
                    minDist = face.distance;
                    closestFace = face;
                }
            }

            if (closestFace == null) break; // 异常情况

            // 3. 沿最近面法线方向寻找支撑点
            Vector3 support = GetMinkowskiSupport(shapeA, shapeB, closestFace.normal);

            // 4. 检查终止条件
            float supportDist = Vector3.Dot(support, closestFace.normal);
            if (Mathf.Abs(supportDist - minDist) < EPA_TOLERANCE)
            {
                normal = closestFace.normal;
                depth = supportDist;
                return true;
            }

            // 5. 扩展多胞体
            // 找到所有能“看到”新支撑点的面（即法线方向与 新点-面顶点 夹角锐角）
            // 这些面将被移除，并在其边缘构建新的面连接到支撑点
            List<EPAFace> facesToRemove = new List<EPAFace>();
            
            // 使用边列表来寻找“地平线” (Horizon)
            // 地平线是指：被移除面 和 保留面 之间的交界线
            // 简单的算法：统计所有被移除面的边，如果一条边只出现一次，它就是地平线边；如果出现两次（两个被移除面共用），它是内部边。
            List<EPAEdge> edges = new List<EPAEdge>();

            foreach (var face in faces)
            {
                // 判断点是否在面法线的前方 (Dot > 0)
                if (Vector3.Dot(face.normal, support - face.a) > EPA_TOLERANCE)
                {
                    facesToRemove.Add(face);
                    // 添加该面的三条边
                    AddEdge(edges, face.a, face.b);
                    AddEdge(edges, face.b, face.c);
                    AddEdge(edges, face.c, face.a);
                }
            }

            // 移除可见面
            foreach (var face in facesToRemove)
            {
                faces.Remove(face);
            }

            // 6. 构建新的面
            // 剩下的 edges 列表中，只出现一次的边就是地平线
            // 我们需要从地平线向新支撑点构建新面
            foreach (var edge in edges)
            {
                faces.Add(new EPAFace(edge.a, edge.b, support));
            }
        }

        return false;
    }

    // 辅助方法：添加边到列表，如果边已存在（说明是两个被移除面共用的内部边），则移除它
    // 这样最后剩下的就是只属于一个被移除面的边（即地平线边）
    private static void AddEdge(List<EPAEdge> edges, Vector3 a, Vector3 b)
    {
        EPAEdge edge = new EPAEdge(a, b);
        // 查找是否存在反向边或同向边（因为我们只关心几何位置）
        // 在这里简单的列表查找即可，因为数量很少
        int index = edges.FindIndex(e => e.Equals(edge));
        
        if (index != -1)
        {
            // 如果找到了，说明这条边被两个“可见面”共享，它是内部边，不是地平线，移除它
            edges.RemoveAt(index);
        }
        else
        {
            // 没找到，暂时加入
            edges.Add(edge);
        }
    }

    #endregion
}