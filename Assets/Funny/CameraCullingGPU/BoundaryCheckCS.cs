using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

public class BoundaryCheckCS : MonoBehaviour
{
    public ComputeShader computeShader;
    private ComputeBuffer buffer1, buffer2;

    public Material material;
    public Mesh mesh;


    public GameObject testObject;
    public Transform anchorPoint;

    const int maxResolution = 500;
    [SerializeField, Range(10, maxResolution)]
    public int resolution = 10;
    Camera camera => GetComponent<Camera>();


    Vector4[] spheresPos;
    int[] res;

    Vector4[] frustumPlanes = new Vector4[6];


    public Vector4 _WaveA = new Vector4(1f, 0f, 0.5f, 10f);
    public Vector4 _WaveB =new Vector4 (0f, 1f, 0.25f, 20f);
    public Vector4 _WaveC = new Vector4(1f, 1f, 0.15f, 10f);


    float minDistance = float.MaxValue;

    Vector2Int FindNearestIndexInPosDatasets(Vector4[] posDatasets,GameObject geo)
    {
        int index = 0;

        for (int i = 0; i < posDatasets.Length; i++)
        {
            Vector3 geoPos = geo.transform.position;
            Vector3 XZPos = new Vector3(geoPos.x, 0, geoPos.z);

            float distance = Vector3.Distance(XZPos, posDatasets[i]);


            if (distance < minDistance)
            {
                minDistance = distance;
                index = i;
            }

        }

        int x = index % resolution;
        int y = (index - x) / resolution;

        return new Vector2Int(x,y);
    }

    private void PrepareDataset(int resolution)
    {
        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                spheresPos[j + i * resolution] = new Vector4(j - resolution / 2, 0.0f, i - resolution / 2, 1.0f);
                res[j + i * resolution] = 0;
            }
        }
    }
    void Start()
    {
        int kernel = computeShader.FindKernel("CSMain");


        spheresPos = new Vector4[resolution * resolution+1];
        buffer1 = new ComputeBuffer(maxResolution * maxResolution, sizeof(float) * 4);

        res = new int[resolution * resolution];
        buffer2 = new ComputeBuffer(maxResolution * maxResolution, sizeof(int));

        //tempBuffer = new ComputeBuffer(1, sizeof(float) * 4);


        computeShader.SetInt(Shader.PropertyToID("resolution"), resolution);
        computeShader.SetVector(Shader.PropertyToID("_WaveA"), _WaveA);
        computeShader.SetVector(Shader.PropertyToID("_WaveB"), _WaveB);
        computeShader.SetVector(Shader.PropertyToID("_WaveC"), _WaveC);
        PrepareDataset(resolution);
        spheresPos[resolution * resolution] =new Vector4(anchorPoint.position.x, 0.0f, anchorPoint.position.z, 1.0f);

    }



    // Update is called once per frame
    void Update()
    {
        

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
        for (int i = 0; i < 6; i++)
        {
            frustumPlanes[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance);
            
        }

        computeShader.SetInt(Shader.PropertyToID("resolution"), resolution);
        computeShader.SetVector(Shader.PropertyToID("_WaveA"), _WaveA);
        computeShader.SetVector(Shader.PropertyToID("_WaveB"), _WaveB);
        computeShader.SetVector(Shader.PropertyToID("_WaveC"), _WaveC);

        PrepareDataset(resolution);
        spheresPos[resolution * resolution] = new Vector4(anchorPoint.position.x, 0.0f, anchorPoint.position.z, 1.0f);

        CameraFrusturmCullingGPU(frustumPlanes);

        
    }


    void CameraFrusturmCullingGPU(Vector4[] frustumPlanes)
    {
        computeShader.SetVectorArray(Shader.PropertyToID("_FP"), frustumPlanes);

        buffer1.SetData(spheresPos);
        computeShader.SetBuffer(0, "spherePos", buffer1);

        buffer2.SetData(res);
        computeShader.SetBuffer(0, "res", buffer2);

        computeShader.Dispatch(0, Mathf.CeilToInt(resolution / 8.0f), Mathf.CeilToInt(resolution / 8.0f), 1);

        material.SetBuffer(Shader.PropertyToID("_Positions"), buffer1);
        material.SetBuffer(Shader.PropertyToID("_Res"), buffer2);

        buffer1.GetData(spheresPos);
        Vector3 newPos = spheresPos[resolution * resolution];
        testObject.transform.position = newPos+new Vector3(0.0f,1.0f,0.0f);
        
        var bounds = new Bounds(Vector3.zero, Vector3.one * (resolution+1));
        Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, resolution*resolution+1);

        

    }

    public class QuadtreeNode
    {
        public Rect boundary;  // 节点的矩形边界
        public Vector2 center;  // 节点的中心
        public Vector3 data;  // 节点存储的数据
        public bool hasData;  // 节点是否包含数据
        public QuadtreeNode topLeft, topRight, bottomLeft, bottomRight;  // 子节点

        public QuadtreeNode(Rect boundary)
        {
            this.boundary = boundary;
            center = new Vector2(boundary.x + boundary.width / 2, boundary.y + boundary.height / 2);
            hasData = false;
        }
    }

    public class Quadtree
    {
        private const int Capacity = 4;  // 每个节点最大存储的数据数量
        private QuadtreeNode root;  // 树的根节点

        public Quadtree(Rect boundary)
        {
            root = new QuadtreeNode(boundary);
        }

        public void Insert(Vector2 point, Vector3 data)
        {
            Insert(root, point, data);
        }

        private void Insert(QuadtreeNode node, Vector2 point, Vector3 data)
        {
            if (!node.boundary.Contains(point))
            {
                return;
            }

            if (!node.hasData)
            {
                node.data = data;
                node.hasData = true;
                return;
            }

            if (node.boundary.width <= 1 && node.boundary.height <= 1)
            {
                return;
            }

            // 如果节点已经有数据，且不是叶子节点，则分割节点
            if (node.hasData && node.boundary.width > 1 && node.boundary.height > 1)
            {
                SplitNode(node);
                Insert(node, point, data);
            }
        }

        private void SplitNode(QuadtreeNode node)
        {
            float subWidth = node.boundary.width / 2;
            float subHeight = node.boundary.height / 2;

            node.hasData = false;
            node.data = Vector3.zero;

            node.topLeft = new QuadtreeNode(new Rect(node.boundary.x, node.boundary.y, subWidth, subHeight));
            node.topRight = new QuadtreeNode(new Rect(node.boundary.x + subWidth, node.boundary.y, subWidth, subHeight));
            node.bottomLeft = new QuadtreeNode(new Rect(node.boundary.x, node.boundary.y + subHeight, subWidth, subHeight));
            node.bottomRight = new QuadtreeNode(new Rect(node.boundary.x + subWidth, node.boundary.y + subHeight, subWidth, subHeight));
        }

        public Vector3 FindNearest(Vector2 point)
        {
            return FindNearest(root, point);
        }


        private Vector3 FindNearest(QuadtreeNode node, Vector2 point)
        {
            if (!node.boundary.Contains(point))
            {
                return Vector3.zero;
            }

            if (!node.hasData)
            {
                return node.data;
            }

            if (node.boundary.width <= 1 && node.boundary.height <= 1)
            {
                return node.data;
            }

            Vector3 nearestData = node.data;

            // 查询每个子节点
            Vector3 topLeftData = FindNearest(node.topLeft, point);
            Vector3 topRightData = FindNearest(node.topRight, point);
            Vector3 bottomLeftData = FindNearest(node.bottomLeft, point);
            Vector3 bottomRightData = FindNearest(node.bottomRight, point);

            // 找到最近的数据
            float distanceToNearest = Vector3.Distance(nearestData, point);
            float distanceToTopLeft = Vector3.Distance(topLeftData, point);
            float distanceToTopRight = Vector3.Distance(topRightData, point);
            float distanceToBottomLeft = Vector3.Distance(bottomLeftData, point);
            float distanceToBottomRight = Vector3.Distance(bottomRightData, point);

            if (distanceToTopLeft < distanceToNearest)
            {
                nearestData = topLeftData;
            }
            if (distanceToTopRight < distanceToNearest)
            {
                nearestData = topRightData;
            }
            if (distanceToBottomLeft < distanceToNearest)
            {
                nearestData = bottomLeftData;
            }
            if (distanceToBottomRight < distanceToNearest)
            {
                nearestData = bottomRightData;
            }

            return nearestData;
        }
    }


    private void OnDestroy()
    {
        if(buffer1!=null &&buffer2 != null)
        {
            //objecsBuffer.Release();
            buffer1.Release();
            //buffer1 = null;
            buffer2.Release();
            //buffer2 = null;
        }
    }

    private void OnDisable()
    {
        if (buffer1 != null && buffer2 != null)
        {
            buffer1.Release();
            //buffer1 = null;
            buffer2.Release();
            //buffer2 = null;
        }
    }
}
