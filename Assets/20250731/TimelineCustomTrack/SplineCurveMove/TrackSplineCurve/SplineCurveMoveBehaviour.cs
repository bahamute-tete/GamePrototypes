using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Collections.Generic;

/// <summary>
/// 旋转轴允许位掩码。用于 SplineCurveMoveBehaviour 的 Axis Lock 功能。
/// </summary>
[System.Flags]
public enum AxisMask
{
    None = 0,
    X = 1 << 0,
    Y = 1 << 1,
    Z = 1 << 2,
    All = X | Y | Z,
}

[System.Serializable]
public class SplineCurveMoveBehaviour : PlayableBehaviour, ISerializationCallbackReceiver
{
    internal TimelineClip TimelineClip { get; set; }
    [SerializeField] private CatmullRomSpline spline = new CatmullRomSpline();
    [Tooltip("开启：路径数据相对于轨道绑定目标的父级；关闭：世界空间。\n" +
             "不是相对于 NewPath 或控制点父级。目标父级与世界坐标重合时，开关效果相同。\n" +
             "Clip 设置 Reference Frame 时优先使用该参考系，本开关不生效。")]
    [SerializeField] private bool useLocalSpace = false;

    [Tooltip("旋转计算模式。\n" +
             "Tangent: 仅按切线方向。\n" +
             "TangentWithRoll: PTF 标架 + 控制点 roll 插值（推荐），朝向仍跟随切线。")]
    [SerializeField] private CatmullRomSpline.RotationMode rotationMode = CatmullRomSpline.RotationMode.TangentWithRoll;

    [SerializeField] private bool applyRotation = true;

    [Tooltip("允许旋转的轴（位掩码）。默认 All = 不锁定（物体完全朝向曲线方向）。\n" +
             "仅勾选 Y：在路径参考系内保持水平；参考系自身倾斜仍会带动整体朝向。\n" +
             "其他组合按欧拉拆分应用（forward 接近垂直时可能受万向锁影响）。")]
    [SerializeField] private AxisMask allowedRotationAxes = AxisMask.All;

    [Tooltip("位移曲线，输入 = Timeline 归一化时间，输出 = 弧长归一化进度 ∈ [0,1]。")]
    [SerializeField] private AnimationCurve displacementCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    public CatmullRomSpline Spline => spline;
    public bool UseLocalSpace { get => useLocalSpace; set => useLocalSpace = value; }
    public CatmullRomSpline.RotationMode RotationMode
    {
        get => CatmullRomSpline.NormalizeRotationMode(rotationMode);
        set => rotationMode = CatmullRomSpline.NormalizeRotationMode(value);
    }

    public void OnBeforeSerialize() => rotationMode = CatmullRomSpline.NormalizeRotationMode(rotationMode);
    public void OnAfterDeserialize() => rotationMode = CatmullRomSpline.NormalizeRotationMode(rotationMode);
    public bool ApplyRotation { get => applyRotation; set => applyRotation = value; }
    public AxisMask AllowedRotationAxes { get => allowedRotationAxes; set => allowedRotationAxes = value; }
    public AnimationCurve DisplacementCurve => displacementCurve;
    public Vector3 RotationOffset { get => rotationOffset; set => rotationOffset = value; }

    public bool AlignToPath
    {
        get => applyRotation;
        set => applyRotation = value;
    }

    // 采样结果 —— 由 Mixer 读取
    public bool HasValidSample { get; private set; }
    public Vector3 SampledPosition { get; private set; }
    public Quaternion SampledRotation { get; private set; }

    // 路径事件 —— 由 Clip.CreatePlayable 注入
    public List<SplinePathEvent> PathEvents { get; set; }

    // -------------------------------------------------------------------- //
    // Phase 1：参考系（Reference Frame）支持
    // -------------------------------------------------------------------- //
    /// <summary>
    /// 运行时由 SplineCurveMoveClip.CreatePlayable 注入的参考系 Transform。
    ///
    /// 语义：
    ///   - null（默认）：Spline 数据按"世界空间"或"target.parent 局部空间"解释（取决于 UseLocalSpace 旧字段）。
    ///   - 非 null：Spline 数据按此 Transform 的局部坐标系解释。Mixer 在混合前会调用
    ///     refFrame.TransformPoint(SampledPosition) 把每个 sample 变换到世界空间，
    ///     然后跨 Clip 加权混合，最终写入 target.position（世界）。
    ///
    /// 用途：用于角色乘载具等"运动参考系"场景——Spline 路径在载具局部空间内描述（比如
    /// 甲板上的步行路线），载具的世界 Transform 由独立 Track 驱动，两者复合得到角色世界位置。
    ///
    /// 非序列化字段：由 Clip 在每次 Playable 创建时通过 ExposedReference 解析后赋值。
    /// 编辑器侧可视化（Scene View Gizmo）需要直接访问 Clip 上的 ExposedReference 而不是此运行时值。
    /// </summary>
    public Transform ResolvedReferenceFrame { get; set; }

    // 当前帧检测到的事件 —— 由 Mixer 读取并派发
    private readonly List<SplinePathEvent> _triggeredThisFrame = new List<SplinePathEvent>();
    public IReadOnlyList<SplinePathEvent> TriggeredEventsThisFrame => _triggeredThisFrame;

    private float _lastSampledArcS;
    private bool _hasLastS;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        base.OnBehaviourPlay(playable, info);
        HasValidSample = false;
        _hasLastS = false;
        _triggeredThisFrame.Clear();
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (spline.ControlPoints.Count < 2)
        {
            HasValidSample = false;
            _triggeredThisFrame.Clear();
            return;
        }

        float normalized = SplineClipTime.Normalize(playable, TimelineClip);

        float s = displacementCurve.Evaluate(normalized);
        float t = spline.ArcLengthToT(s);

        SampledPosition = spline.GetPoint(t);

        Quaternion rot = applyRotation
            ? spline.GetRotation(t, rotationMode, rotationOffset)
            : Quaternion.identity;

        // Axis Lock：在路径朝向和旋转偏移计算后应用。
        if (applyRotation && allowedRotationAxes != AxisMask.All)
        {
            rot = ApplyAxisLock(rot, allowedRotationAxes);
        }

        SampledRotation = rot;
        HasValidSample = true;

        // 路径事件检测
        _triggeredThisFrame.Clear();
        if (PathEvents != null && PathEvents.Count > 0)
        {
            if (_hasLastS)
            {
                float lo = Mathf.Min(_lastSampledArcS, s);
                float hi = Mathf.Max(_lastSampledArcS, s);
                // 防大跳：如果 s 跨度 > 0.5，认为是 Timeline 跳转，不触发
                if (hi - lo < 0.5f)
                {
                    for (int i = 0; i < PathEvents.Count; i++)
                    {
                        var ev = PathEvents[i];
                        if (ev == null) continue;
                        // 用半开区间 (lo, hi]，避免边界重复触发
                        if (ev.arcLengthRatio > lo && ev.arcLengthRatio <= hi)
                            _triggeredThisFrame.Add(ev);
                    }
                }
            }
            _lastSampledArcS = s;
            _hasLastS = true;
        }
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        HasValidSample = false;
        _triggeredThisFrame.Clear();
    }

    /// <summary>
    /// 主动在指定 normalized 时间位置采样，结果存入 SampledPosition / SampledRotation。
    /// 由 Mixer 在边界 snap（Timeline 滑块完全位于 Clip 之外时）调用，
    /// 保证物体处于精确的曲线起点或终点。
    ///
    /// 不参与路径事件检测（_lastSampledArcS 不更新）。
    /// </summary>
    public void SampleAtNormalized(float normalized)
        => SampleAtArcProgress(displacementCurve.Evaluate(Mathf.Clamp01(normalized)));

    // Shared by runtime time sampling and the full-path Scene preview; no events are dispatched.
    internal void SampleAtArcProgress(float s)
    {
        if (spline.ControlPoints.Count < 2)
        {
            HasValidSample = false;
            return;
        }

        float t = spline.ArcLengthToT(s);

        SampledPosition = spline.GetPoint(t);

        Quaternion rot = applyRotation
            ? spline.GetRotation(t, rotationMode, rotationOffset)
            : Quaternion.identity;

        // Axis Lock：与 ProcessFrame 保持一致。
        if (applyRotation && allowedRotationAxes != AxisMask.All)
        {
            rot = ApplyAxisLock(rot, allowedRotationAxes);
        }

        SampledRotation = rot;
        HasValidSample = true;
    }

    /// <summary>
    /// 把允许的旋转轴掩码应用到 rot 上。
    /// - All：直接返回（不锁定）
    /// - None：返回 identity
    /// - Y only：特化路径 —— 用 forward 投影到 XZ 平面 + LookRotation，避开欧拉万向锁，最稳定。
    /// - 其他组合：欧拉拆分，清零禁用轴分量。Forward 接近 ±Y 时可能出现万向锁现象。
    /// </summary>
    private static Quaternion ApplyAxisLock(Quaternion rot, AxisMask allowed)
    {
        if (allowed == AxisMask.All) return rot;
        if (allowed == AxisMask.None) return Quaternion.identity;

        // 最常用的特化：只允许绕 Y 轴（水平 yaw 转向）
        if (allowed == AxisMask.Y)
        {
            Vector3 fwd = rot * Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            return Quaternion.LookRotation(fwd.normalized, Vector3.up);
        }

        // 通用：欧拉拆分（Unity 用 ZXY 顺序，eulerAngles 始终返回 X ∈ [0,360) 的规范化形式）
        Vector3 e = rot.eulerAngles;
        if ((allowed & AxisMask.X) == 0) e.x = 0f;
        if ((allowed & AxisMask.Y) == 0) e.y = 0f;
        if ((allowed & AxisMask.Z) == 0) e.z = 0f;
        return Quaternion.Euler(e);
    }

    public void AddControlTransform(Vector3 p) => spline.AddPoint(p);
    public void AddControlTransform(Vector3 p, Quaternion r) => spline.AddPoint(p, r);
    public void RemoveControlTransform(int index) => spline.RemovePoint(index);
    public void SetControlTransform(int index, Vector3 p) => spline.SetPoint(index, p);
    public void SetControlTransform(int index, Vector3 p, Quaternion r) => spline.SetPoint(index, p, r);

    public Vector3 GetControlPoint(int index)
    {
        if (index >= 0 && index < spline.ControlPoints.Count)
            return spline.ControlPoints[index];
        return Vector3.zero;
    }

    public int GetControlControlPointsCount() => spline.ControlPoints.Count;
}
