using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Collider))]
public class SphereFogVolume : MonoBehaviour
{
    public static SphereFogVolume ActiveVolume { get; private set; }

    [Header("Fog")]
    [Min(0.001f)] public float smoothness = 5f;
    [Range(0, 1)] public float density = 1f;
    [ColorUsage(true, true)]
    public Color fogColor = new Color(0.6f, 0.65f, 0.7f, 1f);

    [Header("Skybox")]
    [Tooltip("是否让 SphereFog 影响使用 MagicSkybox 的天空球。\n" +
             "开 → 玩家在雾体积内时天空被雾色染;关 → 天空不受任何影响。")]
    public bool affectSkybox = true;

    [Tooltip("把天空盒视作距相机此远 (米) 的一个虚拟点。\n" +
             "当雾体积尺寸 > 此值时,虚拟点落入体积内的清净区 → 天空显现;\n" +
             "当雾体积尺寸 < 此值时,虚拟点在体积外的雾区 → 天空被遮。\n" +
             "经验值: 想看到天空的房间尺度的 1-2 倍。比如 20m 室内空间,设 30-50。")]
    [Min(0.1f)] public float skyDistance = 50f;

    [Header("Noise (顶点级或 Shader 自选)")]
    public Texture2D noiseTexture;
    public float noiseScale = 0.1f;
    public float noiseStrength = 3f;
    public Vector3 noiseSpeed = new Vector3(0.02f, 0.01f, 0.015f);

    [System.NonSerialized] public Collider fogCollider;

    static readonly int ID_Shape = Shader.PropertyToID("_SF_FogShape");
    static readonly int ID_Center = Shader.PropertyToID("_SF_FogCenter");
    static readonly int ID_Radius = Shader.PropertyToID("_SF_SphereRadius");
    static readonly int ID_AxisX = Shader.PropertyToID("_SF_BoxAxisX");
    static readonly int ID_AxisY = Shader.PropertyToID("_SF_BoxAxisY");
    static readonly int ID_AxisZ = Shader.PropertyToID("_SF_BoxAxisZ");
    static readonly int ID_Smooth = Shader.PropertyToID("_SF_Smoothness");
    static readonly int ID_Density = Shader.PropertyToID("_SF_Density");
    static readonly int ID_Color = Shader.PropertyToID("_SF_FogColor");
    static readonly int ID_NScale = Shader.PropertyToID("_SF_NoiseScale");
    static readonly int ID_NStr = Shader.PropertyToID("_SF_NoiseStrength");
    static readonly int ID_NSpeed = Shader.PropertyToID("_SF_NoiseSpeed");
    static readonly int ID_AffectSky = Shader.PropertyToID("_SF_AffectSky");
    static readonly int ID_SkyDistance = Shader.PropertyToID("_SF_SkyDistance");

    void OnEnable()
    {
        fogCollider = GetComponent<Collider>();
        ActiveVolume = this;
    }

    void OnDisable()
    {
        if (ActiveVolume == this)
        {
            ActiveVolume = null;
            // 清掉 density 让所有 Shader 不再起雾
            Shader.SetGlobalFloat(ID_Density, 0f);
            // 同时关掉天空雾,避免下次别的 Volume 启用前残留这个开关
            Shader.SetGlobalFloat(ID_AffectSky, 0f);
        }
    }

    void LateUpdate()
    {
        if (fogCollider == null) return;

        Transform t = transform;

        switch (fogCollider)
        {
            case SphereCollider s:
                {
                    Vector3 ls = t.lossyScale;
                    float worldR = s.radius * Mathf.Max(ls.x, Mathf.Max(ls.y, ls.z));
                    Shader.SetGlobalFloat(ID_Shape, 0f);
                    Shader.SetGlobalVector(ID_Center, t.TransformPoint(s.center));
                    Shader.SetGlobalFloat(ID_Radius, worldR);
                    break;
                }
            case BoxCollider b:
                {
                    Vector3 ls = t.lossyScale;
                    Vector3 half = b.size * 0.5f;
                    Shader.SetGlobalFloat(ID_Shape, 1f);
                    Shader.SetGlobalVector(ID_Center, t.TransformPoint(b.center));
                    Shader.SetGlobalVector(ID_AxisX, new Vector4(t.right.x, t.right.y, t.right.z, half.x * Mathf.Abs(ls.x)));
                    Shader.SetGlobalVector(ID_AxisY, new Vector4(t.up.x, t.up.y, t.up.z, half.y * Mathf.Abs(ls.y)));
                    Shader.SetGlobalVector(ID_AxisZ, new Vector4(t.forward.x, t.forward.y, t.forward.z, half.z * Mathf.Abs(ls.z)));
                    break;
                }
        }

        Shader.SetGlobalFloat(ID_Smooth, Mathf.Max(0.001f, smoothness));
        Shader.SetGlobalFloat(ID_Density, density);
        Shader.SetGlobalColor(ID_Color, fogColor);
        Shader.SetGlobalFloat(ID_NScale, noiseScale);
        Shader.SetGlobalFloat(ID_NStr, noiseStrength);
        Shader.SetGlobalVector(ID_NSpeed, noiseSpeed);
        Shader.SetGlobalFloat(ID_AffectSky, affectSkybox ? 1f : 0f);
        Shader.SetGlobalFloat(ID_SkyDistance, Mathf.Max(0.1f, skyDistance));
    }
}