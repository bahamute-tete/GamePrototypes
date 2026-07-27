using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// =============================================================================
// God Ray Render Feature
// -----------------------------------------------------------------------------
// Settings here are STRUCTURAL: shader reference, downsample factor, exclude
// layer mask. Runtime tuning (intensity, threshold, blur strength, color) lives
// on GodRayVolumeComponent — drop one of those into a VolumeProfile to control
// the effect per-room or animate it via Timeline.
//
// Material is created once and reused across pass recreations. This avoids the
// classic URP RenderFeature pitfall where every Inspector slider drag rebuilds
// the material (causing GC pressure & Editor stutter).
// =============================================================================

public class GodRayRenderFeature : ScriptableRendererFeature
{
    // -------------------------------------------------------------------------
    // Source mode controls how Pass 0 (Highlight) decides which pixels become
    // god ray sources. Switch via Inspector — change is picked up next frame.
    // -------------------------------------------------------------------------
    public enum SourceMode
    {
        [Tooltip("Cheap. Bright pixels (sky + threshold) become sources. Needs visible bright sources to look good.")]
        LuminanceThreshold = 0,

        [Tooltip("~3x cost of Luminance. Per-pixel ray-march toward the light, counting unobstructed depth samples. Stable in any lighting; works indoors with no sky.")]
        ScreenSpaceOcclusion = 1,
    }

    [System.Serializable]
    public class Settings
    {
        [Tooltip("When in the URP frame this pass runs. AfterRenderingTransparents is the typical choice.")]
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;

        [Tooltip("The GodRay shader. Assign Custom/GodRay_RadialBlur here.")]
        public Shader shader;

        [Tooltip("How sources are detected. See SourceMode tooltips for cost/quality tradeoff.")]
        public SourceMode sourceMode = SourceMode.LuminanceThreshold;

        [Tooltip("Resolution divider for the bright-mask / blur RTs. 2 = half-res (recommended), 4 = quarter-res for very low-end Mobile VR.")]
        [Range(1, 4)] public int downsample = 2;

        [Tooltip("Occlusion mode only: number of depth samples per pixel along the ray to the light. 6-8 for Mobile VR, 12-16 for desktop.")]
        [Range(4, 24)] public int occlusionSteps = 8;

        [Tooltip("Occlusion mode only: how far (in screen-space UV) each pixel marches toward the light. 0.4-0.6 is a sensible range.")]
        [Range(0.1f, 1.0f)] public float occlusionMaxRayLength = 0.5f;

        [Tooltip("Geometry on these layers will NOT contribute to god ray sources (e.g. UI, characters, hands).")]
        public LayerMask excludeLayers = 0;
    }

    public Settings settings = new Settings();

    private GodRayRenderPass _pass;
    private Material         _material;

    public override void Create()
    {
        if (settings.shader == null)
            return;

        // Release any RTs held by a previous pass instance — Create() is also
        // called on every Inspector change, and without this the old pass's
        // RTHandles would leak each time the user touches a slider.
        if (_pass != null)
        {
            _pass.Cleanup();
            _pass = null;
        }

        // Reuse material across Inspector edits — only create when missing or
        // when the shader reference has changed.
        if (_material == null || _material.shader != settings.shader)
        {
            CoreUtils.Destroy(_material);
            _material = CoreUtils.CreateEngineMaterial(settings.shader);
        }

        _pass = new GodRayRenderPass(_material, settings)
        {
            renderPassEvent = settings.passEvent
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.shader == null || _material == null || _pass == null)
            return;

        // Skip non-game cameras: reflection probes & preview windows don't need
        // god rays and would just waste GPU + cause flicker in the Inspector.
        var cameraType = renderingData.cameraData.cameraType;
        if (cameraType == CameraType.Reflection || cameraType == CameraType.Preview)
            return;

        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        if (_pass != null)
        {
            _pass.Cleanup();
            _pass = null;
        }
        if (_material != null)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }
    }
}
