using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class MagicWaterController : MonoBehaviour
{
    // ============================================================
    //   Water Plane Follow
    // ============================================================
    [Header("Plane Follow")]
    [Tooltip("水面 Y 高度（世界空间）")]
    public float waterLevelY = 0f;

    [Tooltip("跟随目标（一般为 VR 相机）。留空则使用 Camera.main")]
    public Transform followTarget;


    // ============================================================
    //   Sky Material Reference
    //   ★ 重要：拖入当前场景使用的 MagicSkybox 材质实例
    //   tint/exposure/rotation 是这张材质的 per-material 属性，
    //   Controller 会通过 SetColor/SetFloat 直接写入它
    // ============================================================
    [Header("Sky Material")]
    [Tooltip("当前 Lighting → Environment 里的天空盒材质。\n" +
             "_TintA/B、_ExposureA/B、_RotationA/B 这 6 个参数会被推送到这张材质上。\n" +
             "如果为空，sky 状态字段将无效。")]
    public Material skyMaterial;


    // ============================================================
    //   Sky Transition (drives Sky Cubemap A↔B mix in shader)
    //   shader 里 _SkyBlend 是 GLOBAL（不在 CBUFFER 内）→ 走 Shader.SetGlobal
    // ============================================================
    [Header("Sky Transition (0 = Sky A, 1 = Sky B)")]
    [Tooltip("天空 cubemap A↔B 的过渡值。\n" +
             "通常由 SkyTransitionTrack（Timeline）驱动，也可以脚本直接赋值。")]
    [Range(0, 1)] public float skyBlend = 0f;


    // ============================================================
    //   Sky A State (tint / exposure / rotation)
    //   shader 里这些在 CBUFFER 内 → 走 material.SetXXX
    //   由 SkyATrack 在 Timeline 上动画化，也可以手动在 Inspector 调
    // ============================================================
    [Header("Sky A State")]
    public Color skyTintA = Color.white;
    [Range(0, 8)]      public float skyExposureA = 1f;
    [Range(-360, 360)] public float skyRotationA = 0f;


    // ============================================================
    //   Sky B State (tint / exposure / rotation)
    // ============================================================
    [Header("Sky B State")]
    public Color skyTintB = Color.white;
    [Range(0, 8)]      public float skyExposureB = 1f;
    [Range(-360, 360)] public float skyRotationB = 0f;


    // ============================================================
    //   Horizon Glow Transition (drives Glow A↔B params)
    //   horizon glow 参数都是 GLOBAL → 走 Shader.SetGlobal
    // ============================================================
    [Header("Horizon Glow Transition (0 = Glow A, 1 = Glow B)")]
    [Tooltip("天际线发光 A↔B 的过渡值，与 skyBlend 独立。")]
    [Range(0, 1)] public float horizonBlend = 0f;


    // ============================================================
    //   Horizon Glow A (horizonBlend = 0)
    // ============================================================
    [Header("Horizon Glow A (horizonBlend = 0)")]
    [ColorUsage(false, true)] public Color horizonColorA = new Color(1.2f, 0.8f, 0.45f, 1f);
    [Min(0)] public float horizonIntensityA = 2.0f;
    [Range(0.1f, 40f)] public float horizonFalloffA = 6.0f;
    [Min(0)] public float haloIntensityA = 0.4f;
    [Range(0.1f, 10f)] public float haloFalloffA = 1.2f;


    // ============================================================
    //   Horizon Glow B (horizonBlend = 1)
    // ============================================================
    [Header("Horizon Glow B (horizonBlend = 1)")]
    [ColorUsage(false, true)] public Color horizonColorB = new Color(0.3f, 0.4f, 1.0f, 1f);
    [Min(0)] public float horizonIntensityB = 0.6f;
    [Range(0.1f, 40f)] public float horizonFalloffB = 4.0f;
    [Min(0)] public float haloIntensityB = 0.25f;
    [Range(0.1f, 10f)] public float haloFalloffB = 1.5f;


    // ============================================================
    //   Shader Property IDs
    // ============================================================
    static readonly int ID_SkyBlend         = Shader.PropertyToID("_SkyBlend");

    // 这 6 个是 per-material（CBUFFER 内），走 material.SetXXX
    static readonly int ID_TintA            = Shader.PropertyToID("_TintA");
    static readonly int ID_ExposureA        = Shader.PropertyToID("_ExposureA");
    static readonly int ID_RotationA        = Shader.PropertyToID("_RotationA");
    static readonly int ID_TintB            = Shader.PropertyToID("_TintB");
    static readonly int ID_ExposureB        = Shader.PropertyToID("_ExposureB");
    static readonly int ID_RotationB        = Shader.PropertyToID("_RotationB");

    // 这些是 global（在 HorizonGlow.hlsl 里以全局形式声明）
    // 用 _MW_ 前缀避免与项目其它 shader 的同名属性冲突（防止 SetGlobal 与他人注册的 Texture 类型互撞）
    static readonly int ID_HorizonColor     = Shader.PropertyToID("_MW_HorizonColor");
    static readonly int ID_HorizonIntensity = Shader.PropertyToID("_MW_HorizonIntensity");
    static readonly int ID_HorizonFalloff   = Shader.PropertyToID("_MW_HorizonFalloff");
    static readonly int ID_HaloIntensity    = Shader.PropertyToID("_MW_HaloIntensity");
    static readonly int ID_HaloFalloff      = Shader.PropertyToID("_MW_HaloFalloff");


    void OnEnable()   {
        skyBlend = 0;
        horizonBlend = 0;
        Push(); 
    }
    void OnValidate() { if (isActiveAndEnabled) Push(); }

    void LateUpdate()
    {
        // 水面跟随相机 XZ，Y 锁定
        Transform t = followTarget;
        if (t == null && Camera.main != null) t = Camera.main.transform;
        if (t != null)
        {
            Vector3 p = t.position;
            transform.position = new Vector3(p.x, waterLevelY, p.z);
        }

        Push();
    }

    void Push()
    {
        // ====================================================
        //   GLOBAL: Sky Blend
        // ====================================================
        Shader.SetGlobalFloat(ID_SkyBlend, skyBlend);

        // ====================================================
        //   PER-MATERIAL: Sky A/B Tint / Exposure / Rotation
        //   这些参数在 shader 的 CBUFFER 内，必须写到材质上
        // ====================================================
        if (skyMaterial != null)
        {
            skyMaterial.SetColor(ID_TintA,     skyTintA);
            skyMaterial.SetFloat(ID_ExposureA, skyExposureA);
            skyMaterial.SetFloat(ID_RotationA, skyRotationA);

            skyMaterial.SetColor(ID_TintB,     skyTintB);
            skyMaterial.SetFloat(ID_ExposureB, skyExposureB);
            skyMaterial.SetFloat(ID_RotationB, skyRotationB);
        }

        // ====================================================
        //   GLOBAL: Horizon Glow A → B（由 horizonBlend 控制）
        // ====================================================
        Color c  = Color.Lerp(horizonColorA,    horizonColorB,    horizonBlend);
        float i  = Mathf.Lerp(horizonIntensityA, horizonIntensityB, horizonBlend);
        float fo = Mathf.Lerp(horizonFalloffA,   horizonFalloffB,   horizonBlend);
        float hi = Mathf.Lerp(haloIntensityA,    haloIntensityB,    horizonBlend);
        float hf = Mathf.Lerp(haloFalloffA,      haloFalloffB,      horizonBlend);

        Shader.SetGlobalColor(ID_HorizonColor,     c);
        Shader.SetGlobalFloat(ID_HorizonIntensity, i);
        Shader.SetGlobalFloat(ID_HorizonFalloff,   fo);
        Shader.SetGlobalFloat(ID_HaloIntensity,    hi);
        Shader.SetGlobalFloat(ID_HaloFalloff,      hf);
    }
}
