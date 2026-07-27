using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class ColorAdjustmentsClip : PlayableAsset, ITimelineClipAsset
{
    public ColorAdjustmentsBehaviour template = new ColorAdjustmentsBehaviour();

    public ClipCaps clipCaps =>
        ClipCaps.Blending | ClipCaps.Extrapolation | ClipCaps.ClipIn | ClipCaps.SpeedMultiplier;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<ColorAdjustmentsBehaviour>.Create(graph, template);
    }
}
