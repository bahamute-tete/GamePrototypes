using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

public class GrassDraw : MonoBehaviour
{
    // Start is called before the first frame update
    public Vector2 _Size = new Vector2(40.0f, 40.0f);
    public int grassDensity = 250;
    public GameObject grassPrefab;


    private int _positionsCount;
    private List<Vector3> _positions= new List<Vector3>();
    ComputeBuffer _positionBuffer;

    public Material instanceMat;
    public Mesh _instanceMesh;
    static int bufferID = Shader.PropertyToID("positionBuffer");

    RenderParams renderParams;

    void Start()
    {
        Vector2 _StartPos = -_Size / 2.0f;
        Vector2 offset = new Vector2(_Size.x /grassDensity, _Size.y / grassDensity);
        Vector2 halfcellSize = offset / 2.0f;
        var grassObject = new Vector3[grassDensity, grassDensity];


        _positions.Clear();

        for (int i = 0; i < grassObject.GetLength(0); i++)
        {
            for (int j = 0; j < grassObject.GetLength(1); j++)
            {
                Vector3 offsetPos = new Vector3(_StartPos.x + offset.x * i, 0, _StartPos.y + offset.y * j);
                grassObject[i, j] = offsetPos + new Vector3(Random.Range(-halfcellSize.x, halfcellSize.x), 0, Random.Range(-halfcellSize.y, halfcellSize.y));
                // GameObject grassInstance = Instantiate(grassPrefab, grassObject[i,j], Quaternion.identity);
                //grassInstance.transform.parent = transform;
                _positions.Add(grassObject[i, j]);
            }
        }


        _positionsCount = _positions.Count;
        _positionBuffer?.Release();
        if (_positionsCount == 0) return;
        _positionBuffer = new ComputeBuffer(_positionsCount, 12);
        _positionBuffer.SetData(_positions);
        instanceMat.SetBuffer(bufferID, _positionBuffer);

        
        renderParams = new RenderParams(instanceMat);
        renderParams.worldBounds = new Bounds(
            transform.position,
            new Vector3(_Size.x, 0, _Size.y));
        renderParams.shadowCastingMode = ShadowCastingMode.TwoSided;




    }

    // Update is called once per frame
    void Update()
    {
        if (_positionsCount == 0) return;
        Graphics.RenderMeshPrimitives(renderParams, _instanceMesh, 0, _positionsCount);
    }

    private void OnDestroy()
    {
        _positionBuffer?.Release();
        _positionBuffer = null;
    }
}
