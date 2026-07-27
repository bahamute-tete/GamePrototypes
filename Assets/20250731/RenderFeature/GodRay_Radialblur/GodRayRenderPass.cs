using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// =============================================================================
// God Ray Render Pass (URP 14, XR Single Pass Instanced compatible)
// -----------------------------------------------------------------------------
// Pipeline overview per frame:
//
//   1. Build exclusion mask (DrawRenderers with override material, Pass 3)
//   2. Blitter ➜ Pass 0 : source        ➜ brightMaskRT     (extract sources)
//   3. Blitter ➜ Pass 1 : brightMaskRT  ➜ blurredRT        (radial blur)
//   4. Blitter ➜ Pass 2 : source        ➜ compositeRT      (composite)
//   5. Blitter (copy)   : compositeRT   ➜ source           (write back)
//
// The compositeRT round-trip is unavoidable: Pass 2 reads the original color
// AND writes color, which can't be the same RT. We absorb that cost on a
// full-resolution RGB-packed RT (HDR-friendly, smaller than RGBA32).
//
// All RTHandles are persistent across frames and only reallocated when the
// camera target descriptor changes (resolution, format, etc.) — see
// RenderingUtils.ReAllocateIfNeeded.
// =============================================================================

public class GodRayRenderPass : ScriptableRenderPass
{
    private static readonly ProfilingSampler s_ProfilingSampler =
        new ProfilingSampler("God Ray (Radial Blur)");

    // Material is owned by the RenderFeature and shared across pass recreations.
    private readonly Material _material;

    // Settings reference: the RenderFeature owns it. Holding the reference (not
    // a copy) means Inspector edits to Settings are picked up next frame without
    // needing to recreate the pass — no GC, no pipeline rebuild.
    private readonly GodRayRenderFeature.Settings _settings;

    // Persistent RTs (allocated once, resized only on descriptor change).
    private RTHandle _exclusionMaskRT;
    private RTHandle _brightMaskRT;
    private RTHandle _blurredRT;
    // _compositeRT removed — Pass 2 now uses hardware additive blend (Blend One One)
    // and writes directly into the camera color target. One fewer full-res RT,
    // one fewer tile resolve on Adreno.

    // Per-eye light screen positions (max 2 for stereo).
    private readonly Vector4[] _lightScreenPositions = new Vector4[2];

    // Pass indices (must match shader pass order).
    private const int PASS_HIGHLIGHT      = 0;
    private const int PASS_RADIAL_BLUR    = 1;
    private const int PASS_COMPOSITE      = 2;
    private const int PASS_EXCLUSION_MASK = 3;

    // Shader keyword that toggles Pass 0 between luminance threshold and
    // screen-space occlusion ray-march. Compiled into 2 variants of Pass 0.
    private const string KW_OCCLUSION_MODE = "_GODRAY_SOURCE_OCCLUSION";

    // Shader property IDs (cached for hot path).
    private static readonly int ID_LightScreenPos        = Shader.PropertyToID("_LightScreenPos");
    private static readonly int ID_LightColor            = Shader.PropertyToID("_LightColor");
    private static readonly int ID_TintColor             = Shader.PropertyToID("_TintColor");
    private static readonly int ID_GodRayParams          = Shader.PropertyToID("_GodRayParams");
    private static readonly int ID_GodRayParams2         = Shader.PropertyToID("_GodRayParams2");
    private static readonly int ID_SampleCount           = Shader.PropertyToID("_SampleCount");
    private static readonly int ID_AngleAttenuation      = Shader.PropertyToID("_AngleAttenuation");
    private static readonly int ID_UseSkyOnly            = Shader.PropertyToID("_UseSkyOnly");
    private static readonly int ID_OcclusionSteps        = Shader.PropertyToID("_OcclusionSteps");
    private static readonly int ID_OcclusionMaxRayLength = Shader.PropertyToID("_OcclusionMaxRayLength");
    private static readonly int ID_ExclusionMaskRT       = Shader.PropertyToID("_ExclusionMaskRT");

    private static readonly List<ShaderTagId> s_ShaderTagIds = new List<ShaderTagId>
    {
        new ShaderTagId("UniversalForward"),
        new ShaderTagId("UniversalForwardOnly"),
        new ShaderTagId("SRPDefaultUnlit"),
        new ShaderTagId("LightweightForward"),
    };

    public GodRayRenderPass(Material material, GodRayRenderFeature.Settings settings)
    {
        _material = material;
        _settings = settings;
    }

    // -------------------------------------------------------------------------
    // OnCameraSetup is the canonical place in URP 14 to allocate/resize RTs.
    // We declare we need both Color and Depth as inputs (so the renderer
    // guarantees they're available by the time we run).
    // -------------------------------------------------------------------------
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);

        var camDesc = renderingData.cameraData.cameraTargetDescriptor;
        camDesc.depthBufferBits = 0;
        camDesc.msaaSamples     = 1;

        // Low-res descriptor for the bright-mask / blur ping-pong.
        int downsample = Mathf.Max(1, _settings.downsample);
        var lowResDesc   = camDesc;
        lowResDesc.width  = Mathf.Max(1, camDesc.width  / downsample);
        lowResDesc.height = Mathf.Max(1, camDesc.height / downsample);

        // Mask format: single channel, low-res. Big bandwidth saving on Mobile VR.
        var maskDesc = lowResDesc;
        maskDesc.graphicsFormat = GraphicsFormat.R8_UNorm;

        // Color RTs: prefer R11G11B10 (HDR pack, no alpha) when supported. Else
        // fall back to whatever the camera target uses.
        var colorDesc = lowResDesc;
        if (SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, FormatUsage.Render))
            colorDesc.graphicsFormat = GraphicsFormat.B10G11R11_UFloatPack32;

        RenderingUtils.ReAllocateIfNeeded(ref _exclusionMaskRT, maskDesc,
            FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_GodRayExclusionMaskRT");
        RenderingUtils.ReAllocateIfNeeded(ref _brightMaskRT, colorDesc,
            FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_GodRayBrightMaskRT");
        RenderingUtils.ReAllocateIfNeeded(ref _blurredRT, colorDesc,
            FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_GodRayBlurredRT");
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (_material == null) return;

        // Volume-driven runtime parameters.
        var stack  = VolumeManager.instance.stack;
        var volume = stack.GetComponent<GodRayVolumeComponent>();
        if (volume == null || !volume.IsActive())
            return;

        var cameraData = renderingData.cameraData;

        UpdateMaterialParameters(renderingData, volume);

        var cmd    = CommandBufferPool.Get();
        var source = cameraData.renderer.cameraColorTargetHandle;

        using (new ProfilingScope(cmd, s_ProfilingSampler))
        {
            // -----------------------------------------------------------------
            // Step 1: Build exclusion mask
            // Clear to black (= nothing excluded), then optionally rasterize
            // excluded-layer geometry as solid white (Pass 3).
            // -----------------------------------------------------------------
            CoreUtils.SetRenderTarget(cmd, _exclusionMaskRT, ClearFlag.Color, Color.black);

            if (_settings.excludeLayers.value != 0)
            {
                // Flush so SetRenderTarget takes effect before DrawRenderers.
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var filterSettings = new FilteringSettings(RenderQueueRange.all, _settings.excludeLayers);
                var drawSettings   = CreateDrawingSettings(
                    s_ShaderTagIds, ref renderingData, SortingCriteria.CommonOpaque);
                drawSettings.overrideMaterial          = _material;
                drawSettings.overrideMaterialPassIndex = PASS_EXCLUSION_MASK;

                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings);
            }

            cmd.SetGlobalTexture(ID_ExclusionMaskRT, _exclusionMaskRT);

            // -----------------------------------------------------------------
            // Step 2/3: God ray generation. Both source modes share the same
            // pipeline (Pass 0 → Pass 1) — Pass 0's behavior is selected by
            // the _GODRAY_SOURCE_OCCLUSION shader keyword. This keeps Pass 1's
            // radial blur logic unchanged across modes.
            //
            //   LuminanceThreshold   : Pass 0 picks bright sources from camera color
            //   ScreenSpaceOcclusion : Pass 0 ray-marches depth toward the light
            // -----------------------------------------------------------------
            bool useOcclusion = _settings.sourceMode ==
                GodRayRenderFeature.SourceMode.ScreenSpaceOcclusion;
            CoreUtils.SetKeyword(_material, KW_OCCLUSION_MODE, useOcclusion);

            Blitter.BlitCameraTexture(cmd, source,        _brightMaskRT, _material, PASS_HIGHLIGHT);
            Blitter.BlitCameraTexture(cmd, _brightMaskRT, _blurredRT,    _material, PASS_RADIAL_BLUR);

            // -----------------------------------------------------------------
            // Step 4: Composite directly onto the camera color via hardware
            // additive blend (Pass 2 has Blend One One). The shader emits
            // godRay*tint, the GPU adds it onto whatever's already in the
            // destination — which IS the camera color target. No intermediate
            // RT, no copy-back blit.
            // -----------------------------------------------------------------
            Blitter.BlitCameraTexture(cmd, _blurredRT, source, _material, PASS_COMPOSITE);
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    // -------------------------------------------------------------------------
    // Per-frame parameter upload. Critical XR bit: light screen position is
    // computed PER EYE using the eye-specific view & projection matrices.
    // -------------------------------------------------------------------------
    private void UpdateMaterialParameters(RenderingData renderingData, GodRayVolumeComponent volume)
    {
        var lightingData = renderingData.lightData;
        var cameraData   = renderingData.cameraData;
        var cam          = cameraData.camera;

        // Resolve main directional light.
        Light mainLight = lightingData.mainLightIndex >= 0
            ? lightingData.visibleLights[lightingData.mainLightIndex].light
            : null;

        // In Unity, Light.transform.forward is the *photon propagation direction*.
        // The sun itself sits OPPOSITE that direction (where the photons come from).
        // Naming this `lightForward` (not `lightDir`) keeps the sign math unambiguous
        // — the original `lightDir = -forward` + `cam - lightDir` paired up to two
        // negations, putting the virtual sun on the wrong side of the camera.
        Vector3 lightForward = mainLight != null ? mainLight.transform.forward : Vector3.down;
        Color   lightColor   = mainLight != null ? mainLight.color * mainLight.intensity : Color.white;

        // Place a virtual "sun" 10,000 m away in the direction OPPOSITE to photon
        // travel — that's where light actually comes from. 10 km is plenty given
        // typical LBE room scales.
        Vector3 lightPosWS = cam.transform.position - lightForward * 10000f;
        Vector4 lightPosWS4 = new Vector4(lightPosWS.x, lightPosWS.y, lightPosWS.z, 1f);

        // View count: 2 in XR Single Pass Instanced, 1 in mono.
        int viewCount = 1;
#if ENABLE_VR && ENABLE_XR_MODULE
        if (cameraData.xr.enabled)
            viewCount = 2;
#endif

        for (int viewIndex = 0; viewIndex < viewCount; viewIndex++)
        {
            Matrix4x4 view = cameraData.GetViewMatrix(viewIndex);
            Matrix4x4 proj = cameraData.GetProjectionMatrix(viewIndex);
            Matrix4x4 vp   = proj * view;

            Vector4 clip = vp * lightPosWS4;

            // w <= 0 means the point is behind the eye. Mark and zero-blank.
            float behindCamera = clip.w <= 0f ? 1f : 0f;

            Vector2 ndc = behindCamera > 0.5f
                ? Vector2.zero
                : new Vector2(clip.x / clip.w, clip.y / clip.w);

            Vector2 screenUV = ndc * 0.5f + new Vector2(0.5f, 0.5f);
            _lightScreenPositions[viewIndex] = new Vector4(screenUV.x, screenUV.y, behindCamera, 0f);
        }

        // Mono path: replicate so the shader's [unity_StereoEyeIndex] works regardless.
        if (viewCount == 1)
            _lightScreenPositions[1] = _lightScreenPositions[0];

        _material.SetVectorArray(ID_LightScreenPos, _lightScreenPositions);
        _material.SetColor(ID_LightColor, lightColor);
        _material.SetColor(ID_TintColor, volume.tintColor.value);
        _material.SetVector(ID_GodRayParams, new Vector4(
            volume.threshold.value,
            volume.blurStrength.value,
            volume.blurFalloff.value,
            volume.intensity.value));
        _material.SetVector(ID_GodRayParams2, new Vector4(
            volume.sunDiscIntensity.value,
            volume.sunDiscSize.value,
            0f, 0f));
        _material.SetInt(ID_SampleCount, volume.sampleCount.value);
        _material.SetFloat(ID_UseSkyOnly, volume.skyOnly.value ? 1f : 0f);

        // Occlusion-mode parameters. Always uploaded — they're cheap to set and
        // become live the instant the user flips the SourceMode dropdown.
        _material.SetInt(ID_OcclusionSteps, Mathf.Max(1, _settings.occlusionSteps));
        _material.SetFloat(ID_OcclusionMaxRayLength, _settings.occlusionMaxRayLength);

        // Angle attenuation: god rays are strongest when the camera looks TOWARD
        // the sun. dot(camForward, -lightForward) = dot(camForward, toLight) which
        // is +1 looking straight at the sun, 0 at 90°, -1 looking directly away.
        // SmoothStep softly fades anything outside the forward hemisphere so light
        // crossing the view edge doesn't pop on/off.
        Vector3 camForward       = cam.transform.forward;
        float   angleDot         = Vector3.Dot(camForward, -lightForward);
        float   angleAttenuation = Mathf.SmoothStep(-0.1f, 0.4f, angleDot);
        _material.SetFloat(ID_AngleAttenuation, angleAttenuation);
    }

    // -------------------------------------------------------------------------
    // RTHandle release. Called from the RenderFeature's Dispose.
    // -------------------------------------------------------------------------
    public void Cleanup()
    {
        _exclusionMaskRT?.Release();
        _brightMaskRT?.Release();
        _blurredRT?.Release();
        _exclusionMaskRT = null;
        _brightMaskRT    = null;
        _blurredRT       = null;
    }
}
