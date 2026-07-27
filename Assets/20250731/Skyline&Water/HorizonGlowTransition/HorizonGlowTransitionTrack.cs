using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// ====================================================================
//   Horizon Glow Transition Track
//   绑定 MagicWaterController；clip 类型为 HorizonGlowTransitionClip
//   驱动 controller.horizonBlend
// ====================================================================

[TrackColor(1.0f, 0.6f, 0.3f)]
[TrackClipType(typeof(HorizonGlowTransitionClip))]
[TrackBindingType(typeof(MagicWaterController))]
[DisplayName("Custom/Skyline/Horizon Glow Transition Track")]
public class HorizonGlowTransitionTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        var mixer    = ScriptPlayable<HorizonGlowTransitionMixerBehaviour>.Create(graph, inputCount);
        var behaviour = mixer.GetBehaviour();

        // 收集所有 clip 的边界信息
        behaviour.bounds = new List<HorizonGlowTransitionMixerBehaviour.ClipBound>();
        foreach (var clip in GetClips())
        {
            var asset = clip.asset as HorizonGlowTransitionClip;
            if (asset == null) continue;

            behaviour.bounds.Add(new HorizonGlowTransitionMixerBehaviour.ClipBound
            {
                startTime  = clip.start,
                endTime    = clip.end,
                startBlend = asset.startBlend,
                endBlend   = asset.endBlend
            });
        }

        behaviour.bounds.Sort((a, b) => a.startTime.CompareTo(b.startTime));

        return mixer;
    }
}
