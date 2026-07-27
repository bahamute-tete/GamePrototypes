using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// ====================================================================
//   Horizon Glow A Track
//   绑定 MagicWaterController；clip 类型为 HorizonGlowAClip
//   驱动 controller 的 A 组参数：horizonColorA / horizonIntensityA / etc.
// ====================================================================

[TrackColor(0.9f, 0.55f, 0.2f)]
[TrackClipType(typeof(HorizonGlowAClip))]
[TrackBindingType(typeof(MagicWaterController))]
[DisplayName("Custom/Skyline/Horizon Glow A Track")]
public class HorizonGlowATrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        var mixer     = ScriptPlayable<HorizonGlowAMixerBehaviour>.Create(graph, inputCount);
        var behaviour = mixer.GetBehaviour();

        behaviour.bounds = new List<HorizonGlowAMixerBehaviour.ClipBound>();
        foreach (var clip in GetClips())
        {
            var asset = clip.asset as HorizonGlowAClip;
            if (asset == null) continue;

            behaviour.bounds.Add(new HorizonGlowAMixerBehaviour.ClipBound
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
