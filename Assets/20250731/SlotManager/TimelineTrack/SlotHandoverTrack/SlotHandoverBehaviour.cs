using System;
using UnityEngine;
using UnityEngine.Playables;

namespace SlotSystem.Timeline
{
    [Serializable]
    public class SlotHandoverBehaviour : PlayableBehaviour
    {
        // 运行时解析,不序列化
        [NonSerialized] public GameObject item;
        [NonSerialized] public ResolvedPoseTarget source;
        [NonSerialized] public ResolvedPoseTarget carry;
        [NonSerialized] public ResolvedPoseTarget dest;
        [NonSerialized] public bool useCarry;

        // 由 clip 拷入的参数
        public double pickupDuration = 0.4;
        public double putdownDuration = 0.4;
        public float arcHeight = 0f;
        public bool compensateScale = false;
        public AnimationCurve pickupEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve putdownEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    }
}
