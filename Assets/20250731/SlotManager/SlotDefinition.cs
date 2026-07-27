using System;
using UnityEngine;

namespace SlotSystem
{
    /// <summary>挂点绑定方式:Humanoid 用标准骨骼枚举,Generic 用相对路径。</summary>
    public enum SlotBindMode
    {
        HumanoidBone, // 仅 Humanoid avatar 有效,走 Animator.GetBoneTransform
        BonePath,     // 任意 rig,走相对 skeletonRoot 的层级路径
    }

    /// <summary>占用策略。</summary>
    public enum SlotOccupancy
    {
        Single, // 单占:Attach 时先清掉已有挂载物(手持武器、道具)
        Multi,  // 多占:可叠加多个挂载物(特效挂点常用)
    }

    /// <summary>挂载姿态处理。</summary>
    public enum AttachMode
    {
        Snap,          // 归零本地位移/旋转,贴合挂点
        PreserveWorld, // 保持世界位姿,仅换父级
    }

    /// <summary>
    /// 单个挂点定义。纯数据,不是 MonoBehaviour。
    /// boneTransform 为已解析的直接引用(主路径,验证期可直接拖入);
    /// bindMode + 枚举/路径为可重绑的元数据(回退,模型重导入后用来 rebind)。
    /// </summary>
    [Serializable]
    public class SlotDefinition
    {
        [Tooltip("唯一标识,用于 Attach/Detach/查询")]
        public string slotId;

        [Header("骨骼绑定")]
        [Tooltip("已解析的目标骨骼。可直接拖入做快速验证;为空时按下方元数据重绑")]
        public Transform boneTransform;

        public SlotBindMode bindMode = SlotBindMode.BonePath;

        [Tooltip("bindMode = HumanoidBone 时使用")]
        public HumanBodyBones humanoidBone = HumanBodyBones.Hips;

        [Tooltip("bindMode = BonePath 时使用,相对 skeletonRoot 的层级路径,如 'Hips/Spine/Head'")]
        public string bonePath;

        [Header("相对骨骼的偏移")]
        public Vector3 localPosition = Vector3.zero;
        public Vector3 localEulerAngles = Vector3.zero;
        public Vector3 localScale = Vector3.one;

        [Header("挂载策略")]
        public SlotOccupancy occupancy = SlotOccupancy.Single;

        // 运行时缓存,不序列化(重建时回填)
        [NonSerialized] public Transform anchor;
    }
}
