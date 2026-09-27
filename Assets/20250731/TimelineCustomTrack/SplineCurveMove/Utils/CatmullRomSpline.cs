using UnityEngine;
using System.Collections.Generic;
using LiangZhu.Geometry.Curves;

/// <summary>
/// 保持原公开 API 与序列化布局不变（被序列化进 Timeline clip，且被全局命名空间无限定符引用）。
/// 内部已重构为 LiangZhu.Geometry.Curves 底层框架的 facade：
///   求值 -> CatmullRomEvaluator，弧长 -> ArcLengthTable，PTF 帧 -> ParallelTransportFrames，
///   控制点 roll 插值等应用层逻辑仍留在本类。
/// </summary>
[System.Serializable]
public class CatmullRomSpline
{
    [SerializeField] private List<Vector3> controlPoints = new List<Vector3>();
    [SerializeField] private List<Quaternion> controlRotations = new List<Quaternion>();
    [SerializeField] private bool isLoop = false;
    [SerializeField] private Alpha alpha = Alpha.Centripetal;
    [SerializeField, Range(32, 1024)] private int arcLutResolution = 256;
    [SerializeField] private float[] groundHeightLut;   // 地面投影高度 LUT（按 t 索引 i/(n-1)）；空=不投影。烘焙产物，运行时只查表。
    // 新烘焙保存路径空间的完整三维点；旧资源的 groundHeightLut 仍可读取。
    [SerializeField] private Vector3[] groundPointLut;

    public enum Alpha { Uniform = 0, Centripetal = 1, Chordal = 2 }
    public enum RotationMode { Tangent = 0, TangentWithRoll = 1 }

    // 保持两个已有模式的序列化值；已移除模式的旧值统一降级为沿切线。
    public static RotationMode NormalizeRotationMode(RotationMode mode)
        => mode == RotationMode.TangentWithRoll ? RotationMode.TangentWithRoll : RotationMode.Tangent;

    public List<Vector3> ControlPoints => controlPoints;
    public List<Quaternion> ControlRotations => controlRotations;
    public bool IsLoop { get => isLoop; set { isLoop = value; InvalidateCache(); } }
    public Alpha AlphaMode { get => alpha; set { alpha = value; InvalidateCache(); } }
    public float TotalLength { get { EnsureCache(); return _totalLength; } }
    public int SegmentCount => isLoop ? controlPoints.Count : Mathf.Max(0, controlPoints.Count - 1);

    // ---- 底层框架对象（均不序列化，按需重建） ----
    [System.NonSerialized] private CatmullRomEvaluator _eval;
    [System.NonSerialized] private ICurveEvaluator _effectiveEval;
    [System.NonSerialized] private ArcLengthTable _arc;
    [System.NonSerialized] private Quaternion[] _frameLut;
    [System.NonSerialized] private Quaternion[] _rotationsCache;
    [System.NonSerialized] private float _totalLength;
    [System.NonSerialized] private bool _cacheValid;
    [System.NonSerialized] private int _cachedHash;


    /// <summary>是否已烘焙地面投影。</summary>
    public bool HasGroundProjection => (groundPointLut != null && groundPointLut.Length >= 2) ||
        (groundHeightLut != null && groundHeightLut.Length >= 2);
    /// <summary>
    /// 烘焙地面投影：沿曲线采样、每点经 groundSampler 投影到地面，存入三维点 LUT。
    /// groundSampler = (世界点)->(是否命中, 命中Y)，由上层用 RayMesh 向下射线实现。
    /// 内部用解析曲线（_eval）采样、不读已有 LUT，故重复烘焙不会叠加投影。
    /// </summary>
    public void BakeGroundProjection(System.Func<Vector3, (bool hit, float y)> groundSampler, float yOffset)
        => BakeGroundProjection(groundSampler, yOffset, Matrix4x4.identity);

    /// <summary>localToWorld 描述路径空间；查询和偏移使用世界空间，结果转回路径空间。</summary>
    public void BakeGroundProjection(System.Func<Vector3, (bool hit, float y)> groundSampler,
        float yOffset, Matrix4x4 localToWorld)
    {
        if (groundSampler == null) { ClearGroundProjection(); return; }
        EnsureCache();
        if (_eval == null || _eval.PointCount < 2) { ClearGroundProjection(); return; }

        int count = Mathf.Max(2, arcLutResolution + 1);
        var points = new Vector3[count];
        Matrix4x4 worldToLocal = localToWorld.inverse;
        for (int i = 0; i < count; i++)
        {
            Vector3 worldPoint = localToWorld.MultiplyPoint3x4(_eval.Evaluate((float)i / (count - 1)));
            var sample = groundSampler(worldPoint);
            if (sample.hit) worldPoint.y = sample.y + yOffset;
            points[i] = worldToLocal.MultiplyPoint3x4(worldPoint);
        }
        if (isLoop) points[count - 1] = points[0];
        groundPointLut = points;
        groundHeightLut = null;
        InvalidateCache();
    }

    /// <summary>清除地面投影，路径恢复为解析曲线。</summary>
    public void ClearGroundProjection()
    {
        groundHeightLut = null;
        groundPointLut = null;
        InvalidateCache();
    }

    public void AddPoint(Vector3 p) => AddPoint(p, Quaternion.identity);

    public void AddPoint(Vector3 p, Quaternion r)
    {
        controlPoints.Add(p);
        SyncRotationsLength();
        controlRotations[controlRotations.Count - 1] = r;
        InvalidateCache();
    }

    public void RemovePoint(int index)
    {
        if (index < 0 || index >= controlPoints.Count) return;
        controlPoints.RemoveAt(index);
        if (index < controlRotations.Count) controlRotations.RemoveAt(index);
        InvalidateCache();
    }

    public void SetPoint(int index, Vector3 p)
    {
        if (index < 0 || index >= controlPoints.Count) return;
        controlPoints[index] = p;
        InvalidateCache();
    }

    public void SetPoint(int index, Vector3 p, Quaternion r)
    {
        if (index < 0 || index >= controlPoints.Count) return;
        controlPoints[index] = p;
        SyncRotationsLength();
        controlRotations[index] = r;
        InvalidateCache();
    }

    public void SetRotation(int index, Quaternion r)
    {
        SyncRotationsLength();
        if (index < 0 || index >= controlRotations.Count) return;
        controlRotations[index] = r;
        InvalidateCache();
    }

    public Vector3 GetControlPointPosition(int index)
    {
        if (index < 0 || index >= controlPoints.Count) return Vector3.zero;
        return controlPoints[index];
    }

    public Quaternion GetControlPointRotation(int index)
    {
        SyncRotationsLength();
        if (index < 0 || index >= controlRotations.Count) return Quaternion.identity;
        return controlRotations[index];
    }

    public void InvalidateCache() => _cacheValid = false;

    private void SyncRotationsLength()
    {
        while (controlRotations.Count < controlPoints.Count)
            controlRotations.Add(Quaternion.identity);
        while (controlRotations.Count > controlPoints.Count)
            controlRotations.RemoveAt(controlRotations.Count - 1);
    }

    public Vector3 GetPoint(float t)
    {
        EnsureCache();
        return _effectiveEval.Evaluate(t);
    }

    public Vector3 GetTangent(float t)
    {
        EnsureCache();
        Vector3 tangent = _effectiveEval.EvaluateDerivative(t);
        float mag = tangent.magnitude;
        return mag > 1e-6f ? tangent / mag : Vector3.forward;
    }

    public Quaternion GetRotation(float t, RotationMode mode = RotationMode.TangentWithRoll,
                                  Vector3 rotationOffsetEuler = default)
    {
        EnsureCache();
        if (_eval.PointCount == 0) return Quaternion.identity;

        Quaternion offset = Quaternion.Euler(rotationOffsetEuler);

        Vector3 tan = GetTangent(t);
        if (tan.sqrMagnitude < 1e-8f) tan = Vector3.forward;

        if (NormalizeRotationMode(mode) == RotationMode.Tangent)
            return Quaternion.LookRotation(tan) * offset;

        Quaternion ptf = SamplePTFFrame(t);
        float roll = SampleRollAngle(t);
        Quaternion rollQ = Quaternion.AngleAxis(roll, Vector3.forward);
        return ptf * rollQ * offset;
    }

    public Vector3 GetPointByArcLength(float s) => GetPoint(ArcLengthToT(s));
    public Vector3 GetTangentByArcLength(float s) => GetTangent(ArcLengthToT(s));
    public Quaternion GetRotationByArcLength(float s, RotationMode mode = RotationMode.TangentWithRoll,
                                             Vector3 offsetEuler = default)
        => GetRotation(ArcLengthToT(s), mode, offsetEuler);

    public float ArcLengthToT(float s)
    {
        EnsureCache();
        return _arc.ArcLengthToT(s);
    }

    public void RedistributeEvenly(int count)
    {
        if (controlPoints.Count < 2 || count < 2) return;

        EnsureCache();
        if (_totalLength <= 1e-6f) return;

        var newPos = new List<Vector3>(count);
        var newRot = new List<Quaternion>(count);
        for (int i = 0; i < count; i++)
        {
            float s = (float)i / (count - 1);
            float t = ArcLengthToT(s);
            newPos.Add(GetPoint(t));
            newRot.Add(GetRotation(t, RotationMode.TangentWithRoll));
        }

        controlPoints = newPos;
        controlRotations = newRot;
        InvalidateCache();
    }

    private void EnsureCache()
    {
        SyncRotationsLength();
        int hash = ComputeControlPointsHash();
        if (_cacheValid && hash == _cachedHash) return;

        int n = controlPoints.Count;

        // 关键点旋转缓存（关键点旋转 / roll 逻辑仍在本类，需要它）
        if (_rotationsCache == null || _rotationsCache.Length != n) _rotationsCache = new Quaternion[n];
        for (int i = 0; i < n; i++) _rotationsCache[i] = controlRotations[i];

        // 配置底层求值器
        if (_eval == null) _eval = new CatmullRomEvaluator();
        _eval.SetControlPoints(controlPoints, isLoop, (CatmullRomAlpha)(int)alpha);
        _effectiveEval = HasGroundProjection
            ? (ICurveEvaluator)new ProjectedCurveEvaluator(_eval, groundPointLut, groundHeightLut)
            : _eval;

        // 弧长表
        if (_arc == null) _arc = new ArcLengthTable();
        _arc.Build(_effectiveEval, arcLutResolution);
        _totalLength = _arc.TotalLength;

        // PTF 帧 LUT（含闭环 holonomy 校正）
        _frameLut = ParallelTransportFrames.Build(_effectiveEval, arcLutResolution, isLoop);

        _cachedHash = hash;
        _cacheValid = true;
    }

    private int ComputeControlPointsHash()
    {
        unchecked
        {
            int h = controlPoints.Count;
            h = h * 31 + (isLoop ? 1 : 0);
            h = h * 31 + (int)alpha;
            if (controlPoints.Count > 0)
            {
                h = h * 31 + controlPoints[0].GetHashCode();
                h = h * 31 + controlPoints[controlPoints.Count - 1].GetHashCode();
            }
            if (controlRotations.Count > 0)
            {
                h = h * 31 + controlRotations[0].GetHashCode();
                h = h * 31 + controlRotations[controlRotations.Count - 1].GetHashCode();
            }
            return h;
        }
    }

    // PTF 帧采样：转调底层（ComputeRollAtControlPoint 与 GetRotation 共用）
    private Quaternion SamplePTFFrame(float t)
    {
        var frame = ParallelTransportFrames.Sample(_frameLut, t, isLoop);
        // Interpolated frames only approximate the exact tangent between LUT samples.
        // Preserve transported twist while aligning forward to the actual curve derivative.
        return Quaternion.FromToRotation(frame * Vector3.forward, GetTangent(t)) * frame;
    }

    private float SampleRollAngle(float t)
    {
        int n = _rotationsCache.Length;
        if (n == 0) return 0f;
        if (n == 1) return ComputeRollAtControlPoint(0);

        int i0, i1;
        float frac;

        if (isLoop)
        {
            t = t - Mathf.Floor(t);
            float scaled = t * n;
            i0 = Mathf.FloorToInt(scaled) % n;
            i1 = (i0 + 1) % n;
            frac = scaled - Mathf.Floor(scaled);
        }
        else
        {
            t = Mathf.Clamp01(t);
            float scaled = t * (n - 1);
            i0 = Mathf.FloorToInt(scaled);
            if (i0 >= n - 1) return ComputeRollAtControlPoint(n - 1);
            i1 = i0 + 1;
            frac = scaled - i0;
        }

        float roll0 = ComputeRollAtControlPoint(i0);
        float roll1 = ComputeRollAtControlPoint(i1);
        float diff = Mathf.DeltaAngle(roll0, roll1);
        return roll0 + diff * frac;
    }

    private float ComputeRollAtControlPoint(int i)
    {
        int n = _rotationsCache.Length;
        if (n == 0) return 0f;
        i = Mathf.Clamp(i, 0, n - 1);

        float t = isLoop ? (float)i / n : (n > 1 ? (float)i / (n - 1) : 0f);

        Quaternion ptf = SamplePTFFrame(t);
        Quaternion key = _rotationsCache[i];

        Quaternion local = Quaternion.Inverse(ptf) * key;
        Vector3 localUp = local * Vector3.up;
        float angleRad = Mathf.Atan2(-localUp.x, localUp.y);
        return angleRad * Mathf.Rad2Deg;
    }

    /// <summary>位置、长度、朝向和曲率共用同一条投影曲线，不再混用投影前后的几何。</summary>
    private sealed class ProjectedCurveEvaluator : ICurveEvaluator
    {
        private readonly CatmullRomEvaluator _source;
        private readonly Vector3[] _points;
        private readonly float[] _heights;
        public bool IsLoop => _source.IsLoop;
        public int SegmentCount => _source.SegmentCount;

        public ProjectedCurveEvaluator(CatmullRomEvaluator source, Vector3[] points, float[] heights)
        {
            _source = source;
            _points = points != null && points.Length >= 2 ? points : null;
            _heights = heights;
        }

        public Vector3 Evaluate(float t)
        {
            t = IsLoop ? t - Mathf.Floor(t) : Mathf.Clamp01(t);
            int count = _points != null ? _points.Length : _heights.Length;
            float scaled = t * (count - 1);
            int index = Mathf.Min(Mathf.FloorToInt(scaled), count - 2);
            float fraction = scaled - index;
            if (_points != null) return Vector3.Lerp(_points[index], _points[index + 1], fraction);
            // 兼容旧高度资源，仍用旧解析 XZ，但所有派生数据从此求值器重建。
            Vector3 point = _source.Evaluate(t);
            point.y = Mathf.Lerp(_heights[index], _heights[index + 1], fraction);
            return point;
        }

        public Vector3 EvaluateDerivative(float t)
        {
            // 投影位置是离散 LUT 的线性插值。微小差分会落在同一条线段内，
            // 跨节点时切线会突变。跨一个采样间隔估算平滑切线，
            // 让 PTF 获得稳定的转弯信息。
            int count = _points != null ? _points.Length : _heights.Length;
            float h = 1f / (count - 1);
            float a = IsLoop ? t - h : Mathf.Clamp01(t - h);
            float b = IsLoop ? t + h : Mathf.Clamp01(t + h);
            return b - a > 1e-8f ? (Evaluate(b) - Evaluate(a)) / (b - a) : Vector3.zero;
        }
    }
}
