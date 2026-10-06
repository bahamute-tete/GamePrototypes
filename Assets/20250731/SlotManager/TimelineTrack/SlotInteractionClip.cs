using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SlotSystem.Timeline
{
    public enum SlotInteractionMode { Transfer, Follow }
    public enum SlotInteractionEndMode { FollowTarget, RestoreOriginal }
    public enum SlotTargetKind { Slot, Transform, World }

    [Serializable]
    public sealed class SlotPoseTarget
    {
        public SlotTargetKind kind;
        public ExposedReference<SlotManager> manager;
        public string slotId;
        public ExposedReference<Transform> target;
        [Tooltip("World 模式为固定世界位置；其他模式为目标坐标系内偏移")]
        public Vector3 position;
        public Vector3 eulerAngles;

        public bool TryResolve(IExposedPropertyTable resolver, out Transform parent,
            out Vector3 localPosition, out Quaternion localRotation)
        {
            parent = null;
            localPosition = position;
            localRotation = Quaternion.Euler(eulerAngles);
            if (kind == SlotTargetKind.World) return true;
            if (kind == SlotTargetKind.Transform)
            {
                parent = target.Resolve(resolver);
                return parent != null;
            }
            var owner = manager.Resolve(resolver);
            if (owner == null) return false;
            parent = owner.GetAnchor(slotId);
            // Slot offsets and scale use the same anchor as the attachment API.
            return parent != null;
        }

        public bool TryGetPose(IExposedPropertyTable resolver, out Vector3 p, out Quaternion r)
        {
            if (!TryResolve(resolver, out var parent, out p, out r)) return false;
            if (parent != null) { p = parent.TransformPoint(p); r = parent.rotation * r; }
            return true;
        }
    }

    [Serializable]
    public sealed class SlotInteractionClip : PlayableAsset, ITimelineClipAsset
    {
        public SlotInteractionMode mode;
        public SlotPoseTarget source = new SlotPoseTarget();
        public SlotPoseTarget destination = new SlotPoseTarget();
        [Min(0)] public float arcHeight;
        public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public SlotInteractionEndMode endMode = SlotInteractionEndMode.FollowTarget;
        [Tooltip("道具自身抓握点的本地位置。该点对齐到目标，而不是模型原点。")]
        public Vector3 gripPosition;
        public Vector3 gripEulerAngles;
        public ClipCaps clipCaps => ClipCaps.None;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
            => Playable.Create(graph);
    }
}
