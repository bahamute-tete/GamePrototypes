using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

public class GodRayRenderFeatureRaidalBlur : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;
        public Shader shader;

        [Header("GodRaySetting")]
        [Range(0, 2)]  public float blurStrength = 1.0f;
        [Range(0, 2)]  public float blurFalloff  = 0.5f;
        [Range(2, 64)] public int   sampleCount  = 16;
        [Range(0, 10)] public float intensity    = 1.0f;

        [Header("Brightness Threshold")]
        [Range(0, 1)] public float threshold = 0.5f;

        [ColorUsage(true, true)]
        public Color tintColor = Color.white;

        [Header("Exclusion")]
        [Tooltip("Objects on these layers will NOT produce god-ray light streaks.")]
        public LayerMask excludeLayers = 0;
    }

    public Settings settings = new Settings();
    GodRayRenderPassRaidalBlur _godRayRenderPass;

    public override void Create()
    {
        // Properly clean up before recreating (avoids material leaks on Inspector edits)
        if (_godRayRenderPass != null)
        {
            _godRayRenderPass.Cleanup();
            _godRayRenderPass = null;
        }

        _godRayRenderPass = new GodRayRenderPassRaidalBlur(
            settings.shader,
            settings.threshold,
            settings.blurStrength,
            settings.blurFalloff,
            settings.sampleCount,
            settings.intensity,
            settings.tintColor,
            settings.excludeLayers
        );

        _godRayRenderPass.renderPassEvent = settings.passEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.shader == null || _godRayRenderPass == null)
            return;

        renderer.EnqueuePass(_godRayRenderPass);
    }

    protected override void Dispose(bool disposing)
    {
        if (_godRayRenderPass != null)
        {
            _godRayRenderPass.Cleanup();
            _godRayRenderPass = null;
        }
    }
}
