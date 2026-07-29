using UnityEngine;
using UnityEngine.VFX;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 把 GaussianSplatData 投喂给 VFX Graph。
/// 每帧用 Compute Shader 把每个高斯的 3D 协方差投影到屏幕，
/// 计算出 quad 在 VFX 局部空间中的尺寸 + 屏幕空间旋转，让 VFX Graph 渲染对应的椭圆 billboard。
///
/// 数据约定：buffer 中的位置 = VFX 局部（模型）空间坐标；
/// GameObject 的 Transform 负责摆放（移动 / 旋转 / 均匀缩放），
/// compute 每帧用 modelView = cam.worldToCameraMatrix * vfx.localToWorldMatrix 投影。
/// 注意：非均匀缩放 billboard 方案表达不了，检测到会告警。
///
/// 用 [ExecuteAlways] 让 Edit Mode 下也能驱动（基于 Scene View 相机）。
/// 桌面端单目渲染，未考虑 VR/XR Single Pass Instanced。
/// </summary>
//[ExecuteAlways]
[DisallowMultipleComponent]
public class VFXGraphicsBuffer_OS : MonoBehaviour
{
    // ======================================================================
    // Inspector
    // ======================================================================
    [Header("VFX 资源")]
    public VisualEffect    visualEffect;
    public GaussianSplatData splatData;
    public ComputeShader   computeShader;

    [Tooltip("不填则：运行时取 Camera.main；编辑器下取最近激活的 Scene View 相机")]
    public Camera targetCamera;

    [Header("音频驱动 (可选)")]
    [HideInInspector]
    public bool useAudioSpectrum = false;

    // ======================================================================
    // 内部状态
    // ======================================================================
    AudioSource audioSource;
    Vector2[]   audioBands = new Vector2[8];

    GraphicsBuffer positionBuffer;
    GraphicsBuffer colorBuffer;
    GraphicsBuffer scaleBuffer;
    GraphicsBuffer rotationBuffer;
    GraphicsBuffer outputTransformBuffer;
    GraphicsBuffer audioBandBuffer;

    int  kernelTransform;
    int  splatCount;
    int  threadGroups;
    bool isInitialized;
    bool warnedNonUniformScale;

    // 缓存属性 ID（避免每帧字符串哈希）
    static readonly int ID_Count          = Shader.PropertyToID("_Count");
    static readonly int ID_PositionBuffer = Shader.PropertyToID("_PositionBuffer");
    static readonly int ID_ScaleBuffer    = Shader.PropertyToID("_ScaleBuffer");
    static readonly int ID_RotationBuffer = Shader.PropertyToID("_RotationBuffer");
    static readonly int ID_OutputBuffer   = Shader.PropertyToID("_OutputTransformBuffer");
    static readonly int ID_ModelViewMatrix = Shader.PropertyToID("_ModelViewMatrix");
    static readonly int ID_ProjMatrix      = Shader.PropertyToID("_ProjectionMatrix");
    static readonly int ID_ScreenSize      = Shader.PropertyToID("_ScreenSize");
    static readonly int ID_NearPlane       = Shader.PropertyToID("_NearPlane");
    static readonly int ID_InvModelScale   = Shader.PropertyToID("_InvModelScale");

    // VFX Graph 端的属性名（和 VFX Graph 资源 Blackboard 里的命名保持一致）
    const string VFX_PositionBuffer  = "PositionBuffer";
    const string VFX_ColorBuffer     = "ColorBuffer";
    const string VFX_TransformBuffer = "TransformMatrixBuffer";
    const string VFX_PointCount      = "PointCount";
    const string VFX_AudioBands      = "AudioBandsFrequence";


    // ======================================================================
    // 生命周期
    // ======================================================================
    void OnEnable()
    {
        // 真正的初始化推迟到 Tick 里做（OnEnable 时 Inspector 字段可能还未就绪）
#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorApplication.update += EditorUpdate;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
#endif
        Cleanup();
    }

    void OnDestroy() => Cleanup();

    // 运行时驱动
    void Update()
    {
        if (!Application.isPlaying) return;
        Tick();
    }

#if UNITY_EDITOR
    // Edit Mode 驱动
    void EditorUpdate()
    {
        if (Application.isPlaying) return;
        if (this == null) { EditorApplication.update -= EditorUpdate; return; }

        Tick();
        SceneView.RepaintAll();   // 让 Scene View 跟着刷
    }
#endif

    /// <summary>
    /// 主循环。第一次或 OnValidate 之后会自动重新初始化。
    /// </summary>
    void Tick()
    {
        if (!isInitialized)
        {
            Initialize();
            if (!isInitialized) return;
        }

        Camera cam = ResolveCamera();
        if (cam == null) return;

        DispatchComputeShader(cam);
    }


    // ======================================================================
    // 初始化 / 释放
    // ======================================================================
    void Initialize()
    {
        if (isInitialized) return;
        if (visualEffect == null || splatData == null || computeShader == null) return;
        if (splatData.positions == null || splatData.positions.Length == 0) return;

        splatCount   = splatData.positions.Length;
        threadGroups = Mathf.CeilToInt(splatCount / 64.0f);
        audioSource  = GetComponent<AudioSource>();

        // ---- 静态数据 buffer ----
        positionBuffer        = new GraphicsBuffer(GraphicsBuffer.Target.Structured, splatCount, sizeof(float) * 3);
        colorBuffer           = new GraphicsBuffer(GraphicsBuffer.Target.Structured, splatCount, sizeof(float) * 4);
        scaleBuffer           = new GraphicsBuffer(GraphicsBuffer.Target.Structured, splatCount, sizeof(float) * 3);
        rotationBuffer        = new GraphicsBuffer(GraphicsBuffer.Target.Structured, splatCount, sizeof(float) * 4);
        outputTransformBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, splatCount, sizeof(float) * 4);

        positionBuffer.SetData(splatData.positions);
        colorBuffer.SetData(splatData.colors);
        scaleBuffer.SetData(splatData.scales);
        rotationBuffer.SetData(splatData.rotations);

        if (useAudioSpectrum && audioSource != null)
            audioBandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 8, sizeof(float) * 2);

        // ---- Compute Shader 绑定（一次）----
        kernelTransform = computeShader.FindKernel("ComputeSplatTransform");
        computeShader.SetInt(ID_Count, splatCount);
        computeShader.SetBuffer(kernelTransform, ID_PositionBuffer, positionBuffer);
        computeShader.SetBuffer(kernelTransform, ID_ScaleBuffer,    scaleBuffer);
        computeShader.SetBuffer(kernelTransform, ID_RotationBuffer, rotationBuffer);
        computeShader.SetBuffer(kernelTransform, ID_OutputBuffer,   outputTransformBuffer);

        // ---- VFX Graph 绑定（一次）----
        visualEffect.SetInt(VFX_PointCount, splatCount);
        visualEffect.SetGraphicsBuffer(VFX_PositionBuffer,  positionBuffer);
        visualEffect.SetGraphicsBuffer(VFX_ColorBuffer,     colorBuffer);
        visualEffect.SetGraphicsBuffer(VFX_TransformBuffer, outputTransformBuffer);

        // 先跑一次让首帧就有有效数据
        Camera cam = ResolveCamera();
        if (cam != null) DispatchComputeShader(cam);

        // 触发 VFX 播放（Edit Mode 下也有效）
        visualEffect.Play();

        isInitialized = true;
    }

    void Cleanup()
    {
        positionBuffer?.Release();        positionBuffer = null;
        colorBuffer?.Release();           colorBuffer = null;
        scaleBuffer?.Release();           scaleBuffer = null;
        rotationBuffer?.Release();        rotationBuffer = null;
        outputTransformBuffer?.Release(); outputTransformBuffer = null;
        audioBandBuffer?.Release();       audioBandBuffer = null;
        isInitialized = false;
    }


    // ======================================================================
    // 每帧 Dispatch
    // ======================================================================
    void DispatchComputeShader(Camera cam)
    {
        // 局部坐标管线：buffer 里是 VFX 局部（模型）空间坐标，
        // modelView = 世界→相机 · 局部→世界，一次性把 splat 变到 view 空间
        Transform vfxT = visualEffect.transform;
        computeShader.SetMatrix(ID_ModelViewMatrix, cam.worldToCameraMatrix * vfxT.localToWorldMatrix);
        computeShader.SetMatrix(ID_ProjMatrix, GL.GetGPUProjectionMatrix(cam.projectionMatrix, false));
        computeShader.SetVector(ID_ScreenSize, new Vector2(cam.pixelWidth, cam.pixelHeight));
        computeShader.SetFloat (ID_NearPlane,  cam.nearClipPlane);

        // 尺寸补偿：compute 算出的是世界单位半轴，VFX quad size 是局部单位。
        // 仅支持均匀缩放；非均匀缩放 billboard 方案表达不了，检测到告警一次。
        Vector3 s = vfxT.lossyScale;
        if (!warnedNonUniformScale &&
            (Mathf.Abs(s.x - s.y) > 1e-4f || Mathf.Abs(s.x - s.z) > 1e-4f))
        {
            warnedNonUniformScale = true;
            Debug.LogWarning("[VFXGraphicsBuffer] 检测到非均匀缩放，3DGS billboard 仅支持均匀缩放，请保持 XYZ 一致。", this);
        }
        computeShader.SetFloat(ID_InvModelScale, 1f / Mathf.Max(Mathf.Abs(s.x), 1e-8f));

        computeShader.Dispatch(kernelTransform, threadGroups, 1, 1);

        if (useAudioSpectrum) UpdateAudioBands();
    }

    void UpdateAudioBands()
    {
        if (audioBandBuffer == null) return;

        for (int i = 0; i < 8; i++)
        {
            float fre = Mathf.Max(AudioVis._audioBandBuffer[i], 0f);
            float amp = Mathf.Max(AudioVis._amplitudeBuffer, 0f);
            audioBands[i] = new Vector2(fre, amp);
        }
        audioBandBuffer.SetData(audioBands);
        visualEffect.SetGraphicsBuffer(VFX_AudioBands, audioBandBuffer);
    }


    // ======================================================================
    // 相机选择
    //   1. Inspector 指定的 targetCamera
    //   2. 运行时: Camera.main
    //   3. Edit Mode: 当前 Scene View 相机
    // ======================================================================
    Camera ResolveCamera()
    {
        if (targetCamera != null) return targetCamera;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            SceneView sv = SceneView.lastActiveSceneView;
            if (sv != null && sv.camera != null) return sv.camera;
        }
#endif
        return Camera.main;
    }


    // ======================================================================
    // Gizmo / Editor 辅助
    // ======================================================================
    void OnDrawGizmos()
    {
        if (visualEffect == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(visualEffect.transform.position, Vector3.one * 10f);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Inspector 字段被改后清理一次，让下一帧 Tick 重新 Initialize
        if (isInitialized) Cleanup();
    }
#endif
}
