using UnityEngine;

/// <summary>能量门的新增显示参数。基础组件仍负责交线、RT 和共享平面坐标。</summary>
[ExecuteAlways, DefaultExecutionOrder(1100), DisallowMultipleComponent]
[RequireComponent(typeof(DissolvePlaneContact))]
public sealed class ContactEnergyGateSettings : MonoBehaviour
{
    [ColorUsage(false, true), Tooltip("叠加在接触柔光中的能量颜色，支持 HDR。")]
    public Color energyColor = new Color(0.05f, 1.2f, 2.5f, 1);

    [Range(0,5), Tooltip("流动能量丝的亮度；不会影响基础亮线。")]
    public float energyIntensity = 1.2f;

    [Tooltip("采样 R 通道作为能量图案。建议无缝灰度图、Repeat、关闭 sRGB、开启 Mipmap。留空使用中灰，不生成程序噪声。")]
    public Texture2D noiseTexture;

    [Min(0.1f), Tooltip("每米重复整张贴图的次数，越大图案越小；更换贴图后可重新调整。")]
    public float noiseScale = 9;

    [Tooltip("额外的辉光噪声图，R 通道与原辉光噪声相乘。白色保留、黑色挖空；留空不改变原效果。不影响能量图案或接触亮线。")]
    public Texture2D glowNoiseTexture;

    [Min(0.1f), Tooltip("额外辉光图每米重复次数，独立控制第二层图案大小。建议贴图 Repeat、关闭 sRGB、开启 Mipmap。")]
    public float glowNoiseScale = 2;

    [Range(0,1), Tooltip("辉光乘噪声的强度，无单位：0 保留原辉光，1 完整乘贴图 R 通道。不旋转图案、不移动辉光位置。") ]
    public float glowDistortion = 1;

    [Min(0.1f), Tooltip("辉光扰动贴图每米重复次数。小值产生大块起伏，大值产生细碎边缘；与能量图案密度独立。")]
    public float glowDistortionScale = 2;

    [Range(0,1), Tooltip("外围噪声剔除门槛。越大越破碎，0 恢复单纯灰度相乘；保护靠近接触线的内圈。")]
    public float glowEdgeBreakup = 0.75f;

    [Range(0.001f,0.2f), Tooltip("破碎缺口的柔和程度。越小越利落，越大越柔和；始终保留屏幕抗锯齿。")]
    public float glowEdgeSoftness = 0.025f;

    [Tooltip("沿平面 right/up 的流速，单位米/秒。")]
    public Vector2 flowSpeed = new Vector2(0.12f, 0.55f);

    [Range(0,1), Tooltip("能量亮度的呼吸幅度。0 表示不呼吸。")]
    public float pulseAmount = 0.2f;

    [Range(0,5), Tooltip("轮廓内闪动光点的亮度；不是脱离平面的粒子。")]
    public float sparkIntensity = 1.5f;

    [Tooltip("编辑模式固定预览时间；运行时自动使用游戏时间。")]
    public float previewTime = 1.5f;

    MeshRenderer target;
    MaterialPropertyBlock properties;
    MaterialPropertyBlock previousProperties;

    void OnEnable()
    {
        target = GetComponent<MeshRenderer>();
        properties = new MaterialPropertyBlock();
        previousProperties = new MaterialPropertyBlock();
        // 保存原属性块，禁用时恢复。本组件不改写共享材质或基础脚本。
        target.GetPropertyBlock(previousProperties);
    }
    void LateUpdate() { ApplySettings(); }

    public void ApplySettings()
    {
        if (!isActiveAndEnabled || target == null) return;
        // 1100 执行顺序晚于基础组件的 1000，确保临时显示材质已经就绪。
        target.GetPropertyBlock(properties);
        // 显式写入灰色兜底，清空贴图时不会残留上一次绑定的纹理。
        properties.SetTexture("_NoiseTex", noiseTexture != null ? noiseTexture : Texture2D.grayTexture);
        // 白色是乘法单位元；清空新增贴图后恢复原结果，不残留上次绑定。
        properties.SetTexture("_GlowNoiseTex", glowNoiseTexture != null ? glowNoiseTexture : Texture2D.whiteTexture);
        properties.SetFloat("_GlowNoiseScale", Mathf.Max(0.1f,glowNoiseScale));
        properties.SetFloat("_GlowBaseNoiseEnabled", noiseTexture != null ? 1 : 0);
        properties.SetColor("_EnergyColor", energyColor);
        properties.SetFloat("_EnergyIntensity", energyIntensity);
        properties.SetFloat("_NoiseScale", Mathf.Max(0.1f, noiseScale));
        // 任意一张噪声图存在即可控制辉光；两张都为空才关闭调制。
        bool hasGlowNoise = noiseTexture != null || glowNoiseTexture != null;
        properties.SetFloat("_GlowDistortion", hasGlowNoise ? Mathf.Clamp01(glowDistortion) : 0);
        properties.SetFloat("_GlowDistortionScale", Mathf.Max(0.1f, glowDistortionScale));
        // 未提供噪声贴图时关闭侵蚀，避免中灰兜底把整圈统一裁掉。
        properties.SetFloat("_GlowEdgeBreakup", hasGlowNoise ? Mathf.Clamp01(glowEdgeBreakup) : 0);
        properties.SetFloat("_GlowEdgeSoftness", Mathf.Clamp(glowEdgeSoftness,0.001f,0.2f));
        properties.SetVector("_FlowSpeed", new Vector4(flowSpeed.x,flowSpeed.y,0,0));
        properties.SetFloat("_PulseAmount", pulseAmount);
        properties.SetFloat("_SparkIntensity", sparkIntensity);
        // 固定预览时间可复现截图；播放模式每帧推进，暂停时随游戏时间停止。
        properties.SetFloat("_EffectTime", Application.isPlaying ? Time.time : previewTime);
        target.SetPropertyBlock(properties);
    }
    void OnDisable()
    {
        if (target != null) target.SetPropertyBlock(previousProperties);
    }
}


