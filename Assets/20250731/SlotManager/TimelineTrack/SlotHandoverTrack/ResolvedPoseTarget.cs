using UnityEngine;

namespace SlotSystem.Timeline
{
    /// <summary>PoseTarget 解析后的运行时形态:要么 SlotManager + slotId,要么一个 Transform。</summary>
    public struct ResolvedPoseTarget
    {
        public bool isSlot;
        public SlotManager manager;
        public string slotId;
        public Transform tr;

        public static ResolvedPoseTarget FromSlot(SlotManager m, string id)
            => new ResolvedPoseTarget { isSlot = true, manager = m, slotId = id };

        public static ResolvedPoseTarget FromTransform(Transform t)
            => new ResolvedPoseTarget { isSlot = false, tr = t };

        public bool IsValid => isSlot ? manager != null : tr != null;

        /// <summary>取世界位姿(用于过渡混合;slot 走 TryGetSlotPose,不依赖锚点)。</summary>
        public bool TryGetPose(out Vector3 pos, out Quaternion rot)
        {
            if (isSlot)
            {
                if (manager != null && manager.TryGetSlotPose(slotId, out pos, out rot)) return true;
            }
            else if (tr != null)
            {
                pos = tr.position;
                rot = tr.rotation;
                return true;
            }
            pos = Vector3.zero;
            rot = Quaternion.identity;
            return false;
        }

        /// <summary>
        /// 取用于 parent 的绑定:父级 Transform + 本地偏移。
        /// slot → 骨骼 + slot 偏移(不依赖锚点物体);transform → 该 Transform + 零偏移。
        /// </summary>
        public bool TryGetParentBinding(out Transform parent, out Vector3 localPos, out Quaternion localRot)
        {
            if (isSlot)
            {
                if (manager != null && manager.TryGetSlotBone(slotId, out parent, out localPos, out localRot))
                    return true;
            }
            else if (tr != null)
            {
                parent = tr;
                localPos = Vector3.zero;
                localRot = Quaternion.identity;
                return true;
            }
            parent = null;
            localPos = Vector3.zero;
            localRot = Quaternion.identity;
            return false;
        }
    }
}
