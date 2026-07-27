
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class ShapeForGJK : MonoBehaviour
{
    public List<Vector3> shapePoints= new List<Vector3>();
    private List<Vector3> vertices = new List<Vector3>();
   // public List<Vector3> Vertices => vertices;

    // 1. 添加缓存列表
    private List<Vector3> _cachedWorldVertices = new List<Vector3>();

    private Vector3[] _cachedPositionsArray; // 2. 添加缓存数组

    public bool isManual = false;

    [Min(3)]
    public int expectVertexCount = 10;
    public float minRange = 1.0f;
    public float maxRange = 2.0f;

    public Color lineColor = Color.green;

    LineRenderer lineRenderer;
    private Material lineMaterial;

    // Start is called before the first frame update
    void Start()
    {
        RendererSetting();

        if (isManual)
        {
            MannualSetPoints();
        }
        else
        {
            CreateConvexShape(expectVertexCount, minRange, maxRange);
        }
            

        UpdateShapeVisual();
    }

    private void RendererSetting()
    {
        lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        if (lineMaterial == null)
        {
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
        }
        lineRenderer.material = lineMaterial;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
        lineRenderer.loop = true;
    }

    private void Update()
    {
        UpdateShapeVisual();
    }


    public void SetManualPoints(List<Vector3> points)
    {
        shapePoints = new List<Vector3>(points);
        isManual = true;
        MannualSetPoints();
        UpdateShapeVisual();

    }
    private void MannualSetPoints()
    {
        vertices.Clear();
        if (shapePoints.Count < 3)
        {
            Debug.Log("Simplex at least need 3 points");
        }
        else
        {
            vertices.AddRange(shapePoints);
        }

        //顶点顺序调整为凸包
        //MakeConvex(vertices);

    }

    private void UpdateShapeVisual()
    {
        if (lineRenderer != null && vertices.Count > 0)
        {
             GetVerticesWorldPosition();

             if (_cachedPositionsArray == null || _cachedPositionsArray.Length != _cachedWorldVertices.Count)
            {
                _cachedPositionsArray = new Vector3[_cachedWorldVertices.Count];
            }

            // 将 List 数据复制到 Array，不产生 GC
             _cachedWorldVertices.CopyTo(_cachedPositionsArray);

            lineRenderer.positionCount = vertices.Count;
            lineRenderer.SetPositions(_cachedPositionsArray);

            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;
        }
    }

    public List<Vector3> GetVerticesWorldPosition()
    { 
        if (_cachedWorldVertices.Count != vertices.Count)
        {
            _cachedWorldVertices.Clear();
            for (int i = 0; i < vertices.Count; i++)
            {
                _cachedWorldVertices.Add(Vector3.zero);
            }
        }

         for (int i = 0; i < vertices.Count; i++)
        {
            _cachedWorldVertices[i] = transform.TransformPoint(vertices[i]);
        }
        return _cachedWorldVertices;
    }

    public void CreateConvexShape(int expectVertexCount, float minRange, float maxRange)
    {
        vertices.Clear();
        List<float> angles = new List<float>();

        
        for (int i = 0; i < expectVertexCount; i++)
        {
            angles.Add(Random.Range(0f, 360f));
        }

        angles.Sort();

        for (int i = 0; i < angles.Count; i++)
        {
            
            if (i > 0 && angles[i] - angles[i - 1] < 1.0f) continue;
            
            float angleRad = angles[i] * Mathf.Deg2Rad;

            float r = Random.Range(minRange, maxRange);

            Vector3 vertex = new Vector3(Mathf.Cos(angleRad) * r, Mathf.Sin(angleRad) * r,0 );
            vertices.Add(vertex);
        }

        MakeConvex(vertices);
    }

    private static void MakeConvex(List<Vector3> vertices)
    {
        if (vertices.Count < 3) return;

        bool changed = true;
        while (changed && vertices.Count >= 3)
        {
            changed = false;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p0 = vertices[i];
                Vector3 p1 = vertices[(i + 1) % vertices.Count];
                Vector3 p2 = vertices[(i + 2) % vertices.Count];

                Vector3 edge1 = p1 - p0;
                Vector3 edge2 = p2 - p1;

                
                float crossZ = edge1.x * edge2.y - edge1.y * edge2.x;

                // 假设逆时针生成的，向左拐 (Cross > 0) 如果 Cross < 0，移除中间点
                if (crossZ < 0)
                {
                    vertices.RemoveAt((i + 1) % vertices.Count);
                    changed = true;
                    break; 
                }
            }
        }
    }


    private void OnDrawGizmos()
    {
       if (isManual && shapePoints.Count > 3)
       {
           vertices.Clear();
           vertices.AddRange(shapePoints);
       }

       if (vertices.Count < 3) return;

       //MakeConvex(vertices);

       Gizmos.color = lineColor;
       for (int i = 0; i < vertices.Count; i++)
       {
           Vector3 currentVertex = transform.TransformPoint(vertices[i]);
           Vector3 nextVertex = transform.TransformPoint(vertices[(i + 1) % vertices.Count]);
           Gizmos.DrawLine(currentVertex, nextVertex);
       }
    }
}
