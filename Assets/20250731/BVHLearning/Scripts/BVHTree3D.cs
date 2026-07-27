using System.Collections.Generic;
using UnityEngine;

public struct Point3D
{
    public float x, y, z;

    public Point3D(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
}

public struct AABB3D
{
    public float minX, minY, minZ;
    public float maxX, maxY, maxZ;

    public AABB3D(float minX, float minY, float minZ,
                  float maxX, float maxY, float maxZ)
    {
        this.minX = minX;
        this.minY = minY;
        this.minZ = minZ;
        this.maxX = maxX;
        this.maxY = maxY;
        this.maxZ = maxZ;
    }

    public bool Contains(Point3D point)
    {
        return point.x >= minX && point.x <= maxX &&
               point.y >= minY && point.y <= maxY &&
               point.z >= minZ && point.z <= maxZ;
    }

    public bool Intersects(AABB3D region)
    {
        return !(region.maxX < minX || region.minX > maxX ||
                 region.maxY < minY || region.minY > maxY ||
                 region.maxZ < minZ || region.minZ > maxZ);
    }
}

public class BVHNode3D
{
    public AABB3D boundingBox;
    public List<Point3D> points;
    public BVHNode3D leftNode, rightNode;

    public bool isLeaf => leftNode == null && rightNode == null;
}

public class BVHTree3D
{
    public BVHNode3D root;
    private static int MAX_POINTS_PER_NODE = 4;

    public BVHTree3D(List<Point3D> points)
    {
        root = BuildTree(points);
    }

    private BVHNode3D BuildTree(List<Point3D> points)
    {
        BVHNode3D node = new BVHNode3D();
        node.boundingBox = CalculateAABB(points);

        if (points.Count <= MAX_POINTS_PER_NODE)
        {
            node.points = new List<Point3D>(points);
            return node;
        }

        var (leftPoints, rightPoints) = SplitPoints(points);
        node.leftNode = BuildTree(leftPoints);
        node.rightNode = BuildTree(rightPoints);

        return node;
    }

    private (List<Point3D> left, List<Point3D> right) SplitPoints(List<Point3D> points)
    {
        // 计算每个轴的范围，选择最长的轴进行分割
        (float min, float max, int axis) = GetLongestAxis(points);

        float mid = (min + max) / 2.0f;
        var left = new List<Point3D>();
        var right = new List<Point3D>();

        // 根据最长轴分割点
        foreach (var point in points)
        {
            float value = axis switch
            {
                0 => point.x,
                1 => point.y,
                _ => point.z
            };

            if (value <= mid)
                left.Add(point);
            else
                right.Add(point);
        }

        // 处理分割后某一侧为空的情况
        if (left.Count == 0 || right.Count == 0)
        {
            left.Clear();
            right.Clear();
            for (int i = 0; i < points.Count; i++)
            {
                if (i < points.Count / 2)
                    left.Add(points[i]);
                else
                    right.Add(points[i]);
            }
        }

        return (left, right);
    }

    // 找到最长的轴（x=0, y=1, z=2）
    private (float min, float max, int axis) GetLongestAxis(List<Point3D> points)
    {
        float minX = points[0].x, maxX = points[0].x;
        float minY = points[0].y, maxY = points[0].y;
        float minZ = points[0].z, maxZ = points[0].z;

        foreach (var point in points)
        {
            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
            minY = Mathf.Min(minY, point.y);
            maxY = Mathf.Max(maxY, point.y);
            minZ = Mathf.Min(minZ, point.z);
            maxZ = Mathf.Max(maxZ, point.z);
        }

        float sizeX = maxX - minX;
        float sizeY = maxY - minY;
        float sizeZ = maxZ - minZ;

        float maxSize = Mathf.Max(sizeX, sizeY, sizeZ);

        if (maxSize == sizeX) return (minX, maxX, 0);
        if (maxSize == sizeY) return (minY, maxY, 1);
        return (minZ, maxZ, 2);
    }

    private AABB3D CalculateAABB(List<Point3D> points)
    {
        if (points.Count == 0)
            return new AABB3D(0, 0, 0, 0, 0, 0);

        float minX = points[0].x, minY = points[0].y, minZ = points[0].z;
        float maxX = points[0].x, maxY = points[0].y, maxZ = points[0].z;

        foreach (var point in points)
        {
            minX = Mathf.Min(minX, point.x);
            minY = Mathf.Min(minY, point.y);
            minZ = Mathf.Min(minZ, point.z);
            maxX = Mathf.Max(maxX, point.x);
            maxY = Mathf.Max(maxY, point.y);
            maxZ = Mathf.Max(maxZ, point.z);
        }

        return new AABB3D(minX, minY, minZ, maxX, maxY, maxZ);
    }

    public List<Point3D> Query(AABB3D region)
    {
        List<Point3D> result = new List<Point3D>();
        QueryNode(root, region, result);
        return result;
    }

    private void QueryNode(BVHNode3D node, AABB3D region, List<Point3D> result)
    {
        if (node == null) return;

        if (!node.boundingBox.Intersects(region)) return;

        if (node.isLeaf)
        {
            if (node.points != null)
            {
                foreach (var point in node.points)
                {
                    if (region.Contains(point))
                        result.Add(point);
                }
            }
            return;
        }

        QueryNode(node.leftNode, region, result);
        QueryNode(node.rightNode, region, result);
    }

    public void Clear()
    {
        ClearNode(root);
        root = null;
    }

    private void ClearNode(BVHNode3D node)
    {
        if (node == null) return;

        if (node.isLeaf)
        {
            node.points?.Clear();
            node.points = null;
        }
        else
        {
            ClearNode(node.leftNode);
            ClearNode(node.rightNode);
            node.leftNode = null;
            node.rightNode = null;
        }
    }
}
