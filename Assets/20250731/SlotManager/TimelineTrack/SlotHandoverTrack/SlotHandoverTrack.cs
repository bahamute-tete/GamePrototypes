using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.ComponentModel;

namespace SlotSystem.Timeline
{
    /// <summary>
    /// Handover 轨。不设 TrackBindingType——一次搬运可能涉及多个角色/物体,
    /// 每个 clip 通过 ExposedReference 自带 源 / 搬运 / 目标 三个点位 + 被移动物体。
    /// </summary>
    [TrackClipType(typeof(SlotHandoverClip))]
    [TrackColor(0.95f, 0.60f, 0.20f)]
    [DisplayName("Custom/Slot Handover Track")]
    public class SlotHandoverTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<SlotHandoverMixer>.Create(graph, inputCount);
        }
    }
}
