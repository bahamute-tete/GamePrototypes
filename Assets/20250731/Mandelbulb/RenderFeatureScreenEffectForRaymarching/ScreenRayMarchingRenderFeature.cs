using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal; 
using UnityEngine.Rendering;

public class ScreenRayMarchingRenderFeature : ScriptableRendererFeature 
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public Shader shader;
        
        [Header("RayMarching Settings")]
        public RayMarchingRenderSettings marchingSettings = new RayMarchingRenderSettings
        {
            aoIntensity = 0.3f
        };

        [Header("Cloud Settings")]
        public FogSettings fogSettings = new FogSettings
        {
            boxCenter = new Vector3(0, 2, 0),
            boxSize = new Vector3(3, 2, 3),
            baseColor = new Color(1.0f, 0.9f, 0.8f, 1.0f),
            targetColor = new Color(1.0f, 0.5f, 0.3f, 1.0f),
            henyeyGreenstein_G = 0.7f,
            absorption = 1.0f,
            scatteringCoeff = 1.0f,
            ambientLightIntensity = 0.5f,
            directLightIntensity = 1.0f,
            density = 2.0f,
            stepSize = 0.1f
        };
    }

    public Settings settings = new Settings();
    ScreenRayMarchingRenderPass screenRayMarchingRenderPass;

    public override void Create()
    {
        screenRayMarchingRenderPass = new ScreenRayMarchingRenderPass(
            settings.shader,
            settings.marchingSettings,
            settings.fogSettings
        );

        screenRayMarchingRenderPass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.shader == null) return;

        screenRayMarchingRenderPass.UpdateSettings(settings.marchingSettings, settings.fogSettings);
        renderer.EnqueuePass(screenRayMarchingRenderPass);
    }

   
}
