
using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

using Matrix4x4 = UnityEngine.Matrix4x4;
using Plane = UnityEngine.Plane;
using Quaternion = UnityEngine.Quaternion;
using Random = UnityEngine.Random;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class IntancingTest : MonoBehaviour
{
    // Start is called before the first frame update
    public Vector2 _Size = new Vector2(40.0f, 40.0f);
    public int cubeDensity = 250;

    private int _positionsCount;
    private List<Vector3> _positions = new List<Vector3>();
    private List<Quaternion> _Rotations = new List<Quaternion>();
    private float yRotate = 0;
    private List<float> _uniformScale = new List<float>();

    ComputeBuffer _positionBuffer;
    ComputeBuffer _MatrixBuffer;

    public Material instanceMat;
    public Mesh _instanceMesh;
    static int bufferID = Shader.PropertyToID("positionBuffer");
    static int matID = Shader.PropertyToID("matrixBuffer");
    static int stepID = Shader.PropertyToID("_step");

    [Min(1.0f)]
    public float step = 1.0f;

    RenderParams renderParams;
    Bounds bounds;

    Vector3[,] cubeObject;
    Quaternion[,] cubeQuaternion;
    Vector3[,] cubeScale;
    Matrix4x4[,] trsMatrixs;
    private List<Matrix4x4> matrix4X4s;

    Matrix4x4[,] perMatrixTranslates;

    float[,] dirFactor;


    Vector2 _StartPos;
    Vector2 cellSize;
    Vector2 halfcellSize;




    private Plane _plane;

    Vector2Int bottomLeftCameraCell, topRightCameraCell;



    private static event Action UpdateCulling;


   


    void Start()
    {
        _StartPos = -_Size / 2.0f;
        cellSize = new Vector2(_Size.x / cubeDensity, _Size.y / cubeDensity);
        halfcellSize = cellSize / 2.0f;
        step = _Size.x / cubeDensity;
        cubeObject = new Vector3[cubeDensity, cubeDensity];
        cubeQuaternion = new Quaternion[cubeDensity, cubeDensity];
        cubeScale  = new Vector3[cubeDensity, cubeDensity];
        dirFactor = new float[cubeDensity, cubeDensity];
        trsMatrixs = new Matrix4x4[cubeDensity, cubeDensity];
        perMatrixTranslates = new Matrix4x4[cubeDensity, cubeDensity];


        _plane = new Plane(Vector3.up, 0.0f);


        
        

        for (int i = 0; i < cubeObject.GetLength(0); i++)
        {
            for (int j = 0; j < cubeObject.GetLength(1); j++)
            {
                Matrix4x4 trs = CreateMatrix(i, j);

                trsMatrixs[i, j] = trs;

               
                perMatrixTranslates[i, j] =Matrix4x4.identity;


                //Vector3 postion = new Vector3(0f, Random.Range(-1.0f, 1.0f) * Time.deltaTime * 0.1f, 0f);

                //perMatrixTranslates[i, j] = Matrix4x4.Translate(postion);


            }
        }




        bounds = new Bounds(transform.position, new Vector3(_Size.x, 9.0f, _Size.y));
        matrix4X4s = new List<Matrix4x4>();

        // _positions = new List<Vector3>();
        renderParams = new RenderParams(instanceMat);
        renderParams.worldBounds = bounds;
        renderParams.shadowCastingMode = ShadowCastingMode.On;


        UpdateCellPos();
        UpdateCulling += UpdateCellPos;

    }

    private Matrix4x4 CreateMatrix(int i, int j)
    {
        Vector3 cellSizePos = new Vector3(_StartPos.x + cellSize.x * i, 0, _StartPos.y + cellSize.y * j);
        Vector3 randomOffset = new Vector3(Random.Range(-halfcellSize.x, halfcellSize.x),
                                            Random.Range(-1.0f, 1.0f),
                                            Random.Range(-halfcellSize.y, halfcellSize.y));

        dirFactor[i, j] = Mathf.Sign(Random.Range(-1, 1));

        cubeObject[i, j] = cellSizePos + randomOffset;


        //_positions.Add(cubeObject[i, j]);

        cubeQuaternion[i, j] = Quaternion.Euler(Random.Range(-90.0f, 90.0f), Random.Range(0.0f, 360.0f), Random.Range(-90.0f, 90.0f));
        cubeScale[i, j] = Vector3.one *step;


        Matrix4x4 trs = Matrix4x4.TRS(cubeObject[i, j], cubeQuaternion[i, j], cubeScale[i, j]);
        return trs;
    }

    private Matrix4x4 CreateMatrix(int i, int j,float t)
    {
        Vector3 cellSizePos = new Vector3(_StartPos.x + cellSize.x * i, 0, _StartPos.y + cellSize.y * j);
        Vector3 randomOffset = new Vector3(Random.Range(-halfcellSize.x, halfcellSize.x),
                                            Random.Range(-1.0f, 1.0f),
                                            Random.Range(-halfcellSize.y, halfcellSize.y));

        dirFactor[i, j] = Mathf.Sign(Random.Range(-1, 1));

        cubeObject[i, j] = cellSizePos + randomOffset + Vector3.up*t;


        //_positions.Add(cubeObject[i, j]);

        cubeQuaternion[i, j] = Quaternion.Euler(Random.Range(-90.0f, 90.0f), Random.Range(0.0f, 360.0f), Random.Range(-90.0f, 90.0f));
        cubeScale[i, j] = Vector3.one;


        Matrix4x4 trs = Matrix4x4.TRS(cubeObject[i, j], cubeQuaternion[i, j], cubeScale[i, j]);
        return trs;
    }

    // Update is called once per frame
    void Update()
    {




        if (Camera.main.transform.hasChanged)
        {
            UpdateCulling?.Invoke();
        }

       // Graphics.RenderMeshPrimitives(renderParams, _instanceMesh, 0, matrix4X4s.Count);
        Graphics.DrawMeshInstancedProcedural( _instanceMesh, 0, instanceMat, bounds, matrix4X4s.Count);
    }

    private void OnDestroy()
    {
        //_positionBuffer?.Release();
        //_positionBuffer = null;

        _MatrixBuffer?.Release();
        _MatrixBuffer = null;

        UpdateCulling -= UpdateCellPos;

    }


    private Vector3 Raycast(Vector3 position)
    {
        var ray = Camera.main.ScreenPointToRay(position);
        _plane.Raycast(ray, out var enter);
        return ray.GetPoint(enter);
    }

    float TriangleWave(float In)
    {
        return  2.0f * Mathf.Abs(2.0f * (In - Mathf.Floor(0.5f + In))) - 1.0f;
    }

    public void UpdatePositions(Vector2Int bottomLeftCameraCell, Vector2Int topRightCameraCell)
    {
        //_positions.Clear();
        matrix4X4s.Clear();



        for (int i = bottomLeftCameraCell.x; i < topRightCameraCell.x; i++)
        {
            for (int j = bottomLeftCameraCell.y; j < topRightCameraCell.y; j++)
            {

                //trsMatrixs[i, j] = perMatrixTranslates[i,j] * trsMatrixs[i, j];
                matrix4X4s.Add(trsMatrixs[i, j]);
            }
        }


        //_positionsCount = _positions.Count;
        //_positionBuffer?.Release();
        //if (_positionsCount == 0) return;
        //_positionBuffer = new ComputeBuffer(_positionsCount, 3 * 4);
        //_positionBuffer.SetData(_positions);
        //instanceMat.SetBuffer(bufferID, _positionBuffer);

        _MatrixBuffer?.Release();
        if (matrix4X4s.Count == 0) return;


        _MatrixBuffer = new ComputeBuffer(matrix4X4s.Count, 16 * 4);

        _MatrixBuffer.SetData(matrix4X4s);

        instanceMat.SetBuffer(matID, _MatrixBuffer);

        instanceMat.SetFloat(stepID, step);
    }


    private void UpdateCellPos()
    {

        var bottomLeftCameraCorner = Raycast(Vector3.zero);
        var bottomRightCameraCorner = Raycast(new Vector3(Screen.width,0));
        var topLeftCameraCorner = Raycast(new Vector3(0.0f, Screen.height));
        var topRightCameraCorner = Raycast(new Vector3(Screen.width, Screen.height));

        float x1 = (bottomLeftCameraCorner.x - _StartPos.x);
        float x2 = (bottomRightCameraCorner.x - _StartPos.x);
        float x3 = (topLeftCameraCorner.x - _StartPos.x);
        float x4 = (topRightCameraCorner.x - _StartPos.x);

        float z1 = (bottomLeftCameraCorner.z - _StartPos.y);
        float z2 = (bottomRightCameraCorner.z - _StartPos.y);
        float z3 = (topLeftCameraCorner.z - _StartPos.y);
        float z4 = (topRightCameraCorner.z - _StartPos.y);

        float minx = Mathf.Min(x1, Mathf.Min(x2, Mathf.Min(x3, x4)));
        float minz = Mathf.Min(z1, Mathf.Min(z2, Mathf.Min(z3, z4)));

        float maxx = Mathf.Max(x1, Mathf.Max(x2, Mathf.Max(x3, x4)));
        float maxz = Mathf.Max(z1, Mathf.Max(z2, Mathf.Max(z3, z4)));

        bottomLeftCameraCell = new Vector2Int(
            Mathf.Clamp(Mathf.FloorToInt(minx/ cellSize.x), 0,
                cubeDensity - 1),
            Mathf.Clamp(Mathf.FloorToInt(minz / cellSize.y), 0,
                cubeDensity - 1));

        topRightCameraCell = new Vector2Int(
            Mathf.Clamp(Mathf.FloorToInt(maxx/ cellSize.x) + 1, 0,
                cubeDensity - 1),
            Mathf.Clamp(Mathf.FloorToInt(maxz / cellSize.y) + 1, 0,
                cubeDensity - 1));

        UpdatePositions(bottomLeftCameraCell, topRightCameraCell);

    }
}
