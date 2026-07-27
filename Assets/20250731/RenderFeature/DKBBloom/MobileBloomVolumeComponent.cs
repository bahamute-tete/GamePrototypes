using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable, VolumeComponentMenu("Custom/Dual Kawase Bloom")]
public class MobileBloomVolumeComponent : VolumeComponent, IPostProcessComponent
{
    [Header("Threshold")]
    public MinFloatParameter     threshold = new MinFloatParameter(1.0f, 0f);
    public ClampedFloatParameter softKnee  = new ClampedFloatParameter(0.5f, 0f, 1f);

    [Header("Bloom")]
    // intensity 默认 0,只有 Volume 主动 override 才开,符合 URP 规范
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 4f);
    public ColorParameter        tint      = new ColorParameter(Color.white, hdr: false, showAlpha: false, showEyeDropper: true);
    public ClampedFloatParameter scatter   = new ClampedFloatParameter(0.7f, 0f, 1f);

    [Header("Quality")]
    public ClampedIntParameter iterations          = new ClampedIntParameter(3, 1, 4);
    public BoolParameter       halfResolutionStart = new BoolParameter(true);

    public bool IsActive()         => intensity.value > 0f;
    public bool IsTileCompatible() => false;
}
