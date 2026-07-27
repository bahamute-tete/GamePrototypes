using System;
using UnityEngine;
using UnityEngine.Playables;

// =============================================================================
// GodRayBehaviour
// -----------------------------------------------------------------------------
// Per-clip data container. Each clip on the GodRayTrack holds a serialized
// instance of this; the mixer reads it via input playable and blends across
// overlapping clips.
//
// Only continuous-domain parameters live here (float / Color). Discrete or
// structural settings (sampleCount, skyOnly, sourceMode, downsample) belong on
// the VolumeComponent or RenderFeature where they're not meant to be animated.
// =============================================================================

[Serializable]
public class GodRayBehaviour : PlayableBehaviour
{
    [Tooltip("Overall god ray intensity at this clip. 0 = effectively off.")]
    public float intensity = 1f;

    [Range(0f, 1f)]
    [Tooltip("Luminance threshold (used only in Luminance source mode).")]
    public float threshold = 0.7f;

    [Tooltip("Length of the radial trails.")]
    public float blurStrength = 1f;

    [Tooltip("How quickly rays fall off from the light.")]
    public float blurFalloff = 0.5f;

    [ColorUsage(showAlpha: false, hdr: true)]
    [Tooltip("Color tint applied during composite. HDR-capable for dramatic golden/sunset rays.")]
    public Color tintColor = Color.white;
}
