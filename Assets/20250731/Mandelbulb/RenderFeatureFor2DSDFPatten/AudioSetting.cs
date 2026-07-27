using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class AudioSetting : MonoBehaviour
{


    public float[] _Bands = new float[8];

    private Screen2DSDFPatternRenderFeature screen2DSDFPatternRenderFeature;
    // Start is called before the first frame update
    void Start()
    {

        FindRenderFeature();

        if (screen2DSDFPatternRenderFeature == null)
        {
            Debug.LogError("Can not Find Screen2DSDFPatternRenderFeature!");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (screen2DSDFPatternRenderFeature == null) return;

        for (int i = 0; i < 8; i++)
        {
           _Bands[i] = Mathf.Max(AudioVis._audioBandBuffer[i], 0f);
        }


        screen2DSDFPatternRenderFeature.settings.audioBands = _Bands;

    }

    void FindRenderFeature()
    {
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        if (urpAsset == null)
        {
            Debug.LogError("Current render pipeline is not URP!");
            return;
        }

        Debug.Log("Found URP Asset: " + urpAsset.name);

        // 尝试获取渲染器数据列表
        var rendererDataListProperty = typeof(UniversalRenderPipelineAsset).GetProperty("rendererDataList",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        ScriptableRendererData[] rendererDataList = null;

        if (rendererDataListProperty != null)
        {
            rendererDataList = rendererDataListProperty.GetValue(urpAsset) as ScriptableRendererData[];
        }
        else
        {
            // 备用方法：使用 m_RendererDataList 字段
            var rendererDataListField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (rendererDataListField != null)
            {
                rendererDataList = rendererDataListField.GetValue(urpAsset) as ScriptableRendererData[];
            }
        }

        if (rendererDataList == null || rendererDataList.Length == 0)
        {
            Debug.LogError("Could not find any renderer data in URP asset!");
            return;
        }

        Debug.Log($"Found {rendererDataList.Length} renderer data(s)");

        // 遍历所有渲染器数据
        for (int i = 0; i < rendererDataList.Length; i++)
        {
            var rendererData = rendererDataList[i];
            if (rendererData == null)
            {
                Debug.LogWarning($"Renderer data at index {i} is null");
                continue;
            }

            Debug.Log($"Checking renderer data [{i}]: {rendererData.name}");

            // 获取渲染特性列表
            var rendererFeaturesProperty = typeof(ScriptableRendererData).GetProperty("rendererFeatures",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            List<ScriptableRendererFeature> features = null;

            if (rendererFeaturesProperty != null)
            {
                features = rendererFeaturesProperty.GetValue(rendererData) as List<ScriptableRendererFeature>;
            }
            else
            {
                // 备用方法：使用 m_RendererFeatures 字段
                var rendererFeaturesField = typeof(ScriptableRendererData).GetField("m_RendererFeatures",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (rendererFeaturesField != null)
                {
                    features = rendererFeaturesField.GetValue(rendererData) as List<ScriptableRendererFeature>;
                }
            }

            if (features == null || features.Count == 0)
            {
                Debug.LogWarning($"Renderer data [{i}] has no features");
                continue;
            }

            Debug.Log($"Renderer data [{i}] has {features.Count} feature(s)");

            // 遍历所有特性
            for (int j = 0; j < features.Count; j++)
            {
                var feature = features[j];
                if (feature == null)
                {
                    Debug.LogWarning($"Feature at index {j} is null");
                    continue;
                }

                Debug.Log($"Feature [{j}]: {feature.name} (Type: {feature.GetType().Name}, Active: {feature.isActive})");

                if (feature is Screen2DSDFPatternRenderFeature)
                {
                    screen2DSDFPatternRenderFeature = feature as Screen2DSDFPatternRenderFeature;
                    Debug.Log($"<color=green>Successfully found Screen2DSDFPatternRenderFeature in renderer data [{i}]!</color>");
                    return;
                }
            }
        }

        Debug.LogError("Screen2DSDFPatternRenderFeature not found in any renderer data!");
    }
}
