using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class FogClip : PlayableAsset, ITimelineClipAsset
{
    public FogBehaviour template = new FogBehaviour();

    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        // ScriptPlayable clones the template. Keep the serialized field names for existing clips.
        var playable = ScriptPlayable<FogBehaviour>.Create(graph, template);
        playable.GetBehaviour().Validate();
        return playable;
    }

    private void OnValidate()
    {
        if (template == null) template = new FogBehaviour();
        template.Validate();
    }
}
