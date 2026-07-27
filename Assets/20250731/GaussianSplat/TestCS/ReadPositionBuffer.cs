using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class ReadPositionBuffer : MonoBehaviour
{

    public ComputeShader computeShader;
    public Mesh mesh;

    private ComputeBuffer _PositionBuffer;
    private ComputeBuffer argsBuffer;
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

    private int _kernel;
    private int _count;
    private int _threadGroupSizeX, _threadGroupSizeY;

    public Texture2D positionEXR;
    public Material material;

    public bool _isInitialized = false;


    // Start is called before the first frame update
    void Initialize()
    {
        if (_isInitialized) return;

        if (positionEXR == null || computeShader == null || mesh == null || material == null) return;

        int width = positionEXR.width;
        int height = positionEXR.height;

        
        _count = width * height;

        _PositionBuffer = new ComputeBuffer(_count, sizeof(float) * 3);

        _kernel = computeShader.FindKernel("LoadEXRTexture");


        computeShader.SetTexture(_kernel, "_ExrTexture", positionEXR);
        computeShader.SetInt("_Width", width);
        computeShader.SetInt("_Height", height);

        computeShader.SetBuffer(_kernel, "_PositionBuffer", _PositionBuffer);

        _threadGroupSizeX = Mathf.CeilToInt(width / 8.0f);
        _threadGroupSizeY = Mathf.CeilToInt(height / 8.0f);


        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        args[0] = (uint)mesh.GetIndexCount(0);
        args[1] = (uint)_count;
        args[2] = (uint)mesh.GetIndexStart(0);
        args[3] = (uint)mesh.GetBaseVertex(0);

        argsBuffer.SetData(args);

        _isInitialized = true;
    }

    void OnEnable()
    {
        Initialize();
    }

    void OnDisable()
    {
        Cleanup();
    }

    // Update is called once per frame
    void Update()
    {

        if (!_isInitialized)
        {
            Initialize();
            return;
        }

        if (_PositionBuffer == null || argsBuffer == null) return;

        computeShader.Dispatch(_kernel, _threadGroupSizeX, _threadGroupSizeY, 1);

        material.SetBuffer("_PositionBuffer", _PositionBuffer);

        Graphics.DrawMeshInstancedIndirect(
            mesh,
            0,
            material,
            new Bounds(Vector3.zero, Vector3.one * 1000f),
            argsBuffer
            
        );
    }

    void Cleanup()
    {
        if (_PositionBuffer != null)
        {
            _PositionBuffer.Release();
            _PositionBuffer = null;
        }

        if (argsBuffer != null)
        {
            argsBuffer.Release();
            argsBuffer = null;
        }

        _isInitialized = false;
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    #if UNITY_EDITOR
    void OnValidate()
    {
        if (_isInitialized)
        {
            Cleanup();
        }
    }
    #endif
}
