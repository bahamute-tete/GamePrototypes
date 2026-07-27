// LiangZhu - 时间回溯日历 / Timeline TrackAsset
// 绑定到场景里的 TimeRollDriver,承载 TimeRollClip。
// 接上轨道后记得把 Driver 的 _selfDrive 关掉,交给本轨驱动。

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.ComponentModel;

namespace LiangZhu.TimeRoll
{
    [TrackColor(0.35f, 0.75f, 0.95f)]
    [TrackClipType(typeof(TimeRollClip))]
    [TrackBindingType(typeof(TimeRollDriver))]
    [DisplayName("Custom/TimeRoll/TimeRollTrack")]
    public class TimeRollTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<TimeRollMixerBehaviour>.Create(graph, inputCount);
        }
    }
}
