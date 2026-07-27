using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

[ExecuteInEditMode]
public class CSGuassianSplatRender : MonoBehaviour
{
    [Header("Settings")]
    public GaussianSplatData splatData;
    public ComputeShader computeShader;
    public Material splatMaterial;


    private ComputeBuffer _PositionBuffer;
    private ComputeBuffer _ColorBuffer;
    private ComputeBuffer _ScaleBuffer;
    private ComputeBuffer _RotationBuffer;
    private ComputeBuffer _OutputTranformMatrixBuffer;

    private ComputeBuffer _SortBuffer; // 包含 {depth, index}
    private ComputeBuffer _OrderBuffer; // 仅 {index} 给 Render Shader 用

    private ComputeBuffer _ArgsBuffer;
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

    private Camera m_camera;

    private int count = 1000;
    int threadGroups = 100;
    
    // Start is called before the first frame update
    void OnEnable()
    {
        Initialize();
    }

    void Start()
    {
        // OnEnable 已经负责初始化，但为了双重保险
        if (_PositionBuffer == null) Initialize();
    }

    // Update is called once per frame
    void Update()
    {
        if (splatMaterial == null) return;
        
        // 在编辑器模式下，Buffer 可能会丢失，需要重新检查并初始化
        if (_PositionBuffer == null) Initialize();

        if (_PositionBuffer == null || _ArgsBuffer == null) return;

        m_camera = Camera.main;
#if UNITY_EDITOR
        // 如果不在运行模式，优先使用 SceneView 的相机，以便在编辑器视窗中正确显示和排序
        if (!Application.isPlaying)
        {
            var sceneView = UnityEditor.SceneView.lastActiveSceneView;
            if (sceneView != null) m_camera = sceneView.camera;
        }
#endif
        if(m_camera == null) return;

        int kernel = computeShader.FindKernel("GuassianSplatRender");
        
        computeShader.SetMatrix("_ViewMatrix", m_camera.worldToCameraMatrix);
        computeShader.SetMatrix("_ProjectionMatrix", GL.GetGPUProjectionMatrix(m_camera.projectionMatrix, false));
        computeShader.SetFloat("_Fov", m_camera.fieldOfView);
        computeShader.SetVector("_ScreenSize", new Vector2(m_camera.pixelWidth, m_camera.pixelHeight));
        computeShader.SetInt("_Count", count);

        computeShader.SetBuffer(kernel, "_PositionBuffer", _PositionBuffer);
        computeShader.SetBuffer(kernel, "_ScaleBuffer", _ScaleBuffer);
        computeShader.SetBuffer(kernel, "_RotationBuffer", _RotationBuffer);
        computeShader.SetBuffer(kernel, "_OutputTranformMatrixBuffer", _OutputTranformMatrixBuffer);
        
        threadGroups = Mathf.CeilToInt(count / 256.0f);
        computeShader.Dispatch(kernel, threadGroups, 1, 1);

        SortSplats(m_camera);

        splatMaterial.SetBuffer("_ColorBuffer", _ColorBuffer);
        splatMaterial.SetBuffer("_PositionBuffer", _PositionBuffer);
        splatMaterial.SetBuffer("_OutputTranformMatrixBuffer", _OutputTranformMatrixBuffer);
        splatMaterial.SetBuffer("_OrderBuffer", _OrderBuffer); 
        splatMaterial.SetMatrix("_LocalToWorldMatrix", transform.localToWorldMatrix);



        Graphics.DrawProceduralIndirect(splatMaterial,
                                        new Bounds(transform.position, Vector3.one * 1000),
                                        MeshTopology.Triangles,
                                        _ArgsBuffer);
    }

    void Initialize()
    {
        ReleaseBuffers(); // 初始化前先清理旧 Buffer

        if (splatData == null) return;
        if (computeShader == null) return;
        m_camera = Camera.main;
        count = splatData.positions.Length;

        _ArgsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        args[0] = 6; 
        args[1] = (uint)count;
        args[2] = 0;
        args[3] = 0;
        _ArgsBuffer.SetData(args);

        _PositionBuffer = new ComputeBuffer(count, sizeof(float) * 3,ComputeBufferType.Structured);
        _ColorBuffer = new ComputeBuffer( count, sizeof(float) * 4, ComputeBufferType.Structured);
        _ScaleBuffer = new ComputeBuffer( count, sizeof(float) * 3, ComputeBufferType.Structured);
        _RotationBuffer = new ComputeBuffer(count, sizeof(float) * 4,ComputeBufferType.Structured);

        _OrderBuffer = new ComputeBuffer(count, sizeof(uint)); 

        uint[] indices = new uint[count];
        for(uint i=0; i<count; i++) indices[i] = i;
        _OrderBuffer.SetData(indices);
        
        //虽然命名为Matrix  但是实际是使用了一个Vector4  只传递了 scale.x  scale.y angle 0 四个值
        _OutputTranformMatrixBuffer = new ComputeBuffer(count, sizeof(float) * 4, ComputeBufferType.Structured);

        _PositionBuffer.SetData(splatData.positions);
        _ColorBuffer.SetData(splatData.colors);
        _ScaleBuffer.SetData(splatData.scales);
        _RotationBuffer.SetData(splatData.rotations);
    }

    void SortSplats(Camera cam)
    {
        if (cam == null) return;
        if (_PositionBuffer == null) return;
        if (_OutputTranformMatrixBuffer == null) return;

        int kernelCalcDepth = computeShader.FindKernel("CalcViewDepth");
        int kernelSort = computeShader.FindKernel("BitonicSort");
        int kernelCopy = computeShader.FindKernel("CopySortedIndices");

        int numSplats = count;
         // Bitonic Sort 需要数据长度是 2 的幂次
        int paddedCount = Mathf.NextPowerOfTwo(numSplats); 

        if (_SortBuffer == null||_SortBuffer.count != paddedCount)
        {
           if (_SortBuffer != null) _SortBuffer.Release();
            // float depth(4) + uint index(4) = 8 bytes
            _SortBuffer = new ComputeBuffer(paddedCount, 8); 

        } 

        computeShader.SetMatrix("_ViewMatrix", cam.worldToCameraMatrix);
        computeShader.SetInt("_Count", numSplats);

        computeShader.SetBuffer(kernelCalcDepth, "_PositionBuffer", _PositionBuffer);
        computeShader.SetBuffer(kernelCalcDepth, "_SortBuffer", _SortBuffer);

        computeShader.Dispatch(kernelCalcDepth, Mathf.CeilToInt(paddedCount / 256.0f), 1, 1);

        for (int k = 2; k <= paddedCount; k <<= 1)
        {
            for (int j = k >> 1; j > 0; j >>= 1)
            {
                computeShader.SetInt("_SortLevel", j);
                computeShader.SetInt("_SortMask", k);
                computeShader.SetBuffer(kernelSort, "_SortBuffer", _SortBuffer);

                // 关键修正：使用浮点除法确保向上取整，且只需要一次 Dispatch
                // Shader 逻辑中使用 id.x 作为索引 i，并判断 index < j，因此需要覆盖所有索引
                computeShader.Dispatch(kernelSort, Mathf.CeilToInt(paddedCount / 256.0f), 1, 1);
                
            }
        }

        computeShader.SetBuffer(kernelCopy, "_SortBuffer", _SortBuffer);
        computeShader.SetBuffer(kernelCopy, "_OrderBuffer", _OrderBuffer);
        computeShader.SetInt("_Count", numSplats);
        
        computeShader.Dispatch(kernelCopy, Mathf.CeilToInt(numSplats / 256.0f), 1, 1);
    }
    
    private void OnDisable()
    {
        ReleaseBuffers();
    }

    private void OnDestroy() {
        ReleaseBuffers();
    }

    void ReleaseBuffers()
    {
        if (_PositionBuffer != null) { _PositionBuffer.Release(); _PositionBuffer = null; }
        if (_ColorBuffer != null) { _ColorBuffer.Release(); _ColorBuffer = null; }
        if (_ScaleBuffer != null) { _ScaleBuffer.Release(); _ScaleBuffer = null; }
        if (_RotationBuffer != null) { _RotationBuffer.Release(); _RotationBuffer = null; }
        if (_OutputTranformMatrixBuffer != null) { _OutputTranformMatrixBuffer.Release(); _OutputTranformMatrixBuffer = null; }
        if (_SortBuffer != null) { _SortBuffer.Release(); _SortBuffer = null; }
        if (_OrderBuffer != null) { _OrderBuffer.Release(); _OrderBuffer = null; }
        if (_ArgsBuffer != null) { _ArgsBuffer.Release(); _ArgsBuffer = null; }
    }

}
