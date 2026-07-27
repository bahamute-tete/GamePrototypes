using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class MobileBloomRenderFeature : ScriptableRendererFeature
{
    [Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        [HideInInspector] public Shader shader;
    }

    public Settings settings = new Settings();
    private MobileBloomRenderPass _pass;
    private Material _material;

    public override void Create()
    {
        if (settings.shader == null)
            settings.shader = Shader.Find("Hidden/PostProcess/MobileBloom");
        if (settings.shader == null) return;

        if (_material == null)
            _material = CoreUtils.CreateEngineMaterial(settings.shader);

        _pass = new MobileBloomRenderPass(_material)
        {
            renderPassEvent = settings.renderPassEvent
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass == null || _material == null) return;
        var camType = renderingData.cameraData.cameraType;
        if (camType != CameraType.Game && camType != CameraType.SceneView) return;

        // 从当前 VolumeStack 拉取活跃 component;不存在或 intensity=0 时整段跳过
        var stack = VolumeManager.instance.stack;
        var comp  = stack.GetComponent<MobileBloomVolumeComponent>();
        if (comp == null || !comp.IsActive()) return;

        _pass.SetVolumeComponent(comp);
        renderer.EnqueuePass(_pass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (_pass == null) return;
        _pass.SetSource(renderer.cameraColorTargetHandle);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
        CoreUtils.Destroy(_material);
    }
}
