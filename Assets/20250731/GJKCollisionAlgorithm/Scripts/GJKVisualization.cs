using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GJKVisualization : MonoBehaviour
{

    public ShapeForGJK shapeA,shapeB;

    private LineRenderer simplexRenderer;
    private LineRenderer directionRenderer;
    private LineRenderer axisRenderer;
    private LineRenderer minkowskiRenderer;
    private LineRenderer normalRenderer;

    private bool collisionDetected = false;

    public bool start = false;
    public bool isShowInfo = true;
    private bool isRunning = false;

    public float stepDelay = 1.0f;

    private float depth=0f;
    private Vector3 normal = Vector3.zero;

    public TextMeshProUGUI gjk_result;
    public TextMeshProUGUI epa_normal;
    public TextMeshProUGUI epa_depth;
    // Start is called before the first frame update
    void Start()
    {
        RendererSetting();
    }

    private void RendererSetting()
    {
        GameObject simplexObj = new GameObject("SimplexRenderer");
        simplexRenderer = simplexObj.AddComponent<LineRenderer>();
        simplexRenderer.material = new Material(Shader.Find("Sprites/Default"));
        simplexRenderer.startColor = Color.yellow;
        simplexRenderer.endColor = Color.yellow;
        simplexRenderer.startWidth = 0.03f;
        simplexRenderer.endWidth = 0.03f;


        GameObject directionObj = new GameObject("DirectionRenderer");
        directionRenderer = directionObj.AddComponent<LineRenderer>();
        directionRenderer.material = new Material(Shader.Find("Sprites/Default"));
        directionRenderer.startColor = Color.blue;
        directionRenderer.endColor = Color.blue;
        directionRenderer.startWidth = 0.02f;
        directionRenderer.endWidth = 0.02f;


        GameObject axisObj = new GameObject("AxisRenderer");
        axisRenderer = axisObj.AddComponent<LineRenderer>();
        axisRenderer.material = new Material(Shader.Find("Sprites/Default"));
        Color axisColor = new Color(0.5f, 0.5f, 0.5f, 0.3f); // 淡灰色，半透明
        axisRenderer.startColor = axisColor;
        axisRenderer.endColor = axisColor;
        axisRenderer.startWidth = 0.02f;
        axisRenderer.endWidth = 0.02f;
        axisRenderer.positionCount = 4;
        axisRenderer.useWorldSpace = true;

        //minkowski difference renderer
        GameObject minkowskiObj = new GameObject("MinkowskiRenderer");
        minkowskiRenderer = minkowskiObj.AddComponent<LineRenderer>();
        minkowskiRenderer.material = new Material(Shader.Find("Sprites/Default"));
        Color minkowskiColor = new Color(0.0f, 1.0f, 1.0f, 0.5f); // 青色，半透明
        minkowskiRenderer.startColor = minkowskiColor;
        minkowskiRenderer.endColor = minkowskiColor;
        minkowskiRenderer.startWidth = 0.02f;
        minkowskiRenderer.endWidth = 0.02f;
        minkowskiRenderer.loop = true;


        GameObject normalObj = new GameObject("EPANormalRenderer");
        normalRenderer = normalObj.AddComponent<LineRenderer>();
        normalRenderer.material = new Material(Shader.Find("Sprites/Default"));
        Color normalColor = new Color(1.0f, 1.0f, 1.0f, 0.5f);
        normalRenderer.startColor = normalColor;
        normalRenderer.endColor = normalColor;
        normalRenderer.startWidth = 0.02f;
        normalRenderer.endWidth = 0.02f;
    }

    // Update is called once per frame
    void Update()
    {
        var posA = shapeA.GetVerticesWorldPosition();
        var posB = shapeB.GetVerticesWorldPosition();

        GJKAlgorithm.PolygonShape shapeAGjk = new GJKAlgorithm.PolygonShape(posA);
        GJKAlgorithm.PolygonShape shapeBGjk = new GJKAlgorithm.PolygonShape(posB);
 
        collisionDetected = GJKAlgorithm.GJK(shapeAGjk, shapeBGjk, is2D: true);

        if (collisionDetected)
            (depth,normal) = GJKAlgorithm.EPA(shapeAGjk, shapeBGjk, out normal, out depth, is2D: true) ? (depth, normal) : (0f, Vector3.zero); 

        if (gjk_result)
        {
            gjk_result.text = collisionDetected ? "Collision Detected" : "No Collision";
        }

        if (epa_depth)
        {
            epa_depth.text = collisionDetected ? $"{depth:F2}" : "Null";
        }

        if (epa_normal)
        {
            epa_normal.text = collisionDetected ? $"({normal.x:F2},{normal.y:F2},{normal.z:F2})":"Null";
        }

        if (isShowInfo)
            UpdateVisulization();
    }



    void UpdateVisulization()
    {
        DrawAxis();

        DrawMinkowskiDifference();

        if (collisionDetected)
        {
            DrawSimplex();
            DrawSearchDirection();
            DrawEPANormal();
        }
        else
        {
            simplexRenderer.positionCount = 0;
            directionRenderer.positionCount = 0;
            normalRenderer.positionCount = 0;
        }
    }


    private void DrawAxis()
    {
        float length = 100f; 
        axisRenderer.positionCount = 4;
        // X轴 (从左到右)
        axisRenderer.SetPosition(0, Vector3.left * length);
        axisRenderer.SetPosition(1, Vector3.right * length);
        axisRenderer.positionCount = 5;
        axisRenderer.SetPosition(0, Vector3.left * length);
        axisRenderer.SetPosition(1, Vector3.right * length);
        axisRenderer.SetPosition(2, Vector3.zero); 
        axisRenderer.SetPosition(3, Vector3.up * length);
        axisRenderer.SetPosition(4, Vector3.down * length);
    }
 

    private void DrawMinkowskiDifference()
    {
        var points = GJKAlgorithm.GetMinkowskiDifferenceVertices(shapeA.GetVerticesWorldPosition(), shapeB.GetVerticesWorldPosition());
        minkowskiRenderer.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
        {
            minkowskiRenderer.SetPosition(i, points[i]);
        }
    }
   
    private void DrawSimplex()
    {
        
            var simplex = GJKAlgorithm.simplex;
            simplexRenderer.positionCount = simplex.Count;

            for (int i = 0; i < simplex.Count; i++)
            {
                simplexRenderer.SetPosition(i, simplex[i]);
            }

            simplexRenderer.loop = simplex.Count == 3 ? true : false;
        
       
       
    }

    private void DrawSearchDirection()
    {
        if (GJKAlgorithm.simplex.Count == 3)
        {
            Vector3 lastPnt = GJKAlgorithm.simplex[GJKAlgorithm.simplex.Count - 1];
            Vector3 dir = GJKAlgorithm.currentDir.normalized;

            directionRenderer.positionCount = 2;
            directionRenderer.SetPosition(0, lastPnt);
            directionRenderer.SetPosition(1, lastPnt + dir*2.0f);
        }
    }


    private void DrawEPANormal()
    {
        if (collisionDetected)
        {
            Vector3 lastPnt = Vector3.zero;
            Vector3 dir = normal;

            normalRenderer.positionCount = 2;
            normalRenderer.SetPosition(0, lastPnt);
            normalRenderer.SetPosition(1, lastPnt + dir * depth);
        }
    }

}
