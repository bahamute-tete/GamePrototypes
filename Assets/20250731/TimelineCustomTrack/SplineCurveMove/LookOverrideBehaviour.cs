using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 旋转偏移叠加空间。
/// Local：右乘 —— 绕物体自身轴叠加，跟随基础朝向（例如基础朝东 + Yaw -90° = 朝北）。
/// World：左乘 —— 绕世界轴叠加，绝对方向旋转（路径有 pitch 时与 Local 行为不同）。
///
/// Phase 3 起：当 Clip 设置了 ResolvedReferenceFrame，此枚举会被覆盖——offset 改为
/// "绕 refFrame 的局部轴"。即便 Track 选了 Local 或 World，单个 Clip 设了 refFrame 后
/// 该 Clip 的 offset 都以 refFrame 为基准。
/// </summary>
public enum LookOverrideSpace
{
    Local,
    World,
}

[System.Serializable]
public class LookOverrideBehaviour : PlayableBehaviour
{
    [Tooltip("X 轴：Clip 内归一化时间 [0,1]；Y 轴：相对基础朝向的 yaw 偏移（度）。\n" +
             "Unity 左手系，绕 Y 轴正值 = 顺时针 = 右转。\"左转 90°\" 用 -90。")]
    [SerializeField] private AnimationCurve yawCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);

    [Tooltip("X 轴：Clip 内归一化时间 [0,1]；Y 轴：pitch 偏移（度，正值低头）。")]
    [SerializeField] private AnimationCurve pitchCurve = AnimationCurve.Constant(0f, 1f, 0f);

    [Tooltip("X 轴：Clip 内归一化时间 [0,1]；Y 轴：roll 偏移（度，正值右倾）。")]
    [SerializeField] private AnimationCurve rollCurve = AnimationCurve.Constant(0f, 1f, 0f);

    public AnimationCurve YawCurve => yawCurve;
    public AnimationCurve PitchCurve => pitchCurve;
    public AnimationCurve RollCurve => rollCurve;

    // 采样结果 —— 由 Mixer 读取
    public bool HasValidSample { get; private set; }
    public Quaternion SampledOffset { get; private set; }

    // -------------------------------------------------------------------- //
    // Phase 3：参考系（Reference Frame）支持
    // -------------------------------------------------------------------- //
    /// <summary>
    /// 运行时由 LookOverrideClip.CreatePlayable 注入的参考系 Transform。
    ///
    /// 语义：
    ///   - null（默认）：Offset 解释方式由 LookOverrideTrack.applySpace 决定（Local 右乘 / World 左乘）。
    ///     完全保留现有行为。
    ///   - 非 null：Offset 被解释为"绕 refFrame 局部轴的旋转"，applySpace 被忽略。
    ///     Mixer 通过共轭变换 refFrame.rotation × offset × refFrame.rotation⁻¹ 把 offset 表达到
    ///     世界空间后左乘到 target.rotation 上。
    ///
    /// 典型用途：角色在倾斜/摇晃的载具上需要保持某种姿态时，offset 应绕载具的轴而非角色当前
    /// 的（已被载具姿态污染的）轴。例如船下倾时角色仰头，refFrame=ship + pitch=-30° 即可，
    /// 无论 ship 当前姿态如何，offset 始终是"绕船自己的水平 X 轴 30°"。
    ///
    /// 非序列化字段：由 Clip 通过 ExposedReference 在每次 Playable 创建时解析后赋值。
    /// </summary>
    public Transform ResolvedReferenceFrame { get; set; }

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        base.OnBehaviourPlay(playable, info);
        HasValidSample = false;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        double time = playable.GetTime();
        double duration = playable.GetDuration();

        // 边界 snap：与 SplineCurveMoveBehaviour 保持一致，避免端点浮点不精确
        const double BOUNDARY_EPSILON = 1e-4;
        float normalized;
        if (duration <= 1e-6) normalized = 0f;
        else if (time <= BOUNDARY_EPSILON) normalized = 0f;
        else if (time >= duration - BOUNDARY_EPSILON) normalized = 1f;
        else normalized = (float)(time / duration);

        float yaw   = yawCurve   != null ? yawCurve.Evaluate(normalized)   : 0f;
        float pitch = pitchCurve != null ? pitchCurve.Evaluate(normalized) : 0f;
        float roll  = rollCurve  != null ? rollCurve.Evaluate(normalized)  : 0f;

        // Unity 欧拉顺序 ZXY，参数顺序 (pitch=X, yaw=Y, roll=Z)
        // 此 offset 在哪个坐标系下解释，由 Mixer 根据 ResolvedReferenceFrame / ApplySpace 决定。
        SampledOffset = Quaternion.Euler(pitch, yaw, roll);
        HasValidSample = true;
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        HasValidSample = false;
    }
}
