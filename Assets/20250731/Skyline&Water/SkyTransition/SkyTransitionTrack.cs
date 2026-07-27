using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// ====================================================================
//   Sky Transition Track
//   绑定 MagicWaterController；clip 类型为 SkyTransitionClip
//   驱动 controller.skyBlend
//
//   在 CreateTrackMixer 阶段把所有 clip 的 [start, end] 和 [startBlend, endBlend]
//   收集起来传给 Mixer，让 Mixer 在 clip 时间范围之外也能 hold 住对应的边界值。
// ====================================================================

[TrackColor(0.4f, 0.6f, 1.0f)]
[TrackClipType(typeof(SkyTransitionClip))]
[TrackBindingType(typeof(MagicWaterController))]
[DisplayName("Custom/Skyline/Sky Transition Track")]
public class SkyTransitionTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        var mixer    = ScriptPlayable<SkyTransitionMixerBehaviour>.Create(graph, inputCount);
        var behaviour = mixer.GetBehaviour();

        // 收集所有 clip 的边界信息，让 Mixer 在 clip 范围之外也能输出正确的 hold 值
        behaviour.bounds = new List<SkyTransitionMixerBehaviour.ClipBound>();
        foreach (var clip in GetClips())
        {
            var asset = clip.asset as SkyTransitionClip;
            if (asset == null) continue;

            behaviour.bounds.Add(new SkyTransitionMixerBehaviour.ClipBound
            {
                startTime  = clip.start,
                endTime    = clip.end,
                startBlend = asset.startBlend,
                endBlend   = asset.endBlend
            });
        }

        // 按时间排序方便后面查找
        behaviour.bounds.Sort((a, b) => a.startTime.CompareTo(b.startTime));

        return mixer;
    }
}
