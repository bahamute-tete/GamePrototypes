
using System.Collections.Generic;
using UnityEngine;

public enum SeperateDir
{
    X,
    Y
}

public struct ShapeCenter
{
    public float x, y;

    public ShapeCenter(float x, float y)
    {
        this.x = x;
        this.y = y;
    }
}

public struct AABBRegion
{
    public float minX, minY, maxX, maxY;
    public AABBRegion(float minX, float minY, float maxX, float maxY)
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

    public bool Intersects(AABBRegion region)
    {
        bool intersects = !(region.maxX < minX || region.minX > maxX || region.maxY < minY || region.minY > maxY);
        return intersects;
    }

}

public struct BVHShape
{
    public AABBRegion aabb;
    public ShapeForGJK shape;

    public BVHShape(ShapeForGJK shape)
    {

        this.shape = shape;

        Vector3 center = shape.transform.position;
        float r = shape.maxRange;
        aabb = new AABBRegion(center.x - r, center.y - r, center.x + r, center.y + r);
    }
}

public class BVHNodeGJK
{
    public AABBRegion boundingBox;
    public List<BVHShape> objects;
    public BVHNodeGJK leftNode, rightNode;
    public bool isLeaf => leftNode == null && rightNode == null;
}


public class BVHTreeGJK
{
    public BVHNodeGJK rootNode;
    public int MaxCountPerNode { get; private set; }
    public BVHTreeGJK(List<ShapeForGJK> shapes)
    {
        List<BVHShape> allShapes = new List<BVHShape>(shapes.Count);

        foreach (var s in shapes)
        {
            allShapes.Add(new BVHShape(s));
        }

        rootNode = BuildTree(allShapes);
    }


    private BVHNodeGJK BuildTree(List<BVHShape> objects, int maxCountPerNode = 4, SplitAxis splitAxis = SplitAxis.X)
    {
        BVHNodeGJK node = new BVHNodeGJK();

        node.boundingBox = CaculateAABB(objects);

        MaxCountPerNode = maxCountPerNode;

        if (objects.Count <= MaxCountPerNode)
        {
            node.objects = new List<BVHShape>(objects);
            return node;
        }

        var (leftObjects, rightObjects) = SplitObjects(objects, splitAxis);



        node.leftNode = BuildTree(leftObjects);
        node.rightNode = BuildTree(rightObjects);
        return node;
    }
    private AABBRegion CaculateAABB(List<BVHShape> objects)
    {
        if (objects.Count == 0) return new AABBRegion(0, 0, 0, 0);

        float minX = objects[0].aabb.minX, minY = objects[0].aabb.minY;
        float maxX = objects[0].aabb.maxX, maxY = objects[0].aabb.maxY;

        foreach (var obj in objects)
        {
            minX = Mathf.Min(minX, obj.aabb.minX);
            minY = Mathf.Min(minY, obj.aabb.minY);

            maxX = Mathf.Max(maxX, obj.aabb.maxX);
            maxY = Mathf.Max(maxY, obj.aabb.maxY);
        }
        return new AABBRegion(minX, minY, maxX, maxY);
    }

    private (List<BVHShape> leftObjects, List<BVHShape> rightObjects) SplitObjects(List<BVHShape> objects, SplitAxis splitAxis)
    {
        float min = float.MaxValue, max = float.MinValue;

        foreach (var obj in objects)
        {
            float value = splitAxis == SplitAxis.X ? (obj.aabb.minX + obj.aabb.maxX) / 2.0f : (obj.aabb.minY + obj.aabb.maxY) / 2.0f;
            if (value < min) min = value;
            if (value > max) max = value;
        }

        float mid = (min + max) / 2.0f;

        var leftObjects = new List<BVHShape>();
        var rightObjects = new List<BVHShape>();

        foreach (var obj in objects)
        {
            float value = splitAxis == SplitAxis.X ? (obj.aabb.minX + obj.aabb.maxX) / 2.0f : (obj.aabb.minY + obj.aabb.maxY) / 2.0f;
            if (value <= mid)
                leftObjects.Add(obj);
            else
                rightObjects.Add(obj);
        }

        return (leftObjects, rightObjects);
    }

   

    public void GetCollisionPairs(List<ShapeForGJK> allShapes, HashSet<(int, int)> outPairs)
    {
        foreach (var shape in allShapes)
        {
            BVHShape obj = new BVHShape(shape);
            List<ShapeForGJK> candinates = new List<ShapeForGJK>();

            Query(rootNode, obj.aabb, candinates);

            foreach (var candinate in candinates)
            {
                if (shape.GetInstanceID() < candinate.GetInstanceID())
                {
                    outPairs.Add((shape.GetInstanceID(), candinate.GetInstanceID()));
                }
            }
        }
    }

    public void Query(BVHNodeGJK node, AABBRegion region, List<ShapeForGJK> result)
    {
        if (node == null || !node.boundingBox.Intersects(region)) return;

        if (node.isLeaf)
        {
            foreach (var obj in node.objects)
            {
                if (obj.aabb.Intersects(region))
                {
                    result.Add(obj.shape);
                }
            }
        }
        else
        {
            Query(node.leftNode, region, result);
            Query(node.rightNode, region, result);
        }
    }


    public void Clear()
    {
        ClearNode(rootNode);
        rootNode = null;
    }

    private void ClearNode(BVHNodeGJK node)
    {
        if (node == null) return;

        if (node.isLeaf)
        {
            node.objects?.Clear();
            node.objects = null;
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
