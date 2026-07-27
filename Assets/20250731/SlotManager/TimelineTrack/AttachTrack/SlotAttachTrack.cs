using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SlotSystem.Timeline
{
    /// <summary>
    /// 挂载轨。绑定一个 SlotManager;轨上每个 clip 在其时长内把目标物体挂到指定 slot。
    /// </summary>
    [TrackClipType(typeof(SlotAttachClip))]
    [TrackBindingType(typeof(SlotManager))]
    [TrackColor(0.30f, 0.70f, 1.00f)]
    [DisplayName("Custom/SlotAttachTrack")]
    public class SlotAttachTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<SlotAttachMixer>.Create(graph, inputCount);
        }
    }
}
