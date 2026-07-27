using UnityEngine;

/// <summary>
/// 挂载在带有 MeshRenderer + Plane Mesh 的 GameObject 上。
/// 负责：运行时参数同步到 Material、可选的摄像机跟随。
///
/// 使用步骤：
///   1. 创建一个 3D Object > Plane，放置到想要的世界 Y 高度（角色小腿处）
///   2. 新建 Material，Shader 选 Custom/GroundFog
///   3. 将 Material 赋给 Plane 的 MeshRenderer
///   4. 将本脚本挂载到 Plane 上
///   5. 在 URP Asset 中打开 Depth Texture
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public class GroundFogController : MonoBehaviour
{
    // ─── 颜色与密度 ─────────────────────────────────────────────────────
    [Header("Color & Density")]
    [ColorUsage(false, false)]
    public Color  fogColor   = new Color(0.85f, 0.92f, 1.00f, 1.00f);

    [Range(0f, 1f)]
    public float fogDensity  = 0.75f;

    // ─── 噪声 ────────────────────────────────────────────────────────────
    [Header("Noise")]
    public Texture2D noiseTex;
    public float     noiseScale  = 0.08f;
    public float     noisePower  = 1.4f;
    public float     flowSpeed   = 0.04f;

    // ─── 深度软边（控制角色腿部与雾气的融合过渡范围）────────────────────
    [Header("Depth Soft Edge")]
    [Tooltip("值越大，角色/场景物体与雾气的交界过渡越柔和。建议从 1~2 开始调。")]
    public float softEdgeRange = 1.2f;

    // ─── 形状 ────────────────────────────────────────────────────────────
    [Header("Shape")]
    [Tooltip("边缘淡出的幂次，值越大衰减越快（雾气中心更浓）")]
    public float edgeFadePower = 1.2f;

    // ─── 摄像机跟随 ──────────────────────────────────────────────────────
    [Header("Camera Follow")]
    [Tooltip("开启后，Plane 会在 XZ 平面跟随摄像机，确保始终覆盖玩家周围")]
    public bool      followCamera    = true;
    public Transform cameraTransform;

    // ─── 私有 ─────────────────────────────────────────────────────────────
    private MeshRenderer _meshRenderer;
    private Material     _material;

    // ShaderPropertyID 缓存，避免每帧字符串哈希
    static readonly int ID_FogColor      = Shader.PropertyToID("_FogColor");
    static readonly int ID_FogDensity    = Shader.PropertyToID("_FogDensity");
    static readonly int ID_NoiseTex      = Shader.PropertyToID("_NoiseTex");
    static readonly int ID_NoiseScale    = Shader.PropertyToID("_NoiseScale");
    static readonly int ID_NoisePower    = Shader.PropertyToID("_NoisePower");
    static readonly int ID_Speed         = Shader.PropertyToID("_Speed");
    static readonly int ID_SoftEdgeRange = Shader.PropertyToID("_SoftEdgeRange");
    static readonly int ID_EdgeFadePower = Shader.PropertyToID("_EdgeFadePower");

    // ─────────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        _meshRenderer = GetComponent<MeshRenderer>();

        // 运行时使用实例化 Material，编辑器下共享 sharedMaterial（避免污染资产）
#if UNITY_EDITOR
        _material = _meshRenderer.sharedMaterial;
#else
        _material = _meshRenderer.material;
#endif

        if (cameraTransform == null)
            cameraTransform = Camera.main != null ? Camera.main.transform : null;
    }

    void OnDisable()
    {
#if !UNITY_EDITOR
        // 运行时销毁实例化 Material，防止内存泄漏
        if (_material != null)
            Destroy(_material);
#endif
    }

    void Update()
    {
        if (_material == null) return;

        SyncMaterialProperties();
        HandleCameraFollow();
    }

    // ─── 同步参数到 Material ──────────────────────────────────────────────
    void SyncMaterialProperties()
    {
        _material.SetColor  (ID_FogColor,      fogColor);
        _material.SetFloat  (ID_FogDensity,    fogDensity);
        _material.SetFloat  (ID_NoiseScale,    noiseScale);
        _material.SetFloat  (ID_NoisePower,    noisePower);
        _material.SetFloat  (ID_Speed,         flowSpeed);
        _material.SetFloat  (ID_SoftEdgeRange, softEdgeRange);
        _material.SetFloat  (ID_EdgeFadePower, edgeFadePower);

        if (noiseTex != null)
            _material.SetTexture(ID_NoiseTex, noiseTex);
    }

    // ─── 摄像机跟随（只移动 XZ，Y 保持不变） ────────────────────────────
    void HandleCameraFollow()
    {
        if (!followCamera || cameraTransform == null) return;

        Vector3 pos = transform.position;
        pos.x = cameraTransform.position.x;
        pos.z = cameraTransform.position.z;
        transform.position = pos;
    }

#if UNITY_EDITOR
    // 编辑器下每帧也更新，方便实时预览参数调整
    void OnValidate()
    {
        if (_material == null) return;
        SyncMaterialProperties();
    }
#endif
}
