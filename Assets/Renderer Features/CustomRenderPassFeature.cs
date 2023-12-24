using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static CustomRenderPassFeature;

public class CustomRenderPassFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Header("Draw Renderers Settings")]
        public LayerMask layerMask = 1;
        public Material overrideMaterial;
        public int overrideMaterialPass;
        public string colorTargetDestinationID = "";

        [Header("Blit Settings")]
        public Material blitMaterial;
    }

    public Settings settings;

    public Shader shader;
    private Material material;

    public RenderPassEvent _Event = RenderPassEvent.AfterRenderingOpaques;

    CustomRenderPass _CustomRenderPass;


    public class CustomRenderPass : ScriptableRenderPass
    {
        private Settings settings;
        private ProfilingSampler _ProfilingSampler;

        // (constructor, method name should match class name)
        public CustomRenderPass(Settings settings, string name)
        {
            // pass our settings class to the pass, so we can access them inside OnCameraSetup/Execute/etc
            this.settings = settings;

            // set up ProfilingSampler used in Execute method
            _ProfilingSampler = new ProfilingSampler(name);

        }
        // Called before executing the render pass.
        // Used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // To create a Color Target :
            var colorDesc = renderingData.cameraData.cameraTargetDescriptor;
            // must set to 0 to specify a colour target
            colorDesc.depthBufferBits = 0;


            // To create a Depth Target :
            var depthDesc = renderingData.cameraData.cameraTargetDescriptor;
            // should be default anyway
            depthDesc.depthBufferBits = 32; 

            
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            throw new System.NotImplementedException();
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            base.OnCameraCleanup(cmd);
        }

        private RenderTargetIdentifier rtDestinationColor;
        private RenderTargetIdentifier rtDestinationDepth;

        public void Setup(RenderTargetIdentifier destColor, RenderTargetIdentifier destDepth)
        { 
            this.rtDestinationColor = destColor;
            this.rtDestinationDepth = destColor;
        }

        //public void ReleaseTargets()
        //{
        //    colorTarget?.Release();
        //    depthTarget?.Release();
        //}
    }

    public override void Create()
    {
        // Create may be called multiple times... so :
        if (material == null || material.shader != shader)
        {
            // only create material if null or different shader has been assigned

            if (material!=null) CoreUtils.Destroy(material);

            // destroy material using previous shader

            material = CoreUtils.CreateEngineMaterial(shader);
            // or alternative method that uses the shader name (string):
            //material = CoreUtils.CreateEngineMaterial("Hidden/Internal-DepthNormalsTexture");
            // assumes the required shader is in the build (and variant, if keywords are set)
            // e.g. could add the shader to the "Always Included Shaders" in Project Settings -> Graphics
        }
        _CustomRenderPass = new CustomRenderPass(settings, name);
        _CustomRenderPass.renderPassEvent = _Event;
    }

    public bool showInSceneView;
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera (every frame!)

        if (renderingData.cameraData.isPreviewCamera) return;
        // Ignore feature for editor/inspector previews & asset thumbnails
        if (renderingData.cameraData.isSceneViewCamera) return;
        // Ignore feature for scene view
        // If the feature uses camera targets, you may want to expose a bool/tickbox instead, e.g.
        if (!showInSceneView && renderingData.cameraData.isSceneViewCamera) return;

        // (could alternatively use "cameraData.cameraType == CameraType enum" for these)
        if (renderingData.cameraData.camera != Camera.main) return;
        // Ignore all cameras except the camera tagged as MainCamera
        // Though may be better to use Multiple Renderer Assets (see below)

        // Tell URP to generate the Camera Depth Texture
        _CustomRenderPass.ConfigureInput(ScriptableRenderPassInput.Depth);

        // Tell URP to generate the Camera Normals and Depth Textures
        // m_ScriptablePass.ConfigureInput(ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Depth);

        renderer.EnqueuePass(_CustomRenderPass);
    }


    //public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    //{
    //    RenderTargetIdentifier color = renderer.cameraColorTarget;
    //    RenderTargetIdentifier depth = renderer.cameraDepthTarget;

    //    //for 2022 version
    //    //RTHandle color = renderer.cameraColorTargetHandle;
    //    //RTHandle depth = renderer.cameraDepthTargetHandle;
    //    //_CustomRenderPass.Setup(color, depth);
    //}





}
