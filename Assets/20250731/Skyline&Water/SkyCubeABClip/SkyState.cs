using UnityEngine;

// ====================================================================
//   SkyState
//   Sky 一组参数的打包结构体（tint / exposure / rotation）
//   SkyAClip 和 SkyBClip 都用这个类型存 start/end state
// ====================================================================

[System.Serializable]
public struct SkyState
{
    public Color tint;
    [Range(0, 8)]      public float exposure;
    [Range(-360, 360)] public float rotation;

    public static SkyState Default => new SkyState
    {
        tint     = Color.white,
        exposure = 1f,
        rotation = 0f,
    };

    public static SkyState Lerp(SkyState a, SkyState b, float t)
    {
        return new SkyState
        {
            tint     = Color.Lerp(a.tint, b.tint, t),
            exposure = Mathf.Lerp(a.exposure, b.exposure, t),
            rotation = Mathf.Lerp(a.rotation, b.rotation, t),
        };
    }
}
