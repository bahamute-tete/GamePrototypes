using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SlotSystem.Timeline
{
    [Serializable]
    public class SlotHandoverClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("被移动的物体(特效 / 道具)")]
        public ExposedReference<Transform> item;

        [Header("点位")]
        public PoseTarget source = new PoseTarget();

        [Tooltip("是否经过搬运挂点(角色的手)。关掉则整条 clip 直接 源点 → 目标点 混合")]
        public bool useCarry = true;
        public PoseTarget carry = new PoseTarget();

        public PoseTarget dest = new PoseTarget();

        [Header("过渡")]
        [Tooltip("拿起时长(秒)")] public double pickupDuration = 0.4;
        [Tooltip("放下时长(秒)")] public double putdownDuration = 0.4;
        [Tooltip("过渡时的垂直抬升弧高(0 = 直线)")] public float arcHeight = 0f;
        public AnimationCurve pickupEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve putdownEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Tooltip("carry parent 时补偿父级(骨骼)缩放,避免道具被缩放")]
        public bool compensateScale = false;

        // 二值 / 不混合 / 不外插
        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<SlotHandoverBehaviour>.Create(graph);
            var b = playable.GetBehaviour();
            var resolver = graph.GetResolver();

            var itemTr = item.Resolve(resolver);
            b.item = itemTr != null ? itemTr.gameObject : null;
            b.source = source.Resolve(resolver);
            b.carry = carry.Resolve(resolver);
            b.dest = dest.Resolve(resolver);
            b.useCarry = useCarry;

            b.pickupDuration = pickupDuration;
            b.putdownDuration = putdownDuration;
            b.arcHeight = arcHeight;
            b.compensateScale = compensateScale;
            b.pickupEase = pickupEase;
            b.putdownEase = putdownEase;

            return playable;
        }
    }
}
