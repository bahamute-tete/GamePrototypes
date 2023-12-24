using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

public class TestRenderFeature : ScriptableRendererFeature
{

    private TestRenderPass _TestRenderPass;

    public Material material;
    public Mesh mesh;

   
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material != null && mesh != null)
        { 
            renderer.EnqueuePass(_TestRenderPass);

        
        }
    }

    public override void Create()
    {
        _TestRenderPass =new TestRenderPass(material, mesh);
        _TestRenderPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
    }


    class TestRenderPass : ScriptableRenderPass
    {

        private Material _material;
        private Mesh _mesh;
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("TestRenderPass");


            //cmd.DrawMesh(_mesh, Matrix4x4.identity, _material);

            Camera camera = renderingData.cameraData.camera;

            cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);

            Vector3 scale = new Vector3(1, camera.aspect, 1);

            foreach (VisibleLight visibleLight in renderingData.lightData.visibleLights)
            { 
                Light light =visibleLight.light;

                Vector3 position = camera.WorldToViewportPoint(light.transform.position) * 2 - Vector3.one;

                position.z = 0;

                cmd.DrawMesh(_mesh, Matrix4x4.TRS(position, Quaternion.identity, scale),_material,0);
            }


            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public TestRenderPass(Material material, Mesh mesh)
        {
            _material = material;
            _mesh = mesh;
        }
    }


}
