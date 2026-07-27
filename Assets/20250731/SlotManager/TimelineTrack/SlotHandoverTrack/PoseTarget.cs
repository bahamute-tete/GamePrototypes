using System;
using UnityEngine;

namespace SlotSystem.Timeline
{
    public enum PoseTargetKind { Slot, Transform }

    /// <summary>
    /// clip 上的"点位"引用。可指向某个 SlotManager 的挂点,或一个普通 Transform。
    /// clip 是资产,只能通过 ExposedReference 引场景对象;CreatePlayable 时解析成 ResolvedPoseTarget。
    /// </summary>
    [Serializable]
    public class PoseTarget
    {
        public PoseTargetKind kind = PoseTargetKind.Slot;

        [Tooltip("kind = Slot 时:目标挂点所属的 SlotManager")]
        public ExposedReference<SlotManager> slotManager;

        [Tooltip("kind = Slot 时:挂点 id")]
        public string slotId;

        [Tooltip("kind = Transform 时:目标 Transform")]
        public ExposedReference<Transform> transform;

        public ResolvedPoseTarget Resolve(IExposedPropertyTable resolver)
        {
            if (kind == PoseTargetKind.Slot)
                return ResolvedPoseTarget.FromSlot(slotManager.Resolve(resolver), slotId);
            return ResolvedPoseTarget.FromTransform(transform.Resolve(resolver));
        }
    }
}
