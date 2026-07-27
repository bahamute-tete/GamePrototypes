using UnityEngine;
using UnityEngine.Playables;
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
public class SplineCurveMoveBehaviour : PlayableBehaviour
{
    [SerializeField] private CatmullRomSpline spline = new CatmullRomSpline();
    [SerializeField] private bool useLocalSpace = false;

    [Tooltip("旋转计算模式。\n" +
             "Tangent: 仅按切线方向。\n" +
             "TangentWithRoll: PTF 标架 + 关键点 roll 插值（推荐）。\n" +
             "KeyframeOnly: 直接 Slerp 关键点旋转，忽略路径方向。")]
    [SerializeField] private CatmullRomSpline.RotationMode rotationMode = CatmullRomSpline.RotationMode.TangentWithRoll;

    [SerializeField] private bool applyRotation = true;

    [Tooltip("允许旋转的轴（位掩码）。默认 All = 不锁定（物体完全朝向曲线方向）。\n" +
             "勾选 Y 单选 = 只允许绕 Y 轴（水平 yaw 转向），物体始终保持水平 —— 适合车辆 / 角色。\n" +
             "其他组合按欧拉拆分应用（forward 接近垂直时可能受万向锁影响）。\n" +
             "注意：开启 Auto Banking 时如果禁用 Z 轴，banking 的 roll 也会被锁掉。")]
    [SerializeField] private AxisMask allowedRotationAxes = AxisMask.All;

    [Tooltip("位移曲线，输入 = Timeline 归一化时间，输出 = 弧长归一化进度 ∈ [0,1]。")]
    [SerializeField] private AnimationCurve displacementCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    // -------------------------------------------------------------------- //
    // 阶段 4：自动 Banking
    // -------------------------------------------------------------------- //
    [Tooltip("启用自动 Banking：根据曲率与速度自动倾斜物体（朝圆心）。")]
    [SerializeField] private bool autoBanking = false;

    [Tooltip("Banking 强度倍数。1.0 = 物理精确（atan(v² * κ / g)）。" +
             "降低让倾斜更柔和，增大让倾斜更夸张。")]
    [SerializeField] private float bankingScale = 1f;

    [Tooltip("Banking 最大倾斜角（度）。")]
    [SerializeField, Range(0f, 89f)] private float bankingClampDeg = 60f;

    [Tooltip("用于 Banking 公式的重力加速度（米/秒²）。地球 = 9.81。")]
    [SerializeField] private float gravityForBanking = 9.81f;

    public CatmullRomSpline Spline => spline;
    public bool UseLocalSpace { get => useLocalSpace; set => useLocalSpace = value; }
    public CatmullRomSpline.RotationMode RotationMode { get => rotationMode; set => rotationMode = value; }
    public bool ApplyRotation { get => applyRotation; set => applyRotation = value; }
    public AxisMask AllowedRotationAxes { get => allowedRotationAxes; set => allowedRotationAxes = value; }
    public AnimationCurve DisplacementCurve => displacementCurve;
    public Vector3 RotationOffset { get => rotationOffset; set => rotationOffset = value; }
    public bool AutoBanking { get => autoBanking; set => autoBanking = value; }
    public float BankingScale { get => bankingScale; set => bankingScale = value; }
    public float BankingClampDeg { get => bankingClampDeg; set => bankingClampDeg = value; }
    public float GravityForBanking { get => gravityForBanking; set => gravityForBanking = value; }

    public bool AlignToPath
    {
        get => applyRotation && rotationMode != CatmullRomSpline.RotationMode.KeyframeOnly;
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

        double time = playable.GetTime();
        double duration = playable.GetDuration();

        // 边界 snap：Timeline 滑块在 Clip 起止处时浮点 time 可能不精确，
        // 用 epsilon 把 normalized 强制对齐到 0 或 1。
        const double BOUNDARY_EPSILON = 1e-4;
        float normalized;
        if (duration <= 1e-6) normalized = 0f;
        else if (time <= BOUNDARY_EPSILON) normalized = 0f;
        else if (time >= duration - BOUNDARY_EPSILON) normalized = 1f;
        else normalized = (float)(time / duration);

        float s = displacementCurve.Evaluate(normalized);
        float t = spline.ArcLengthToT(s);

        SampledPosition = spline.GetPoint(t);

        Quaternion rot = applyRotation
            ? spline.GetRotation(t, rotationMode, rotationOffset)
            : Quaternion.identity;

        // Auto Banking
        if (applyRotation && autoBanking)
        {
            float v = ComputeSpeed(normalized, (float)duration);
            float kSigned = spline.GetSignedCurvatureAtT(t);
            // 物理：θ = atan(v² * κ / g)，朝圆心倾斜
            float bankRad = Mathf.Atan2(kSigned * v * v * bankingScale, Mathf.Max(gravityForBanking, 1e-3f));
            float bankDeg = Mathf.Clamp(bankRad * Mathf.Rad2Deg, -bankingClampDeg, bankingClampDeg);
            rot = rot * Quaternion.AngleAxis(bankDeg, Vector3.forward);
        }

        // Axis Lock：在所有旋转计算后应用（位于 banking 之后，确保 banking 的 roll 也被锁定）
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
    /// 注意：此路径不计算 Banking——边界位置物理速度为 0，bank 角为 0。
    /// 也不参与路径事件检测（_lastSampledArcS 不更新）。
    /// </summary>
    public void SampleAtNormalized(float normalized)
    {
        if (spline.ControlPoints.Count < 2)
        {
            HasValidSample = false;
            return;
        }

        normalized = Mathf.Clamp01(normalized);
        float s = displacementCurve.Evaluate(normalized);
        float t = spline.ArcLengthToT(s);

        SampledPosition = spline.GetPoint(t);

        Quaternion rot = applyRotation
            ? spline.GetRotation(t, rotationMode, rotationOffset)
            : Quaternion.identity;

        // Axis Lock：与 ProcessFrame 保持一致（banking 在边界处速度为 0 不计算，所以这里没 banking 步骤）
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

    private float ComputeSpeed(float normalizedTime, float duration)
    {
        if (duration <= 1e-6f) return 0f;
        const float h = 1e-3f;
        float n1 = Mathf.Max(0f, normalizedTime - h);
        float n2 = Mathf.Min(1f, normalizedTime + h);
        if (n2 - n1 < 1e-8f) return 0f;
        float s1 = displacementCurve.Evaluate(n1);
        float s2 = displacementCurve.Evaluate(n2);
        float dsdn = (s2 - s1) / (n2 - n1);
        return dsdn / duration * spline.TotalLength;
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
