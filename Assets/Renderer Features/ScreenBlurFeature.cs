using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ScreenBlurFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public enum FilterType {Box=0,Gaussian };

        [Range(1,4),Min(1)]
        public int downSampe = 1;
        [Range(1,20), Min(1)]
        public float spread = 1f;
        [Range(3, 121), Min(3)]
        public int gridSize = 3;
        public FilterType filterType = FilterType.Box;
        public RenderPassEvent renderPass = RenderPassEvent.AfterRenderingOpaques;
        public Shader shader;
         
    }

    public Settings settings = new Settings();

    private Material material;
    private ScreenBlurPass screenBlurPass;

    private class ScreenBlurPass : ScriptableRenderPass
    {


        private Settings settings;
        private RenderPassEvent renderPass;



        string profilerTag = "ScreenBluerPass";

        private Shader shader;

        RenderTargetIdentifier colorBufer, tempColorBuffer;

        int tempColorBufferID = Shader.PropertyToID("_tempColorBuffer");
        int gridSizeID = Shader.PropertyToID("_GridSize");
        int SpreadID = Shader.PropertyToID("_Spread");

        Material material;
        public ScreenBlurPass(Settings settings)
        { 
            this.settings= settings;
            renderPass = settings.renderPass;

            if (material == null) material = CoreUtils.CreateEngineMaterial(settings.shader);

            material.SetInt(gridSizeID, settings.gridSize);
            material.SetFloat(SpreadID, settings.spread);
            if (settings.filterType == Settings.FilterType.Gaussian)
                 material.EnableKeyword("_GAUSSIAN_FILTER");
            else
                material.DisableKeyword("_GAUSSIAN_FILTER");



        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor textureDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            textureDescriptor.width /= settings.downSampe;
            textureDescriptor.height/= settings.downSampe;

            textureDescriptor.depthBufferBits = 0;

            colorBufer = renderingData.cameraData.renderer.cameraColorTarget;

            cmd.GetTemporaryRT(tempColorBufferID, textureDescriptor, FilterMode.Bilinear);

            tempColorBuffer = new RenderTargetIdentifier(tempColorBufferID);


        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, new ProfilingSampler(profilerTag)))
            {
                Blit(cmd, colorBufer, tempColorBuffer, material, 0);
                Blit(cmd, tempColorBuffer, colorBufer, material, 1);

            }
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null) throw new ArgumentNullException("cmd");

            cmd.ReleaseTemporaryRT(tempColorBufferID);
        }
    }


    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(screenBlurPass);
    }

    public override void Create()
    {
        screenBlurPass = new ScreenBlurPass(settings);
    }
}
