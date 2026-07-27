using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 以本物体为父级，缓存其子物体的初始 Transform（位置、旋转、缩放）<b>并持久化到 scene 文件</b>，
/// 基于指定 BoxCollider 或 SphereCollider 的表面为每个子物体生成目标位置。
///
/// 每个 child 的运动路径是一条 quadratic Bezier（起点、控制点、目标点）；
/// 形状由 PathShape 枚举控制；时间维度有独立窗口 (phaseStart, phaseEnd) ⊆ [0,1]。
///
/// Rebuild 阶段会用重试式放置 + Bezier 路径采样做路径间穿插检测；
/// child 半径来自 Collider.bounds.extents.magnitude（多 collider 取 union），
/// 无 Collider 时使用 fallbackRadius。
///
/// <b>关于 starts 的持久化</b>：startPositions / startRotations / startScales 是序列化数组，
/// 配合 cachedChildrenRefs 做"按 Transform 引用复用"。修改任意运行参数（seed、collider、形状等）
/// 触发的 Rebuild <b>不会</b>重新采集 start，所以无论当前 normalizedValue 是多少，
/// 改完参数把 value 调回 0 都能精确回到真实初始 Transform。
///
/// 仅在以下情况会重新采集 start：
///   - 第一次见到某个 child（全新加入）
///   - 用户显式调用 CaptureCurrentAsStarts() / 同名 ContextMenu
///
/// SpaceMode 切换由 ConvertCachedStarts 做就地空间转换，也不会丢初始数据。
///
/// 适用：Unity 2022.3 / URP / 移动 VR。
/// 不支持：CapsuleCollider / MeshCollider / TerrainCollider；Sphere collider 在父级非均匀缩放下退化为椭球（法线为近似）。
/// 注意 1：Scale 始终写入 child.localScale，与 SpaceMode 无关。
/// 注意 2：新增 child 时，建议先 SnapToStart() 让画面回到初始态，再让 Rebuild 自动采集新 child 的 start。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class ChildScatterByCollider : MonoBehaviour
{
    public enum SpaceMode
    {
        World,
        LocalToThisParent
    }

    public enum PathShape
    {
        /// <summary>直线。控制点 = 中点。</summary>
        Straight,
        /// <summary>抛物线。控制点沿世界 up 上拱。</summary>
        ArcUp,
        /// <summary>花瓣发散。控制点沿"目标 Collider 中心 → 路径中点"方向向外。</summary>
        FanOutFromTargetCenter,
        /// <summary>随机垂直扰动。控制点沿"起点→终点"连线的随机垂直方向偏移。</summary>
        PerpendicularRandom
    }

    [Header("Control")]
    [Range(0f, 1f)]
    [Tooltip("归一化控制值。0 = 所有子物体回到初始 Transform；1 = 所有子物体到达目标 Transform。")]
    [SerializeField] private float normalizedValue = 0f;

    [Tooltip("插值曲线。每个 child 在自己的本地时间窗内独立 evaluate。")]
    [SerializeField] private AnimationCurve interpolationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Target Surface")]
    [Tooltip("目标点采样所在的 Collider 表面。支持 BoxCollider 和 SphereCollider。")]
    [SerializeField] private Collider targetCollider;

    [Tooltip("距离表面的偏移厚度，单位米（世界空间）。正=外侧，负=内侧。(X, Y) = (min, max)。")]
    [SerializeField] private Vector2 thicknessRange = Vector2.zero;

    [Tooltip("随机种子。相同 child 顺序、相同 collider、相同 seed 会得到稳定的随机结果。")]
    [SerializeField] private int randomSeed = 12345;

    [Header("Path Shape")]
    [SerializeField] private PathShape pathShape = PathShape.PerpendicularRandom;

    [Tooltip("控制点距中点的偏移量范围（米）。Straight 模式下忽略。")]
    [SerializeField] private Vector2 arcHeight = new Vector2(0.3f, 0.8f);

    [Header("Time Stagger")]
    [SerializeField] private bool useTimeStagger = true;

    [Range(0f, 0.9f)]
    [Tooltip("错峰幅度。0 = 完全同步；0.3 = 各 child 窗口起点在 [0, 0.3] 区间随机，窗口长度 = 1 - 0.3 = 0.7。")]
    [SerializeField] private float maxStagger = 0.3f;

    [Header("Path Collision Avoidance")]
    [SerializeField] private bool avoidPathCrossing = true;

    [Range(4, 64)]
    [Tooltip("沿 Bezier 采样点数。越大越精确，Rebuild 越慢。")]
    [SerializeField] private int crossingSampleCount = 12;

    [Min(1f)]
    [Tooltip("子物体半径安全膨胀系数。1 = 严格用 Collider.bounds。")]
    [SerializeField] private float radiusInflation = 1.0f;

    [Min(0f)]
    [Tooltip("child 无 Collider 时使用的回退半径（米）。")]
    [SerializeField] private float fallbackRadius = 0.1f;

    [Range(1, 64)]
    [Tooltip("单个 child 最大重试次数。超过仍冲突会接受最后一次并打 warning。")]
    [SerializeField] private int maxRetries = 16;

    [Header("Random Rotation")]
    [SerializeField] private bool randomizeRotation = true;
    [SerializeField] private Vector2 randomRotationX = new Vector2(0f, 360f);
    [SerializeField] private Vector2 randomRotationY = new Vector2(0f, 360f);
    [SerializeField] private Vector2 randomRotationZ = new Vector2(0f, 360f);

    [Header("Random Scale")]
    [Tooltip("缩放始终写入 child.localScale，与 SpaceMode 无关。")]
    [SerializeField] private bool randomizeScale = false;
    [SerializeField] private bool uniformScale = true;
    [SerializeField] private Vector2 randomScaleX = new Vector2(1f, 1f);
    [SerializeField] private Vector2 randomScaleY = new Vector2(1f, 1f);
    [SerializeField] private Vector2 randomScaleZ = new Vector2(1f, 1f);

    [Header("Children")]
    [SerializeField] private bool includeInactiveChildren = true;
    [SerializeField] private bool includeNestedChildren = false;
    [SerializeField] private bool excludeTargetColliderHierarchy = true;

    [Header("Transform Space")]
    [SerializeField] private SpaceMode transformSpace = SpaceMode.World;

    [Header("Editor / Timeline Preview")]
    [SerializeField] private bool realtimePreviewInEditor = true;
    [SerializeField] private bool detectAnimatedValueEveryFrame = true;
    [SerializeField] private bool autoRebuildTargetsInEditor = true;
    [SerializeField] private bool drawGizmos = true;

    // ============================================================
    // 持久化 starts（核心修复）
    // 这些字段序列化到 scene 文件，跨 domain reload / scene reopen 保留。
    // ============================================================
    [SerializeField, HideInInspector] private Vector3[] startPositions = System.Array.Empty<Vector3>();
    [SerializeField, HideInInspector] private Quaternion[] startRotations = System.Array.Empty<Quaternion>();
    [SerializeField, HideInInspector] private Vector3[] startScales = System.Array.Empty<Vector3>();
    [SerializeField, HideInInspector] private Transform[] cachedChildrenRefs = System.Array.Empty<Transform>();
    [SerializeField, HideInInspector] private SpaceMode startsSpaceMode = SpaceMode.World;
    [SerializeField, HideInInspector] private bool startsCached = false;

    // ============================================================
    // 运行时派生数据（不持久化，每次 Rebuild 重算）
    // ============================================================
    private readonly List<Transform> children = new List<Transform>(128);

    private Vector3[] targetPositions = System.Array.Empty<Vector3>();
    private Vector3[] controlPoints = System.Array.Empty<Vector3>();
    private Quaternion[] targetRotations = System.Array.Empty<Quaternion>();
    private Vector3[] targetScales = System.Array.Empty<Vector3>();
    private float[] phaseStarts = System.Array.Empty<float>();
    private float[] phaseEnds = System.Array.Empty<float>();
    private float[] childRadii = System.Array.Empty<float>();

    // Rebuild 阶段内循环的 world-space scratch（避免反复 TransformPoint）
    private Vector3[] scratchStartWorld = System.Array.Empty<Vector3>();
    private Vector3[] scratchTargetWorld = System.Array.Empty<Vector3>();
    private Vector3[] scratchControlWorld = System.Array.Empty<Vector3>();

    private bool hasBuilt;
    private float lastAppliedNormalizedValue = float.NaN;

#if UNITY_EDITOR
    private int lastEditorRandomSeed;
    private Collider lastEditorTargetCollider;
    private Vector2 lastEditorThicknessRange;
    private PathShape lastEditorPathShape;
    private Vector2 lastEditorArcHeight;
    private bool lastEditorUseTimeStagger;
    private float lastEditorMaxStagger;
    private bool lastEditorAvoidPathCrossing;
    private int lastEditorCrossingSampleCount;
    private float lastEditorRadiusInflation;
    private float lastEditorFallbackRadius;
    private int lastEditorMaxRetries;
    private bool lastEditorIncludeInactiveChildren;
    private bool lastEditorIncludeNestedChildren;
    private bool lastEditorExcludeTargetColliderHierarchy;
    private SpaceMode lastEditorTransformSpace;
    private bool lastEditorRandomizeRotation;
    private Vector2 lastEditorRotationX;
    private Vector2 lastEditorRotationY;
    private Vector2 lastEditorRotationZ;
    private bool lastEditorRandomizeScale;
    private bool lastEditorUniformScale;
    private Vector2 lastEditorScaleX;
    private Vector2 lastEditorScaleY;
    private Vector2 lastEditorScaleZ;
#endif

    public float NormalizedValue
    {
        get => normalizedValue;
        set
        {
            normalizedValue = Mathf.Clamp01(value);
            Apply(true);
        }
    }

    public int ChildCount => children.Count;

    private void OnEnable()
    {
        if (!hasBuilt) Rebuild();
        else Apply(true);
    }

    private void Update()
    {
        if (!detectAnimatedValueEveryFrame) return;
        if (!Application.isPlaying && !realtimePreviewInEditor) return;
        if (!hasBuilt) TryRebuildSilently();
        ApplyIfValueChanged();
    }

    private void LateUpdate()
    {
        if (!detectAnimatedValueEveryFrame) return;
        if (!Application.isPlaying && !realtimePreviewInEditor) return;
        ApplyIfValueChanged();
    }

    private void OnDidApplyAnimationProperties()
    {
        normalizedValue = Mathf.Clamp01(normalizedValue);
        if (!hasBuilt) TryRebuildSilently();
        Apply(true);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        normalizedValue = Mathf.Clamp01(normalizedValue);
        NormalizeRanges();

        if (Application.isPlaying)
        {
            Apply(true);
            return;
        }

        if (!realtimePreviewInEditor && !autoRebuildTargetsInEditor) return;

        UnityEditor.EditorApplication.delayCall -= DelayedEditorRefresh;
        UnityEditor.EditorApplication.delayCall += DelayedEditorRefresh;
    }

    private void DelayedEditorRefresh()
    {
        if (this == null || Application.isPlaying) return;

        bool targetSettingsChanged = HaveEditorTargetSettingsChanged();

        if (!hasBuilt)
        {
            Rebuild();
            CacheEditorSettings();
            return;
        }

        if (autoRebuildTargetsInEditor && targetSettingsChanged)
        {
            Rebuild();
            CacheEditorSettings();
            return;
        }

        if (realtimePreviewInEditor)
        {
            Apply(true);
            UnityEditor.SceneView.RepaintAll();
        }
    }

    private bool HaveEditorTargetSettingsChanged()
    {
        return lastEditorRandomSeed != randomSeed
               || lastEditorTargetCollider != targetCollider
               || lastEditorThicknessRange != thicknessRange
               || lastEditorPathShape != pathShape
               || lastEditorArcHeight != arcHeight
               || lastEditorUseTimeStagger != useTimeStagger
               || !Mathf.Approximately(lastEditorMaxStagger, maxStagger)
               || lastEditorAvoidPathCrossing != avoidPathCrossing
               || lastEditorCrossingSampleCount != crossingSampleCount
               || !Mathf.Approximately(lastEditorRadiusInflation, radiusInflation)
               || !Mathf.Approximately(lastEditorFallbackRadius, fallbackRadius)
               || lastEditorMaxRetries != maxRetries
               || lastEditorIncludeInactiveChildren != includeInactiveChildren
               || lastEditorIncludeNestedChildren != includeNestedChildren
               || lastEditorExcludeTargetColliderHierarchy != excludeTargetColliderHierarchy
               || lastEditorTransformSpace != transformSpace
               || lastEditorRandomizeRotation != randomizeRotation
               || lastEditorRotationX != randomRotationX
               || lastEditorRotationY != randomRotationY
               || lastEditorRotationZ != randomRotationZ
               || lastEditorRandomizeScale != randomizeScale
               || lastEditorUniformScale != uniformScale
               || lastEditorScaleX != randomScaleX
               || lastEditorScaleY != randomScaleY
               || lastEditorScaleZ != randomScaleZ;
    }

    private void CacheEditorSettings()
    {
        lastEditorRandomSeed = randomSeed;
        lastEditorTargetCollider = targetCollider;
        lastEditorThicknessRange = thicknessRange;
        lastEditorPathShape = pathShape;
        lastEditorArcHeight = arcHeight;
        lastEditorUseTimeStagger = useTimeStagger;
        lastEditorMaxStagger = maxStagger;
        lastEditorAvoidPathCrossing = avoidPathCrossing;
        lastEditorCrossingSampleCount = crossingSampleCount;
        lastEditorRadiusInflation = radiusInflation;
        lastEditorFallbackRadius = fallbackRadius;
        lastEditorMaxRetries = maxRetries;
        lastEditorIncludeInactiveChildren = includeInactiveChildren;
        lastEditorIncludeNestedChildren = includeNestedChildren;
        lastEditorExcludeTargetColliderHierarchy = excludeTargetColliderHierarchy;
        lastEditorTransformSpace = transformSpace;
        lastEditorRandomizeRotation = randomizeRotation;
        lastEditorRotationX = randomRotationX;
        lastEditorRotationY = randomRotationY;
        lastEditorRotationZ = randomRotationZ;
        lastEditorRandomizeScale = randomizeScale;
        lastEditorUniformScale = uniformScale;
        lastEditorScaleX = randomScaleX;
        lastEditorScaleY = randomScaleY;
        lastEditorScaleZ = randomScaleZ;
    }
#endif

    // ============================================================
    // Rebuild：复用优先；只在新 child 或显式失效时才重新采集 start
    // ============================================================

    [ContextMenu("Rebuild / Regenerate All Targets")]
    public void Rebuild()
    {
        if (targetCollider == null)
        {
            Debug.LogWarning($"[{nameof(ChildScatterByCollider)}] Target Collider is null.", this);
            hasBuilt = false;
            return;
        }

        if (!(targetCollider is BoxCollider) && !(targetCollider is SphereCollider))
        {
            Debug.LogWarning($"[{nameof(ChildScatterByCollider)}] Only BoxCollider and SphereCollider are supported.", this);
            hasBuilt = false;
            return;
        }

        NormalizeRanges();
        CollectChildren();
        int count = children.Count;

        // ----- A) SpaceMode 切换：对已缓存 starts 做就地空间转换 -----
        if (startsCached
            && startsSpaceMode != transformSpace
            && startPositions != null && startPositions.Length > 0
            && startRotations != null && startRotations.Length == startPositions.Length
            && startScales != null && startScales.Length == startPositions.Length)
        {
            ConvertCachedStarts(startsSpaceMode, transformSpace);
            startsSpaceMode = transformSpace;
        }

        // ----- B) 构建"上次 child → starts 索引"查找表 -----
        bool canReuse = startsCached
                        && cachedChildrenRefs != null
                        && startPositions != null
                        && startRotations != null
                        && startScales != null
                        && cachedChildrenRefs.Length == startPositions.Length
                        && startPositions.Length == startRotations.Length
                        && startPositions.Length == startScales.Length;

        Dictionary<Transform, int> oldIndexByChild = null;
        Vector3[] oldStartPositions = null;
        Quaternion[] oldStartRotations = null;
        Vector3[] oldStartScales = null;

        if (canReuse)
        {
            oldStartPositions = startPositions;
            oldStartRotations = startRotations;
            oldStartScales = startScales;
            oldIndexByChild = new Dictionary<Transform, int>(cachedChildrenRefs.Length);
            for (int i = 0; i < cachedChildrenRefs.Length; i++)
            {
                Transform t = cachedChildrenRefs[i];
                if (t != null && !oldIndexByChild.ContainsKey(t))
                    oldIndexByChild[t] = i;
            }

            // 强制分配新数组，避免在写回 starts[i] 时读到的是从同数组别位置的旧数据
            startPositions = new Vector3[count];
            startRotations = new Quaternion[count];
            startScales = new Vector3[count];
        }
        else
        {
            EnsureArraySize(ref startPositions, count);
            EnsureArraySize(ref startRotations, count);
            EnsureArraySize(ref startScales, count);
        }

        EnsureArraySize(ref targetPositions, count);
        EnsureArraySize(ref controlPoints, count);
        EnsureArraySize(ref targetRotations, count);
        EnsureArraySize(ref targetScales, count);
        EnsureArraySize(ref phaseStarts, count);
        EnsureArraySize(ref phaseEnds, count);
        EnsureArraySize(ref childRadii, count);
        EnsureArraySize(ref scratchStartWorld, count);
        EnsureArraySize(ref scratchTargetWorld, count);
        EnsureArraySize(ref scratchControlWorld, count);

        Random.State oldState = Random.state;
        Random.InitState(randomSeed);

        Vector3 targetColliderWorldCenter = targetCollider.bounds.center;
        int conflictWarnings = 0;
        int newlyCapturedCount = 0;

        // ----- C) 主循环 -----
        for (int i = 0; i < count; i++)
        {
            Transform child = children[i];

            // 1) starts：复用 or 采集
            if (oldIndexByChild != null && oldIndexByChild.TryGetValue(child, out int oldIdx))
            {
                startPositions[i] = oldStartPositions[oldIdx];
                startRotations[i] = oldStartRotations[oldIdx];
                startScales[i] = oldStartScales[oldIdx];
            }
            else
            {
                startPositions[i] = GetPosition(child);
                startRotations[i] = GetRotation(child);
                startScales[i] = child.localScale;
                newlyCapturedCount++;
            }

            childRadii[i] = ComputeChildRadius(child);

            // 2) rot / scale 目标
            targetRotations[i] = randomizeRotation ? GenerateRandomRotation() : startRotations[i];
            targetScales[i] = randomizeScale
                ? Vector3.Scale(startScales[i], GenerateRandomScaleMultiplier())
                : startScales[i];

            // 3) target 表面采样
            SampleSurfacePoint(targetCollider, out Vector3 surfacePoint, out Vector3 surfaceNormal);
            float thickness = Random.Range(thicknessRange.x, thicknessRange.y);
            Vector3 targetWorld = surfacePoint + surfaceNormal * thickness;

            // 4) 起点世界坐标——必须来自缓存 start，不是 child 当前位置
            Vector3 startWorld = transformSpace == SpaceMode.World
                ? startPositions[i]
                : transform.TransformPoint(startPositions[i]);

            // 5) 重试式放置 control + phase
            Vector3 chosenControlWorld = Vector3.zero;
            float chosenPhaseStart = 0f;
            float chosenPhaseEnd = 1f;
            bool placedClean = false;

            int attempts = avoidPathCrossing ? maxRetries : 1;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                chosenControlWorld = SampleControlPointWorld(startWorld, targetWorld, targetColliderWorldCenter);
                SampleTimeWindow(out chosenPhaseStart, out chosenPhaseEnd);

                if (!avoidPathCrossing || i == 0)
                {
                    placedClean = true;
                    break;
                }

                if (!HasConflictAgainstPlaced(i, startWorld, targetWorld, chosenControlWorld, chosenPhaseStart, chosenPhaseEnd))
                {
                    placedClean = true;
                    break;
                }
            }

            if (!placedClean) conflictWarnings++;

            scratchStartWorld[i] = startWorld;
            scratchTargetWorld[i] = targetWorld;
            scratchControlWorld[i] = chosenControlWorld;

            targetPositions[i] = transformSpace == SpaceMode.World
                ? targetWorld
                : transform.InverseTransformPoint(targetWorld);

            controlPoints[i] = transformSpace == SpaceMode.World
                ? chosenControlWorld
                : transform.InverseTransformPoint(chosenControlWorld);

            phaseStarts[i] = chosenPhaseStart;
            phaseEnds[i] = chosenPhaseEnd;
        }

        Random.state = oldState;

        // ----- D) 更新缓存 -----
        EnsureArraySize(ref cachedChildrenRefs, count);
        for (int i = 0; i < count; i++) cachedChildrenRefs[i] = children[i];
        startsSpaceMode = transformSpace;
        startsCached = true;

        if (conflictWarnings > 0)
        {
            Debug.LogWarning(
                $"[{nameof(ChildScatterByCollider)}] {conflictWarnings}/{count} children could not find a clean path within {maxRetries} retries. " +
                $"Try: lower density, smaller radii, larger arcHeight, more stagger, or higher maxRetries.",
                this);
        }

        if (newlyCapturedCount > 0 && canReuse)
        {
            Debug.Log(
                $"[{nameof(ChildScatterByCollider)}] Captured fresh start for {newlyCapturedCount} new child(ren) from their current transforms. " +
                $"If they appeared at the wrong initial pose, set normalizedValue = 0 before adding new children.",
                this);
        }

        hasBuilt = true;
        Apply(true);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            CacheEditorSettings();
            UnityEditor.SceneView.RepaintAll();
        }
#endif
    }

    [ContextMenu("Regenerate Targets Only")]
    public void RegenerateTargetsOnly()
    {
        if (!hasBuilt) { Rebuild(); return; }
        Rebuild();
    }

    /// <summary>
    /// 强制丢弃缓存 starts，从当前 child transforms 重新采集。
    /// 推荐工作流：先 SnapToStart() 让画面回到初始状态，确认 child 摆放正确，再调用此方法把"现在所见"固化为新 starts。
    /// </summary>
    [ContextMenu("Capture Current As Starts (Discard Cached)")]
    public void CaptureCurrentAsStarts()
    {
        startsCached = false;
        cachedChildrenRefs = System.Array.Empty<Transform>();
        Rebuild();
    }

    /// <summary>
    /// 一键归零：设置 normalizedValue=0 并 Apply。保存场景前调用一次，可以避免 child 当前 transform 序列化为非初始状态。
    /// </summary>
    [ContextMenu("Snap To Start (NormalizedValue = 0)")]
    public void SnapToStart()
    {
        normalizedValue = 0f;
        Apply(true);
    }

    [ContextMenu("Apply Current Normalized Value")]
    public void Apply() => Apply(true);

    public void Apply(bool force)
    {
        normalizedValue = Mathf.Clamp01(normalizedValue);

        if (!force && Mathf.Approximately(lastAppliedNormalizedValue, normalizedValue))
            return;

        if (!hasBuilt || children.Count == 0)
        {
            lastAppliedNormalizedValue = normalizedValue;
            return;
        }

        float globalT = normalizedValue;

        for (int i = 0; i < children.Count; i++)
        {
            Transform child = children[i];
            if (child == null) continue;

            float localT = Mathf.InverseLerp(phaseStarts[i], phaseEnds[i], globalT);
            float t = interpolationCurve != null ? interpolationCurve.Evaluate(localT) : localT;
            t = Mathf.Clamp01(t);

            Vector3 position = EvalQuadraticBezier(startPositions[i], controlPoints[i], targetPositions[i], t);
            Quaternion rotation = Quaternion.SlerpUnclamped(startRotations[i], targetRotations[i], t);
            Vector3 scale = Vector3.LerpUnclamped(startScales[i], targetScales[i], t);

            SetPosition(child, position);
            SetRotation(child, rotation);
            child.localScale = scale;
        }

        lastAppliedNormalizedValue = normalizedValue;
    }

    private void ApplyIfValueChanged()
    {
        normalizedValue = Mathf.Clamp01(normalizedValue);
        Apply(false);
    }

    private void TryRebuildSilently()
    {
        if (targetCollider == null) return;
        Rebuild();
    }

    // ============================================================
    // SpaceMode 切换时对 starts 做就地空间转换（保留真实初始值）
    // ============================================================

    private void ConvertCachedStarts(SpaceMode from, SpaceMode to)
    {
        if (from == to) return;

        Quaternion parentRot = transform.rotation;
        Quaternion parentRotInv = Quaternion.Inverse(parentRot);

        for (int i = 0; i < startPositions.Length; i++)
        {
            if (from == SpaceMode.World && to == SpaceMode.LocalToThisParent)
            {
                startPositions[i] = transform.InverseTransformPoint(startPositions[i]);
                startRotations[i] = parentRotInv * startRotations[i];
            }
            else // LocalToThisParent → World
            {
                startPositions[i] = transform.TransformPoint(startPositions[i]);
                startRotations[i] = parentRot * startRotations[i];
            }
            // localScale 与 SpaceMode 无关，不变
        }
    }

    // ============================================================
    // 路径形状 / 时间窗 / 冲突检测
    // ============================================================

    private Vector3 SampleControlPointWorld(Vector3 startWorld, Vector3 targetWorld, Vector3 referenceCenter)
    {
        Vector3 mid = (startWorld + targetWorld) * 0.5f;
        float height = Random.Range(arcHeight.x, arcHeight.y);

        switch (pathShape)
        {
            case PathShape.Straight:
                return mid;

            case PathShape.ArcUp:
                return mid + Vector3.up * height;

            case PathShape.FanOutFromTargetCenter:
            {
                Vector3 outward = mid - referenceCenter;
                if (outward.sqrMagnitude < 1e-6f) outward = Random.onUnitSphere;
                else outward.Normalize();
                return mid + outward * height;
            }

            case PathShape.PerpendicularRandom:
            {
                Vector3 dir = targetWorld - startWorld;
                float lenSqr = dir.sqrMagnitude;
                if (lenSqr < 1e-6f)
                {
                    return mid + Random.onUnitSphere * height;
                }
                dir /= Mathf.Sqrt(lenSqr);

                Vector3 anyAxis = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
                Vector3 perp1 = Vector3.Cross(dir, anyAxis).normalized;
                Vector3 perp2 = Vector3.Cross(dir, perp1);

                float angle = Random.value * 2f * Mathf.PI;
                Vector3 randPerp = perp1 * Mathf.Cos(angle) + perp2 * Mathf.Sin(angle);
                return mid + randPerp * height;
            }

            default:
                return mid;
        }
    }

    private void SampleTimeWindow(out float pStart, out float pEnd)
    {
        if (!useTimeStagger || maxStagger <= 0f)
        {
            pStart = 0f;
            pEnd = 1f;
            return;
        }

        float windowLen = 1f - maxStagger;
        pStart = Random.value * maxStagger;
        pEnd = pStart + windowLen;
    }

    private bool HasConflictAgainstPlaced(int i,
                                          Vector3 startWorldI, Vector3 targetWorldI, Vector3 controlWorldI,
                                          float pStartI, float pEndI)
    {
        float radiusI = childRadii[i];
        int K = crossingSampleCount;
        float invK = 1f / (K - 1);

        for (int j = 0; j < i; j++)
        {
            Vector3 sj = scratchStartWorld[j];
            Vector3 tj = scratchTargetWorld[j];
            Vector3 cj = scratchControlWorld[j];
            float pStartJ = phaseStarts[j];
            float pEndJ = phaseEnds[j];
            float radiusJ = childRadii[j];

            float radiusSum = radiusI + radiusJ;
            float radiusSqr = radiusSum * radiusSum;

            for (int k = 0; k < K; k++)
            {
                float globalT = k * invK;
                float localTi = Mathf.InverseLerp(pStartI, pEndI, globalT);
                float localTj = Mathf.InverseLerp(pStartJ, pEndJ, globalT);

                Vector3 posI = EvalQuadraticBezier(startWorldI, controlWorldI, targetWorldI, localTi);
                Vector3 posJ = EvalQuadraticBezier(sj, cj, tj, localTj);

                if ((posI - posJ).sqrMagnitude < radiusSqr) return true;
            }
        }

        return false;
    }

    private static Vector3 EvalQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return (u * u) * p0 + (2f * u * t) * p1 + (t * t) * p2;
    }

    // ============================================================
    // 半径量取 / 子物体收集 / Space 工具 / 随机生成 / 范围归一
    // ============================================================

    private float ComputeChildRadius(Transform child)
    {
        var colliders = child.GetComponentsInChildren<Collider>(true);
        if (colliders == null || colliders.Length == 0) return fallbackRadius;

        Bounds combined = default;
        bool hasAny = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null) continue;
            if (c == targetCollider) continue;

            Bounds b = c.bounds;
            if (!hasAny) { combined = b; hasAny = true; }
            else combined.Encapsulate(b);
        }

        if (!hasAny) return fallbackRadius;
        return combined.extents.magnitude * radiusInflation;
    }

    private static void EnsureArraySize<T>(ref T[] array, int size)
    {
        if (array == null || array.Length != size) array = new T[size];
    }

    private void CollectChildren()
    {
        children.Clear();

        if (includeNestedChildren)
        {
            GetComponentsInChildren(includeInactiveChildren, children);
            children.Remove(transform);

            if (excludeTargetColliderHierarchy && targetCollider != null)
            {
                Transform targetRoot = targetCollider.transform;
                children.RemoveAll(child => child == null || child == targetRoot || child.IsChildOf(targetRoot));
            }
        }
        else
        {
            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = transform.GetChild(i);

                if (!includeInactiveChildren && !child.gameObject.activeInHierarchy) continue;

                if (excludeTargetColliderHierarchy && targetCollider != null)
                {
                    Transform targetRoot = targetCollider.transform;
                    if (child == targetRoot || child.IsChildOf(targetRoot)) continue;
                }

                children.Add(child);
            }
        }
    }

    private Vector3 GetPosition(Transform item)
        => transformSpace == SpaceMode.World ? item.position : item.localPosition;

    private void SetPosition(Transform item, Vector3 position)
    {
        if (transformSpace == SpaceMode.World) item.position = position;
        else item.localPosition = position;
    }

    private Quaternion GetRotation(Transform item)
        => transformSpace == SpaceMode.World ? item.rotation : item.localRotation;

    private void SetRotation(Transform item, Quaternion rotation)
    {
        if (transformSpace == SpaceMode.World) item.rotation = rotation;
        else item.localRotation = rotation;
    }

    private Quaternion GenerateRandomRotation()
    {
        Vector3 euler = new Vector3(
            Random.Range(randomRotationX.x, randomRotationX.y),
            Random.Range(randomRotationY.x, randomRotationY.y),
            Random.Range(randomRotationZ.x, randomRotationZ.y));
        return Quaternion.Euler(euler);
    }

    private Vector3 GenerateRandomScaleMultiplier()
    {
        if (uniformScale)
        {
            float u = Random.Range(randomScaleX.x, randomScaleX.y);
            return new Vector3(u, u, u);
        }
        return new Vector3(
            Random.Range(randomScaleX.x, randomScaleX.y),
            Random.Range(randomScaleY.x, randomScaleY.y),
            Random.Range(randomScaleZ.x, randomScaleZ.y));
    }

    private void NormalizeRanges()
    {
        randomRotationX = SortVector2(randomRotationX);
        randomRotationY = SortVector2(randomRotationY);
        randomRotationZ = SortVector2(randomRotationZ);
        randomScaleX = SortVector2(randomScaleX);
        randomScaleY = SortVector2(randomScaleY);
        randomScaleZ = SortVector2(randomScaleZ);
        thicknessRange = SortVector2(thicknessRange);
        arcHeight = SortVector2(arcHeight);
    }

    private static Vector2 SortVector2(Vector2 value)
        => value.x <= value.y ? value : new Vector2(value.y, value.x);

    // ============================================================
    // 表面采样
    // ============================================================

    private static void SampleSurfacePoint(Collider col, out Vector3 worldPoint, out Vector3 worldNormal)
    {
        if (col is BoxCollider box) { SamplePointOnBoxSurface(box, out worldPoint, out worldNormal); return; }
        if (col is SphereCollider sphere) { SamplePointOnSphereSurface(sphere, out worldPoint, out worldNormal); return; }
        worldPoint = col.bounds.center;
        worldNormal = Vector3.up;
    }

    private static void SamplePointOnBoxSurface(BoxCollider box, out Vector3 worldPoint, out Vector3 worldNormal)
    {
        Vector3 size = box.size;
        float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;

        float axArea = size.y * size.z;
        float ayArea = size.x * size.z;
        float azArea = size.x * size.y;
        float total = 2f * (axArea + ayArea + azArea);

        if (total <= Mathf.Epsilon)
        {
            worldPoint = box.transform.TransformPoint(box.center);
            worldNormal = box.transform.up;
            return;
        }

        float r = Random.value * total;
        Vector3 localPoint;
        Vector3 localNormal;

        if (r < axArea)
        { localPoint = box.center + new Vector3(hx, Random.Range(-hy, hy), Random.Range(-hz, hz)); localNormal = Vector3.right; }
        else if (r < 2f * axArea)
        { localPoint = box.center + new Vector3(-hx, Random.Range(-hy, hy), Random.Range(-hz, hz)); localNormal = Vector3.left; }
        else if (r < 2f * axArea + ayArea)
        { localPoint = box.center + new Vector3(Random.Range(-hx, hx), hy, Random.Range(-hz, hz)); localNormal = Vector3.up; }
        else if (r < 2f * axArea + 2f * ayArea)
        { localPoint = box.center + new Vector3(Random.Range(-hx, hx), -hy, Random.Range(-hz, hz)); localNormal = Vector3.down; }
        else if (r < 2f * axArea + 2f * ayArea + azArea)
        { localPoint = box.center + new Vector3(Random.Range(-hx, hx), Random.Range(-hy, hy), hz); localNormal = Vector3.forward; }
        else
        { localPoint = box.center + new Vector3(Random.Range(-hx, hx), Random.Range(-hy, hy), -hz); localNormal = Vector3.back; }

        worldPoint = box.transform.TransformPoint(localPoint);
        Vector3 dir = box.transform.TransformDirection(localNormal);
        float mag = dir.magnitude;
        worldNormal = mag > Mathf.Epsilon ? dir / mag : Vector3.up;
    }

    private static void SamplePointOnSphereSurface(SphereCollider sphere, out Vector3 worldPoint, out Vector3 worldNormal)
    {
        Vector3 dir = Random.onUnitSphere;
        Vector3 localPoint = sphere.center + dir * sphere.radius;
        worldPoint = sphere.transform.TransformPoint(localPoint);
        Vector3 worldDir = sphere.transform.TransformDirection(dir);
        float mag = worldDir.magnitude;
        worldNormal = mag > Mathf.Epsilon ? worldDir / mag : Vector3.up;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || !hasBuilt || children.Count == 0) return;

        const int gizmoSegments = 16;

        for (int i = 0; i < children.Count; i++)
        {
            Vector3 sw = transformSpace == SpaceMode.World ? startPositions[i]  : transform.TransformPoint(startPositions[i]);
            Vector3 tw = transformSpace == SpaceMode.World ? targetPositions[i] : transform.TransformPoint(targetPositions[i]);
            Vector3 cw = transformSpace == SpaceMode.World ? controlPoints[i]   : transform.TransformPoint(controlPoints[i]);

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Vector3 prev = sw;
            for (int s = 1; s <= gizmoSegments; s++)
            {
                float t = s / (float)gizmoSegments;
                Vector3 cur = EvalQuadraticBezier(sw, cw, tw, t);
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }

            // start 位置（青色），与"运行时 child 当前位置"区分开
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.95f);
            Gizmos.DrawWireSphere(sw, 0.03f);

            // 控制点（黄）
            Gizmos.color = new Color(0.95f, 0.85f, 0.2f, 0.65f);
            Gizmos.DrawWireSphere(cw, 0.025f);

            // 终点（橙）
            Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.95f);
            Gizmos.DrawWireSphere(tw, 0.035f);

            if (childRadii != null && childRadii.Length == children.Count)
            {
                Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.2f);
                Gizmos.DrawWireSphere(tw, childRadii[i]);
            }
        }
    }
#endif
}
