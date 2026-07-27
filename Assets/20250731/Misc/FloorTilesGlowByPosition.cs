using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 基于角色相对坐标控制地砖 _EmissionColor 的发光效果。
/// 两种模式(enum 切换):
///   Radial   — 圆形径向, 按平面距离 smoothstep;
///   GridCell — 离散格子, 把角色换算成 (col,row), 按格距(Chebyshev/Manhattan)点亮。
/// 支持多角色(Max/Additive), 走 renderer 级加法式 MPB, 与 _BaseMap_ST / Dissolve 共存。
/// 每帧只刷发光值变化的砖。
/// </summary>
[ExecuteAlways]
public class FloorTilesGlowByPosition : MonoBehaviour
{
    public enum GridPlane { XZ, XY, ZY }
    public enum CombineMode { Max, Additive }
    public enum HighlightMode { Radial, GridCell }
    public enum CellMetric { Chebyshev, Manhattan }
    public enum RippleStyle { Pulse, RevealOnce }

    [Header("地砖根 (留空 = 本物体)")]
    [SerializeField] Transform tilesRoot;

    [Header("角色 (可多个, 运行时用 Register/Unregister)")]
    [SerializeField] List<Transform> characters = new List<Transform>();

    [Header("模式")]
    [SerializeField] HighlightMode mode = HighlightMode.Radial;
    [SerializeField] GridPlane gridPlane = GridPlane.XZ;

    [Header("Radial 参数 (本地空间单位)")]
    [Tooltip("此半径内全亮")]
    [SerializeField] float innerRadius = 1.0f;
    [Tooltip("到此半径熄灭。<= innerRadius 即硬边")]
    [SerializeField] float outerRadius = 2.5f;

    [Header("GridCell 参数 (单位=格数)")]
    [SerializeField] CellMetric cellMetric = CellMetric.Chebyshev;
    [Tooltip("此格距内全亮。0 = 只亮角色所在格")]
    [SerializeField] float cellInnerRadius = 0f;
    [Tooltip("到此格距熄灭。<= inner 即硬边(无过渡)")]
    [SerializeField] float cellOuterRadius = 1f;
    [Tooltip("推断网格 pitch 时忽略小于此值的位置抖动")]
    [SerializeField] float gridSnapEpsilon = 0.001f;

    [Header("GridCell 波纹扩散")]
    [Tooltip("开启波纹。Pulse=同心环持续向外荡开; RevealOnce=进入范围时单次延迟亮起")]
    [SerializeField] bool cellRipple = false;
    [SerializeField] RippleStyle rippleStyle = RippleStyle.Pulse;

    [Header("Pulse 参数 (同心环, 单位=格)")]
    [Tooltip("环向外扩散速度(格/秒)")]
    [SerializeField] float rippleSpeed = 5f;
    [Tooltip("相邻两环的间距(格)。越大环越稀")]
    [SerializeField] float rippleSpacing = 4f;
    [Tooltip("环的锐利度(衰减指数), 越大环越细")]
    [SerializeField] float rippleSharpness = 2f;

    [Header("RevealOnce 参数 (单次延迟)")]
    [Tooltip("每多 1 格距离, 增加的亮起延迟(秒)")]
    [SerializeField] float cellDelayPerCell = 0.06f;
    [Tooltip("每格额外的随机延迟上限(秒)")]
    [SerializeField] float cellDelayJitter = 0.1f;
    [Tooltip("所有格统一的起始延迟(秒)")]
    [SerializeField] float cellBaseDelay = 0f;

    [Header("发光颜色 (HDR)")]
    [ColorUsage(true, true)]
    [SerializeField] Color glowColor = new Color(1.0f, 0.55f, 0.2f, 1f) * 2f;
    [SerializeField] CombineMode combineMode = CombineMode.Max;

    [Header("时间过渡")]
    [Tooltip("发光强度平滑速度(每秒)。0 = 瞬时")]
    [SerializeField] float fadeSpeed = 10f;

    [Header("Shader 属性 / 优化")]
    [SerializeField] string emissionPropertyName = "_EmissionColor";
    [Tooltip("强度变化小于此值则跳过 MPB 写入")]
    [SerializeField] float changeEpsilon = 0.003f;
    [SerializeField] bool previewInEditMode = true;

    int _emissionPropId;
    MaterialPropertyBlock _mpb;

    Renderer[] _tiles;
    Vector2[]  _tilePlanePos;   // 砖本地平面坐标(u,v)
    int[]      _tileCol, _tileRow;
    float      _minU, _minV, _pitchU, _pitchV;
    float[]    _current, _applied;
    float[]    _revealAt;       // 波纹: 该格允许亮起的时刻; <0 表示当前不在范围/未排程
    bool _dirtyTiles = true;

    float[] _charU, _charV;

    Transform Root => tilesRoot != null ? tilesRoot : transform;

    public void RegisterCharacter(Transform t)
    {
        if (t != null && !characters.Contains(t)) characters.Add(t);
    }
    public void UnregisterCharacter(Transform t) => characters.Remove(t);

    void OnEnable()
    {
        _dirtyTiles = true;
        EnsureSetup();
    }

    void OnDisable()
    {
        if (_tiles == null) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        for (int i = 0; i < _tiles.Length; i++)
        {
            if (_tiles[i] == null) continue;
            if (_applied != null && _applied[i] <= 0f) continue;
            WriteEmission(_tiles[i], 0f);
            if (_current != null) _current[i] = 0f;
            if (_applied != null) _applied[i] = 0f;
        }
    }

    [ContextMenu("重新收集地砖")]
    public void RefreshTiles() { _dirtyTiles = true; EnsureSetup(); }

    [ContextMenu("全部熄灭")]
    public void ClearAll()
    {
        if (_tiles == null) return;
        for (int i = 0; i < _tiles.Length; i++)
        {
            if (_tiles[i] == null) continue;
            WriteEmission(_tiles[i], 0f);
            _current[i] = 0f; _applied[i] = 0f;
            if (_revealAt != null) _revealAt[i] = -1f;
        }
    }

    void EnsureSetup()
    {
        _emissionPropId = Shader.PropertyToID(emissionPropertyName);
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        if (!_dirtyTiles && _tiles != null) return;

        var root = Root;
        var list = new List<Renderer>();
        for (int i = 0; i < root.childCount; i++)
        {
            var r = root.GetChild(i).GetComponent<Renderer>();
            if (r != null) list.Add(r);
        }

        int n = list.Count;
        _tiles = list.ToArray();
        _tilePlanePos = new Vector2[n];
        _tileCol = new int[n];
        _tileRow = new int[n];
        _current = new float[n];
        _applied = new float[n];
        _revealAt = new float[n];

        var us = new float[n];
        var vs = new float[n];
        for (int i = 0; i < n; i++)
        {
            GetPlaneCoords(_tiles[i].transform.localPosition, out float u, out float v);
            _tilePlanePos[i] = new Vector2(u, v);
            us[i] = u; vs[i] = v;
            _current[i] = 0f;
            _applied[i] = -1f; // 强制首帧写入
        }
        if (_revealAt != null)
            for (int i = 0; i < n; i++) _revealAt[i] = -1f;

        // 推断网格 (供 GridCell 模式用; 静态只算一次)
        _minU = float.MaxValue; _minV = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            if (us[i] < _minU) _minU = us[i];
            if (vs[i] < _minV) _minV = vs[i];
        }
        _pitchU = DetectPitch(us, gridSnapEpsilon);
        _pitchV = DetectPitch(vs, gridSnapEpsilon);
        for (int i = 0; i < n; i++)
        {
            _tileCol[i] = (_pitchU > 0f) ? Mathf.RoundToInt((us[i] - _minU) / _pitchU) : 0;
            _tileRow[i] = (_pitchV > 0f) ? Mathf.RoundToInt((vs[i] - _minV) / _pitchV) : 0;
        }

        _dirtyTiles = false;
    }

    void Update()
    {
        if (!Application.isPlaying && !previewInEditMode) return;
        if (_tiles == null || _dirtyTiles) EnsureSetup();
        if (_tiles.Length == 0) return;

        var root = Root;

        int charCount = 0;
        for (int i = 0; i < characters.Count; i++)
            if (characters[i] != null) charCount++;
        if (_charU == null || _charU.Length < charCount)
        {
            _charU = new float[Mathf.Max(charCount, 4)];
            _charV = new float[Mathf.Max(charCount, 4)];
        }
        int ci = 0;
        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i] == null) continue;
            Vector3 local = root.InverseTransformPoint(characters[i].position);
            GetPlaneCoords(local, out float cu, out float cv);
            _charU[ci] = cu; _charV[ci] = cv; ci++;
        }

        bool instant = !Application.isPlaying || fadeSpeed <= 0f;
        float lerpT = instant ? 1f : 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime);

        if (mode == HighlightMode.Radial) UpdateRadial(charCount, lerpT);
        else UpdateGridCell(charCount, lerpT);
    }

    void UpdateRadial(int charCount, float lerpT)
    {
        float outer2 = outerRadius * outerRadius;
        for (int i = 0; i < _tiles.Length; i++)
        {
            var r = _tiles[i];
            if (r == null) continue;
            Vector2 tp = _tilePlanePos[i];

            float target = 0f;
            for (int c = 0; c < charCount; c++)
            {
                float dx = tp.x - _charU[c];
                float dy = tp.y - _charV[c];
                float d2 = dx * dx + dy * dy;
                if (d2 >= outer2 && outerRadius > innerRadius) continue;

                float intensity = Falloff(Mathf.Sqrt(d2), innerRadius, outerRadius);
                target = Accumulate(target, intensity);
            }
            ApplyTarget(i, r, target, lerpT);
        }
    }

    void UpdateGridCell(int charCount, float lerpT)
    {
        float now = Now();
        bool pulse = cellRipple && rippleStyle == RippleStyle.Pulse;
        // 脉冲自身已含时间形状, 不再走 fade 平滑(否则会糊掉环); RevealOnce 仍用 fade 淡入
        float applyLerp = pulse ? 1f : lerpT;
        float spacing = Mathf.Max(0.001f, rippleSpacing);
        float sharp = Mathf.Max(1f, rippleSharpness);

        for (int i = 0; i < _tiles.Length; i++)
        {
            var r = _tiles[i];
            if (r == null) continue;
            int tc = _tileCol[i], tr = _tileRow[i];

            float target = 0f;
            float minDist = float.MaxValue;
            for (int c = 0; c < charCount; c++)
            {
                int cc = (_pitchU > 0f) ? Mathf.RoundToInt((_charU[c] - _minU) / _pitchU) : 0;
                int cr = (_pitchV > 0f) ? Mathf.RoundToInt((_charV[c] - _minV) / _pitchV) : 0;

                int dc = Mathf.Abs(tc - cc);
                int dr = Mathf.Abs(tr - cr);
                float cellDist = (cellMetric == CellMetric.Chebyshev)
                    ? Mathf.Max(dc, dr)
                    : (dc + dr);

                if (cellDist > cellOuterRadius && cellOuterRadius > cellInnerRadius) continue;

                float spatial = Falloff(cellDist, cellInnerRadius, cellOuterRadius);

                float intensity;
                if (pulse)
                {
                    // 同心环: 领先位置 = now*speed, 落后 cellDist 的相位决定环亮度
                    float lead = now * rippleSpeed - cellDist;
                    float ring = 0f;
                    if (lead >= 0f)
                    {
                        float f = Mathf.Repeat(lead, spacing) / spacing; // 0..1, 0=环刚到
                        ring = Mathf.Pow(1f - f, sharp);                 // 前沿亮, 向后衰减
                    }
                    intensity = spatial * ring;
                }
                else
                {
                    intensity = spatial;
                }

                target = Accumulate(target, intensity);
                if (cellDist < minDist) minDist = cellDist;
            }

            float effTarget = target;
            if (cellRipple && rippleStyle == RippleStyle.RevealOnce)
            {
                bool inRange = target > 0f;
                if (inRange)
                {
                    if (_revealAt[i] < 0f)
                        _revealAt[i] = now + cellBaseDelay
                                     + minDist * cellDelayPerCell
                                     + Random.value * cellDelayJitter;
                    effTarget = (now >= _revealAt[i]) ? target : 0f;
                }
                else
                {
                    _revealAt[i] = -1f;
                }
            }

            ApplyTarget(i, r, effTarget, applyLerp);
        }
    }

    /// <summary>RevealOnce 模式下, 把所有格的排程清空, 角色静止时也能手动重新荡一次。</summary>
    [ContextMenu("触发一次波纹 (RevealOnce)")]
    public void TriggerRippleOnce()
    {
        if (_revealAt == null) return;
        for (int i = 0; i < _revealAt.Length; i++) _revealAt[i] = -1f;
    }

    float Now()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return (float)UnityEditor.EditorApplication.timeSinceStartup;
#endif
        return Time.time;
    }

    float Accumulate(float acc, float v)
        => combineMode == CombineMode.Max ? Mathf.Max(acc, v) : acc + v;

    void ApplyTarget(int i, Renderer r, float target, float lerpT)
    {
        if (combineMode == CombineMode.Max) target = Mathf.Clamp01(target);

        float cur = Mathf.Lerp(_current[i], target, lerpT);
        if (Mathf.Abs(cur - target) < 1e-4f) cur = target; // 收尾吸附
        _current[i] = cur;

        if (Mathf.Abs(cur - _applied[i]) > changeEpsilon || (cur == 0f && _applied[i] != 0f))
        {
            WriteEmission(r, cur);
            _applied[i] = cur;
        }
    }

    /// <summary>d 在 [inner,outer] 间 smoothstep; inner 内为 1, outer 外为 0; outer<=inner 时硬边。</summary>
    static float Falloff(float d, float inner, float outer)
    {
        if (outer <= inner) return d <= inner ? 1f : 0f;
        if (d <= inner) return 1f;
        if (d >= outer) return 0f;
        float t = (d - inner) / (outer - inner);
        return 1f - (t * t * (3f - 2f * t));
    }

    /// <summary>排序后找最小正间距, 作为规则网格 pitch。</summary>
    static float DetectPitch(float[] values, float eps)
    {
        var sorted = (float[])values.Clone();
        System.Array.Sort(sorted);
        float pitch = float.MaxValue;
        for (int i = 1; i < sorted.Length; i++)
        {
            float d = sorted[i] - sorted[i - 1];
            if (d > eps && d < pitch) pitch = d;
        }
        return (pitch == float.MaxValue) ? 0f : pitch;
    }

    void WriteEmission(Renderer r, float intensity)
    {
        r.GetPropertyBlock(_mpb);
        _mpb.SetColor(_emissionPropId, glowColor * intensity);
        r.SetPropertyBlock(_mpb);
    }

    void GetPlaneCoords(Vector3 p, out float u, out float v)
    {
        switch (gridPlane)
        {
            case GridPlane.XY: u = p.x; v = p.y; break;
            case GridPlane.ZY: u = p.z; v = p.y; break;
            default:           u = p.x; v = p.z; break;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (outerRadius < 0f) outerRadius = 0f;
        if (innerRadius < 0f) innerRadius = 0f;
        if (cellOuterRadius < 0f) cellOuterRadius = 0f;
        if (cellInnerRadius < 0f) cellInnerRadius = 0f;
        _emissionPropId = Shader.PropertyToID(emissionPropertyName);
        _dirtyTiles = true;
    }
#endif
}
