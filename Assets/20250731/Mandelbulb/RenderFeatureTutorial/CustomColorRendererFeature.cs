using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[System.Serializable]
public class CustomColorRenderFeature : ScriptableRendererFeature
{
    [Header("CustomePameters")]
    public LayerMask  layerMask = 1<<0;// 默认渲染第0层（Default层）
    public Material material;
    public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    public Color targetColor = Color.white;

    public CustomColorRenderPass _customRenderPass;

    public override void Create()
    {
        // 实例化自定义渲染通道
        _customRenderPass = new CustomColorRenderPass(
            "CustomColorRenderPass",
            layerMask,
            targetColor,
            material);

         // 设置渲染通道的执行时机（关键！决定在URP渲染流程的哪个阶段执行）
        _customRenderPass.renderPassEvent = renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // 将自定义渲染通道添加到渲染器中
        if (material!= null)
        {
             _customRenderPass.Setup(targetColor, layerMask);
            renderer.EnqueuePass(_customRenderPass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        // 清理资源
        _customRenderPass = null;
    }
}
