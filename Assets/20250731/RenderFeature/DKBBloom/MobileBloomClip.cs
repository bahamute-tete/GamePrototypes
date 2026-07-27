using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class MobileBloomClip : PlayableAsset, ITimelineClipAsset
{
    public MobileBloomBehaviour template = new MobileBloomBehaviour();

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation | ClipCaps.Looping;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<MobileBloomBehaviour>.Create(graph, template);
    }
}
