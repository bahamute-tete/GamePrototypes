using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal; 
using UnityEngine.Rendering;

public class Screen2DSDFPatternRenderFeature : ScriptableRendererFeature 
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public Shader shader;

        public Color outerColor=Color.white;
        public Color innerColor=Color.white;

        public float[] audioBands = new float[8];
    }

    public Settings settings = new Settings();
    Screen2DSDFPatternRenderPass screenRayMarchingRenderPass;

    public override void Create()
    {
        screenRayMarchingRenderPass = new Screen2DSDFPatternRenderPass(
            settings.shader,
            settings.outerColor,
            settings.innerColor,

            settings.audioBands

        );

        screenRayMarchingRenderPass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.shader == null) return;


        screenRayMarchingRenderPass.UpdateAudioBands(settings.audioBands);

        renderer.EnqueuePass(screenRayMarchingRenderPass);
    }

   
}
