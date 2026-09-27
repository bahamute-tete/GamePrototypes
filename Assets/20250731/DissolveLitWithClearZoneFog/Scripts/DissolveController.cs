// Local: bounds-normalized progress. World geometry: position/radius, independent of bounds.
using System.Collections.Generic;
using UnityEngine;


public enum DissolveParameter { Amount, PlaneOffset, Radius, EdgeWidth, EdgeIntensity }

[ExecuteAlways]
[DisallowMultipleComponent]
public class DissolveController : MonoBehaviour
{
    // ---- 枚举 ---------------------------------------------------------------
    public enum DissolveMode  { Noise = 0, Direction = 1, Radial = 2 }
    public enum DissolveSpace { Local = 0, World = 1 }

    // ---- 动画驱动量 ---------------------------------------------------------
    [Range(0f, 1f)]
    [Tooltip("Timeline 或代码驱动的消融进度。Editor 里拖动也能实时预览。")]
    public float amount = 0f;

    // ---- Mode + Space -------------------------------------------------------
    public DissolveMode  mode  = DissolveMode.Noise;

    [Tooltip("Local：按每个 Renderer 的 Bounds 将 Amount 归一化。World：Direction/Radial 按 Origin 和 Radius 定位；Noise 仍使用 Amount。")]
    public DissolveSpace space = DissolveSpace.World;

    [Tooltip("World Direction / Radial 的定位物体。只读取世界位置，不读取旋转或缩放。")]
    public Transform worldOrigin;
    [Tooltip("世界平面沿 Direction 移动的距离（米），相对于 Origin，可为负数。")]
    public float planeOffset;
    [Min(0f)] public float DissolveEdgeIntensity = 3f;
    [Min(0f), Tooltip("World Radial 的球半径，单位为世界米；与定位物体缩放无关。")]
    public float radius = 1f;
    [Tooltip("反转 Direction 模式的消失侧。")]
    public bool directionReverse;
    [Min(0f), Tooltip("Direction / Radial：Local 为局部长度，World 为米。Noise 为噪声阈值宽度。0 关闭边缘发光。")]
    public float DissolveEdgeWidth = .1f;
    [ColorUsage(true, true)]
    public Color DissolveEdgeColor = new Color(1f, .5f, .1f, 1f);
    public bool UsesWorldGeometry => space == DissolveSpace.World && mode != DissolveMode.Noise;
    public bool UsesAmount => !UsesWorldGeometry;
    // ---- Noise 模式 ---------------------------------------------------------
    [Range(0.001f, 5f)]
    [Tooltip("Noise 频率。值越大噪点越密。")]
    public float noiseScale = 2.0f;

    [Tooltip("噪声贴图 (R 通道)。置空时走过程化 ValueNoise。\n" +
             "Triplanar 等权三向投影采样,3 个 tap。\n" +
             "美术能直接给出想要的消融图案。")]
    public Texture2D noiseTexture;

    [Range(0f, 1f)]
    [Tooltip("Axis / Radial 的边缘扰动强度，0 保持原来的平直边缘。共用上面的 Noise 贴图、频率和坐标空间；Noise 模式不受此参数影响。")]
    public float edgeNoiseStrength = 0f;

    // ---- Axis 模式 ----------------------------------------------------------
    [Tooltip("方向。\n" +
             "Space=Local: 各 renderer 自身本地方向 (0,1,0)=各自的 +Y\n" +
             "Space=World: 世界方向 (0,1,0)=世界 +Y, 整组共享")]
    public Vector3 axisDirection = Vector3.up;

    // ---- Radial 模式 --------------------------------------------------------
    [Tooltip("勾选后从外向内消失 (球状物体推荐)")]
    public bool radialReverse = false;

    // ---- 受控对象 -----------------------------------------------------------
    public List<Renderer> controlledRenderers = new List<Renderer>();

    // ---- 末态处理 -----------------------------------------------------------
    [Range(0.9f, 1.0f)]
    [Tooltip("amount 超此阈值时自动关 Renderer.enabled, 彻底消除 A2C 残留点。\n" +
             "回落时自动恢复。设为 1 = 不自动关。")]
    public float hideThreshold = 0.995f;

    [Tooltip("自动管理 Renderer 显隐。Amount 使用 Hide Threshold；World Direction/Radial 使用完整 Bounds 和边缘余量判断。")]
    public bool autoToggleRenderer = true;
    [Min(0f), Tooltip("World 完全消失剔除的额外安全距离（米）。为屏幕导数抗锯齿保留余量；远距离或很宽的 AA 边缘可增大。不会改变 Shader 溶解边界。")]
    public float worldCullPadding = 0.1f;

    // ---- Shader Property IDs ------------------------------------------------
    static readonly int ID_Amount        = Shader.PropertyToID("_DissolveAmount");
    static readonly int ID_Mode          = Shader.PropertyToID("_DissolveMode");
    static readonly int ID_Space         = Shader.PropertyToID("_DissolveSpace");
    static readonly int ID_EdgeNoiseStrength = Shader.PropertyToID("_DissolveEdgeNoiseStrength");
    static readonly int ID_NoiseScale    = Shader.PropertyToID("_DissolveNoiseScale");
    static readonly int ID_NoiseTex      = Shader.PropertyToID("_DissolveNoiseTex");
    static readonly int ID_UseNoiseTex   = Shader.PropertyToID("_DissolveUseNoiseTex");
    static readonly int ID_Axis          = Shader.PropertyToID("_DissolveAxis");
    static readonly int ID_AxisCenter    = Shader.PropertyToID("_DissolveAxisCenter");
    static readonly int ID_Radial        = Shader.PropertyToID("_DissolveRadial");
    static readonly int ID_RadialReverse = Shader.PropertyToID("_DissolveRadialReverse");
    static readonly int ID_SpatialMode = Shader.PropertyToID("_DissolveSpatialMode");
    static readonly int ID_EdgeUnits = Shader.PropertyToID("_DissolveEdgeUnits");
    static readonly int ID_EdgeWidth = Shader.PropertyToID("_DissolveEdgeWidth");
    static readonly int ID_EdgeColor = Shader.PropertyToID("_DissolveEdgeColor");
    static readonly int ID_EdgeIntensity = Shader.PropertyToID("_DissolveEdgeIntensity");
    [Tooltip("按帧重新读取 Bounds，不计算蒙皮顶点。仅归一化 Direction/Radial 需要。蒙皮 Bounds 需由 SkinnedMeshRenderer 更新；新 World 几何模式不依赖 Bounds。")]
    public bool refreshAnimatedBounds = false;

    sealed class TargetCache
    {
        public Renderer renderer;
        public MeshFilter meshFilter;
        public SkinnedMeshRenderer skinned;
        public Mesh mesh;
        public Bounds localBounds;
        public Matrix4x4 matrix;
        public Vector4 axis, radial;
        public float axisCenter;
    }

    MaterialPropertyBlock _mpb;
    readonly List<TargetCache> _targets = new List<TargetCache>();
    readonly Dictionary<Renderer, bool> _originalRendererStates = new Dictionary<Renderer, bool>();
    readonly List<Renderer> _releasedRenderers = new List<Renderer>();
    readonly HashSet<Renderer> _targetSet = new HashSet<Renderer>();
    bool _shapeDirty = true, _boundsDirty = true;
    float _lastAmount = float.NaN, _lastHideThreshold = float.NaN;
    bool _lastAutoToggleRenderer;
    int _lastBoundsFrame = -1;
    DissolveMode _lastMode;
    DissolveSpace _lastSpace;
    float _lastNoiseScale;
    float _lastEdgeNoiseStrength;
    Texture2D _lastNoiseTexture;
    Vector3 _lastAxisDirection;
    bool _lastRadialReverse;
    bool _lastWorldGeometry, _lastDirectionReverse;
    Transform _lastOrigin;
    Vector3 _lastOriginPosition;
    float _lastRadius, _lastEdgeWidth;
    float _lastPlaneOffset, _lastEdgeIntensity;
    Color _lastEdgeColor;

    static readonly int ID_Enabled = Shader.PropertyToID("_DissolveEnabled");
    static readonly int ID_CoverageWidth = Shader.PropertyToID("_DissolveCoverageWidth");
    readonly System.Collections.Generic.List<Material> _visibilityMaterials = new System.Collections.Generic.List<Material>();
    bool CanHideForDissolve(Renderer renderer)
    {
        renderer.GetSharedMaterials(_visibilityMaterials);
        foreach (var material in _visibilityMaterials)
            if (material != null && material.HasProperty(ID_Enabled) && material.GetFloat(ID_Enabled) <= 0.5f)
                return false; // A non-dissolving submesh must remain visible.
        return true;
    }

    // Use a conservative world AABB, never a center-only test. This also remains
    // available while the Renderer is disabled, allowing movement to restore it.
    bool WorldFullyDissolved(TargetCache target)
    {
        if (!worldOrigin) return false;
        var r = target.renderer;
        if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) return false;
        Bounds bounds = r.bounds;
        if (target.skinned != null)
        {
            // Use the authored animation envelope rather than an invisible skinned
            // renderer's possibly stale last visible frame. localBounds must contain
            // the animation, as required for Unity's own frustum culling.
            Bounds local = target.skinned.localBounds;
            Matrix4x4 m = r.localToWorldMatrix;
            Vector3 x = m.MultiplyVector(new Vector3(local.extents.x, 0, 0));
            Vector3 y = m.MultiplyVector(new Vector3(0, local.extents.y, 0));
            Vector3 z = m.MultiplyVector(new Vector3(0, 0, local.extents.z));
            Vector3 e = new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x), Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y), Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
            bounds = new Bounds(m.MultiplyPoint3x4(local.center), e * 2);
        }
        Vector3 center = bounds.center, extents = bounds.extents;
        if (!Finite(center.x) || !Finite(center.y) || !Finite(center.z) || !Finite(extents.x) || !Finite(extents.y) || !Finite(extents.z)) return false;
        float maximumDistance;
        if (mode == DissolveMode.Direction)
        {
            Vector3 n = new Vector3(target.axis.x, target.axis.y, target.axis.z);
            maximumDistance = Vector3.Dot(center, n) - target.axisCenter
                + Mathf.Abs(n.x)*extents.x + Mathf.Abs(n.y)*extents.y + Mathf.Abs(n.z)*extents.z;
        }
        else
        {
            Vector3 origin = worldOrigin.position;
            if (radialReverse)
                maximumDistance = Mathf.Max(0, radius) - Vector3.Distance(origin, bounds.ClosestPoint(origin));
            else
            {
                Vector3 delta = center - origin;
                Vector3 farthest = new Vector3(Mathf.Abs(delta.x), Mathf.Abs(delta.y), Mathf.Abs(delta.z)) + extents;
                maximumDistance = farthest.magnitude - Mathf.Max(0, radius);
            }
        }
        float padding = Mathf.Max(0, worldCullPadding), width = 0;
        if (!Finite(maximumDistance) || maximumDistance >= -padding) return false;
        r.GetSharedMaterials(_visibilityMaterials);
        if (_visibilityMaterials.Count == 0) return false;
        foreach (var material in _visibilityMaterials)
        {
            // Do not hide unrelated or non-dissolving submeshes on a mixed renderer.
            if (!material || material.shader.name != "Custom/LiangZhu/Opaque_Dissolve_Lit" || material.GetFloat(ID_Enabled) <= .5f) return false;
            width = Mathf.Max(width, material.GetFloat(ID_CoverageWidth));
        }
        r.GetPropertyBlock(_mpb);
        if (_mpb.HasFloat(ID_Enabled) && _mpb.GetFloat(ID_Enabled) <= .5f) return false;
        if (_mpb.HasFloat(ID_CoverageWidth)) width = _mpb.GetFloat(ID_CoverageWidth);
        // Per-material-index property blocks can override this controller's block.
        // Their semantics are unknown here, so keep that renderer on the GPU.
        for (int i = 0; i < _visibilityMaterials.Count; i++)
        {
            r.GetPropertyBlock(_worldSubmeshBlock, i);
            if (!_worldSubmeshBlock.isEmpty) return false;
        }
        float noiseMargin = Mathf.Max(0, edgeNoiseStrength);
        if (noiseMargin > 0 && noiseTexture)
        {
            // World noise does not saturate HDR/signed textures; their range cannot
            // be inferred without reading texture data. Keep rendering in that case.
            string format = noiseTexture.graphicsFormat.ToString();
            if (!format.EndsWith("_UNorm") && !format.EndsWith("_SRGB")) return false;
        }
        float margin = noiseMargin + Mathf.Max(0, width) * .5f + padding;
        return Finite(maximumDistance) && Finite(margin) && maximumDistance < -margin;
    }
    MaterialPropertyBlock _worldSubmeshBlock;
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public void SetAmount(float a) { amount = Mathf.Clamp01(a); Apply(); }
    public DissolveParameter RecommendedParameter => UsesAmount ? DissolveParameter.Amount : mode == DissolveMode.Direction ? DissolveParameter.PlaneOffset : DissolveParameter.Radius;
    public bool SupportsParameter(DissolveParameter p) => p == DissolveParameter.Amount ? UsesAmount : p == DissolveParameter.PlaneOffset ? UsesWorldGeometry && mode == DissolveMode.Direction : p == DissolveParameter.Radius ? UsesWorldGeometry && mode == DissolveMode.Radial : true;
    public static string ParameterFieldName(DissolveParameter p) => p == DissolveParameter.Amount ? "amount" : p == DissolveParameter.PlaneOffset ? "planeOffset" : p == DissolveParameter.Radius ? "radius" : p == DissolveParameter.EdgeWidth ? "DissolveEdgeWidth" : "DissolveEdgeIntensity";
    public float GetParameter(DissolveParameter p) => p == DissolveParameter.Amount ? amount : p == DissolveParameter.PlaneOffset ? planeOffset : p == DissolveParameter.Radius ? radius : p == DissolveParameter.EdgeWidth ? DissolveEdgeWidth : DissolveEdgeIntensity;
    public void SetParameter(DissolveParameter p, float value)
    {
        SetParameterValue(p, value);
        Apply();
    }
    // Timeline accumulates values in the existing fields. The graph coordinator
    // commits after every dissolve track has participated in this evaluation.
    internal void SetTimelineParameter(DissolveParameter p, float value)
    {
        SetParameterValue(p, value);
    }
    internal void FlushTimelineParameters() { Apply(); }
#if UNITY_EDITOR
    public int RendererPropertyWriteCount { get; private set; }
#endif
    void SetParameterValue(DissolveParameter p, float value)
    {
        switch (p)
        {
            case DissolveParameter.Amount: amount = Mathf.Clamp01(value); break;
            case DissolveParameter.PlaneOffset: planeOffset = value; break;
            case DissolveParameter.Radius: radius = Mathf.Max(0, value); break;
            case DissolveParameter.EdgeWidth: DissolveEdgeWidth = Mathf.Max(0, value); break;
            case DissolveParameter.EdgeIntensity: DissolveEdgeIntensity = Mathf.Max(0, value); break;
        }
    }
    public void ForceRefresh()
    {
        _boundsDirty = _shapeDirty = true;
        _lastAmount = float.NaN;
        Apply();
    }
    void OnEnable() { ForceRefresh(); }
    // Defer Unity object access until the main-thread update.
    void OnValidate() { _boundsDirty = _shapeDirty = true; _lastAmount = float.NaN; }
    void OnDisable() { RestoreRendererStates(); }
    void LateUpdate() { Apply(); }

    void RestoreRendererStates()
    {
        foreach (var entry in _originalRendererStates)
            if (entry.Key != null) entry.Key.enabled = entry.Value;
        _originalRendererStates.Clear();
    }

    bool SettingsChanged()
    {
        return mode != _lastMode || space != _lastSpace || noiseScale != _lastNoiseScale || edgeNoiseStrength != _lastEdgeNoiseStrength
            || noiseTexture != _lastNoiseTexture || axisDirection != _lastAxisDirection
            || radialReverse != _lastRadialReverse

            || UsesWorldGeometry != _lastWorldGeometry || worldOrigin != _lastOrigin
            || (worldOrigin != null && worldOrigin.position != _lastOriginPosition)
            || radius != _lastRadius || directionReverse != _lastDirectionReverse
            || DissolveEdgeWidth != _lastEdgeWidth || DissolveEdgeColor != _lastEdgeColor
            || planeOffset != _lastPlaneOffset || DissolveEdgeIntensity != _lastEdgeIntensity;
    }

    void UpdateTargets()
    {
        bool changed = _targets.Count != controlledRenderers.Count;
        if (!changed)
            for (int i = 0; i < _targets.Count; i++)
                if (!ReferenceEquals(_targets[i].renderer, controlledRenderers[i])) { changed = true; break; }
        if (changed)
        {
            _targets.Clear();
            _targetSet.Clear();
            foreach (var r in controlledRenderers)
            {
                _targets.Add(new TargetCache {
                    renderer = r,
                    meshFilter = r != null ? r.GetComponent<MeshFilter>() : null,
                    skinned = r as SkinnedMeshRenderer
                });
                if (r != null) _targetSet.Add(r);
            }
            _releasedRenderers.Clear();
            foreach (var entry in _originalRendererStates)
                if (entry.Key == null || !_targetSet.Contains(entry.Key)) _releasedRenderers.Add(entry.Key);
            foreach (var r in _releasedRenderers)
            {
                if (r != null) r.enabled = _originalRendererStates[r];
                _originalRendererStates.Remove(r);
            }
            _boundsDirty = _shapeDirty = true;
        }

        bool needsBounds = mode != DissolveMode.Noise && !UsesWorldGeometry;

        bool animate = needsBounds && refreshAnimatedBounds && (!Application.isPlaying || _lastBoundsFrame != Time.frameCount);
        if (animate) { _boundsDirty = true; _lastBoundsFrame = Time.frameCount; }
        foreach (var target in _targets)
        {
            if (target.renderer == null) continue;

            if (!needsBounds) continue;
            Mesh mesh = target.meshFilter != null ? target.meshFilter.sharedMesh
                      : target.skinned != null ? target.skinned.sharedMesh : null;
            if (_boundsDirty || mesh != target.mesh)
            {
                target.mesh = mesh;
                target.localBounds = target.skinned != null ? target.skinned.localBounds
                    : mesh != null ? mesh.bounds : new Bounds(Vector3.zero, Vector3.one);
                _shapeDirty = true;
            }
        }
        _boundsDirty = false;
    }

    void RefreshShapeCache()
    {
        Vector3 axis = axisDirection.sqrMagnitude > 1e-6f ? axisDirection.normalized : Vector3.up;
        if (directionReverse) axis = -axis;
        foreach (var target in _targets)
        {
            if (target.renderer == null) continue;
            if (UsesWorldGeometry)
            {
                Vector3 origin = worldOrigin != null ? worldOrigin.position : Vector3.zero;
                target.axis = new Vector4(axis.x, axis.y, axis.z, 1f);
                target.axisCenter = Vector3.Dot(origin, axis) + (directionReverse ? -planeOffset : planeOffset);
                target.radial = new Vector4(origin.x, origin.y, origin.z, Mathf.Max(0f, radius));
                continue;
            }
            if (mode == DissolveMode.Direction)
            {
                Vector3 ext = target.localBounds.extents;
                float extent = Mathf.Abs(axis.x) * ext.x + Mathf.Abs(axis.y) * ext.y + Mathf.Abs(axis.z) * ext.z;
                target.axis = new Vector4(axis.x, axis.y, axis.z, extent);
                target.axisCenter = Vector3.Dot(target.localBounds.center, axis);
            }
            else if (mode == DissolveMode.Radial)
            {
                Vector3 center = target.localBounds.center;
                float radius = ComputeMaxDistanceFromCenterToCorners(target.localBounds, center);
                target.radial = new Vector4(center.x, center.y, center.z, radius);
            }
        }
        _lastMode = mode; _lastSpace = space; _lastNoiseScale = noiseScale; _lastEdgeNoiseStrength = edgeNoiseStrength;
        _lastNoiseTexture = noiseTexture; _lastAxisDirection = axisDirection;
        _lastRadialReverse = radialReverse;

        _lastWorldGeometry = UsesWorldGeometry; _lastOrigin = worldOrigin;
        _lastOriginPosition = worldOrigin != null ? worldOrigin.position : Vector3.zero;
        _lastRadius = radius; _lastDirectionReverse = directionReverse;
        _lastEdgeWidth = DissolveEdgeWidth; _lastEdgeColor = DissolveEdgeColor; _lastPlaneOffset = planeOffset; _lastEdgeIntensity = DissolveEdgeIntensity;
    }

    void Apply()
    {
        if (SettingsChanged())
        {
            if (mode != _lastMode || space != _lastSpace || UsesWorldGeometry != _lastWorldGeometry) _boundsDirty = true;
            _shapeDirty = true;
        }
        UpdateTargets();
        bool updateShape = _shapeDirty;
        float clamped = Mathf.Clamp01(amount);
        bool updateAmount = clamped != _lastAmount;
        // World bounds/materials can change without any dissolve parameter changing.
        bool updateVisibility = (UsesWorldGeometry && autoToggleRenderer) || updateShape || updateAmount || autoToggleRenderer != _lastAutoToggleRenderer || hideThreshold != _lastHideThreshold || (UsesAmount && autoToggleRenderer && hideThreshold < 1f && clamped >= hideThreshold);
        if (!updateShape && !updateAmount && !updateVisibility) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        if (_worldSubmeshBlock == null) _worldSubmeshBlock = new MaterialPropertyBlock();
        if (updateShape) RefreshShapeCache();
        if (!autoToggleRenderer) RestoreRendererStates();
        bool shouldHide = UsesAmount && hideThreshold < 1f && clamped >= hideThreshold;
        foreach (var target in _targets)
        {
            var r = target.renderer;
            if (r == null) continue;
            if (updateShape || updateAmount)
            {
                // Read before merging: preserve other components' color/emission overrides.
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(ID_Amount, clamped);
                if (updateShape)
                {
                    _mpb.SetFloat(ID_Mode, (float)mode);
                    _mpb.SetFloat(ID_Space, (float)space);
                    // 0 = legacy/normalized; 1 = world geometry; 2 = missing origin (visible, no clipping).
                    _mpb.SetFloat(ID_SpatialMode, UsesWorldGeometry ? (worldOrigin != null ? 1f : 2f) : 0f);
                    _mpb.SetFloat(ID_EdgeUnits, 1f);
                    _mpb.SetFloat(ID_EdgeWidth, Mathf.Max(0f, DissolveEdgeWidth));
                    _mpb.SetColor(ID_EdgeColor, DissolveEdgeColor);
                    _mpb.SetFloat(ID_EdgeIntensity, Mathf.Max(0f, DissolveEdgeIntensity));
                    _mpb.SetFloat(ID_NoiseScale, noiseScale);
                    _mpb.SetFloat(ID_EdgeNoiseStrength, Mathf.Clamp01(edgeNoiseStrength));
                    _mpb.SetFloat(ID_UseNoiseTex, noiseTexture != null ? 1f : 0f);
                    if (noiseTexture != null) _mpb.SetTexture(ID_NoiseTex, noiseTexture);
                    _mpb.SetFloat(ID_RadialReverse, radialReverse ? 1f : 0f);
                    if (mode == DissolveMode.Direction)
                    {
                        _mpb.SetVector(ID_Axis, target.axis);
                        _mpb.SetFloat(ID_AxisCenter, target.axisCenter);
                    }
                    else if (mode == DissolveMode.Radial) _mpb.SetVector(ID_Radial, target.radial);
                }
                r.SetPropertyBlock(_mpb);
#if UNITY_EDITOR
                RendererPropertyWriteCount++;
#endif
            }
            if (autoToggleRenderer && updateVisibility)
            {
                if (!_originalRendererStates.TryGetValue(r, out bool originalEnabled))
                {
                    originalEnabled = r.enabled;
                    _originalRendererStates.Add(r, originalEnabled);
                }
                bool hidden = UsesWorldGeometry ? WorldFullyDissolved(target) : shouldHide && CanHideForDissolve(r);
                bool visible = originalEnabled && !hidden;
                if (r.enabled != visible) r.enabled = visible;
            }
        }
        _shapeDirty = false;
        _lastAmount = clamped;
        _lastAutoToggleRenderer = autoToggleRenderer;
        _lastHideThreshold = hideThreshold;
    }


    static float ComputeMaxDistanceFromCenterToCorners(Bounds b, Vector3 center)
    {
        Vector3 mn = b.min, mx = b.max;
        float d = 0f;
        for (int xi = 0; xi < 2; xi++)
        for (int yi = 0; yi < 2; yi++)
        for (int zi = 0; zi < 2; zi++)
        {
            Vector3 c = new Vector3(xi == 0 ? mn.x : mx.x,
                                     yi == 0 ? mn.y : mx.y,
                                     zi == 0 ? mn.z : mx.z);
            d = Mathf.Max(d, (c - center).magnitude);
        }
        return Mathf.Max(d, 1e-4f);
    }

    // ---- 编辑器便利 ---------------------------------------------------------
#if UNITY_EDITOR
    [ContextMenu("Auto-Find Renderers In Children")]
    void AutoFindRenderers()
    {
        var found = GetComponentsInChildren<Renderer>(true);
        controlledRenderers.Clear();
        controlledRenderers.AddRange(found);
        UnityEditor.EditorUtility.SetDirty(this);
        ForceRefresh();
    }

    [ContextMenu("Clear Renderers")]
    void ClearRenderers()
    {
        RestoreRendererStates();
        controlledRenderers.Clear();
        UnityEditor.EditorUtility.SetDirty(this);
    }

    void OnDrawGizmosSelected()
    {
        if (UsesWorldGeometry)
        {
            if (worldOrigin == null) return;
            Vector3 dir = axisDirection.sqrMagnitude > 1e-6f ? axisDirection.normalized : Vector3.up;
            Vector3 p = worldOrigin.position + (mode == DissolveMode.Direction ? dir * planeOffset : Vector3.zero);
            Gizmos.color = new Color(0f, 1f, 1f, .8f);
            if (mode == DissolveMode.Radial) Gizmos.DrawWireSphere(p, Mathf.Max(0f, radius));
            else
            {
                Vector3 n = axisDirection.sqrMagnitude > 1e-6f ? axisDirection.normalized : Vector3.up;
                if (directionReverse) n = -n;
                Vector3 u = Vector3.Cross(n, Mathf.Abs(n.y) < .9f ? Vector3.up : Vector3.right).normalized;
                Vector3 v = Vector3.Cross(n, u);
                Gizmos.DrawLine(p-u-v,p+u-v); Gizmos.DrawLine(p+u-v,p+u+v);
                Gizmos.DrawLine(p+u+v,p-u+v); Gizmos.DrawLine(p-u+v,p-u-v);
                Gizmos.DrawLine(p,p+n);
            }
            return;
        }
    }
#endif
}
