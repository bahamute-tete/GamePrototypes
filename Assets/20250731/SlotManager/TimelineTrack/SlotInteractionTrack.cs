using System.Linq;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SlotSystem.Timeline
{
    [TrackClipType(typeof(SlotInteractionClip))]
    [TrackBindingType(typeof(Transform))]
    [TrackColor(0.95f, 0.60f, 0.20f)]
    [DisplayName("Custom/Slot Interaction Track")]
    public sealed class SlotInteractionTrack : TrackAsset
    {
        [Tooltip("保持道具建图时的世界尺寸。非均匀缩放配合旋转可能产生剪切，建议骨骼使用均匀缩放。")]
        public bool preserveWorldScale = true;
        [Tooltip("仅运行态停止时保留当前结果；编辑器退出预览始终恢复。重新播放会以保留后的状态为初始状态。")]
        public bool keepResultOnStop;

        public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
        {
            var item = director.GetGenericBinding(this) as Transform;
            if (item != null)
            {
                foreach (var property in new[] { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
                    "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w",
                    "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" })
                    driver.AddFromName<Transform>(item.gameObject, property);
            }
            base.GatherProperties(director, driver);
        }

        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            var playable = ScriptPlayable<SlotInteractionMixer>.Create(graph, inputCount);
            var director = go.GetComponent<PlayableDirector>();
            var item = director != null ? director.GetGenericBinding(this) as Transform : null;
            playable.GetBehaviour().Configure(graph, director, item, GetClips().OrderBy(c => c.start).ToArray(), this);
            return playable;
        }
    }
}
