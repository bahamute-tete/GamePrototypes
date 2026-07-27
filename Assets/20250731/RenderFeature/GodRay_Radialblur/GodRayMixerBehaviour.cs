using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;

// =============================================================================
// GodRayMixerBehaviour
// -----------------------------------------------------------------------------
// One instance per GodRayTrack. Each ProcessFrame:
//
//   1. Pull every active input clip's weight + behaviour data
//   2. Weighted-sum the parameters
//   3. Fill any "missing weight" (clip edges that don't sum to 1) with the
//      Volume's cached original values, producing a smooth fade in/out
//   4. Write the result back into the bound Volume's GodRayVolumeComponent
//
// On graph destruction, restore the cached values so the Volume isn't left
// in an animated state when Timeline stops or preview ends.
// =============================================================================

public class GodRayMixerBehaviour : PlayableBehaviour
{
    private GodRayVolumeComponent _component;

    // Original values cached on first ProcessFrame — restored on destroy.
    private bool   _cached;
    private float  _origIntensity;
    private float  _origThreshold;
    private float  _origBlurStrength;
    private float  _origBlurFalloff;
    private Color  _origTintColor;

    // Track which fields we set override on (so we don't accidentally enable
    // override states the user never wanted).
    private bool _origIntensityOverride;
    private bool _origThresholdOverride;
    private bool _origBlurStrengthOverride;
    private bool _origBlurFalloffOverride;
    private bool _origTintColorOverride;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        Volume volume = playerData as Volume;
        if (volume == null || volume.profile == null)
            return;

        if (!volume.profile.TryGet<GodRayVolumeComponent>(out var component))
            return;

        // First-time setup: snapshot original values so we can fade in/out
        // gracefully and restore on destroy.
        if (!_cached || _component != component)
        {
            _component                  = component;
            _origIntensity              = component.intensity.value;
            _origThreshold              = component.threshold.value;
            _origBlurStrength           = component.blurStrength.value;
            _origBlurFalloff            = component.blurFalloff.value;
            _origTintColor              = component.tintColor.value;
            _origIntensityOverride      = component.intensity.overrideState;
            _origThresholdOverride      = component.threshold.overrideState;
            _origBlurStrengthOverride   = component.blurStrength.overrideState;
            _origBlurFalloffOverride    = component.blurFalloff.overrideState;
            _origTintColorOverride      = component.tintColor.overrideState;
            _cached = true;
        }

        int inputCount = playable.GetInputCount();

        float intensity     = 0f;
        float threshold     = 0f;
        float blurStrength  = 0f;
        float blurFalloff   = 0f;
        Color tintColor     = new Color(0f, 0f, 0f, 0f);
        float totalWeight   = 0f;

        for (int i = 0; i < inputCount; i++)
        {
            float w = playable.GetInputWeight(i);
            if (w <= 0f) continue;

            var inputPlayable = (ScriptPlayable<GodRayBehaviour>)playable.GetInput(i);
            var data = inputPlayable.GetBehaviour();
            if (data == null) continue;

            intensity     += data.intensity    * w;
            threshold     += data.threshold    * w;
            blurStrength  += data.blurStrength * w;
            blurFalloff   += data.blurFalloff  * w;
            tintColor     += data.tintColor    * w;
            totalWeight   += w;
        }

        // Fill missing weight with the cached original values. This is what
        // makes clip edges blend back to the scene's natural settings instead
        // of snapping to zero. Exactly mirrors how Unity's VolumeTrack behaves.
        if (totalWeight < 1f)
        {
            float remainder = 1f - totalWeight;
            intensity     += _origIntensity    * remainder;
            threshold     += _origThreshold    * remainder;
            blurStrength  += _origBlurStrength * remainder;
            blurFalloff   += _origBlurFalloff  * remainder;
            tintColor     += _origTintColor    * remainder;
        }

        // Apply. We force override = true while Timeline is driving the values;
        // restored in OnPlayableDestroy.
        component.intensity.value         = intensity;
        component.threshold.value         = threshold;
        component.blurStrength.value      = blurStrength;
        component.blurFalloff.value       = blurFalloff;
        component.tintColor.value         = tintColor;
        component.intensity.overrideState     = true;
        component.threshold.overrideState     = true;
        component.blurStrength.overrideState  = true;
        component.blurFalloff.overrideState   = true;
        component.tintColor.overrideState     = true;
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        if (!_cached || _component == null)
            return;

        // Restore cached values so the Volume isn't left in a Timeline-driven
        // state after preview/play stops.
        _component.intensity.value           = _origIntensity;
        _component.threshold.value           = _origThreshold;
        _component.blurStrength.value        = _origBlurStrength;
        _component.blurFalloff.value         = _origBlurFalloff;
        _component.tintColor.value           = _origTintColor;
        _component.intensity.overrideState   = _origIntensityOverride;
        _component.threshold.overrideState   = _origThresholdOverride;
        _component.blurStrength.overrideState= _origBlurStrengthOverride;
        _component.blurFalloff.overrideState = _origBlurFalloffOverride;
        _component.tintColor.overrideState   = _origTintColorOverride;

        _cached    = false;
        _component = null;
    }
}
