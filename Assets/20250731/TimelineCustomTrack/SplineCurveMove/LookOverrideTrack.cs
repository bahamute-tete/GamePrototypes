using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.ComponentModel;

/// <summary>
/// 旋转偏移叠加轨道。在 SplineCurveMoveTrack 计算出的【基础朝向】（曲线切线方向）上，
/// 叠加由 Clip 三条 AnimationCurve（yaw/pitch/roll）驱动的旋转偏移。
///
/// === 使用约定 ===
/// 1. 在 Timeline 里把此 Track 放在 SplineCurveMoveTrack 的【下方】。
///    Timeline 从上到下依次执行 mixer：
///      - 上方 SplineCurveMoveMixer 先执行，写入 transform.position + 切线朝向 rotation
///      - 本 Track 的 LookOverrideMixer 后执行，在该 rotation 上叠加偏移
///    顺序写反了不会报错，但偏移方向会变得不直观。
/// 2. 两个 Track 必须绑定到【同一个 Transform】。
/// 3. 单 Clip 即可表达"转过去 → 停留 → 转回来"全套动作（用 yaw 曲线的关键帧编排）。
///    不要为每个动作建一个 Clip 然后串接 —— 单 Clip 编辑维护成本更低。
///
/// === Phase 3：参考系（Reference Frame）支持 ===
/// 如果某个 Clip 设置了 referenceFrame 字段，则该 Clip 的 offset 解释方式被覆盖为
/// "绕 refFrame 的局部轴"，此 Track 的 applySpace 字段对那个 Clip 失效。其他未设
/// refFrame 的 Clip 仍按 applySpace 工作。两类 Clip 可在同一 Track 内混用，
/// Mixer 各自先转世界 delta 再混合，重叠区平滑过渡。
///
/// 配合 refFrame 时需要确保 refFrame Transform 的姿态在本 Track 执行前已是本帧新值，
/// 即把驱动 refFrame 的 Track（通常是载具的 SplineCurveMoveTrack）排在本 Track 上方。
/// </summary>
[TrackColor(0.85f, 0.55f, 0.2f)]
[TrackClipType(typeof(LookOverrideClip))]
[TrackBindingType(typeof(Transform))]
[DisplayName("Custom/SplineMove/Look Override Track")]
public class LookOverrideTrack : TrackAsset
{
    [Tooltip("旋转叠加空间（用于未设置 referenceFrame 的 Clip）。\n" +
             "Local（推荐）：右乘 —— 绕物体自身轴叠加，跟随基础朝向。\n" +
             "  例：基础朝东 + Yaw -90° = 朝北；基础朝北 + Yaw -90° = 朝西。\n" +
             "World：左乘 —— 绕世界轴叠加，绝对方向旋转。\n" +
             "  路径有 pitch（上下坡）时与 Local 行为不同：Local 绕\"物体头顶轴\"，World 绕世界 Y。\n" +
             "\n" +
             "注意：单个 Clip 一旦设了 referenceFrame，该 Clip 的 offset 改为绕 refFrame 局部轴，\n" +
             "此字段对那个 Clip 失效。其他未设 refFrame 的 Clip 仍按此字段解释。")]
    public LookOverrideSpace applySpace = LookOverrideSpace.Local;

    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        var mixerPlayable = ScriptPlayable<LookOverrideMixerBehaviour>.Create(graph, inputCount);
        mixerPlayable.GetBehaviour().ApplySpace = applySpace;
        return mixerPlayable;
    }
}
