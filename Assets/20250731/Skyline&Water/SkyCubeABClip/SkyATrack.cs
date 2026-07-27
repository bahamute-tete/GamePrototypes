using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// ====================================================================
//   Sky A Track
//   绑定 MagicWaterController；clip 类型为 SkyAClip
//   驱动 controller 的 skyTintA / skyExposureA / skyRotationA
// ====================================================================

[TrackColor(0.7f, 0.85f, 0.4f)]
[TrackClipType(typeof(SkyAClip))]
[TrackBindingType(typeof(MagicWaterController))]
[DisplayName("Custom/Skyline/Sky A Track")]
public class SkyATrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        var mixer     = ScriptPlayable<SkyAMixerBehaviour>.Create(graph, inputCount);
        var behaviour = mixer.GetBehaviour();

        behaviour.bounds = new List<SkyAMixerBehaviour.ClipBound>();
        foreach (var clip in GetClips())
        {
            var asset = clip.asset as SkyAClip;
            if (asset == null) continue;

            behaviour.bounds.Add(new SkyAMixerBehaviour.ClipBound
            {
                startTime  = clip.start,
                endTime    = clip.end,
                startState = asset.startState,
                endState   = asset.endState
            });
        }
        behaviour.bounds.Sort((a, b) => a.startTime.CompareTo(b.startTime));

        return mixer;
    }
}
