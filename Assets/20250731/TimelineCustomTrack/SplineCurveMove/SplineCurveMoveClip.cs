using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Collections.Generic;

/// <summary>
/// 停顿绝对位置快照。记录单个停顿在 Timeline 时间轴上的【绝对】位置和时长，
/// 以及它对应的 path 进度值（progressValue）。
///
/// 用途：用户在调整 Clip 在 Timeline 上的 start 或 duration 前，先保存当前停顿
/// 的绝对位置作为参照；之后可以一键把停顿恢复到原本的 Timeline 时刻。
/// </summary>
[System.Serializable]
public class SavedPauseSnapshot
{
    /// <summary>停顿在 Timeline 时间轴上的绝对起始时间（= 保存时 clip.start + pause.startSecLocal）</summary>
    public double absoluteStartSec;

    /// <summary>停顿的持续时长（秒，绝对值，不受 Clip 缩放影响）</summary>
    public double durationSec;

    /// <summary>停顿对应的 path 进度（曲线 value，0~1）</summary>
    public float progressValue;
}

[System.Serializable]
public class SplineCurveMoveClip : PlayableAsset, ITimelineClipAsset
{
    [SerializeField] private SplineCurveMoveBehaviour template = new SplineCurveMoveBehaviour();

    [Tooltip("路径事件列表。物体沿曲线移动跨越某个 arcLengthRatio 时触发。" +
             "目标 Transform 上需挂 SplineEventReceiver 才能接收。")]
    [SerializeField] private List<SplinePathEvent> pathEvents = new List<SplinePathEvent>();

    // -------------------------------------------------------------------- //
    // Phase 1：参考系（Reference Frame）
    // -------------------------------------------------------------------- //
    /// <summary>
    /// 参考系 Transform。决定此 Clip 中 Spline 数据的坐标解释方式：
    ///
    ///   - 未设置（None）：Spline 数据按【世界空间】解释。运行时 Mixer 直接把采样结果写到
    ///     target.position（或当 useLocalSpace=true 时写到 target.localPosition）——这是
    ///     完全的向后兼容路径，所有老 Clip 默认走这里，行为零变化。
    ///
    ///   - 指向一个 Transform（例如载具根节点）：Spline 数据按【该 Transform 的局部坐标系】
    ///     解释。运行时 Mixer 会调用 refFrame.TransformPoint(SampledPosition) 把采样结果变换
    ///     到世界空间，然后跨 Clip 加权混合，最终统一写到 target.position（世界）。
    ///
    /// 当任意激活 Clip 设置了 referenceFrame 时，Mixer 在该帧进入"世界空间混合模式"，
    /// 此时所有 Clip 的混合都在世界空间发生，useLocalSpace 标志被忽略。这是为了保证
    /// 跨参考系混合在数学上是正确的——重叠区两个 Clip 各自变换到世界再插值，避免速度跳变。
    ///
    /// ExposedReference 而不是裸 Transform 引用：因为 Clip 是工程 Asset，不能直接持有
    /// 场景中的 Transform 引用；ExposedReference 通过 PlayableDirector 的 sceneBindings 解析，
    /// 是 Timeline 引用场景对象的标准做法（与 ControlTrack、SignalReceiver 同机制）。
    /// </summary>
    [Tooltip("Spline 路径的参考系 Transform。\n" +
             "为空：路径按世界空间解释（默认，向后兼容）。\n" +
             "指向载具等运动 Transform：路径按该 Transform 局部空间解释——运行时自动变换到世界，\n" +
             "实现角色乘载具等\"运动参考系\"场景。\n" +
             "当任何活跃 Clip 设置了此字段，整帧进入世界空间混合模式，useLocalSpace 被忽略。")]
    public ExposedReference<Transform> referenceFrame;

    // === 动画速度同步（仅 Editor 使用，运行时不读取） ===
    // 这两个字段是为 SplineCurveMoveClipEditor 的"按动画速度调整 Clip 时长"功能服务的。
    // Runtime 完全忽略它们，所以即使运行时配置错了也不会影响播放。
    [Tooltip("启用 Animation Speed Sync 功能。勾选后 Inspector 会显示\"按角色行走速度调整 Clip 时长\"按钮。\n" +
             "此字段仅供 Editor 使用，运行时不读取。")]
    [SerializeField] private bool useAnimationSpeedSync = false;

    [Tooltip("角色行走的等效速度（米/秒）。从 Gait Speed Analyzer 工具测出，或动画师手填。\n" +
             "此字段仅供 Editor 使用，运行时不读取。")]
    [SerializeField] private float animationWalkSpeed = 1.0f;

    // === 停顿绝对时长基线（仅 Editor 使用，运行时不读取） ===
    // 用于"拖动 Clip 后保持停顿绝对时长"的校准功能。
    // 每次主动操作（插入/删除停顿、清除、Pause Adjustment 应用、手动校准）后，
    // Editor 会把这个值更新为当前 Clip.duration。
    // 当 Inspector 检测到 currentDuration ≠ lastBaselineDuration 时，提示用户重新校准。
    // 校准算法：ratio = lastBaselineDuration / currentDuration，把检测到的停顿
    // startSec/durationSec × ratio 恢复成原始绝对值，再 RebuildFromScratch。
    // 初始 -1 表示"未初始化"，Inspector 会在第一次刷新时填上当前 duration（不会触发校准提示）。
    [HideInInspector]
    [SerializeField] private double lastBaselineDuration = -1;

    // === 停顿绝对位置快照（仅 Editor 使用，运行时不读取） ===
    // 用于"拖动/拉伸 Clip 后恢复原停顿绝对位置"功能。
    // 用户主动点击【保存】按钮时，把当前每个停顿的 (clip.start + startSec) 存到这里。
    // 拖动/拉伸 Clip 后，用户点【恢复】，按 (absoluteStartSec - currentClipStart) 算出
    // 新的局部位置，超出 Clip 范围的部分被截断或丢弃。
    [HideInInspector]
    [SerializeField] private List<SavedPauseSnapshot> savedPauseSnapshots = new List<SavedPauseSnapshot>();

    /// <summary>保存快照时 Clip.start 的值（仅供 UI 显示，便于用户了解"快照是在什么时候拍的"）</summary>
    [HideInInspector]
    [SerializeField] private double savedSnapshotClipStartTime = -1;

    // === 端点拖拽 remap 基线（仅 Editor 使用，运行时不读取） ===
    // 记录"上一次已知的 Clip start / duration"，供 SplineCurveMoveClipTimelineEditor.OnClipChanged
    // 在用户拖动 Timeline 端点后计算位移量、判断拖的是左端还是右端。
    //
    //   - 拖【右端】（start 不变、duration 变）：把曲线【最后一个 knot】（value=1 的终点）
    //     沿绝对时间平移，其余 knot 绝对秒数钉死 → 末段变速、停顿绝对位置不变。
    //   - 拖【左端】（右边缘不变、start 变）：把【第一个 knot】（起点）平移到新左端，
    //     其余 knot 保持绝对【全局】位置不变 → 首段变速、停顿绝对位置不变。
    //
    // 曲线横轴是归一化 [0,1]，运行时按 duration 重归一化，所以"改 duration 默认会等比缩放整条曲线"。
    // OnClipChanged 用这个基线把缩放纠正回"只动被拖端"的语义。-1 表示未初始化。
    [HideInInspector][SerializeField] private double trimBaselineStart    = -1;
    [HideInInspector][SerializeField] private double trimBaselineDuration = -1;

    // === 末端拖拽模式（仅 Editor 使用，运行时不读取） ===
    // false（默认）= 方案 A「拉长」：拖右端延长时，末段被拉长 → 角色一直在动、变慢，最后到达终点。
    // true        = 方案 B「到终点停着等」：拖右端延长时，角色按原速先到达终点，再在终点【停住等待】剩余时间
    //               （末端补一段水平 hold）。压缩时两种模式一致。左端拖拽不受此开关影响。
    [Tooltip("末端拖拽模式：\n关（默认）= 拉长：末段变慢、角色一直在动到最后才到终点。\n" +
             "开 = 到终点停着等：角色按原速先到终点，再停在终点等待剩余时间。\n" +
             "仅影响拖【右端延长】时的行为；仅 Editor 使用，运行时不读取。")]
    [SerializeField] private bool holdAtEndOnTrim = false;

    // Inspector 程序化改 duration（插入/删除停顿、速度同步等）时置 true，
    // 让 OnClipChanged 跳过 remap、只重新对齐基线，避免把"插入停顿"误判成"拖右端"。
    // NonSerialized：是瞬时编辑态，不写盘、不参与 Undo。
    [System.NonSerialized] public bool SuppressTrimRemap = false;

    // Inspector 程序化改 duration 发生在哪一帧（Time.frameCount）。SuppressTrimRemap 只覆盖同步重入，
    // 而 Timeline 可能在随后 1~2 帧才补发 OnClipChanged；用这个时间戳做兜底，避免那一次被误判成拖端点。
    // int.MinValue 表示"从未发生过"。
    [System.NonSerialized] public int LastProgrammaticDurationFrame = int.MinValue;

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation | ClipCaps.SpeedMultiplier;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<SplineCurveMoveBehaviour>.Create(graph, template);
        var behaviour = playable.GetBehaviour();
        if (behaviour != null)
        {
            behaviour.PathEvents = pathEvents;

            // Phase 1：解析参考系 Transform（场景内引用）并注入到 Behaviour
            // 解析失败（用户没绑定）时返回 null，Mixer 会按"无参考系"路径处理——
            // 等价于现有行为，不会破坏老 Clip。
            behaviour.ResolvedReferenceFrame = referenceFrame.Resolve(graph.GetResolver());
        }
        return playable;
    }

    public SplineCurveMoveBehaviour Template => template;
    public List<SplinePathEvent> PathEvents => pathEvents;

    public bool UseAnimationSpeedSync
    {
        get => useAnimationSpeedSync;
        set => useAnimationSpeedSync = value;
    }
    public float AnimationWalkSpeed
    {
        get => animationWalkSpeed;
        set => animationWalkSpeed = Mathf.Max(0.001f, value);
    }

    /// <summary>
    /// 停顿绝对时长基线参考。仅 Editor 用，记录上一次主动操作时的 Clip.duration。
    /// 用 -1 表示"未初始化"——此时 Inspector 不应触发校准提示。
    /// </summary>
    public double LastBaselineDuration
    {
        get => lastBaselineDuration;
        set => lastBaselineDuration = value;
    }

    /// <summary>停顿绝对位置快照列表。可读可写。</summary>
    public List<SavedPauseSnapshot> SavedPauseSnapshots
    {
        get => savedPauseSnapshots;
        set => savedPauseSnapshots = value ?? new List<SavedPauseSnapshot>();
    }

    /// <summary>保存快照时 Clip.start 的值（用于 UI 显示）。</summary>
    public double SavedSnapshotClipStartTime
    {
        get => savedSnapshotClipStartTime;
        set => savedSnapshotClipStartTime = value;
    }

    /// <summary>端点拖拽 remap 基线：上一次已知的 Clip.start。</summary>
    public double TrimBaselineStart
    {
        get => trimBaselineStart;
        set => trimBaselineStart = value;
    }

    /// <summary>端点拖拽 remap 基线：上一次已知的 Clip.duration。-1 表示未初始化。</summary>
    public double TrimBaselineDuration
    {
        get => trimBaselineDuration;
        set => trimBaselineDuration = value;
    }

    /// <summary>基线是否已初始化（duration ≥ 0）。未初始化时 OnClipChanged 不做 remap，只记录当前值。</summary>
    public bool TrimBaselineInitialized => trimBaselineDuration >= 0;

    /// <summary>
    /// 末端拖拽模式。false = 拉长变速（默认）；true = 到终点停着等（末端补 hold）。仅 Editor 使用。
    /// </summary>
    public bool HoldAtEndOnTrim
    {
        get => holdAtEndOnTrim;
        set => holdAtEndOnTrim = value;
    }

    /// <summary>
    /// 重新对齐端点拖拽基线。任何"主动"改变 Clip start/duration 的操作（Inspector 插入/删除停顿、
    /// 速度同步，或 OnClipChanged 自身完成 remap 后）都应调用，使下一次拖拽能算出正确的位移量。
    /// </summary>
    public void SyncTrimBaseline(double start, double duration)
    {
        trimBaselineStart    = start;
        trimBaselineDuration = duration;
    }
}
