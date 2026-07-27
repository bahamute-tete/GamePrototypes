using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// =============================================================================
// God Ray Volume Component
// -----------------------------------------------------------------------------
// Runtime, per-volume parameters. Drop one of these into a VolumeProfile on
// each LBE room to have god ray strength / threshold / tint switch as the
// player walks between volumes, or animate them via Timeline (Volume Profile
// clips).
//
// SourceMode (Luminance vs Occlusion) is intentionally NOT here — it lives on
// the RenderFeature itself. That choice drives a shader keyword variant, which
// is project-level structural configuration, not a per-room blend value.
//
// Some parameters are mode-specific. They're uploaded regardless and silently
// ignored by the shader branch that doesn't use them; the tooltips note this.
// =============================================================================

[Serializable]
[VolumeComponentMenuForRenderPipeline("Custom/God Ray (Radial Blur)", typeof(UniversalRenderPipeline))]
public class GodRayVolumeComponent : VolumeComponent, IPostProcessComponent
{
    [Tooltip("Overall god ray intensity. 0 disables the effect (pass early-outs, no GPU cost).")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 10f);

    [Tooltip("[Luminance mode only] Brightness threshold for sources. Pixels brighter than this become god ray emitters.")]
    public ClampedFloatParameter threshold = new ClampedFloatParameter(0.7f, 0f, 1f);

    [Tooltip("Length of the radial blur trails. 0 = no rays, 2 = very long shafts.")]
    public ClampedFloatParameter blurStrength = new ClampedFloatParameter(1f, 0f, 2f);

    [Tooltip("How quickly the rays fall off away from the light. Higher = tighter rays.")]
    public ClampedFloatParameter blurFalloff = new ClampedFloatParameter(0.5f, 0f, 2f);

    [Tooltip("Sample count for the radial blur (Pass 1). 8-12 recommended for Mobile VR (PICO/Quest), 16+ for desktop.")]
    public ClampedIntParameter sampleCount = new ClampedIntParameter(12, 4, 32);

    [Tooltip("Color tint applied during the composite step. HDR-enabled.")]
    public ColorParameter tintColor = new ColorParameter(Color.white, true, false, true);

    [Tooltip("[Luminance mode only] If true, ONLY sky-depth pixels contribute as sources. Use for outdoor sun-shafts where you want a clean look.")]
    public BoolParameter skyOnly = new BoolParameter(false);

    [Tooltip("Sun disc brightness at the light center. 0 = no disc (just streaks). 1-3 typical for visible sun core. Fills the area the radial blur can't reach — fixes the 'black hole' that appears when the light has no naturally bright source pixel.")]
    public ClampedFloatParameter sunDiscIntensity = new ClampedFloatParameter(1.0f, 0f, 5f);

    [Tooltip("Sun disc radius as fraction of screen UV. 0.02 = small bright point, 0.10 = soft glow. 0.04 is a good starting value.")]
    public ClampedFloatParameter sunDiscSize = new ClampedFloatParameter(0.04f, 0.005f, 0.2f);

    public bool IsActive() => intensity.value > 0f;
    public bool IsTileCompatible() => false;
}
