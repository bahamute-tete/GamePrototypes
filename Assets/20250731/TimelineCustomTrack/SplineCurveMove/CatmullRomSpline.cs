using UnityEngine;
using System.Collections.Generic;
using LiangZhu.Geometry.Curves;

/// <summary>
/// 保持原公开 API 与序列化布局不变（被序列化进 Timeline clip，且被全局命名空间无限定符引用）。
/// 内部已重构为 LiangZhu.Geometry.Curves 底层框架的 facade：
///   求值 -> CatmullRomEvaluator，弧长 -> ArcLengthTable，PTF 帧 -> ParallelTransportFrames，
///   曲率 -> CurveAnalysis。关键点旋转 / roll 等带插值假设的应用层逻辑仍留在本类。
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

    public enum Alpha { Uniform = 0, Centripetal = 1, Chordal = 2 }
    public enum RotationMode { Tangent, TangentWithRoll, KeyframeOnly }

    public List<Vector3> ControlPoints => controlPoints;
    public List<Quaternion> ControlRotations => controlRotations;
    public bool IsLoop { get => isLoop; set { isLoop = value; InvalidateCache(); } }
    public Alpha AlphaMode { get => alpha; set { alpha = value; InvalidateCache(); } }
    public float TotalLength { get { EnsureCache(); return _totalLength; } }
    public int SegmentCount => isLoop ? controlPoints.Count : Mathf.Max(0, controlPoints.Count - 1);

    // ---- 底层框架对象（均不序列化，按需重建） ----
    [System.NonSerialized] private CatmullRomEvaluator _eval;
    [System.NonSerialized] private ArcLengthTable _arc;
    [System.NonSerialized] private Quaternion[] _frameLut;
    [System.NonSerialized] private Quaternion[] _rotationsCache;
    [System.NonSerialized] private float _totalLength;
    [System.NonSerialized] private bool _cacheValid;
    [System.NonSerialized] private int _cachedHash;


    /// <summary>是否已烘焙地面投影。</summary>
    public bool HasGroundProjection => groundHeightLut != null && groundHeightLut.Length >= 2;
    /// <summary>
    /// 烘焙地面投影：沿曲线采样、每点经 groundSampler 投影到地面，存入序列化高度 LUT。
    /// groundSampler = (世界点)->(是否命中, 命中Y)，由上层用 RayMesh 向下射线实现。
    /// 内部用解析曲线（_eval）采样、不读已有 LUT，故重复烘焙不会叠加投影。
    /// </summary>
    public void BakeGroundProjection(System.Func<Vector3, (bool hit, float y)> groundSampler, float yOffset)
    {
        if (groundSampler == null) { ClearGroundProjection(); return; }
        EnsureCache();
        if (_eval == null || _eval.PointCount < 2) { ClearGroundProjection(); return; }

        int count = Mathf.Max(2, arcLutResolution + 1);
        groundHeightLut = GroundProjection.BakeHeightLut(_eval, count, yOffset, groundSampler);
    }

    /// <summary>清除地面投影，路径恢复为解析曲线。</summary>
    public void ClearGroundProjection() => groundHeightLut = null;

    /// <summary>按 t 查高度 LUT（线性插值）。闭环按 t 取模，开放按 clamp01。</summary>
    private float SampleGroundHeight(float t)
    {
        int n = groundHeightLut.Length;
        if (n == 0) return 0f;
        if (n == 1) return groundHeightLut[0];

        t = isLoop ? t - Mathf.Floor(t) : Mathf.Clamp01(t);
        float scaled = t * (n - 1);
        int i0 = Mathf.FloorToInt(scaled);
        if (i0 >= n - 1) return groundHeightLut[n - 1];
        float frac = scaled - i0;
        return Mathf.Lerp(groundHeightLut[i0], groundHeightLut[i0 + 1], frac);
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
        Vector3 p = _eval.Evaluate(t);
        if (HasGroundProjection) p.y = SampleGroundHeight(t);   // 贴地：XZ 保持解析曲线，Y 换成烘焙地面高度
        return p;
    }

    public Vector3 GetTangent(float t)
    {
        EnsureCache();
        Vector3 tangent = _eval.EvaluateDerivative(t);
        float mag = tangent.magnitude;
        return mag > 1e-6f ? tangent / mag : Vector3.forward;
    }

    public Quaternion GetRotation(float t, RotationMode mode = RotationMode.TangentWithRoll,
                                  Vector3 rotationOffsetEuler = default)
    {
        EnsureCache();
        if (_eval.PointCount == 0) return Quaternion.identity;

        Quaternion offset = Quaternion.Euler(rotationOffsetEuler);

        if (mode == RotationMode.KeyframeOnly)
            return SampleKeyframeRotation(t) * offset;

        Vector3 tan = GetTangent(t);
        if (tan.sqrMagnitude < 1e-8f) tan = Vector3.forward;

        if (mode == RotationMode.Tangent)
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

    /// <summary>
    /// 返回 t 处的 signed curvature（1/米）。符号约定：圆心在 PTF right 反方向时 > 0。
    /// 用于 Auto Banking 决定倾斜方向。
    /// </summary>
    public float GetSignedCurvatureAtT(float t)
    {
        EnsureCache();
        if (_eval.PointCount < 2) return 0f;
        return CurveAnalysis.SignedCurvature(_eval, _frameLut, t, isLoop);
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
            newRot.Add(SampleKeyframeRotation(t));
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

        // 弧长表
        if (_arc == null) _arc = new ArcLengthTable();
        _arc.Build(_eval, arcLutResolution);
        _totalLength = _arc.TotalLength;

        // PTF 帧 LUT（含闭环 holonomy 校正）
        _frameLut = ParallelTransportFrames.Build(_eval, arcLutResolution, isLoop);

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
    private Quaternion SamplePTFFrame(float t) => ParallelTransportFrames.Sample(_frameLut, t, isLoop);

    private Quaternion SampleKeyframeRotation(float t)
    {
        int n = _rotationsCache.Length;
        if (n == 0) return Quaternion.identity;
        if (n == 1) return _rotationsCache[0];

        if (isLoop)
        {
            t = t - Mathf.Floor(t);
            float scaled = t * n;
            int i0 = Mathf.FloorToInt(scaled) % n;
            int i1 = (i0 + 1) % n;
            float frac = scaled - Mathf.Floor(scaled);
            return Quaternion.Slerp(_rotationsCache[i0], _rotationsCache[i1], frac);
        }
        else
        {
            t = Mathf.Clamp01(t);
            float scaled = t * (n - 1);
            int i0 = Mathf.FloorToInt(scaled);
            if (i0 >= n - 1) return _rotationsCache[n - 1];
            int i1 = i0 + 1;
            float frac = scaled - i0;
            return Quaternion.Slerp(_rotationsCache[i0], _rotationsCache[i1], frac);
        }
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
}
