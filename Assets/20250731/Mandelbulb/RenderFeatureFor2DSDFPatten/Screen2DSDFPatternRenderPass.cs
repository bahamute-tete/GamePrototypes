using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;



class Screen2DSDFPatternRenderPass : ScriptableRenderPass
{
    private Material material;
    private Shader _shader;
    private RenderTargetIdentifier source;

    private Color _outterColor;
    private Color _innerColor;

    private float[] _Bands = new float[8];


    public Screen2DSDFPatternRenderPass(Shader shader,Color outerColor,Color innerColor, float[] bands)
    {
        _shader = shader;
        
        _outterColor = outerColor;
        _innerColor = innerColor;
        _Bands = bands;

        if (shader != null)
            this.material = new Material(shader);
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        source = renderingData.cameraData.renderer.cameraColorTargetHandle;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (material == null || 
            material.shader == null || 
            material.shader != _shader || 
            !material.shader.isSupported)
        {
            if (_shader != null && _shader.isSupported)
            {
                material = new Material(_shader);
            }
            
            if (material == null || material.shader == null || !material.shader.isSupported)
            {
                Debug.LogWarning("shader is not ready, skipping frame");
                return;
            }
        }

        CommandBuffer cmd = CommandBufferPool.Get("2DSDF");
        Camera cam = renderingData.cameraData.camera;

        SetupColor(material, _outterColor, _innerColor);
        SetupAudioData(material, _Bands);

        RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
        desc.depthBufferBits = 0;

        int tempRT1 = Shader.PropertyToID("_Temp2DSDF");
        cmd.GetTemporaryRT(tempRT1, desc);
        cmd.Blit(source, tempRT1, material, 0);
        cmd.Blit(tempRT1, source);
        cmd.ReleaseTemporaryRT(tempRT1);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

   

    
    private void SetupCamera(Camera cam, Material mat)
    {
        Matrix4x4 clipToWorld = cam.cameraToWorldMatrix * cam.projectionMatrix.inverse;
        mat.SetMatrix("_ClipToWorld", clipToWorld);
        mat.SetVector("_CameraPos", cam.transform.position);

        float fov = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        mat.SetVector("_CamParams", new Vector4(fov, cam.aspect, cam.nearClipPlane, cam.farClipPlane));
    }


    private void SetupColor(Material mat, Color outterColor, Color innerColor)
    {
        mat.SetColor("_OutterColor", outterColor);
        mat.SetColor("_InnerColor", innerColor);
    }

    private void SetupAudioData(Material mat, float[] datas)
    {
        mat.SetFloatArray("_MusicFrequencies", _Bands);
       
    }


    public void UpdateAudioBands(float[] bands)
    {
        if (bands != null && bands.Length == 8)
        {
            System.Array.Copy(bands, _Bands, 8);
        }
    }






}