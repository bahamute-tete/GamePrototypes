using UnityEngine;

/// <summary>
/// URP 管线下的 3D Gaussian Splatting 渲染器（仅 DC 颜色，无 SH）。
///
/// 每帧流程：
///   1. ComputeSplatScreen：3D 协方差投影到屏幕，输出 clip 中心 + NDC 椭圆轴
///   2. CalcViewDepth / BitonicSort / CopySortedIndices：back-to-front 排序
///   3. DrawProceduralIndirect：每 splat 画一个屏幕空间 quad，高斯衰减混合
///
/// 使用：挂到一个空 GameObject 上，拖入 GaussianSplatData 资产和
///       GsplatURPCompute.compute；材质留空会自动从 Shader 创建。
/// </summary>
[ExecuteInEditMode]
public class CSGuassianSplatRender : MonoBehaviour
{
    [Header("Data")]
    public GaussianSplatData splatData;

    [Header("Assets")]
    public ComputeShader computeShader;
    [Tooltip("留空则自动用 GaussianSplat/URP_Splat 创建")]
    public Material splatMaterial;

    [Header("Options")]
    [Tooltip("如果资产里存的是对数尺度（PLY 原始格式），勾上会在上传时做 exp")]
    public bool scaleIsLog = false;
    [Tooltip("世界空间整体放大倍数（有些 PLY 数据的坐标系偏小/偏大时调这个）")]
    public float globalScale = 1f;
    [Tooltip("包围盒半径，超出相机的部分会被视锥剔除（DrawProceduralIndirect 整体剔除用）")]
    public float boundsRadius = 1000f;

    [Header("Performance")]
    [Tooltip("只在相机/物体移动时重新排序。Bitonic Sort 对 26 万 splat 每帧要 ~170 次 dispatch，" +
             "是全渲染链路最大的开销；splat 数据静止时排序结果只在视角变化时才需要更新")]
    public bool sortOnlyWhenCameraMoves = true;
    [Tooltip("即使相机没动，也每隔 N 帧强制重排一次（0 = 完全按移动触发）")]
    public int forceSortInterval = 0;

    // ---- 静态数据 buffer ----
    ComputeBuffer _PositionBuffer;
    ComputeBuffer _ColorBuffer;
    ComputeBuffer _ScaleBuffer;
    ComputeBuffer _RotationBuffer;

    // ---- 每帧 compute 输出 ----
    ComputeBuffer _SplatClipPosBuffer;   // float4 clip 中心
    ComputeBuffer _SplatAxisBuffer;      // float4 NDC 椭圆轴

    // ---- 排序 ----
    ComputeBuffer _SortBuffer;           // {float depth, uint index}，2 的幂长度
    ComputeBuffer _SplatOrderBuffer;     // uint 索引，back-to-front

    ComputeBuffer _ArgsBuffer;
    readonly uint[] _args = new uint[5] { 6, 0, 0, 0, 0 };  // 每 instance 6 顶点（两个三角形）

    Material _runtimeMaterial;
    int _count;
    bool _initialized;

    // kernel id 缓存
    int _kernelComputeScreen = -1;
    int _kernelCalcDepth = -1;
    int _kernelBitonicSort = -1;
    int _kernelCopyIndices = -1;

    // 排序触发状态
    bool _hasSortedOnce;
    int _lastSortFrame = -1;
    Vector3 _lastCamPos;
    Quaternion _lastCamRot;
    Vector3 _lastObjPos;
    Quaternion _lastObjRot;
    Vector3 _lastObjScale;

    void OnEnable()
    {
        Initialize();
    }

    void OnDisable()
    {
        ReleaseBuffers();
    }

    void OnDestroy()
    {
        ReleaseBuffers();
        if (_runtimeMaterial != null)
        {
            if (Application.isPlaying) Destroy(_runtimeMaterial);
            else DestroyImmediate(_runtimeMaterial);
            _runtimeMaterial = null;
        }
    }

    void Update()
    {
        // 编辑器模式下 buffer 可能被域重载清掉
        if (!_initialized || _PositionBuffer == null)
            Initialize();
        if (!_initialized || _count == 0)
            return;

        Camera cam = GetRenderCamera();
        if (cam == null) return;

        Matrix4x4 localToWorld = transform.localToWorldMatrix;
        if (!Mathf.Approximately(globalScale, 1f))
            localToWorld = Matrix4x4.Scale(Vector3.one * globalScale) * localToWorld;

        // ---- 1. 投影 ----
        computeShader.SetMatrix("_LocalToWorldMatrix", localToWorld);
        computeShader.SetMatrix("_ViewMatrix", cam.worldToCameraMatrix);
        computeShader.SetMatrix("_ProjectionMatrix", GL.GetGPUProjectionMatrix(cam.projectionMatrix, false));
        computeShader.SetVector("_ScreenSize", new Vector2(cam.pixelWidth, cam.pixelHeight));
        computeShader.SetFloat("_NearPlane", cam.nearClipPlane);
        computeShader.SetInt("_Count", _count);

        computeShader.SetBuffer(_kernelComputeScreen, "_PositionBuffer", _PositionBuffer);
        computeShader.SetBuffer(_kernelComputeScreen, "_ScaleBuffer", _ScaleBuffer);
        computeShader.SetBuffer(_kernelComputeScreen, "_RotationBuffer", _RotationBuffer);
        computeShader.SetBuffer(_kernelComputeScreen, "_SplatClipPos", _SplatClipPosBuffer);
        computeShader.SetBuffer(_kernelComputeScreen, "_SplatAxis", _SplatAxisBuffer);
        computeShader.Dispatch(_kernelComputeScreen, Mathf.CeilToInt(_count / 64f), 1, 1);

        // ---- 2. 排序（按需：相机/物体移动或强制间隔到达时） ----
        if (NeedSort(cam))
        {
            SortSplats(cam, localToWorld);
            _lastCamPos   = cam.transform.position;
            _lastCamRot   = cam.transform.rotation;
            _lastObjPos   = transform.position;
            _lastObjRot   = transform.rotation;
            _lastObjScale = transform.localScale;
            _lastSortFrame = Time.frameCount;
            _hasSortedOnce = true;
        }

        // ---- 3. 绘制 ----
        Material mat = splatMaterial != null ? splatMaterial : _runtimeMaterial;
        mat.SetBuffer("_SplatClipPos", _SplatClipPosBuffer);
        mat.SetBuffer("_SplatAxis", _SplatAxisBuffer);
        mat.SetBuffer("_SplatColor", _ColorBuffer);
        mat.SetBuffer("_SplatOrder", _SplatOrderBuffer);

        Graphics.DrawProceduralIndirect(
            mat,
            new Bounds(transform.position, Vector3.one * boundsRadius),
            MeshTopology.Triangles,
            _ArgsBuffer);
    }

    Camera GetRenderCamera()
    {
        Camera cam = Camera.main;
#if UNITY_EDITOR
        // 编辑器非运行状态下用 SceneView 相机，保证 Scene 窗口里排序/投影正确
        if (!Application.isPlaying)
        {
            var sceneView = UnityEditor.SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
                cam = sceneView.camera;
        }
#endif
        return cam;
    }

    /// <summary>
    /// 是否需要重新排序：数据静止时，深度顺序只随相机与物体的相对运动变化。
    /// </summary>
    bool NeedSort(Camera cam)
    {
        if (!_hasSortedOnce) return true;
        if (!sortOnlyWhenCameraMoves) return true;
        if (forceSortInterval > 0 && Time.frameCount - _lastSortFrame >= forceSortInterval)
            return true;

        bool camMoved =
            (cam.transform.position - _lastCamPos).sqrMagnitude > 1e-6f ||
            Quaternion.Angle(cam.transform.rotation, _lastCamRot) > 0.01f;

        bool objMoved =
            (transform.position - _lastObjPos).sqrMagnitude > 1e-6f ||
            Quaternion.Angle(transform.rotation, _lastObjRot) > 0.01f ||
            (transform.localScale - _lastObjScale).sqrMagnitude > 1e-6f;

        return camMoved || objMoved;
    }

    void Initialize()
    {
        ReleaseBuffers();
        _initialized = false;

        if (splatData == null) { Debug.LogWarning("[GsplatRender] splatData 未绑定", this); return; }
        if (computeShader == null) { Debug.LogWarning("[GsplatRender] computeShader 未绑定", this); return; }
        if (splatData.positions == null || splatData.positions.Length == 0)
        {
            Debug.LogWarning("[GsplatRender] splatData.positions 为空", this);
            return;
        }

        // kernel id（注意名字必须与 .compute 里的 #pragma kernel 一致）
        _kernelComputeScreen = computeShader.FindKernel("ComputeSplatScreen");
        _kernelCalcDepth     = computeShader.FindKernel("CalcViewDepth");
        _kernelBitonicSort   = computeShader.FindKernel("BitonicSort");
        _kernelCopyIndices   = computeShader.FindKernel("CopySortedIndices");

        _count = splatData.positions.Length;

        // 材质：优先用指定的，否则自动创建
        if (splatMaterial == null && _runtimeMaterial == null)
        {
            Shader shader = Shader.Find("GaussianSplat/URP_Splat");
            if (shader != null)
            {
                _runtimeMaterial = new Material(shader);
                _runtimeMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            else
            {
                Debug.LogError("[CSGuassianSplatRender] 找不到 Shader 'GaussianSplat/URP_Splat'，" +
                               "请确认 GaussianSplatURP.shader 已加入项目", this);
                return;
            }
        }

        // ---- 静态数据上传 ----
        _PositionBuffer = new ComputeBuffer(_count, sizeof(float) * 3, ComputeBufferType.Structured);
        _ColorBuffer    = new ComputeBuffer(_count, sizeof(float) * 4, ComputeBufferType.Structured);
        _ScaleBuffer    = new ComputeBuffer(_count, sizeof(float) * 3, ComputeBufferType.Structured);
        _RotationBuffer = new ComputeBuffer(_count, sizeof(float) * 4, ComputeBufferType.Structured);

        _PositionBuffer.SetData(splatData.positions);
        _ColorBuffer.SetData(splatData.colors);

        if (scaleIsLog)
        {
            Vector3[] scales = new Vector3[_count];
            for (int i = 0; i < _count; i++)
            {
                Vector3 s = splatData.scales[i];
                scales[i] = new Vector3(Mathf.Exp(s.x), Mathf.Exp(s.y), Mathf.Exp(s.z));
            }
            _ScaleBuffer.SetData(scales);
        }
        else
        {
            _ScaleBuffer.SetData(splatData.scales);
        }

        // Quaternion[] → Vector4[]（HLSL 端按 xyzw 读）
        Vector4[] rotations = new Vector4[_count];
        for (int i = 0; i < _count; i++)
        {
            Quaternion q = splatData.rotations[i];
            rotations[i] = new Vector4(q.x, q.y, q.z, q.w);
        }
        _RotationBuffer.SetData(rotations);

        // ---- 每帧输出 buffer ----
        _SplatClipPosBuffer = new ComputeBuffer(_count, sizeof(float) * 4, ComputeBufferType.Structured);
        _SplatAxisBuffer    = new ComputeBuffer(_count, sizeof(float) * 4, ComputeBufferType.Structured);

        // ---- 排序 buffer：Bitonic Sort 需要 2 的幂长度 ----
        int paddedCount = Mathf.NextPowerOfTwo(_count);
        _SortBuffer = new ComputeBuffer(paddedCount, 8);  // float depth + uint index
        _SplatOrderBuffer = new ComputeBuffer(_count, sizeof(uint));

        uint[] indices = new uint[_count];
        for (uint i = 0; i < _count; i++) indices[i] = i;
        _SplatOrderBuffer.SetData(indices);

        // ---- 间接绘制参数：6 顶点/instance，count 个 instance ----
        _ArgsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        _args[0] = 6;
        _args[1] = (uint)_count;
        _args[2] = 0;
        _args[3] = 0;
        _ArgsBuffer.SetData(_args);

        _initialized = true;
        Debug.Log($"[GsplatRender] 初始化完成: {_count} splats, 材质={(splatMaterial != null ? splatMaterial.name : "auto:" + _runtimeMaterial.shader.name)}", this);
    }

    void SortSplats(Camera cam, Matrix4x4 localToWorld)
    {
        int paddedCount = _SortBuffer.count;
        int groupsPadded = Mathf.CeilToInt(paddedCount / 256f);
        int groupsCount  = Mathf.CeilToInt(_count / 256f);

        computeShader.SetMatrix("_LocalToWorldMatrix", localToWorld);
        computeShader.SetMatrix("_ViewMatrix", cam.worldToCameraMatrix);
        computeShader.SetInt("_Count", _count);

        // 深度
        computeShader.SetBuffer(_kernelCalcDepth, "_PositionBuffer", _PositionBuffer);
        computeShader.SetBuffer(_kernelCalcDepth, "_SortBuffer", _SortBuffer);
        computeShader.Dispatch(_kernelCalcDepth, groupsPadded, 1, 1);

        // Bitonic Sort 各层级
        for (int k = 2; k <= paddedCount; k <<= 1)
        {
            for (int j = k >> 1; j > 0; j >>= 1)
            {
                computeShader.SetInt("_SortLevel", j);
                computeShader.SetInt("_SortMask", k);
                computeShader.SetBuffer(_kernelBitonicSort, "_SortBuffer", _SortBuffer);
                computeShader.Dispatch(_kernelBitonicSort, groupsPadded, 1, 1);
            }
        }

        // 提取索引
        computeShader.SetBuffer(_kernelCopyIndices, "_SortBuffer", _SortBuffer);
        computeShader.SetBuffer(_kernelCopyIndices, "_SplatOrder", _SplatOrderBuffer);
        computeShader.Dispatch(_kernelCopyIndices, groupsCount, 1, 1);
    }

    void ReleaseBuffers()
    {
        _PositionBuffer?.Release();      _PositionBuffer = null;
        _ColorBuffer?.Release();         _ColorBuffer = null;
        _ScaleBuffer?.Release();         _ScaleBuffer = null;
        _RotationBuffer?.Release();      _RotationBuffer = null;
        _SplatClipPosBuffer?.Release();  _SplatClipPosBuffer = null;
        _SplatAxisBuffer?.Release();     _SplatAxisBuffer = null;
        _SortBuffer?.Release();          _SortBuffer = null;
        _SplatOrderBuffer?.Release();    _SplatOrderBuffer = null;
        _ArgsBuffer?.Release();          _ArgsBuffer = null;
        _initialized = false;
    }
}
