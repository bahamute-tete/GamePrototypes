using System;
using UnityEngine;
using UnityEngine.Playables;

public class FogMixer : PlayableBehaviour
{
    // 保存原始雾设置以便还原
    private bool originalFogEnabled;
    private Color originalFogColor;
    private FogMode originalFogMode;
    private float originalStartDistance;
    private float originalEndDistance;
    private float originalDensity;

    public override void OnGraphStart(Playable playable)
    {
        // 保存原始雾设置
        originalFogEnabled = RenderSettings.fog;
        originalFogColor = RenderSettings.fogColor;
        originalFogMode = RenderSettings.fogMode;
        originalStartDistance = RenderSettings.fogStartDistance;
        originalEndDistance = RenderSettings.fogEndDistance;
        originalDensity = RenderSettings.fogDensity;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        // 启用雾效果
        RenderSettings.fog = true;

        // 初始化混合变量
        Color blendedColor = Color.clear;
        float blendedStartDistance = 0f;
        float blendedEndDistance = 0f;
        float blendedDensity = 0f;
        float totalWeight = 0f;
        bool hasFogClips = false;

        // 处理所有输入剪辑
        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight > 0f)
            {
                ScriptPlayable<FogBehaviour> inputPlayable = (ScriptPlayable<FogBehaviour>)playable.GetInput(i);
                FogBehaviour behaviour = inputPlayable.GetBehaviour();

                // 累加颜色（对所有雾类型都通用）
                blendedColor += behaviour.fogColor * weight;

                // 累加所有雾参数，稍后根据RenderSettings的fogMode决定使用哪个
                blendedStartDistance += behaviour.fogStartDistance * weight;
                blendedEndDistance += behaviour.fogEndDistance * weight;
                blendedDensity += behaviour.fogDensity * weight;

                totalWeight += weight;
                hasFogClips = true;
            }
        }

        // 如果有活动的雾剪辑，应用混合结果
        if (hasFogClips && totalWeight > 0f)
        {
            // 应用混合颜色
            RenderSettings.fogColor = blendedColor / totalWeight;

            // 根据当前RenderSettings中的雾模式应用相应参数
            switch (RenderSettings.fogMode)
            {
                case FogMode.Linear:
                    RenderSettings.fogStartDistance = blendedStartDistance / totalWeight;
                    RenderSettings.fogEndDistance = blendedEndDistance / totalWeight;
                    break;
                case FogMode.Exponential:
                case FogMode.ExponentialSquared:
                    RenderSettings.fogDensity = blendedDensity / totalWeight;
                    break;
            }
        }
        else
        {
            // 没有活动剪辑时还原原始设置
            ResetFogSettings();
        }
    }

    public override void OnGraphStop(Playable playable)
    {
        // 还原原始雾设置
        ResetFogSettings();
    }

    private void ResetFogSettings()
    {
        RenderSettings.fog = originalFogEnabled;
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.fogMode = originalFogMode;
        RenderSettings.fogStartDistance = originalStartDistance;
        RenderSettings.fogEndDistance = originalEndDistance;
        RenderSettings.fogDensity = originalDensity;
    }
}
