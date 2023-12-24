using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BlitMaterialFeature : ScriptableRendererFeature
{

    [System.Serializable]
    public class Setting
    {
        [Header("Draw Renderers Settings")]
        public Material overrideMaterial;
        public int overrideMaterialPass;
        public string colorTargetDestinationID = "";

        [Header("Blit Material")]
        public Material blitMaterial;

    }

    public Setting setting;


    class BlitMaterialPass : ScriptableRenderPass
    {

        private Setting setting;

        //point to a texture
        private RenderTargetIdentifier source;
        //point to a texture variable 
        private RenderTargetHandle tempTexture;

        public void SetSource(RenderTargetIdentifier source)
        { 
            this.source = source;
        }

        public BlitMaterialPass(Setting setting)
        {
            this.setting = setting;
 
            

        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("BlitMaterialCommandBuffer");

            RenderTextureDescriptor textureDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            textureDescriptor.depthBufferBits = 0;

            cmd.GetTemporaryRT(tempTexture.id, textureDescriptor, FilterMode.Bilinear);

            Blit(cmd, source, tempTexture.Identifier(), setting.blitMaterial, -1);
            Blit(cmd, tempTexture.Identifier(), source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    BlitMaterialPass blitMaterialPass;
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        //if (renderingData.cameraData.camera != Camera.main) return;

        if (setting.blitMaterial != null)
        {
            blitMaterialPass.SetSource(renderer.cameraColorTarget);
            renderer.EnqueuePass(blitMaterialPass);
        }
    }

    public override void Create()
    {
        blitMaterialPass = new BlitMaterialPass(setting);
        blitMaterialPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }
}
