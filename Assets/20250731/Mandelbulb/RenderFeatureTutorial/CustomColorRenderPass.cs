using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;


public class CustomColorRenderPass : ScriptableRenderPass
{
    private Material _customMaterial;
    private FilteringSettings _filteringSettings;// 过滤设置（指定渲染的层、渲染队列）
    private RenderStateBlock _renderStateBlock;// 渲染状态（比如深度测试、混合模式）
    private string _profilerTag = "CustomRenderFeature";// 性能分析标签
    private Color _targetColor;

    public CustomColorRenderPass(string profilerTag,LayerMask layerMask,Color targetColor,Material customMaterial)
    {
        _profilerTag = profilerTag;
        _customMaterial = customMaterial;
        _targetColor = targetColor; // 初始化颜色

         // 设置过滤规则：只渲染指定层的不透明对象
        _filteringSettings = new FilteringSettings(RenderQueueRange.all, layerMask);

        // 设置渲染状态：启用深度测试，关闭混合
        _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
        _renderStateBlock.depthState = new DepthState(true, CompareFunction.LessEqual);
        _renderStateBlock.mask |= RenderStateMask.Depth; // 添加深度测试到渲染状态
    }

     // 用于在 Feature 的 AddRenderPasses 中更新参数
    public void Setup(Color targetColor, LayerMask layerMask)
    {
        _targetColor = targetColor;
        _filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        // 如果需要自定义渲染目标，在这里配置（比如创建临时RT）
        // 示例：获取相机的渲染目标
        //base.OnCameraSetup(cmd, ref renderingData);

        RenderTextureDescriptor cameraTextureDescriptor = renderingData.cameraData.cameraTargetDescriptor;
        cameraTextureDescriptor.depthBufferBits = 32;// 确保深度缓冲位数
    }
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (_customMaterial == null) return;

        _customMaterial.SetColor("_TargetColor", _targetColor);

        CommandBuffer cmd = CommandBufferPool.Get(_profilerTag);
        using (new ProfilingScope(cmd, new ProfilingSampler(_profilerTag)))
        {
            // 获取渲染器对象
            var renderer = renderingData.cameraData.renderer;

            DrawingSettings drawingSettings = CreateDrawingSettings(
                new ShaderTagId("UniversalForward"),// 匹配URP的Forward渲染路径
                ref renderingData,
                renderingData.cameraData.defaultOpaqueSortFlags);

            // 筛选：LightMode 叫 "SRPDefaultUnlit" 的物体(比如普通的墙壁、地板)
            // drawingSettings.SetShaderPassName(1, new ShaderTagId("SRPDefaultUnlit")); 
            drawingSettings.overrideMaterial = _customMaterial;// 使用自定义材质
            drawingSettings.overrideMaterialPassIndex = 0;

            context.DrawRenderers(
                renderingData.cullResults,
                ref drawingSettings,
                ref _filteringSettings,
                ref _renderStateBlock);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd); 
        }
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        base.FrameCleanup(cmd);
    }
}
