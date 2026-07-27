using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class LookOverrideClip : PlayableAsset, ITimelineClipAsset
{
    [SerializeField] private LookOverrideBehaviour template = new LookOverrideBehaviour();

    // -------------------------------------------------------------------- //
    // Phase 3：参考系（Reference Frame）
    // -------------------------------------------------------------------- //
    /// <summary>
    /// Look offset 的参考系 Transform。决定 yaw/pitch/roll offset 的轴解释方式：
    ///
    ///   - 未设置（None）：Offset 按 Track 的 applySpace 解释——
    ///       applySpace=Local 时绕角色当前自身轴（右乘到 target.rotation）。
    ///       applySpace=World 时绕世界轴（左乘到 target.rotation）。
    ///     完全向后兼容，所有老 Clip 默认走这里。
    ///
    ///   - 指向一个 Transform（例如载具根节点）：Offset 改为"绕 refFrame 的局部轴"。
    ///     Mixer 通过 refFrame.rotation × offset × refFrame.rotation⁻¹ 把 offset 共轭
    ///     变换到世界空间，再左乘到 target.rotation 上。
    ///     此模式下 Track 的 applySpace 被忽略。
    ///
    /// 典型用途：角色在倾斜或摇晃的载具上需要施加姿态补偿。例如船下倾 30°，希望角色仰头
    /// 保持水平视线——设 refFrame=ship + pitchCurve=-30°，offset 始终绕"船自身的水平 X 轴"
    /// 而不是绕"已倾斜 30° 的角色头顶轴"，行为更直觉。
    ///
    /// ExposedReference 而不是裸 Transform：Clip 是工程 Asset，无法持有场景内 Transform 引用，
    /// 必须通过 PlayableDirector 的 sceneBindings 解析（与 SplineCurveMoveClip.referenceFrame 同机制）。
    /// </summary>
    [Tooltip("Look offset 的参考系 Transform。\n" +
             "为空：offset 按 Track 的 applySpace 解释（默认，向后兼容）。\n" +
             "指向载具等 Transform：offset 改为绕 refFrame 局部轴解释，applySpace 被忽略。\n" +
             "适用场景：载具倾斜/摇晃时角色需要施加姿态补偿，且补偿语义应锁定在载具的轴上而非\n" +
             "角色当前（已被载具污染的）轴上。")]
    public ExposedReference<Transform> referenceFrame;

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation | ClipCaps.SpeedMultiplier;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<LookOverrideBehaviour>.Create(graph, template);
        var behaviour = playable.GetBehaviour();
        if (behaviour != null)
        {
            // Phase 3：解析参考系 Transform 并注入到 Behaviour。
            // 用户未绑定时 Resolve 返回 null，Mixer 自动走 applySpace 路径——完全向后兼容。
            behaviour.ResolvedReferenceFrame = referenceFrame.Resolve(graph.GetResolver());
        }
        return playable;
    }

    public LookOverrideBehaviour Template => template;
}
