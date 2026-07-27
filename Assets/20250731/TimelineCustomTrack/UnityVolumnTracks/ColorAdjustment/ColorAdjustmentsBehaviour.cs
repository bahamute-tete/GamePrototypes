using UnityEngine;
using UnityEngine.Playables;

// Clip 上可编辑的参数,所有字段会与原值在 Mixer 内做权重混合
[System.Serializable]
public class ColorAdjustmentsBehaviour : PlayableBehaviour
{
    [Tooltip("曝光补偿,单位 EV(以 2 为底)")]
    public float postExposure = 0f;

    [Range(-100f, 100f)]
    public float contrast = 0f;

    [ColorUsage(true, true)]   // showAlpha + HDR
    public Color colorFilter = Color.white;

    [Range(-180f, 180f)]
    public float hueShift = 0f;

    [Range(-100f, 100f)]
    public float saturation = 0f;
}
