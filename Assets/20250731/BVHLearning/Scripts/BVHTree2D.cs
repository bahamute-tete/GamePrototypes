
using System.Collections.Generic;
using UnityEngine;


public struct Point2D
{
    public float x, y;

    public Point2D(float x, float y)
    {
        this.x = x;
        this.y = y;
    }
}

public struct AABB
{ 
    public float minX, minY, maxX, maxY;
    public AABB(float minX, float minY, float maxX, float maxY)
    {
        this.minX = minX;
        this.minY = minY;
        this.maxX = maxX;
        this.maxY = maxY;
    }
    public bool Contains(Point2D point)
    {
        return point.x >= minX && point.x <= maxX && point.y >= minY && point.y <= maxY;
    }

    public bool Intersects(AABB region)
    {
        bool intersects = !(region.maxX < minX || region.minX > maxX || region.maxY < minY || region.minY > maxY);
        //Debug.Log($"相交检查: 节点AABB({minX}, {minY}, {maxX}, {maxY}) vs 查询区域({region.minX}, {region.minY}, {region.maxX}, {region.maxY}) = {intersects}");
        return intersects;
    }

}



public enum SplitAxis
{
    X, 
    Y 
}

public class BVHNode2D
{
    public AABB boundingBox;
    public List<Point2D> points;
    public BVHNode2D leftNode, rightNode;

    public bool isLeaf=> leftNode==null && rightNode == null;

}

public class BVHTree2D
{ 
    public BVHNode2D node;

    public int MaxCountPerNode { get; private set; }
    public SplitAxis Axis { get; private set; }

    //private static int MAX_POINTS_PER_NODE = 4; // Maximum number of points per node

    public BVHTree2D(List<Point2D> points)
    {
        node = BuildTree(points);
    }

    private BVHNode2D BuildTree(List<Point2D> points, int maxCountPerNode = 4,SplitAxis splitAxis= SplitAxis.X)
    {
        BVHNode2D node = new BVHNode2D();
        node.boundingBox = CaculateAABB(points);
        MaxCountPerNode = maxCountPerNode;

        if (points.Count <= MaxCountPerNode)
        { 
            node.points = new List<Point2D>(points);
            //Debug.Log($"创建叶子节点，包含 {points.Count} 个点");
            return node;
        }

        var(leftPoints, rightPoints) = SplitPoints(points);
        
        //Debug.Log($"分割节点：总点数={points.Count}, 左={leftPoints.Count}, 右={rightPoints.Count}");

        // 创建左右子节点
        node.leftNode = BuildTree(leftPoints);
        node.rightNode = BuildTree(rightPoints);

        return node;
    }

    private (List<Point2D> leftPoints, List<Point2D> rightPoints) SplitPoints(List<Point2D> points)
    {
        float min, max, mid;

        if (Axis == SplitAxis.X)
        {
            min = points[0].x; max = points[0].x;

            foreach (var point in points)
            {
                if (point.x < min) min = point.x;
                if (point.x > max) max = point.x;
            }

            mid = (min + max) / 2.0f;
        }
        else
        {
            min = points[0].y; max = points[0].y;

            foreach (var point in points)
            {
                if (point.y < min) min = point.y;
                if (point.y > max) max = point.y;
            }

            mid = (min + max) / 2.0f;
        }



        var leftPoints = new List<Point2D>();
        var rightPoints = new List<Point2D>();

        if (Axis == SplitAxis.X)
        {
            foreach (var point in points)
            {
                if (point.x <= mid)
                    leftPoints.Add(point);
                else
                    rightPoints.Add(point);
            }
        }
        else
        {
            foreach (var point in points)
            {
                if (point.y <= mid)
                    leftPoints.Add(point);
                else
                    rightPoints.Add(point);
            }
        }

        //if some node is  empty, we will split the points in half
        if (leftPoints.Count == 0 || rightPoints.Count == 0)
        {
            leftPoints.Clear();
            rightPoints.Clear();

            for (int i = 0; i < points.Count; i++)
            {
                if (i < points.Count / 2)
                {
                    leftPoints.Add(points[i]);
                }
                else
                {
                    rightPoints.Add(points[i]);
                }
            }
        }


        return (leftPoints, rightPoints);
    }

    private AABB CaculateAABB(List<Point2D> points)
    {
        if (points.Count ==0)
            return new AABB(0, 0, 0, 0);

        float minX = points[0].x, minY = points[0].y;
        float maxX = points[0].x, maxY = points[0].y;

        foreach (var point in points)
        { 
            minX = Mathf.Min(minX, point.x);
            minY = Mathf.Min(minY, point.y);
            maxX = Mathf.Max(maxX, point.x);
            maxY = Mathf.Max(maxY, point.y);
        }

        return new AABB(minX, minY, maxX, maxY);
    }


    public List<Point2D> Query(AABB region)
    {
        List<Point2D> result = new List<Point2D>();
        //Debug.Log($"开始BVH查询，查询区域: ({region.minX}, {region.minY}) to ({region.maxX}, {region.maxY})");
        QueryNode(node, region, result);
        //Debug.Log($"BVH查询完成，找到 {result.Count} 个点");
        return result;
    }


    private void QueryNode(BVHNode2D node, AABB region, List<Point2D> result)
    {
        if (node == null) 
        {
            Debug.Log("节点为空，返回");
            return;
        }

        //Debug.Log($"检查节点包围盒: ({node.boundingBox.minX}, {node.boundingBox.minY}) to ({node.boundingBox.maxX}, {node.boundingBox.maxY})");
        
        //if (!node.boundingBox.Intersects(region)) 
        //{
        //    Debug.Log("包围盒不相交，跳过该节点");
        //    return;
        //}

        //Debug.Log("包围盒相交，继续检查");

        if (node.isLeaf)
        {
            //Debug.Log($"到达叶子节点，包含 {(node.points?.Count ?? 0)} 个点");
            
            if (node.points != null)
            {
                foreach (var point in node.points)
                {
                    //Debug.Log($"检查叶子节点中的点: ({point.x}, {point.y})");
                    if (region.Contains(point))
                    {
                        //Debug.Log($"点在查询区域内，添加到结果: ({point.x}, {point.y})");
                        result.Add(point);
                    }
                    //else
                    //{
                    //    Debug.Log($"点不在查询区域内: ({point.x}, {point.y})");
                    //}
                }
            }
            else
            {
                Debug.Log("叶子节点的points为空！");
            }
            return;
        }
        else
        {
            //Debug.Log("非叶子节点，递归查询子节点");
            QueryNode(node.leftNode, region, result);
            QueryNode(node.rightNode, region, result);
        }
    }

    public void Clear()
    {
        ClearNode(node);
        node =null;
    }

    private void ClearNode(BVHNode2D node)
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



