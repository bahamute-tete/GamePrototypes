using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;



class ScreenRayMarchingRenderPass : ScriptableRenderPass
{
    private Material material;
    private Shader _shader;
    private RenderTargetIdentifier source;

    private RayMarchingRenderSettings _renderSettings;
    private FogSettings _fogSettings;

    public ScreenRayMarchingRenderPass(Shader shader, RayMarchingRenderSettings renderSettings, FogSettings fogSettings)
    {
        _shader = shader;
        _renderSettings = renderSettings;
        _fogSettings = fogSettings;

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
                Debug.LogWarning("RayMarching shader is not ready, skipping frame");
                return;
            }
        }

        CommandBuffer cmd = CommandBufferPool.Get("ScreenRayMarching");
        Camera cam = renderingData.cameraData.camera;

        // 设置光照信息
        SetupLighting(renderingData, material);
        
        // 设置相机信息
        SetupCamera(cam, material);
        
        // 设置渲染参数
        SetupRenderSettings(material);
        
        // 设置雾效参数
        SetupFogSettings(material);

        // 执行渲染
        RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
        desc.depthBufferBits = 0;

        int tempRT1 = Shader.PropertyToID("_TempRayMarchingSurface");
        cmd.GetTemporaryRT(tempRT1, desc);
        cmd.Blit(source, tempRT1, material, 0);
        cmd.Blit(tempRT1, source);
        cmd.ReleaseTemporaryRT(tempRT1);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    private void SetupLighting(RenderingData renderingData, Material mat)
    {
        var visibleLights = renderingData.lightData.visibleLights;
        
        Vector3 mainLightDir = new Vector3(0.5f, 1, 0).normalized;
        Color mainLightColor = Color.black;

        Vector4[] pointLightPosRanges = new Vector4[4];
        Vector4[] pointLightColors = new Vector4[4];
        int pointLightCount = 0;

        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight vl = visibleLights[i];
            if (vl.lightType == LightType.Directional)
            {
                mainLightDir = -vl.localToWorldMatrix.GetColumn(2);
                mainLightColor = vl.finalColor;
            }
            else if (vl.lightType == LightType.Point && pointLightCount < 4)
            {
                Vector4 pos = vl.localToWorldMatrix.GetColumn(3);
                pos.w = vl.range;
                pointLightPosRanges[pointLightCount] = pos;
                pointLightColors[pointLightCount] = vl.finalColor;
                pointLightCount++;
            }
        }

        mat.SetVector("_LightDirection", mainLightDir);
        mat.SetVector("_LightColor", mainLightColor);
        mat.SetInt("_PointLightCount", pointLightCount);
        mat.SetVectorArray("_PointLightPosRanges", pointLightPosRanges);
        mat.SetVectorArray("_PointLightColors", pointLightColors);

        // 设置球谐光照
        SetupSphericalHarmonics(mat);
    }

    private void SetupSphericalHarmonics(Material mat)
    {
        SphericalHarmonicsL2 sh = RenderSettings.ambientProbe;
        
        Vector4[] shAr_g_b = new Vector4[3];
        Vector4[] shBr_g_b = new Vector4[3];
        Vector4 shC = Vector4.zero;

        for (int k = 0; k < 3; k++)
        {
            shAr_g_b[k] = new Vector4(sh[k, 3], sh[k, 1], sh[k, 2], sh[k, 0] - sh[k, 6]);
            shBr_g_b[k] = new Vector4(sh[k, 4], sh[k, 5], sh[k, 6] * 3.0f, sh[k, 7]);
        }
        shC = new Vector4(sh[0, 8], sh[1, 8], sh[2, 8], 1.0f);

        mat.SetVector("_SHAr", shAr_g_b[0]);
        mat.SetVector("_SHAg", shAr_g_b[1]);
        mat.SetVector("_SHAb", shAr_g_b[2]);
        mat.SetVector("_SHBr", shBr_g_b[0]);
        mat.SetVector("_SHBg", shBr_g_b[1]);
        mat.SetVector("_SHBb", shBr_g_b[2]);
        mat.SetVector("_SHC", shC);
    }

    private void SetupCamera(Camera cam, Material mat)
    {
        Matrix4x4 clipToWorld = cam.cameraToWorldMatrix * cam.projectionMatrix.inverse;
        mat.SetMatrix("_ClipToWorld", clipToWorld);
        mat.SetVector("_CameraPos", cam.transform.position);

        float fov = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        mat.SetVector("_CamParams", new Vector4(fov, cam.aspect, cam.nearClipPlane, cam.farClipPlane));
    }

    private void SetupRenderSettings(Material mat)
    {
        mat.SetFloat("_AOIntensity", _renderSettings.aoIntensity);
        mat.SetTexture("_SDFTexture", _renderSettings.sdfTexture);
        mat.SetTexture("_EnvironmentCubemap", _renderSettings.environment);
    }

    private void SetupFogSettings(Material mat)
    {
        mat.SetColor("_FogBaseColor", _fogSettings.baseColor);
        mat.SetColor("_FogTargetColor", _fogSettings.targetColor);
        mat.SetFloat("_HenyeyGreenstein_G", _fogSettings.henyeyGreenstein_G);
        mat.SetFloat("_Absorption", _fogSettings.absorption);
        mat.SetFloat("_ScatteringCoeff", _fogSettings.scatteringCoeff);
        mat.SetFloat("_AmbientLightIntensity", _fogSettings.ambientLightIntensity);
        mat.SetFloat("_DirectLightIntensity", _fogSettings.directLightIntensity);
        mat.SetFloat("_FogDensity", _fogSettings.density);
        mat.SetFloat("_StepSize", _fogSettings.stepSize);
        mat.SetVector("_FogBoxCenter", _fogSettings.boxCenter);
        mat.SetVector("_FogBoxSize", _fogSettings.boxSize);
    }

    public void UpdateSettings(RayMarchingRenderSettings renderSettings, FogSettings fogSettings)
    {
        _renderSettings = renderSettings;
        _fogSettings = fogSettings;
    }
}