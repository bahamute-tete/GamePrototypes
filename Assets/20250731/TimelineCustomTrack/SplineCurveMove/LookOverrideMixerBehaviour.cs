using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 旋转偏移轨道的混合器。在【上游 Track】（如 SplineCurveMoveTrack）已写入 transform.rotation 的基础上，
/// 把所有激活 LookOverrideClip 的 SampledOffset 按权重 slerp 后叠加上去。
///
/// === Phase 3 重构 ===
/// 引入 ResolvedReferenceFrame 后，不同 Clip 可能在不同参考系下表达 offset。为正确处理混合，
/// Mixer 改为"每个 input 先各自转为世界空间 delta rotation → 再 slerp 混合 → 最后左乘到 target"：
///
///   for each input:
///     worldDelta_i = ToWorldDelta(offset_i, refFrame_i, applySpace, target.rotation)
///   blendedWorldDelta = slerp(worldDelta_0, worldDelta_1, ..., weights)
///   if (totalWeight < 1) blendedWorldDelta = slerp(identity, blendedWorldDelta, totalWeight)
///   target.rotation = blendedWorldDelta * target.rotation
///
/// 各模式的 ToWorldDelta 公式：
///   - refFrame 模式（refFrame != null）：refFrame.rotation × offset × refFrame.rotation⁻¹
///       绕 refFrame 局部轴的旋转，通过共轭变换到世界空间
///   - Local 模式（refFrame=null, applySpace=Local）：target.rotation × offset × target.rotation⁻¹
///       绕 target 当前自身轴的旋转，共轭到世界
///   - World 模式（refFrame=null, applySpace=World）：offset
///       已经在世界空间，无需变换
///
/// 数学等价性验证（向后兼容保证）：
///   Local 纯模式：blendedWorld = T × blendedLocal × T⁻¹  （slerp 与共轭可交换）
///                 target.rotation_new = blendedWorld × T = T × blendedLocal
///                 等同原右乘代码 target.rotation × blendedOffset ✓
///   World 纯模式：blendedWorld = blendedOffset，直接左乘 ✓
///   纯 identity blend（totalWeight<1）：slerp(I, X, t) 与共轭可交换 ✓
///
/// === 使用约定 ===
/// 滑块在所有 Clip 之外（totalWeight == 0）时直接 return —— 不写 rotation，让上游 Track 的
/// 基础朝向直接生效。这是与 SplineCurveMoveMixerBehaviour 行为不同的地方（Move Mixer 必须始终
/// 把物体放在曲线上，所以有 SnapToBoundary；Look 是叠加层，"不施加" = identity = 透传上游）。
///
/// === Track Evaluate 顺序提醒 ===
/// 若任一 Clip 使用 refFrame=载具，载具的 SplineCurveMoveTrack 必须排在本 Track 上方，
/// 保证读 refFrame.rotation 时已是本帧新值。
/// </summary>
public class LookOverrideMixerBehaviour : PlayableBehaviour
{
    public LookOverrideSpace ApplySpace { get; set; } = LookOverrideSpace.Local;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var target = playerData as Transform;
        if (target == null) return;

        int inputCount = playable.GetInputCount();
        if (inputCount == 0) return;

        // 读一次 target 当前世界旋转（即上游 Track 已写入的值），后续 Local 模式共轭和最终左乘都用这个值。
        // 在 Mixer 内部不会修改 target，所以一次读取即可保证一致性。
        Quaternion targetRot = target.rotation;
        Quaternion targetRotInv = Quaternion.Inverse(targetRot);

        float totalWeight = 0f;
        Quaternion blendedWorldDelta = Quaternion.identity;
        bool firstApplied = false;

        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0f) continue;

            var inputPlayable = (ScriptPlayable<LookOverrideBehaviour>)playable.GetInput(i);
            var b = inputPlayable.GetBehaviour();
            if (b == null || !b.HasValidSample) continue;

            Quaternion offset = b.SampledOffset;
            Quaternion worldDelta = ToWorldDelta(offset, b.ResolvedReferenceFrame, targetRot, targetRotInv);

            if (!firstApplied)
            {
                blendedWorldDelta = worldDelta;
                firstApplied = true;
            }
            else
            {
                float t = weight / (totalWeight + weight);
                blendedWorldDelta = Quaternion.Slerp(blendedWorldDelta, worldDelta, t);
            }

            totalWeight += weight;
        }

        // 滑块在所有 Clip 之外：不施加偏移，让上游 Track 的基础朝向直接生效。
        if (totalWeight <= 0f) return;

        // 部分混合：从 identity 向 blendedWorldDelta slerp，模拟偏移强度的渐入渐出。
        // identity 在任意 frame 下都是 identity，无需特殊处理。
        if (totalWeight < 1f)
        {
            blendedWorldDelta = Quaternion.Slerp(Quaternion.identity, blendedWorldDelta, totalWeight);
        }

        // 统一左乘：世界空间 delta × target.rotation。
        // 对纯 Local 模式而言数学上等价于原右乘代码（已在上方注释中证明）。
        target.rotation = blendedWorldDelta * targetRot;
    }

    /// <summary>
    /// 把 Clip 输出的 offset 转换为"世界空间 delta rotation"。
    /// 三种模式：
    ///   - refFrame != null：绕 refFrame 局部轴，共轭变换到世界
    ///   - applySpace=Local：绕 target 当前自身轴，共轭变换到世界（targetRot × o × targetRot⁻¹）
    ///   - applySpace=World：offset 已在世界，直接返回
    /// </summary>
    private Quaternion ToWorldDelta(
        Quaternion offset,
        Transform refFrame,
        Quaternion targetRot,
        Quaternion targetRotInv)
    {
        if (refFrame != null)
        {
            Quaternion rfRot = refFrame.rotation;
            return rfRot * offset * Quaternion.Inverse(rfRot);
        }

        if (ApplySpace == LookOverrideSpace.Local)
        {
            return targetRot * offset * targetRotInv;
        }

        // World
        return offset;
    }
}
