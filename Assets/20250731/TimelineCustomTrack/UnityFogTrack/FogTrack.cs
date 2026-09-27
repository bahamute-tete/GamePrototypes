using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum FogTrackMode
{
    [InspectorName("跟随场景")] Scene = 0,
    [InspectorName("线性")] Linear = 1,
    [InspectorName("指数")] Exponential = 2,
    [InspectorName("指数平方")] ExponentialSquared = 3
}

[TrackColor(0.7f, 0.7f, 0.9f)]
[TrackClipType(typeof(FogClip))]
[DisplayName("Custom/Fog/Fog Track")]
public class FogTrack : TrackAsset
{
    [Tooltip("整条轨道使用统一雾模式。跟随场景保持旧轨道的行为。")]
    public FogTrackMode mode = FogTrackMode.Scene;

    public FogMode ResolveMode(FogMode sceneMode)
    {
        return mode == FogTrackMode.Scene ? sceneMode : (FogMode)mode;
    }

    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        var playable = ScriptPlayable<FogMixer>.Create(graph, inputCount);
        playable.GetBehaviour().Track = this;
        return playable;
    }
}
