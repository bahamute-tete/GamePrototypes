using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// ====================================================================
//   Horizon Glow B Track
//   绑定 MagicWaterController；clip 类型为 HorizonGlowBClip
//   驱动 controller 的 B 组参数：horizonColorB / horizonIntensityB / etc.
// ====================================================================

[TrackColor(0.3f, 0.5f, 0.9f)]
[TrackClipType(typeof(HorizonGlowBClip))]
[TrackBindingType(typeof(MagicWaterController))]
[DisplayName("Custom/Skyline/Horizon Glow B Track")]
public class HorizonGlowBTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        var mixer     = ScriptPlayable<HorizonGlowBMixerBehaviour>.Create(graph, inputCount);
        var behaviour = mixer.GetBehaviour();

        behaviour.bounds = new List<HorizonGlowBMixerBehaviour.ClipBound>();
        foreach (var clip in GetClips())
        {
            var asset = clip.asset as HorizonGlowBClip;
            if (asset == null) continue;

            behaviour.bounds.Add(new HorizonGlowBMixerBehaviour.ClipBound
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
