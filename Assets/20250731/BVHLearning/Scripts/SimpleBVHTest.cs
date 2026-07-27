
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


public class SimpleBVHTest : MonoBehaviour
{
    BVHTree2D bvhTree;

    public int length = 10; // Number of points to generate
    List<Point2D> points = new List<Point2D>();
    private AABB boundingBox;


    [Header("可视化设置")]
    public bool showPoints = true;
    public bool showBVHNodes = true;
    public bool showQueryRegion = true;
    public bool showQueryResults = true;
    public float pointSize = 0.1f;
    public int maxDepthToShow = 10;

    private List<Point2D> queryResults = new List<Point2D>();

    Vector3 currentMousPos;
    // Start is called before the first frame update
    void Start()
    {

        Vector3 screenPos = Input.mousePosition;
        screenPos.z = 10.0f;

        currentMousPos = screenPos;

        for (int i = 0; i < length; i++)
        {
            float x = Random.Range(0f, 10f);
            float y = Random.Range(0f, 10f);

            var point = new Point2D(x, y);

            points.Add(point);
        }

        bvhTree = new BVHTree2D(points);
       
    }

    // Update is called once per frame
    void Update()
    {

        Vector3 screenPos = Input.mousePosition;
        screenPos.z = 10.0f;
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(screenPos);


        if (screenPos != currentMousPos)
        {
            queryResults.Clear();


            boundingBox = new AABB(mousePos.x, mousePos.y, mousePos.x + 2.0f, mousePos.y + 2.0f);
            //boundingBox = new AABB(region.min.x, region.min.y, region.max.x, region.max.y);
            queryResults = bvhTree.Query(boundingBox);

          
            currentMousPos = screenPos;
        }

    }

    void OnDrawGizmos()
    {
        
        if (showPoints && points != null)
        {
            Gizmos.color = Color.blue;
            foreach (var point in points)
            {
                Gizmos.DrawSphere(new Vector3(point.x, point.y, 0), pointSize);
            }
        }

      
        if (showQueryResults && queryResults != null)
        {
            Gizmos.color = Color.green;
            foreach (var point in queryResults)
            {
                Gizmos.DrawSphere(new Vector3(point.x,point.y,0.1f ), pointSize * 1.5f);
            }

#if UNITY_EDITOR
            if (queryResults.Count != 0)
            { 
                GUIStyle style = new GUIStyle();
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 14;
                style.normal.textColor = Color.green;

                Handles.Label(new Vector3(0,0,0), $"Found {queryResults.Count} points", style);
            }

#endif
        }

        if (showBVHNodes && bvhTree != null)
        {
            DrawBVHNode(bvhTree.node, 0);
        }

  
        if (showQueryRegion)
        {
            Gizmos.color = Color.yellow;


            Vector3 screenPos = Input.mousePosition;
            screenPos.z = 10.0f;
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(screenPos);

            Vector3 center = new Vector3(mousePos.x+1.0f, mousePos.y+1.0f, 0.05f );
            Vector3 size = new Vector3(2.0f,2.0f,0.1f );
            Gizmos.DrawWireCube(center, size);
        }
    }

    void DrawBVHNode(BVHNode2D node, int depth)
    {
        if (node == null || depth > maxDepthToShow) return;

        
        float t = (float)depth / maxDepthToShow;
        Gizmos.color = Color.Lerp(Color.green, Color.magenta, t);
        
        
        Vector3 center = new Vector3(
            (node.boundingBox.minX + node.boundingBox.maxX) * 0.5f,
            (node.boundingBox.minY + node.boundingBox.maxY) * 0.5f,
             depth * 0.1f
        );
        
        Vector3 size = new Vector3(
            node.boundingBox.maxX - node.boundingBox.minX,
            node.boundingBox.maxY - node.boundingBox.minY,
             0.05f
        );
        
        Gizmos.DrawWireCube(center, size);

        
        if (node.leftNode != null)
            DrawBVHNode(node.leftNode, depth + 1);
        if (node.rightNode != null)
            DrawBVHNode(node.rightNode, depth + 1);
    }
}
        
