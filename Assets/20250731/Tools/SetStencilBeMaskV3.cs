using UnityEngine;
using System.Collections.Generic;

public class SetStencilBeMaskV3 : MonoBehaviour
{
    // 第一个Pass的模板设置
    [Header("第一个Pass模板设置")]
    [Range(0, 255)]
    public int _StencilMask = 1;
    
    [Tooltip("模板比较函数")]
    public UnityEngine.Rendering.CompareFunction _StencilCompFunction = UnityEngine.Rendering.CompareFunction.Always;
    
    [Tooltip("模板通过操作")]
    public UnityEngine.Rendering.StencilOp _StencilPassOperation = UnityEngine.Rendering.StencilOp.Keep;
    
    [Tooltip("模板失败操作")]
    public UnityEngine.Rendering.StencilOp _StencilFailOperation = UnityEngine.Rendering.StencilOp.Keep;
    
    [Tooltip("深度测试失败操作")]
    public UnityEngine.Rendering.StencilOp _StencilZFailOperation = UnityEngine.Rendering.StencilOp.Keep;

    //// 第二个Pass的启用开关
    //[Header("第二个Pass模板设置")]
    //public bool _EnableSecondPassStencil = false;
    
    //[Range(0, 255)]
    //public int _StencilMask2 = 1;
    
    //[Tooltip("模板比较函数")]
    //public UnityEngine.Rendering.CompareFunction _StencilCompFunction2 = UnityEngine.Rendering.CompareFunction.Always;
    
    //[Tooltip("模板通过操作")]
    //public UnityEngine.Rendering.StencilOp _StencilPassOperation2 = UnityEngine.Rendering.StencilOp.Keep;
    
    //[Tooltip("模板失败操作")]
    //public UnityEngine.Rendering.StencilOp _StencilFailOperation2 = UnityEngine.Rendering.StencilOp.Keep;
    
    //[Tooltip("深度测试失败操作")]
    //public UnityEngine.Rendering.StencilOp _StencilZFailOperation2 = UnityEngine.Rendering.StencilOp.Keep;

    private List<Material> materials = new List<Material>();

    private void OnEnable()
    {
        //CollectMaterials();
        //ApplyFirstPassSettings();
        //if (_EnableSecondPassStencil)
        //    ApplySecondPassSettings();
    }

    // 收集所有使用的材质
    public void CollectMaterials()
    {
        materials.Clear();
        
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material != null && !materials.Contains(material))
                {
                    materials.Add(material);
                }
            }
        }
        Debug.Log(materials.Count);
    }

    // 应用第一个Pass的模板设置
    public void ApplyFirstPassSettings()
    {
        if (materials.Count == 0)
        {
            CollectMaterials();
        }

        foreach (Material material in materials)
        {
            // 设置第一个Pass的模板测试参数
            //Debug.Log(111111);
            material.SetInt("_StencilMask", _StencilMask);
            material.SetInt("_StencilComp", (int)_StencilCompFunction);
            material.SetInt("_StencilPass", (int)_StencilPassOperation);
            material.SetInt("_StencilFail", (int)_StencilFailOperation);
            material.SetInt("_StencilZFail", (int)_StencilZFailOperation);
        }
        
        Debug.Log($"已应用第一个Pass模板设置到 {materials.Count} 个材质");
    }

    // 应用第二个Pass的模板设置
    //public void ApplySecondPassSettings()
    //{
    //    if (!_EnableSecondPassStencil)
    //    {
    //        Debug.Log("第二个Pass的模板设置已禁用");
    //        return;
    //    }

    //    if (materials.Count == 0)
    //    {
    //        CollectMaterials();
    //    }

    //    foreach (Material material in materials)
    //    {
    //        // 设置第二个Pass的模板测试参数
    //        material.SetInt("_StencilMask2", _StencilMask2);
    //        material.SetInt("_StencilComp2", (int)_StencilCompFunction2);
    //        material.SetInt("_StencilPass2", (int)_StencilPassOperation2);
    //        material.SetInt("_StencilFail2", (int)_StencilFailOperation2);
    //        material.SetInt("_StencilZFail2", (int)_StencilZFailOperation2);
    //    }
        
    //    Debug.Log($"已应用第二个Pass模板设置到 {materials.Count} 个材质");
    //}

    // 重置第一个Pass的设置
    public void ResetFirstPassSettings()
    {
        _StencilMask = 1;
        _StencilCompFunction = UnityEngine.Rendering.CompareFunction.Always;
        _StencilPassOperation = UnityEngine.Rendering.StencilOp.Keep;
        _StencilFailOperation = UnityEngine.Rendering.StencilOp.Keep;
        _StencilZFailOperation = UnityEngine.Rendering.StencilOp.Keep;
        
        ApplyFirstPassSettings();
        Debug.Log("第一个Pass模板设置已重置为默认值");
    }

    //// 重置第二个Pass的设置
    //public void ResetSecondPassSettings()
    //{
    //    if (!_EnableSecondPassStencil)
    //    {
    //        Debug.Log("第二个Pass的模板设置已禁用");
    //        return;
    //    }
        
    //    _StencilMask2 = 1;
    //    _StencilCompFunction2 = UnityEngine.Rendering.CompareFunction.Always;
    //    _StencilPassOperation2 = UnityEngine.Rendering.StencilOp.Keep;
    //    _StencilFailOperation2 = UnityEngine.Rendering.StencilOp.Keep;
    //    _StencilZFailOperation2 = UnityEngine.Rendering.StencilOp.Keep;
        
    //    ApplySecondPassSettings();
    //    Debug.Log("第二个Pass模板设置已重置为默认值");
    //}
}
