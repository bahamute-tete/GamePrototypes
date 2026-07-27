// LightMixerBehaviour.cs
using UnityEngine;
using UnityEngine.Playables;


public class LightMixerBehaviour : PlayableBehaviour
{
    // 绑定与默认值缓存
    Light m_TrackBinding;
    Transform m_Transform;
    bool m_DefaultsCaptured;

    Color m_DefColor;
    float m_DefIntensity;
    float m_DefRange;
    float m_DefSpotAngle;
    Vector3 m_DefLocalPosition;
    Quaternion m_DefLocalRotation;

    static readonly AnimationCurve s_DefaultCurve = AnimationCurve.Constant(0f, 1f, 1f);

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var binding = playerData as Light;
        if (binding == null) return;

        if (!m_DefaultsCaptured) CaptureDefaults(binding);

        // —— Light 属性累加器
        float intensity = 0f;
        Color color = Color.clear;
        float range = 0f;
        float spotAngle = 0f;
        float wIntensity = 0f, wColor = 0f, wRange = 0f, wSpotAngle = 0f;

        // —— Transform 累加器
        Vector3 position = Vector3.zero;
        Quaternion rotation = Quaternion.identity;
        float wPosition = 0f;
        float wRotation = 0f;
        bool rotStarted = false;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);
            if (inputWeight <= 0f) continue;

            var input = playable.GetInput(i);
            var b = ((ScriptPlayable<LightBehaviour>)input).GetBehaviour();

            double duration = input.GetDuration();
            float t = duration > 0.0
                ? Mathf.Clamp01((float)(input.GetTime() / duration))
                : 0f;

            var curve = b.curve ?? s_DefaultCurve;
            float weight = inputWeight * curve.Evaluate(t);

            // Light 核心
            intensity += b.intensity * weight;
            color += b.color * weight;
            wIntensity += weight;
            wColor += weight;

            // Light 形状
            if (b.affectRange) { range += b.range * weight; wRange += weight; }
            if (b.affectSpotAngle) { spotAngle += b.spotAngle * weight; wSpotAngle += weight; }

            // Transform 位置 —— 线性混合
            if (b.affectPosition)
            {
                position += b.position * weight;
                wPosition += weight;
            }

            // Transform 旋转 —— 链式 Slerp(等价于按权重的加权球面平均)
            if (b.affectRotation)
            {
                if (!rotStarted)
                {
                    rotation = b.rotation;
                    wRotation = weight;
                    rotStarted = true;
                }
                else
                {
                    wRotation += weight;
                    rotation = Quaternion.Slerp(rotation, b.rotation, weight / wRotation);
                }
            }
        }

        // —— 边界控制:权重不足 1 时用默认值补齐
        BlendToDefault(ref intensity, wIntensity, m_DefIntensity);
        BlendToDefault(ref color, wColor, m_DefColor);

        binding.intensity = intensity;
        binding.color = color;

        if (wRange > 0f)
        {
            BlendToDefault(ref range, wRange, m_DefRange);
            binding.range = range;
        }
        if (wSpotAngle > 0f)
        {
            BlendToDefault(ref spotAngle, wSpotAngle, m_DefSpotAngle);
            binding.spotAngle = spotAngle;
        }

        // Transform 写回 —— 仅当有 clip 影响时
        if (wPosition > 0f)
        {
            if (wPosition < 1f)
                position += m_DefLocalPosition * (1f - wPosition);
            m_Transform.localPosition = position;
        }
        if (wRotation > 0f)
        {
            if (wRotation < 1f)
                rotation = Quaternion.Slerp(m_DefLocalRotation, rotation, wRotation);
            m_Transform.localRotation = rotation;
        }
    }

    public override void OnPlayableDestroy(Playable playable) => RestoreDefaults();

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (!playable.GetGraph().IsValid()) return;
        if (!playable.GetGraph().IsPlaying()) RestoreDefaults();
    }

    void CaptureDefaults(Light light)
    {
        m_TrackBinding = light;
        m_Transform = light.transform;
        m_DefColor = light.color;
        m_DefIntensity = light.intensity;
        m_DefRange = light.range;
        m_DefSpotAngle = light.spotAngle;
        m_DefLocalPosition = m_Transform.localPosition;
        m_DefLocalRotation = m_Transform.localRotation;
        m_DefaultsCaptured = true;
    }

    void RestoreDefaults()
    {
        if (!m_DefaultsCaptured || m_TrackBinding == null) return;
        m_TrackBinding.color = m_DefColor;
        m_TrackBinding.intensity = m_DefIntensity;
        m_TrackBinding.range = m_DefRange;
        m_TrackBinding.spotAngle = m_DefSpotAngle;
        if (m_Transform != null)
        {
            m_Transform.localPosition = m_DefLocalPosition;
            m_Transform.localRotation = m_DefLocalRotation;
        }
    }

    static void BlendToDefault(ref float v, float w, float def) { if (w < 1f) v += def * (1f - w); }
    static void BlendToDefault(ref Color v, float w, Color def) { if (w < 1f) v += def * (1f - w); }
}

