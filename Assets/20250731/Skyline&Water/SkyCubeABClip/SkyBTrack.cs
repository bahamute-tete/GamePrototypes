using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// ====================================================================
//   Sky B Track
//   绑定 MagicWaterController；clip 类型为 SkyBClip
//   驱动 controller 的 skyTintB / skyExposureB / skyRotationB
// ====================================================================

[TrackColor(0.4f, 0.55f, 0.85f)]
[TrackClipType(typeof(SkyBClip))]
[TrackBindingType(typeof(MagicWaterController))]
[DisplayName("Custom/Skyline/Sky B Track")]
public class SkyBTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        var mixer     = ScriptPlayable<SkyBMixerBehaviour>.Create(graph, inputCount);
        var behaviour = mixer.GetBehaviour();

        behaviour.bounds = new List<SkyBMixerBehaviour.ClipBound>();
        foreach (var clip in GetClips())
        {
            var asset = clip.asset as SkyBClip;
            if (asset == null) continue;

            behaviour.bounds.Add(new SkyBMixerBehaviour.ClipBound
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
