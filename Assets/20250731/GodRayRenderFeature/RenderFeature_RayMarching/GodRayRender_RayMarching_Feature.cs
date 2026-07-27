using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class GodRayRenderRayMarchingFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;
        public Shader shader;

        [Header("Ray Marching")]
        [Range(0f, 0.95f)] public float henyeyGreenstein_G = 0.7f;
        [Range(0.001f, 0.2f)] public float decay = 0.015f;
        [Range(0f, 8f)] public float density = 1.5f;
        [Range(0.1f, 4f)] public float stepSize = 0.3f;
        [Range(4, 256)] public int maxSteps = 64;
        [Range(5f, 500f)] public float maxDistance = 120f;
        [Range(0f, 2f)] public float jitterStrength = 1f;
        [Range(0f, 10f)] public float intensity = 4f;
        [Range(0.2f, 4f)] public float shadowOcclusionContrast = 1f;
        [ColorUsage(true, true)] public Color tintColor = Color.white;

        [Header("Performance")]
        [Range(1, 4)] public int downsample = 1;

        [Header("Temporal")]
        public bool enableTemporalAccumulation = false;
        [Range(0f, 0.98f)] public float taaBlend = 0.9f;

        [Header("Temporal Stabilization")]
        public bool resetHistoryOnCameraMotion = true;
        [Range(0f, 0.2f)] public float cameraPositionThreshold = 0.01f;
        [Range(0f, 5f)] public float cameraRotationThreshold = 0.2f;
        [Range(0f, 0.98f)] public float motionTaaBlend = 0.15f;
    }

    [System.Serializable]
    public struct RaySettings
    {
        public float henyeyGreenstein_G;
        public float decay;
        public float density;
        public float stepSize;
        public int maxSteps;
        public float maxDistance;
        public float jitterStrength;
        public float intensity;
        public float shadowOcclusionContrast;
        public Color tintColor;
        public int downsample;
        public bool enableTemporalAccumulation;
        public float taaBlend;
        public bool resetHistoryOnCameraMotion;
        public float cameraPositionThreshold;
        public float cameraRotationThreshold;
        public float motionTaaBlend;
    }

    public Settings settings = new Settings();

    private GodRayRenderRayMarchingPass _rayRenderRayMarchingPass; 

    private RaySettings BuildRaySettings()
    {
        return new RaySettings
        {
            henyeyGreenstein_G = settings.henyeyGreenstein_G,
            decay = settings.decay,
            density = settings.density,
            stepSize = settings.stepSize,
            maxSteps = settings.maxSteps,
            maxDistance = settings.maxDistance,
            jitterStrength = settings.jitterStrength,
            intensity = settings.intensity,
            shadowOcclusionContrast = settings.shadowOcclusionContrast,
            tintColor = settings.tintColor,
            downsample = settings.downsample,
            enableTemporalAccumulation = settings.enableTemporalAccumulation,
            taaBlend = settings.taaBlend,
            resetHistoryOnCameraMotion = settings.resetHistoryOnCameraMotion,
            cameraPositionThreshold = settings.cameraPositionThreshold,
            cameraRotationThreshold = settings.cameraRotationThreshold,
            motionTaaBlend = settings.motionTaaBlend
        };
    }

    
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
       
        if (settings.shader == null || !settings.shader.isSupported)
        {
            Debug.LogError("Shader is not supported！");
            return;
        }

        if (_rayRenderRayMarchingPass == null)
        {
            Create(); 
        }
       
        if (renderingData.cameraData.cameraTargetDescriptor.width <= 0 ||
            renderingData.cameraData.cameraTargetDescriptor.height <= 0)
        {
            return;
        }

        //关联当前渲染器的目标
        _rayRenderRayMarchingPass.SetupRenderer(renderer);
        _rayRenderRayMarchingPass.UpdateSettings(BuildRaySettings());
        _rayRenderRayMarchingPass.renderPassEvent = settings.passEvent;

        renderer.EnqueuePass(_rayRenderRayMarchingPass);
    }

    public override void Create()
    {
        
        if (settings.shader != null && settings.shader.isSupported)
        {
            _rayRenderRayMarchingPass = new GodRayRenderRayMarchingPass(
                settings.shader,
                BuildRaySettings()
            );
            _rayRenderRayMarchingPass.renderPassEvent = settings.passEvent;
        }
       
    }

    protected override void Dispose(bool disposing)
    {
        if (_rayRenderRayMarchingPass != null)
        {
            _rayRenderRayMarchingPass.Cleanup();
            _rayRenderRayMarchingPass = null;
        }
    }
}