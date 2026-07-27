using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static GodRayRenderRayMarchingFeature;

public class GodRayRenderRayMarchingPass : ScriptableRenderPass
{
    private Material _material;
    private readonly Shader _shader;
    private RaySettings _raySettings;
    private RenderTargetIdentifier _source;
    private ScriptableRenderer _renderer;

    private int _frameIndex = 0;
    private RenderTexture _historyRT; // 历史帧缓存
    private bool _isFirstFrame = true;
    private bool _hasPrevCameraState = false;
    private Vector3 _prevCamPosition;
    private Quaternion _prevCamRotation;

    private static readonly int TempColorRT = Shader.PropertyToID("_TempColorRT");
    private static readonly int GodRayRT = Shader.PropertyToID("_GodRayRT");
    private static readonly int TaaRT = Shader.PropertyToID("_TAART");
    private static readonly int PrevFrameRT = Shader.PropertyToID("_PrevFrame");

    private static void SafeDestroy(Object obj)
    {
        if (obj == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (Application.isPlaying)
            Object.Destroy(obj);
        else
            Object.DestroyImmediate(obj);
#else
        Object.Destroy(obj);
#endif
    }

    public GodRayRenderRayMarchingPass(Shader shader, RaySettings raySettings)
    {
        _shader = shader;
        _raySettings = raySettings;

      
        if (shader != null && shader.isSupported)
        {
            _material = new Material(shader);
            _material.hideFlags = HideFlags.DontSave; // 避免被自动销毁
        }
    }

    public void UpdateSettings(RaySettings raySettings)
    {
        _raySettings = raySettings;
    }

    //关联渲染器
    public void SetupRenderer(ScriptableRenderer renderer)
    {
        _renderer = renderer;
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
      
        if (_renderer != null && renderingData.cameraData.renderer != null)
        {
            _source = renderingData.cameraData.renderer.cameraColorTargetHandle;
        }

        ConfigureInput(ScriptableRenderPassInput.Depth);
        ConfigureClear(ClearFlag.None, Color.black);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        if (_material == null || !_material.shader.isSupported || camera == null)
        {
            return;
        }

        if (renderingData.cameraData.isPreviewCamera)
        {
            return;
        }

        SetupCamera(renderingData, _material);
        SetupLighting(renderingData, _material);
        bool cameraMoved = HasCameraMoved(camera);
        if (_raySettings.resetHistoryOnCameraMotion && cameraMoved)
        {
            _isFirstFrame = true;
        }

        float currentTaaBlend = _raySettings.taaBlend;
        if (_raySettings.resetHistoryOnCameraMotion && cameraMoved)
        {
            currentTaaBlend = Mathf.Min(currentTaaBlend, _raySettings.motionTaaBlend);
        }

        _material.SetFloat("_TAA_Blend", currentTaaBlend);
        SetupRaySettings(_material);

        // 设置帧索引（用于时间抖动）
        _material.SetFloat("_FrameIndex", _frameIndex);

        CommandBuffer cmd = CommandBufferPool.Get("GodRayRayMarching");

        RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
        desc.depthBufferBits = 0;
        desc.msaaSamples = 1;

        RenderTextureDescriptor lowDesc = desc;
        int downsample = Mathf.Max(1, _raySettings.downsample);
        lowDesc.width = Mathf.Max(1, desc.width / downsample);
        lowDesc.height = Mathf.Max(1, desc.height / downsample);

        if (_raySettings.enableTemporalAccumulation)
        {
            // 创建或重用历史帧 RT
            if (_historyRT == null || _historyRT.width != lowDesc.width || _historyRT.height != lowDesc.height)
            {
                if (_historyRT != null)
                {
                    _historyRT.Release();
                    SafeDestroy(_historyRT);
                }

                _historyRT = new RenderTexture(lowDesc)
                {
                    name = "GodRayHistoryRT",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                _historyRT.Create();
                _isFirstFrame = true;
            }
        }
        else
        {
            if (_historyRT != null)
            {
                _historyRT.Release();
                SafeDestroy(_historyRT);
                _historyRT = null;
            }
            _isFirstFrame = true;
        }

        cmd.GetTemporaryRT(TempColorRT, desc, FilterMode.Bilinear);
        cmd.GetTemporaryRT(GodRayRT, lowDesc, FilterMode.Bilinear);
        cmd.Blit(_source, TempColorRT);

        // Pass 0: Ray Marching 生成体积光
        cmd.Blit(_source, GodRayRT, _material, 0);

        int outputGodRayRT = GodRayRT;

        // Pass 1: 时间累积（TAA，可选）
        if (_raySettings.enableTemporalAccumulation)
        {
            cmd.GetTemporaryRT(TaaRT, lowDesc, FilterMode.Bilinear);

            if (_isFirstFrame)
            {
                cmd.Blit(GodRayRT, TaaRT);
                _isFirstFrame = false;
            }
            else
            {
                cmd.SetGlobalTexture(PrevFrameRT, _historyRT);
                cmd.SetGlobalVector("_PrevFrame_TexelSize", new Vector4(1.0f / lowDesc.width, 1.0f / lowDesc.height, 0f, 0f));
                cmd.Blit(GodRayRT, TaaRT, _material, 1);
            }

            cmd.Blit(TaaRT, _historyRT);
            outputGodRayRT = TaaRT;
        }

        // Pass 2: Composite 混合到最终画面
        cmd.SetGlobalTexture("_GodRayTex", outputGodRayRT);
        cmd.Blit(TempColorRT, _source, _material, 2);

        if (_raySettings.enableTemporalAccumulation)
        {
            cmd.ReleaseTemporaryRT(TaaRT);
        }

        cmd.ReleaseTemporaryRT(TempColorRT);
        cmd.ReleaseTemporaryRT(GodRayRT);


        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);

        _frameIndex = (_frameIndex + 1) % 8;
        UpdateCameraState(camera);
    }

    private bool HasCameraMoved(Camera camera)
    {
        if (!_hasPrevCameraState)
        {
            return true;
        }

        float positionDelta = Vector3.Distance(camera.transform.position, _prevCamPosition);
        float rotationDelta = Quaternion.Angle(camera.transform.rotation, _prevCamRotation);

        return positionDelta > _raySettings.cameraPositionThreshold ||
               rotationDelta > _raySettings.cameraRotationThreshold;
    }

    private void UpdateCameraState(Camera camera)
    {
        _prevCamPosition = camera.transform.position;
        _prevCamRotation = camera.transform.rotation;
        _hasPrevCameraState = true;
    }

    private void SetupLighting(RenderingData renderingData, Material mat)
    {
        var lightingData = renderingData.lightData;
        Light mainLight = lightingData.mainLightIndex >= 0 ?
            lightingData.visibleLights[lightingData.mainLightIndex].light : null;

        if (mainLight != null)
        {
            // 光源方向：从场景点指向光源（to-light），用于体积散射相位计算
            Vector3 lightDir = -mainLight.transform.forward;
            mat.SetVector("_LightDir", new Vector4(lightDir.x, lightDir.y, lightDir.z, 0));
            mat.SetColor("_LightColor", mainLight.color * mainLight.intensity);
        }
        else
        {
            mat.SetVector("_LightDir", new Vector4(0, 1, 0, 0));
            mat.SetColor("_LightColor", Color.white);
        }
    }

    private void SetupCamera(RenderingData renderingData, Material mat)
    {
        Camera cam = renderingData.cameraData.camera;
        mat.SetVector("_CamParams", new Vector4(cam.nearClipPlane, cam.farClipPlane, 0, 0));
    }

    private void SetupRaySettings(Material mat)
    {
        mat.SetFloat("_G", _raySettings.henyeyGreenstein_G);
        mat.SetFloat("_Density", _raySettings.density);
        mat.SetFloat("_StepSize", _raySettings.stepSize);
        mat.SetFloat("_MaxDistance", _raySettings.maxDistance);
        mat.SetFloat("_JitterStrength", _raySettings.jitterStrength);
        mat.SetFloat("_MaxSteps", _raySettings.maxSteps);
        mat.SetFloat("_Decay", _raySettings.decay);
        mat.SetFloat("_Intensity", _raySettings.intensity);
        mat.SetFloat("_ShadowOcclusionContrast", _raySettings.shadowOcclusionContrast);
        mat.SetFloat("_UseTemporalAccumulation", _raySettings.enableTemporalAccumulation ? 1f : 0f);
        mat.SetColor("_TintColor", _raySettings.tintColor);
    }

    public void Cleanup()
    {
        if (_material != null)
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                Object.Destroy(_material);
            else
                Object.DestroyImmediate(_material);
#else
            Object.Destroy(_material);
#endif
            _material = null;
        }

        // 释放历史帧 RT
        if (_historyRT != null)
        {
            _historyRT.Release();
            SafeDestroy(_historyRT);
            _historyRT = null;
        }

        _hasPrevCameraState = false;
    }
}