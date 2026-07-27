using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class LiftGammaGainClip : PlayableAsset, ITimelineClipAsset
{
    public LiftGammaGainBehaviour template = new LiftGammaGainBehaviour();

    public ClipCaps clipCaps =>
        ClipCaps.Blending | ClipCaps.Extrapolation | ClipCaps.ClipIn | ClipCaps.SpeedMultiplier;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<LiftGammaGainBehaviour>.Create(graph, template);
    }
}
