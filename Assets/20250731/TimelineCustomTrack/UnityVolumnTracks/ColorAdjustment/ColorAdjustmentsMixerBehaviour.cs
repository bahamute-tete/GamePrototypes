using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ColorAdjustmentsMixerBehaviour : PlayableBehaviour
{
    private const float k_BoundaryEpsilon = 1e-4f;

    private ColorAdjustments m_Component;

    // 原始值缓存
    private float m_OrigPostExposure;
    private float m_OrigContrast;
    private Color m_OrigColorFilter;
    private float m_OrigHueShift;
    private float m_OrigSaturation;

    private bool m_Cached;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var volume = playerData as Volume;
        if (volume == null || volume.profile == null) return;
        if (!volume.profile.TryGet(out m_Component)) return;

        // 首帧缓存
        if (!m_Cached)
        {
            m_OrigPostExposure = m_Component.postExposure.value;
            m_OrigContrast     = m_Component.contrast.value;
            m_OrigColorFilter  = m_Component.colorFilter.value;
            m_OrigHueShift     = m_Component.hueShift.value;
            m_OrigSaturation   = m_Component.saturation.value;
            m_Cached = true;
        }

        int inputCount = playable.GetInputCount();

        float totalWeight = 0f;
        float blendExposure = 0f;
        float blendContrast = 0f;
        Color blendFilter = new Color(0f, 0f, 0f, 0f);
        float blendHueShift = 0f;
        float blendSaturation = 0f;

        for (int i = 0; i < inputCount; i++)
        {
            float w = playable.GetInputWeight(i);
            if (w <= k_BoundaryEpsilon) continue;

            var sp = (ScriptPlayable<ColorAdjustmentsBehaviour>)playable.GetInput(i);
            var b  = sp.GetBehaviour();

            blendExposure   += b.postExposure * w;
            blendContrast   += b.contrast     * w;
            blendFilter     += b.colorFilter  * w;
            blendHueShift   += b.hueShift     * w;
            blendSaturation += b.saturation   * w;
            totalWeight     += w;
        }

        // 边界回退:没有任何活动 clip,直接还原原值
        if (totalWeight <= k_BoundaryEpsilon)
        {
            SnapToBoundary();
            return;
        }

        // 剩余权重用原值填充(支持 clip 部分覆盖)
        float rest = Mathf.Clamp01(1f - totalWeight);

        m_Component.postExposure.value = blendExposure   + m_OrigPostExposure * rest;
        m_Component.contrast.value     = blendContrast   + m_OrigContrast     * rest;
        m_Component.colorFilter.value  = blendFilter     + m_OrigColorFilter  * rest;
        m_Component.hueShift.value     = blendHueShift   + m_OrigHueShift     * rest;
        m_Component.saturation.value   = blendSaturation + m_OrigSaturation   * rest;
    }

    private void SnapToBoundary()
    {
        if (m_Component == null) return;
        m_Component.postExposure.value = m_OrigPostExposure;
        m_Component.contrast.value     = m_OrigContrast;
        m_Component.colorFilter.value  = m_OrigColorFilter;
        m_Component.hueShift.value     = m_OrigHueShift;
        m_Component.saturation.value   = m_OrigSaturation;
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        if (m_Cached) SnapToBoundary();
        m_Cached = false;
        m_Component = null;
    }
}
