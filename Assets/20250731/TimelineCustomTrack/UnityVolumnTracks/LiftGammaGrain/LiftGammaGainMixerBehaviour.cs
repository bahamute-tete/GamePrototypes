using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class LiftGammaGainMixerBehaviour : PlayableBehaviour
{
    private const float k_BoundaryEpsilon = 1e-4f;

    private LiftGammaGain m_Component;

    private Vector4 m_OrigLift;
    private Vector4 m_OrigGamma;
    private Vector4 m_OrigGain;

    private bool m_Cached;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var volume = playerData as Volume;
        if (volume == null || volume.profile == null) return;
        if (!volume.profile.TryGet(out m_Component)) return;

        if (!m_Cached)
        {
            m_OrigLift  = m_Component.lift.value;
            m_OrigGamma = m_Component.gamma.value;
            m_OrigGain  = m_Component.gain.value;
            m_Cached = true;
        }

        int inputCount = playable.GetInputCount();

        float totalWeight = 0f;
        Vector4 blendLift  = Vector4.zero;
        Vector4 blendGamma = Vector4.zero;
        Vector4 blendGain  = Vector4.zero;

        for (int i = 0; i < inputCount; i++)
        {
            float w = playable.GetInputWeight(i);
            if (w <= k_BoundaryEpsilon) continue;

            var sp = (ScriptPlayable<LiftGammaGainBehaviour>)playable.GetInput(i);
            var b  = sp.GetBehaviour();

            blendLift  += b.lift  * w;
            blendGamma += b.gamma * w;
            blendGain  += b.gain  * w;
            totalWeight += w;
        }

        if (totalWeight <= k_BoundaryEpsilon)
        {
            SnapToBoundary();
            return;
        }

        float rest = Mathf.Clamp01(1f - totalWeight);

        m_Component.lift.value  = blendLift  + m_OrigLift  * rest;
        m_Component.gamma.value = blendGamma + m_OrigGamma * rest;
        m_Component.gain.value  = blendGain  + m_OrigGain  * rest;
    }

    private void SnapToBoundary()
    {
        if (m_Component == null) return;
        m_Component.lift.value  = m_OrigLift;
        m_Component.gamma.value = m_OrigGamma;
        m_Component.gain.value  = m_OrigGain;
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        if (m_Cached) SnapToBoundary();
        m_Cached = false;
        m_Component = null;
    }
}
