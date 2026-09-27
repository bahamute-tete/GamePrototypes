using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class SkylineVfxClip : PlayableAsset, ITimelineClipAsset
{
    public ClipCaps clipCaps=>ClipCaps.None;
    public override Playable CreatePlayable(PlayableGraph graph,GameObject owner)=>ScriptPlayable<SkylineVfxBehaviour>.Create(graph);
}
public class SkylineVfxBehaviour : PlayableBehaviour { }
