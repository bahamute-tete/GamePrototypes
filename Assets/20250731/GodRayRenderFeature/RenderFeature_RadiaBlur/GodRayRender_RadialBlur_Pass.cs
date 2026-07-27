using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

public class GodRayRenderPassRaidalBlur : ScriptableRenderPass
{
    private Material _Material;
    private Material _ExclusionMaterial;
    private Shader   _Shader;
    private RenderTargetIdentifier source;

    private float     _Threshold;
    private Color     _TintColor;
    private float     _BlurStrength;
    private float     _BlurFalloff;
    private int       _SampleCount;
    private float     _RayIntensity;
    private LayerMask _ExcludeLayers;

    private static readonly int brightMaskRT    = Shader.PropertyToID("_BrightMaskRT");
    private static readonly int blurredRT       = Shader.PropertyToID("_BlurredRT");
    private static readonly int tempColorRT     = Shader.PropertyToID("_TempColorRT");
    private static readonly int godRayTex       = Shader.PropertyToID("_GodRayTex");
    private static readonly int exclusionMaskRT = Shader.PropertyToID("_ExclusionMaskRT");

    private static readonly List<ShaderTagId> _ShaderTagIds = new List<ShaderTagId>
    {
        new ShaderTagId("UniversalForward"),
        new ShaderTagId("UniversalForwardOnly"),
        new ShaderTagId("SRPDefaultUnlit"),
        new ShaderTagId("LightweightForward"),
    };

    // Pass 3 inside GodRayShader_RadialBlur.shader outputs solid white
    private const int ExclusionMaskPassIndex = 3;


    public GodRayRenderPassRaidalBlur(
        Shader    shader,
        float     threshold,
        float     blurStrength,
        float     falloff,
        int       sampleCount,
        float     rayIntensity,
        Color     tintColor,
        LayerMask excludeLayers)
    {
        _Shader = shader;

        if (shader != null)
        {
            _Material          = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _ExclusionMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        _Threshold     = threshold;
        _TintColor     = tintColor;
        _BlurStrength  = blurStrength;
        _BlurFalloff   = falloff;
        _SampleCount   = sampleCount;
        _RayIntensity  = rayIntensity;
        _ExcludeLayers = excludeLayers;
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        source = renderingData.cameraData.renderer.cameraColorTargetHandle;
        ConfigureInput(ScriptableRenderPassInput.Depth);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (_Material == null || !_Material.shader.isSupported)
        {
            _Material = new Material(_Shader);
            if (_Material == null || _Material.shader == null || !_Material.shader.isSupported)
            {
                Debug.LogWarning("GodRay: shader not ready, skipping frame");
                return;
            }
        }

        CommandBuffer cmd = CommandBufferPool.Get("GodRayRaidalBlur");
        SetupBlurSettings(_Material);
        SetupLighting(renderingData, _Material);

        // ── Descriptors ───────────────────────────────────────────────────────
        RenderTextureDescriptor fullDesc = renderingData.cameraData.cameraTargetDescriptor;
        fullDesc.depthBufferBits = 0;

        RenderTextureDescriptor halfDesc = fullDesc;
        halfDesc.width  /= 2;
        halfDesc.height /= 2;

        // ── Allocate RTs ──────────────────────────────────────────────────────
        cmd.GetTemporaryRT(exclusionMaskRT, fullDesc); // full-res for accurate silhouette
        cmd.GetTemporaryRT(brightMaskRT,    halfDesc);
        cmd.GetTemporaryRT(blurredRT,       halfDesc);
        cmd.GetTemporaryRT(tempColorRT,     halfDesc);

        // ── Build exclusion mask ──────────────────────────────────────────────
        // Clear to black first (= no object is excluded by default)
        cmd.SetRenderTarget(exclusionMaskRT);
        cmd.ClearRenderTarget(false, true, Color.black);

        // Must flush before DrawRenderers so the SetRenderTarget is in effect
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        if (_ExcludeLayers.value != 0 && _ExclusionMaterial != null)
        {
            // Render excluded objects as solid white silhouettes into the mask RT.
            // ZTest LEqual (in the shader pass) ensures only visible surfaces are marked.
            var filteringSettings = new FilteringSettings(RenderQueueRange.all, _ExcludeLayers);
            var drawSettings      = CreateDrawingSettings(
                _ShaderTagIds, ref renderingData, SortingCriteria.CommonOpaque);
            drawSettings.overrideMaterial          = _ExclusionMaterial;
            drawSettings.overrideMaterialPassIndex = ExclusionMaskPassIndex;

            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);
        }

        // ── God-ray pipeline ──────────────────────────────────────────────────
        // Expose the exclusion mask so Pass 0 (Highlight) can zero out
        // excluded-layer pixels before they become a bright source.
        cmd.SetGlobalTexture("_ExclusionMaskRT", exclusionMaskRT);

        // Pass 0: Bright mask  (excluded pixels → zeroed out inside the shader)
        cmd.Blit(source, brightMaskRT, _Material, 0);

        // Pass 1: Radial blur
        cmd.Blit(brightMaskRT, blurredRT, _Material, 1);

        // Keep original color for compositing
        cmd.Blit(source, tempColorRT);

        // Pass 2: Composite (no exclusion logic, just additive blend)
        cmd.SetGlobalTexture("_GodRayTex", blurredRT);
        cmd.Blit(tempColorRT, source, _Material, 2);

        // ── Cleanup ───────────────────────────────────────────────────────────
        cmd.ReleaseTemporaryRT(exclusionMaskRT);
        cmd.ReleaseTemporaryRT(brightMaskRT);
        cmd.ReleaseTemporaryRT(blurredRT);
        cmd.ReleaseTemporaryRT(tempColorRT);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetupLighting(RenderingData renderingData, Material mat)
    {
        var    lightingData = renderingData.lightData;
        Camera cam          = renderingData.cameraData.camera;

        Light mainLight = lightingData.mainLightIndex >= 0
            ? lightingData.visibleLights[lightingData.mainLightIndex].light
            : null;

        if (mainLight != null)
        {
            Vector3 lightDir = -mainLight.transform.forward;
            mat.SetVector("_LightDir",   new Vector4(lightDir.x, lightDir.y, lightDir.z, 0));
            mat.SetColor ("_LightColor", mainLight.color * mainLight.intensity);

            Vector3 lightPosWS = cam.transform.position - lightDir * 10000f;
            Vector3 lightPosVS = cam.worldToCameraMatrix.MultiplyPoint(lightPosWS);
            float   behindCam  = lightPosVS.z > 0 ? 0f : 1f;

            Vector3 lightPosCS     = cam.projectionMatrix.MultiplyPoint(lightPosVS);
            Vector2 lightScreenPos = new Vector2(
                lightPosCS.x * 0.5f + 0.5f,
                lightPosCS.y * 0.5f + 0.5f);

            mat.SetVector("_LightScreenPos",
                new Vector4(lightScreenPos.x, lightScreenPos.y, behindCam, 0));
        }
        else
        {
            mat.SetVector("_LightDir",       new Vector4(0, -1, 0, 0));
            mat.SetColor ("_LightColor",     Color.white);
            mat.SetVector("_LightScreenPos", new Vector4(0.5f, 0.5f, 0, 0));
        }
    }

    private void SetupBlurSettings(Material mat)
    {
        mat.SetFloat("_Threshold",    _Threshold);
        mat.SetColor("_TintColor",    _TintColor);
        mat.SetFloat("_BlurStrength", _BlurStrength);
        mat.SetFloat("_BlurFalloff",  _BlurFalloff);
        mat.SetInt  ("_SampleCount",  _SampleCount);
        mat.SetFloat("_Intensity",    _RayIntensity);
    }

    public void Cleanup()
    {
        DestroyMaterial(ref _Material);
        DestroyMaterial(ref _ExclusionMaterial);
    }

    private static void DestroyMaterial(ref Material mat)
    {
        if (mat == null) return;
#if UNITY_EDITOR
        if (Application.isPlaying) Object.Destroy(mat);
        else                       Object.DestroyImmediate(mat);
#else
        Object.Destroy(mat);
#endif
        mat = null;
    }
}
