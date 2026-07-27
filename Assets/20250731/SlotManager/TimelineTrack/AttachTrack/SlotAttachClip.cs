using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SlotSystem.Timeline
{
    [Serializable]
    public class SlotAttachClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("要挂载的场景物体(特效 / 道具)。clip 是资产,只能通过 ExposedReference 引用场景物体")]
        public ExposedReference<Transform> target;

        [Tooltip("目标挂点 id,需与绑定的 SlotManager 中某个 slotId 一致")]
        public string slotId;

        public AttachMode attachMode = AttachMode.Snap;

        // 挂载是二值状态,不做混合 / 循环 / 外插
        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<SlotAttachBehaviour>.Create(graph);
            var b = playable.GetBehaviour();

            var t = target.Resolve(graph.GetResolver());
            b.resolvedTarget = t != null ? t.gameObject : null;
            b.slotId = slotId;
            b.attachMode = attachMode;

            return playable;
        }
    }
}
