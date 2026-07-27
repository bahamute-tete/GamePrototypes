using UnityEngine;

// ====================================================================
//   HorizonGlowState
//   Horizon Glow 一组参数的打包结构体（5 个字段）
//   HorizonGlowAClip 和 HorizonGlowBClip 都用这个类型存 start/end state
// ====================================================================

[System.Serializable]
public struct HorizonGlowState
{
    [ColorUsage(false, true)] public Color color;
    [Min(0)]                  public float intensity;
    [Range(0.1f, 40f)]        public float falloff;
    [Min(0)]                  public float haloIntensity;
    [Range(0.1f, 10f)]        public float haloFalloff;

    public static HorizonGlowState Default => new HorizonGlowState
    {
        color         = new Color(1.2f, 0.8f, 0.45f, 1f),
        intensity     = 2.0f,
        falloff       = 6.0f,
        haloIntensity = 0.4f,
        haloFalloff   = 1.2f,
    };

    public static HorizonGlowState Lerp(HorizonGlowState a, HorizonGlowState b, float t)
    {
        return new HorizonGlowState
        {
            color         = Color.Lerp(a.color, b.color, t),
            intensity     = Mathf.Lerp(a.intensity, b.intensity, t),
            falloff       = Mathf.Lerp(a.falloff, b.falloff, t),
            haloIntensity = Mathf.Lerp(a.haloIntensity, b.haloIntensity, t),
            haloFalloff   = Mathf.Lerp(a.haloFalloff, b.haloFalloff, t),
        };
    }
}
