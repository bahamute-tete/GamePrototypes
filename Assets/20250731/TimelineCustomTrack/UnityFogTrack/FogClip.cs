using System;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;

[Serializable]
public class FogClip : PlayableAsset, ITimelineClipAsset
{
    public FogBehaviour template = new FogBehaviour();

    public ClipCaps clipCaps
    {
        get { return ClipCaps.Blending; }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<FogBehaviour>.Create(graph, template);
        FogBehaviour fogBehaviour = playable.GetBehaviour();

        // 简单地复制所有参数到实例
        fogBehaviour.fogColor = template.fogColor;
        fogBehaviour.fogStartDistance = template.fogStartDistance;
        fogBehaviour.fogEndDistance = template.fogEndDistance;
        fogBehaviour.fogDensity = template.fogDensity;

        return playable;
    }
}