using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JacobianVisualizer : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int gridResolution = 10;
    [SerializeField] private Vector2 uvMin = Vector2.zero;
    [SerializeField] private Vector2 uvMax = Vector2.one;
    
    [Header("Visualization")]
    [SerializeField] private bool showPoints = true;
    [SerializeField] private bool showCells = true;
    [SerializeField] private bool showVectors = true;
    
    [Header("Prefabs")]
    [SerializeField] private GameObject gridPointPrefab;
    [SerializeField] private GameObject gridCellPrefab;
    
    [Header("Function")]
    [SerializeField] private FunctionType functionType = FunctionType.Polar;
    
    private JacobianFunction currentFunction;
   
    private List<GridPoint> gridPoints = new List<GridPoint>();
    private List<GridCellVisualizer> gridCells = new List<GridCellVisualizer>();
    
    public enum FunctionType
    {
        Identity,Polar,Spherical,Hyperbolic,LogarithmicSpiral,Twist,ScaleRotate,
    }


    public float a = 0.1f; // Parameter for Logarithmic Spiral
    public float strength = 0.5f; // Parameter for Twist
    public float scaleX = 0.5f; // Parameter for ScaleRotate
    public float scaleY = 0.5f; // Parameter for ScaleRotate
    public float angle = Mathf.PI / 4; // Parameter for ScaleRotate

    
    void Start()
    {
        InitializeFunction();
        GenerateGrid();
    }

    // void Update()
    // {
    //     if (Input.GetKeyDown(KeyCode.R))
    //     {
    //         RegenerateGrid();
    //     }

    //     if (Input.GetKeyDown(KeyCode.V))
    //     {
    //         showVectors = !showVectors;
    //         UpdateVisualization();
    //     }
    // }

    private void InitializeFunction()
    {
        switch (functionType)
        {
            case FunctionType.Identity:
                currentFunction = new IdentityFunction();
                break;

            case FunctionType.Polar:
                currentFunction = new PolarFunction();
                break;

            case FunctionType.Spherical:
                currentFunction = new SphericalFunction();
                break;

            case FunctionType.Hyperbolic:
                currentFunction = new HyperbolicFunction();
                break;

            case FunctionType.LogarithmicSpiral:
                currentFunction = new LogarithmicSpiralFunction();
                var parameters = currentFunction.GetParameters();
                currentFunction.SetParameter("a", a);
                break;

            case FunctionType.Twist:
                currentFunction = new TwistFunction();
                var parametersTwist = currentFunction.GetParameters();
                currentFunction.SetParameter("strength", strength);
                break;

            case FunctionType.ScaleRotate:
                currentFunction = new ScaleRotateFunction();
                var parametersSR = currentFunction.GetParameters();
                currentFunction.SetParameter("scaleX", scaleX);
                currentFunction.SetParameter("scaleY", scaleY);
                currentFunction.SetParameter("angle", angle);
                break;
        }
    }
    
    private void GenerateGrid()
    {
        ClearGrid();

        // 生成网格点
        for (int i = 0; i <= gridResolution; i++)
        {
            for (int j = 0; j <= gridResolution; j++)
            {
                float u = Mathf.Lerp(uvMin.x, uvMax.x, i / (float)gridResolution);
                float v = Mathf.Lerp(uvMin.y, uvMax.y, j / (float)gridResolution);

                if (showPoints && gridPointPrefab != null)
                {
                    CreateGridPoint(new Vector2(u, v));
                }
            }
        }


        // 生成网格单元
        if (showCells && gridCellPrefab != null)
        {
           for (int i = 0; i < gridResolution; i++)
           {
               for (int j = 0; j < gridResolution; j++)
               {
                   float u1 = Mathf.Lerp(uvMin.x, uvMax.x, i / (float)gridResolution);
                   float v1 = Mathf.Lerp(uvMin.y, uvMax.y, j / (float)gridResolution);
                   float u2 = Mathf.Lerp(uvMin.x, uvMax.x, (i + 1) / (float)gridResolution);
                   float v2 = Mathf.Lerp(uvMin.y, uvMax.y, (j + 1) / (float)gridResolution);

                   Vector2 uv1 = new Vector2(u1, v1);
                   Vector2 uv2 = new Vector2(u2, v1);
                   Vector2 uv3 = new Vector2(u2, v2);
                   Vector2 uv4 = new Vector2(u1, v2);

                   CreateGridCell(uv1, uv2, uv3, uv4);
               }
           }
        }
    }

    private void CreateGridPoint(Vector2 uv)
    {
        GameObject pointObj = Instantiate(gridPointPrefab, transform);
        pointObj.transform.localScale = Vector3.one * (uvMax.x - uvMin.x) / gridResolution * 0.1f;
        GridPoint point = pointObj.GetComponent<GridPoint>();
        point.uvCoord = uv;
        point.UpdateVisualization(currentFunction, showVectors);
        gridPoints.Add(point);
    }

    private void CreateGridCell(Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector2 uv4)
    {
       GameObject cellObj = Instantiate(gridCellPrefab, transform);
       GridCellVisualizer cell = cellObj.GetComponent<GridCellVisualizer>();
       cell.CreateCell(uv1, uv2, uv3, uv4, currentFunction);
       gridCells.Add(cell);
    }


    private void ClearGrid()
    {
        foreach (var point in gridPoints)
        {
            if (point != null) Destroy(point.gameObject);
        }
        gridPoints.Clear();

        foreach (var cell in gridCells)
        {
           if (cell != null) Destroy(cell.gameObject);
        }
        gridCells.Clear();
    }
    
    private void RegenerateGrid()
    {
        InitializeFunction();
        GenerateGrid();
    }
    
    private void OnValidate()
    {
        if (Application.isPlaying && currentFunction != null)
        {
            RegenerateGrid();
        }
    }
}
