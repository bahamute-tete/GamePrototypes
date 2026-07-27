using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

[CustomEditor(typeof(SplineCurveMoveClip))]
public class SplineCurveMoveClipEditor : Editor
{
    private SerializedProperty templateProp;
    private SerializedProperty pathEventsProp;
    private SerializedProperty curveProp;
    private SerializedProperty useAnimSpeedSyncProp;
    private SerializedProperty animWalkSpeedProp;
    private SerializedProperty refFrameProp;  // Phase 4：参考系

    // ─── Pause Manager 编辑状态 ───
    // 用户在 Pause Manager 里编辑每个停顿的 (起始秒, 时长秒) 时，先存到这个列表里，
    // 不立即写回 curve。点"应用"按钮才重建 curve。
    private System.Collections.Generic.List<CurvePauseQuickEdit.PauseSegment> _editPauses;
    private int _editPausesSourceKeyHash;  // 用来检测 curve 外部变化，触发重新加载

    // 输入框值用【秒】存储；EditorPrefs 持久化
    private const string PREF_PAUSE_START_SEC = "SCMC.PauseStartSec";
    private const string PREF_PAUSE_DUR_SEC   = "SCMC.PauseDurationSec";
    private const string PREF_EXTEND_CLIP     = "SCMC.ExtendClipOnInsert";
    private const string PREF_JOIN_STYLE      = "SCMC.PauseJoinStyle";
    private const string PREF_PRESERVE_SPEED  = "SCMC.PauseAdjustPreserveSpeed";
    private const string PREF_SHOW_OTHER      = "SCMC.ShowOtherFields";
    private const string PREF_SHOW_PAUSE_MGR  = "SCMC.ShowPauseManager";

    // ─── 顶层 Foldout 持久化（用户体验优化版重组） ───
    private const string PREF_FOLD_REF_FRAME = "SCMC.FoldRefFrame";
    private const string PREF_FOLD_DISP      = "SCMC.FoldDisplacement";
    private const string PREF_FOLD_PAUSES    = "SCMC.FoldPauses";
    private const string PREF_FOLD_EVENTS    = "SCMC.FoldEvents";
    private const string PREF_FOLD_LOCATE    = "SCMC.FoldLocate";
    // 曲线参数 Foldout 复用已有的 PREF_SHOW_OTHER，避免老用户已有的折叠状态被重置

    private float PauseStartSec
    {
        get => EditorPrefs.GetFloat(PREF_PAUSE_START_SEC, 0f);
        set => EditorPrefs.SetFloat(PREF_PAUSE_START_SEC, value);
    }
    private float PauseDurationSec
    {
        get => EditorPrefs.GetFloat(PREF_PAUSE_DUR_SEC, 1f);
        set => EditorPrefs.SetFloat(PREF_PAUSE_DUR_SEC, value);
    }
    /// <summary>true = 插入停顿时同步延长 TimelineClip.duration，保持非停顿段速度不变（默认）</summary>
    private bool ExtendClipOnInsert
    {
        get => EditorPrefs.GetBool(PREF_EXTEND_CLIP, true);
        set => EditorPrefs.SetBool(PREF_EXTEND_CLIP, value);
    }
    /// <summary>插入停顿时曲线衔接的 tangent 风格（Linear / Smooth），EditorPrefs 持久化</summary>
    private CurvePauseQuickEdit.JoinStyle PauseJoinStyle
    {
        get => (CurvePauseQuickEdit.JoinStyle)EditorPrefs.GetInt(PREF_JOIN_STYLE, (int)CurvePauseQuickEdit.JoinStyle.Smooth);
        set => EditorPrefs.SetInt(PREF_JOIN_STYLE, (int)value);
    }
    /// <summary>
    /// 停顿调整模式开关：
    ///   true（默认）= 保持速度模式，应用时用 RebuildFromScratch（重算 progressValue → 所有行走段统一速度）
    ///   false = 普通模式，应用时用 RebuildPreservingProgress（保留 progressValue → 停顿水平平移，前后段速度变化）
    /// </summary>
    private bool PauseAdjustPreserveSpeed
    {
        get => EditorPrefs.GetBool(PREF_PRESERVE_SPEED, true);
        set => EditorPrefs.SetBool(PREF_PRESERVE_SPEED, value);
    }
    private bool ShowOtherFields
    {
        get => EditorPrefs.GetBool(PREF_SHOW_OTHER, true);
        set => EditorPrefs.SetBool(PREF_SHOW_OTHER, value);
    }
    private bool ShowPauseManager
    {
        get => EditorPrefs.GetBool(PREF_SHOW_PAUSE_MGR, false);
        set => EditorPrefs.SetBool(PREF_SHOW_PAUSE_MGR, value);
    }

    // ─── 顶层 Foldout 持久化访问器 ───
    private bool FoldRefFrame    { get => EditorPrefs.GetBool(PREF_FOLD_REF_FRAME, true);  set => EditorPrefs.SetBool(PREF_FOLD_REF_FRAME, value); }
    private bool FoldDisplacement{ get => EditorPrefs.GetBool(PREF_FOLD_DISP, true);       set => EditorPrefs.SetBool(PREF_FOLD_DISP, value); }
    private bool FoldPauses      { get => EditorPrefs.GetBool(PREF_FOLD_PAUSES, false);    set => EditorPrefs.SetBool(PREF_FOLD_PAUSES, value); }
    private bool FoldEvents      { get => EditorPrefs.GetBool(PREF_FOLD_EVENTS, false);    set => EditorPrefs.SetBool(PREF_FOLD_EVENTS, value); }
    private bool FoldLocate      { get => EditorPrefs.GetBool(PREF_FOLD_LOCATE, true);     set => EditorPrefs.SetBool(PREF_FOLD_LOCATE, value); }
    // 曲线参数 Foldout 沿用 ShowOtherFields，老用户已有的状态自动迁移

    private double _lastObservedDirectorTime = double.MinValue;

    private void OnEnable()
    {
        templateProp   = serializedObject.FindProperty("template");
        pathEventsProp = serializedObject.FindProperty("pathEvents");
        useAnimSpeedSyncProp = serializedObject.FindProperty("useAnimationSpeedSync");
        animWalkSpeedProp    = serializedObject.FindProperty("animationWalkSpeed");
        refFrameProp = serializedObject.FindProperty("referenceFrame");  // Phase 4
        if (templateProp != null)
            curveProp = templateProp.FindPropertyRelative("displacementCurve");

        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    /// <summary>
    /// 监听 Timeline director.time 变化，触发 Inspector 重绘以更新 playhead 红线。
    /// 仅在 time 真实变化时 Repaint，不每帧无脑刷。
    /// </summary>
    private void OnEditorUpdate()
    {
        var dir = UnityEditor.Timeline.TimelineEditor.inspectedDirector;
        if (dir == null) return;
        if (System.Math.Abs(dir.time - _lastObservedDirectorTime) > 1e-6)
        {
            _lastObservedDirectorTime = dir.time;
            Repaint();
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var clip = target as SplineCurveMoveClip;
        if (clip == null) { base.OnInspectorGUI(); return; }

        var info = TimelineClipContext.Resolve(clip);

        EditorGUILayout.LabelField("Spline Curve Move Clip", EditorStyles.boldLabel);
        DrawClipInfoBar(info);

        // [已停用] 基线漂移检测 + 重新校准（Pause Duration Recalibration）。
        // 端点拖拽 remap（OnClipChanged）已自动保持停顿绝对位置，不再需要手动校准横幅。
        // 漂移警告横幅：始终在顶部、Foldout 之外渲染——警告类元素必须可见，
        // 不应埋进可能被折叠的 Pauses Foldout 里。无漂移时该方法静默 return。
        //DrawBaselineDriftBanner(clip, info);
        // ──────────────────────────────────────────────────────────────────
        // 控制点定位 (Locate Control Points)
        // 从本 Clip 反查 SplineCurveFromTransforms 里的控制点 GameObject，一键 ping 到 Hierarchy。
        // ──────────────────────────────────────────────────────────────────
        FoldLocate = EditorGUILayout.Foldout(FoldLocate, "🎯 控制点定位 (Locate Control Points)", true, EditorStyles.foldoutHeader);
        if (FoldLocate)
        {
            DrawLocateControlPointsSection(clip);
        }

        EditorGUILayout.Space(4);

        EditorGUILayout.Space(4);

        // ──────────────────────────────────────────────────────────────────
        // ① 参考系 (Reference Frame)
        // ──────────────────────────────────────────────────────────────────
        FoldRefFrame = EditorGUILayout.Foldout(FoldRefFrame, "① 参考系 (Reference Frame)", true, EditorStyles.foldoutHeader);
        if (FoldRefFrame)
        {
            DrawReferenceFrameSection(clip, info);
        }

        EditorGUILayout.Space(4);


        // ──────────────────────────────────────────────────────────────────
        // ② 位移曲线 (Displacement Curve)
        // ──────────────────────────────────────────────────────────────────
        FoldDisplacement = EditorGUILayout.Foldout(FoldDisplacement, "② 位移曲线 (Displacement Curve)", true, EditorStyles.foldoutHeader);
        if (FoldDisplacement)
        {
            DrawDisplacementCurveSection(clip, info);
        }

        EditorGUILayout.Space(4);

        // ──────────────────────────────────────────────────────────────────
        // ③ 停顿 (Pauses) — 包含行走速度同步与停顿调整
        // 说明：AnimationSpeedSync 也归入此组，因为它和停顿同属"Clip 时长管理"语义。
        // ──────────────────────────────────────────────────────────────────
        FoldPauses = EditorGUILayout.Foldout(FoldPauses, "③ 停顿 (Pauses)", true, EditorStyles.foldoutHeader);
        if (FoldPauses)
        {
            DrawAnimationSpeedSyncSection(clip, info);
            EditorGUILayout.Space(4);
            DrawPauseAdjustmentSection(clip, info);
        }

        EditorGUILayout.Space(4);

        // ──────────────────────────────────────────────────────────────────
        // ④ 曲线参数及其设置 (Curve Settings) — Alpha / Rotation / Banking / AxisLock
        // ──────────────────────────────────────────────────────────────────
        ShowOtherFields = EditorGUILayout.Foldout(ShowOtherFields, "④ 曲线参数及其设置 (Curve Settings)", true, EditorStyles.foldoutHeader);
        if (ShowOtherFields)
        {
            EditorGUI.indentLevel++;
            DrawTemplateExcept("displacementCurve");
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);

        // ──────────────────────────────────────────────────────────────────
        // ⑤ 事件 (Path Events)
        // ──────────────────────────────────────────────────────────────────
        FoldEvents = EditorGUILayout.Foldout(FoldEvents, "⑤ 事件 (Path Events)", true, EditorStyles.foldoutHeader);
        if (FoldEvents)
        {
            if (pathEventsProp != null)
                EditorGUILayout.PropertyField(pathEventsProp, new GUIContent("路径事件"), true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    // ════════════════════════════════════════════════════════════════════════════════
    //  参考系（Reference Frame） — Phase 4
    //
    //  指定 Spline 数据存储的坐标系。绑定后路径数据存储为相对该 Transform 的局部坐标，
    //  运行时由 SplineCurveMoveMixer 实时变换为世界空间。
    //
    //  典型用途：角色乘载具 —— Spline 在载具甲板局部空间下描述，载具自己用独立 Track
    //  驱动世界位姿，角色最终世界位姿 = 载具 × 局部 Spline 采样。载具旋转 / 倾斜 / 移动时
    //  角色自动同步，曲线数据本身不变。
    //
    //  创作工作流：
    //    1. 把 SplineCurveFromTransforms 的 GameObject parent 到载具下面
    //    2. 在 PlayableDirector 的 Bindings 面板里把本 Clip 的 ExposedReference 绑到载具
    //    3. 编辑控制点 Transform（它们自动随载具移动）
    //    4. 在 SplineCurveFromTransforms 上点【Apply】重新 bake
    // ════════════════════════════════════════════════════════════════════════════════
    private void DrawReferenceFrameSection(SplineCurveMoveClip clip, TimelineClipContext.Info info)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("参考系（Reference Frame）", EditorStyles.miniBoldLabel);

        if (refFrameProp != null)
            EditorGUILayout.PropertyField(refFrameProp, new GUIContent("Reference Frame"));

        Transform resolved = ResolveRefFrameAtEditTime(clip);
        //if (resolved != null)
        //{
        //    EditorGUILayout.HelpBox(
        //        $"曲线数据存储为相对【{resolved.name}】的局部坐标。\n" +
        //        $"运行时 Mixer 实时变换为世界——载具移动/旋转时角色随之联动。\n" +
        //        $"\n" +
        //        $"⚠ 要让 Scene 预览与运行时表现都正确，需做两件事：\n" +
        //        $"  ① 把 SplineCurveFromTransforms 的控制点 GameObject parent 到【{resolved.name}】下面\n" +
        //        $"     （让 Transforms-based 曲线预览跟随）\n" +
        //        $"  ② 在 SplineCurveFromTransforms 组件 Inspector 底部点【Update Timeline Track】\n" +
        //        $"     重新 bake（让存储数据真正变成局部坐标）\n" +
        //        $"\n" +
        //        $"提示：SplineCurveFromTransforms 同时会绘制一条橙色的【Stored Spline Preview】，\n" +
        //        $"读取当前已 bake 的数据应用 refFrame 变换后渲染——这条曲线就是运行时实际路径，\n" +
        //        $"refFrame 一移动它就立刻跟随，不依赖 Transforms parenting。",
        //        MessageType.Info);
        //}
        //else
        //{
        //    EditorGUILayout.HelpBox(
        //        "未绑定参考系——曲线按世界空间解释（默认）。\n" +
        //        "\n" +
        //        "💡 想用 refFrame 模式？推荐搭建顺序：\n" +
        //        "  ① 在场景里建好 PlayableDirector，把 TimelineAsset 拖进去\n" +
        //        "  ② 打开 Timeline 窗口，选中本 Clip\n" +
        //        "  ③ 在【本 Inspector】的 Reference Frame 字段拖入载具 Transform\n" +
        //        "     （必须从 Timeline 窗口选 Clip 进来，否则绑定可能落到错误的 Director 上）\n" +
        //        "  ④ 把 SplineCurveFromTransforms 的控制点 parent 到载具下\n" +
        //        "  ⑤ 在 SplineCurveFromTransforms 上点【Update Timeline Track】完成 bake",
        //        MessageType.None);
        //}
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 编辑器侧解析 referenceFrame 为运行时 Transform，使用 TimelineEditor.inspectedDirector
    /// 作为 IExposedPropertyTable 上下文。Timeline 窗口未打开或 Director 未指向场景时返回 null。
    /// </summary>
    private Transform ResolveRefFrameAtEditTime(SplineCurveMoveClip clip)
    {
        if (clip == null) return null;
        var dir = UnityEditor.Timeline.TimelineEditor.inspectedDirector;
        if (dir == null) return null;
        return clip.referenceFrame.Resolve(dir);
    }

    // ════════════════════════════════════════════════════════════════════════════════
    //  控制点定位 (Locate Control Points)
    //
    //  Clip 的 Spline 是 bake 后的纯位置数据，不含 GameObject 引用。但 bake 的来源——
    //  SplineCurveFromTransforms——仍然保留着控制点的 Transform 引用列表。本区通过反查
    //  拿到那个列表，提供一键 ping 到 Hierarchy 的能力。
    //
    //  反查：遍历场景所有 SplineCurveFromTransforms，调用 TryFindControlPointsForClip(本 clip)，
    //  用对象引用相等匹配。命中即得到控制点列表。
    //
    //  ping：EditorGUIUtility.PingObject 让 Hierarchy 面板自动滚动并高亮闪烁目标——
    //  这是"在层级面板里找到具体位置"的标准 API，不触碰 SceneView。
    // ════════════════════════════════════════════════════════════════════════════════

    // 反查结果缓存：避免每帧 OnInspectorGUI 都做 FindObjectsOfType（开销大）。
    // 以 (clip 实例 id) 为 key，Inspector 刷新频率下用陈旧缓存可接受；
    // 控制点列表结构性变化（增删点）由用户手动点"刷新"或切换选中触发重查。
    private SplineCurveMoveClip _locateCachedClip;
    private SplineCurveFromTransforms _locateCachedOwner;
    private List<Transform> _locateCachedPoints;
    private bool _locateCacheValid;

    private void DrawLocateControlPointsSection(SplineCurveMoveClip clip)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // 缓存失效条件：clip 变了 / 缓存没建立 / 用户点了刷新
        if (!_locateCacheValid || _locateCachedClip != clip)
        {
            RebuildLocateCache(clip);
        }

        // 顶部状态行 + 刷新按钮
        EditorGUILayout.BeginHorizontal();
        if (_locateCachedOwner != null)
            EditorGUILayout.LabelField($"来源: {_locateCachedOwner.name}", EditorStyles.miniLabel);
        else
            EditorGUILayout.LabelField("来源: 未找到关联组件", EditorStyles.miniLabel);

        if (GUILayout.Button(new GUIContent("⟳", "重新扫描场景里的 SplineCurveFromTransforms"), GUILayout.Width(28f)))
        {
            RebuildLocateCache(clip);
        }
        EditorGUILayout.EndHorizontal();

        if (_locateCachedOwner == null || _locateCachedPoints == null)
        {
            EditorGUILayout.HelpBox(
                "未找到管理此 Clip 的 SplineCurveFromTransforms。\n" +
                "可能原因：此 Clip 不是用该工具 bake 的，或组件所在 GameObject 处于 inactive 状态。",
                MessageType.None);
            EditorGUILayout.EndVertical();
            return;
        }

        if (_locateCachedPoints.Count == 0)
        {
            EditorGUILayout.HelpBox("关联组件里此 Clip 的控制点列表为空。", MessageType.None);
            EditorGUILayout.EndVertical();
            return;
        }

        // 控制点逐行列出
        for (int i = 0; i < _locateCachedPoints.Count; i++)
        {
            var t = _locateCachedPoints[i];
            EditorGUILayout.BeginHorizontal();

            if (t == null)
            {
                // 控制点 GameObject 已被删除——列表里留下 null
                var prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                EditorGUILayout.LabelField($"P{i}  →  (已删除)", EditorStyles.miniLabel);
                GUI.color = prevColor;
            }
            else
            {
                GUI.backgroundColor = new Color(1f, 0.85f, 0.4f);
                if (GUILayout.Button(new GUIContent($"📍 P{i}",
                        $"在 Hierarchy 高亮并选中【{t.name}】"),
                        GUILayout.Width(70f)))
                {
                    // ping → Hierarchy 滚动+闪烁；select → 同时选中（Inspector 切过去，方便直接调）
                    EditorGUIUtility.PingObject(t.gameObject);
                    Selection.activeGameObject = t.gameObject;
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.LabelField(t.name, EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(2);

        // 批量操作
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("🔲 选中全部", "把所有有效控制点一次性选中（Hierarchy 里全部高亮）")))
        {
            var valid = new List<Object>();
            foreach (var t in _locateCachedPoints)
                if (t != null) valid.Add(t.gameObject);
            if (valid.Count > 0)
            {
                Selection.objects = valid.ToArray();
                // ping 第一个，触发 Hierarchy 滚动到控制点群附近
                EditorGUIUtility.PingObject(valid[0]);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 重新扫描场景里所有 SplineCurveFromTransforms，反查管理本 clip 的那个，缓存结果。
    /// FindObjectsOfType 开销较大，所以结果缓存，仅在 clip 切换或用户手动刷新时重建。
    /// </summary>
    private void RebuildLocateCache(SplineCurveMoveClip clip)
    {
        _locateCachedClip = clip;
        _locateCachedOwner = null;
        _locateCachedPoints = null;
        _locateCacheValid = true;

        if (clip == null) return;

        var all = Object.FindObjectsOfType<SplineCurveFromTransforms>();
        foreach (var scft in all)
        {
            if (scft == null) continue;
            if (scft.TryFindControlPointsForClip(clip, out var points))
            {
                _locateCachedOwner = scft;
                _locateCachedPoints = points;
                return;
            }
        }
    }

    private void DrawClipInfoBar(TimelineClipContext.Info info)
    {
        if (info.valid)
        {
            string playheadText = info.playheadInClip
                ? $"播放头: {TimedCurveDrawer.FormatTime(info.clipLocalTime)} ({info.normalizedPlayhead * 100f:F1}%)"
                : "播放头不在此 Clip 内";
            EditorGUILayout.LabelField(
                $"Clip 时长 {TimedCurveDrawer.FormatTime(info.duration)}    |    {playheadText}",
                EditorStyles.helpBox);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "未在打开的 Timeline 中找到此 Clip —— 秒数 UI 与 playhead 联动需要 Clip 在 Timeline 上才能生效。\n" +
                "请先把 Clip 拖到 Timeline，并保持 Timeline 窗口打开。",
                MessageType.Warning);
        }
    }

#if false // ===== [已停用] 基线漂移检测 + 重新校准（Pause Duration Recalibration）=====
          // 端点拖拽 remap（OnClipChanged）已自动保持停顿绝对位置，本功能不再需要。
          // 整块用 #if false 关闭，保留代码以便日后参考/恢复。
    // ════════════════════════════════════════════════════════════════════════════════
    //  基线漂移检测 + 重新校准（Pause Duration Recalibration）
    //
    //  问题：用户在 Timeline 拖 Clip 两端改变 duration 时，Unity 默认行为是
    //  所有 keyframe 按归一化时间走 —— 停顿绝对时长被等比例缩放，行走速度变了。
    //
    //  本工具帮用户【恢复停顿到原始绝对时长】：
    //   - SplineCurveMoveClip 上有个 lastBaselineDuration 字段，记录上次主动操作时的 Clip 时长
    //   - 每次插入/删除/清除/Pause Adjustment 应用后，调用 MarkBaseline() 更新该值
    //   - OnInspectorGUI 检测 currentDuration ≠ lastBaselineDuration → 显示警告 + 校准按钮
    //   - 校准 = 按 ratio 把停顿恢复到 baseline 时的绝对值 → RebuildFromScratch
    //   - 校准完成后 baseline 重新对齐 currentDuration
    //
    //  行走斜率 = path / (currentDuration - totalRestoredPause) = 1/(T_new - P)，符合需求
    // ════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 在 Clip 信息条下方画一个横幅：检测到 Clip 时长被外部改变时显示警告 + 校准按钮。
    /// </summary>
    private void DrawBaselineDriftBanner(SplineCurveMoveClip clip, TimelineClipContext.Info info)
    {
        if (!info.valid) return;

        double baseline = clip.LastBaselineDuration;
        double current  = info.duration;

        // 未初始化 → 静默地填上当前时长（不打扰用户）
        if (baseline < 0)
        {
            clip.LastBaselineDuration = current;
            EditorUtility.SetDirty(clip);
            return;
        }

        // 时长一致 → 没漂移，什么也不显示
        if (System.Math.Abs(baseline - current) < 1e-3) return;

        // 漂移了 → 显示警告 + 校准 + 接受当前为新基线 两个按钮
        EditorGUILayout.Space(2);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        double ratio = baseline / current;
        EditorGUILayout.HelpBox(
            $"Clip 时长被改动：{TimedCurveDrawer.FormatTime(baseline)} → {TimedCurveDrawer.FormatTime(current)}\n" +
            $"曲线已被 Unity 等比例缩放，停顿的绝对时长也被改变。\n" +
            $"点【按当前 Clip 重新校准】可恢复停顿绝对时长，行走段填满剩余空间。",
            MessageType.Warning);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(
                new GUIContent("按当前 Clip 重新校准（保持停顿绝对时长）",
                    $"把当前曲线检测到的停顿 startSec/durationSec 乘以 ratio={ratio:F3} 恢复成 baseline 时的绝对值，" +
                    $"然后用 RebuildFromScratch 在当前 Clip 时长 {TimedCurveDrawer.FormatTime(current)} 上重建曲线。\n" +
                    $"行走段填满剩余空间，斜率 = 1/(Clip 时长 - 总停顿)。"),
                GUILayout.Height(28f)))
        {
            RecalibratePauseDurations(clip, info, ratio);
            GUIUtility.ExitGUI();
        }
        if (GUILayout.Button(
                new GUIContent("接受当前状态为新基线",
                    "不重建曲线，只把 lastBaselineDuration 更新为当前 Clip 时长。下次再拖动 Clip 时会从这个新基线检测漂移。"),
                GUILayout.Width(160f), GUILayout.Height(28f)))
        {
            MarkBaseline(clip);
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    /// <summary>
    /// 校准算法：用 ratio 把检测到的停顿恢复成 baseline 时的绝对时长，然后基于当前 Clip 时长重建。
    /// 行走段斜率 = 1/(currentDuration - totalRestoredPause)。
    /// </summary>
    private void RecalibratePauseDurations(SplineCurveMoveClip clip, TimelineClipContext.Info info, double ratio)
    {
        if (curveProp == null || !info.valid) return;

        var curve = curveProp.animationCurveValue;
        var scaled = CurvePauseQuickEdit.DetectPauses(curve, info.duration);

        // 恢复到原始绝对时长：当前是 baseline × (1/ratio) 倍，乘 ratio 还原
        var restored = new System.Collections.Generic.List<CurvePauseQuickEdit.PauseSegment>();
        for (int i = 0; i < scaled.Count; i++)
        {
            var p = scaled[i];
            p.startSec    = (float)(p.startSec    * ratio);
            p.durationSec = (float)(p.durationSec * ratio);
            restored.Add(p);
        }

        Undo.RecordObject(clip, "Recalibrate Pause Durations");

        var newCurve = CurvePauseQuickEdit.RebuildFromScratch(
            info.duration, restored, CurvePauseQuickEdit.JoinStyle.Linear);
        curveProp.animationCurveValue = newCurve;

        // 必须主动 apply（调用方会 ExitGUI）
        serializedObject.ApplyModifiedProperties();

        // 更新 baseline 为当前 Clip 时长 —— 校准完成，新基线对齐
        clip.LastBaselineDuration = info.duration;
        EditorUtility.SetDirty(clip);

        // 同步 Pause Adjustment 缓存
        _editPauses = null;
    }

    /// <summary>记录当前 Clip 时长为新的基线参考。所有主动操作完成后调用。</summary>
    private void MarkBaseline(SplineCurveMoveClip clip)
    {
        if (clip == null) return;
        var info = TimelineClipContext.Resolve(clip);
        if (!info.valid) return;
        clip.LastBaselineDuration = info.duration;
        EditorUtility.SetDirty(clip);
    }
#endif // ===== [已停用] 基线漂移检测 + 重新校准 结束 =====

    private void DrawDisplacementCurveSection(SplineCurveMoveClip clip, TimelineClipContext.Info info)
    {
        if (curveProp == null) return;

        EditorGUILayout.LabelField("位移曲线（横轴 = Clip 内时间，纵轴 = 路径进度 [0,1]）", EditorStyles.miniBoldLabel);

        // ── 末端拖拽模式开关（拖右端延长时的行为）──
        var holdProp = serializedObject.FindProperty("holdAtEndOnTrim");
        if (holdProp != null)
        {
            bool newHold = EditorGUILayout.ToggleLeft(
                new GUIContent("末端：到终点停着等（关 = 拉长变速）",
                    "拖【右端延长】Clip 时的末端行为：\n" +
                    "关（默认/方案A）：末段被拉长，角色一直在动、变慢，到最后才到达终点。\n" +
                    "开（方案B）：角色按原速先到达终点，再停在终点【等待】剩余时间（末端补一段静止）。\n" +
                    "压缩时两种模式一致；左端拖拽不受影响。"),
                holdProp.boolValue);
            if (newHold != holdProp.boolValue) holdProp.boolValue = newHold;
        }

        EditorGUILayout.Space(2);

        // 1) 曲线大编辑区
        Rect curveRect = GUILayoutUtility.GetRect(0f, 180f, GUILayout.ExpandWidth(true));
        EditorGUI.BeginChangeCheck();
        AnimationCurve curve = curveProp.animationCurveValue;
        curve = EditorGUI.CurveField(
            curveRect, GUIContent.none, curve,
            new Color(0.3f, 0.8f, 1f),
            new Rect(0f, 0f, 1f, 1f));
        if (EditorGUI.EndChangeCheck())
            curveProp.animationCurveValue = curve;

        // playhead 红线 + 顶部三角标记
        TimedCurveDrawer.DrawPlayhead(curveRect, info, new Color(1f, 0.25f, 0.25f, 0.9f));

        // 2) 时间轴刻度
        Rect timeAxisRect = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
        TimedCurveDrawer.DrawTimeAxis(timeAxisRect, info.valid ? info.duration : 1.0);

        // 3) 停顿分析栏 + playhead
        Rect analysisRect = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
        CurvePauseAnalysisDrawer.Draw(analysisRect, curve);
        TimedCurveDrawer.DrawPlayhead(analysisRect, info, new Color(1f, 0.3f, 0.3f, 0.95f));

        EditorGUILayout.Space(6);

        // 4) 插入停顿 —— 秒数输入
        DrawInsertPauseControls(clip, info);
    }

    private void DrawInsertPauseControls(SplineCurveMoveClip clip, TimelineClipContext.Info info)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("插入停顿（输入：起始时间 + 停顿时长，单位秒）", EditorStyles.miniBoldLabel);

        // ── 起始时间 ──
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        float newStart = EditorGUILayout.FloatField(new GUIContent("起始时间（秒）"), PauseStartSec);
        if (EditorGUI.EndChangeCheck()) PauseStartSec = Mathf.Max(0f, newStart);

        EditorGUI.BeginDisabledGroup(!info.playheadInClip);
        if (GUILayout.Button(
                new GUIContent("⏱ 用当前播放头", "把 Timeline 当前播放头时间填入起始"),
                GUILayout.Width(110f)))
        {
            PauseStartSec = (float)info.clipLocalTime;
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        // ── 停顿时长 ──
        EditorGUI.BeginChangeCheck();
        float newDur = EditorGUILayout.FloatField(
            new GUIContent("停顿时长（秒）", "停顿持续多少秒"),
            PauseDurationSec);
        if (EditorGUI.EndChangeCheck()) PauseDurationSec = Mathf.Max(0.001f, newDur);

        // ── 自动算出的结束时间（只读显示）──
        float pauseDuration = PauseDurationSec;
        EditorGUILayout.LabelField(
            $"结束时间（自动）= {TimedCurveDrawer.FormatTime(PauseStartSec + pauseDuration)}",
            EditorStyles.miniLabel);

        EditorGUILayout.Space(2);

        // ── 曲线衔接风格 ──
        EditorGUI.BeginChangeCheck();
        var newStyle = (CurvePauseQuickEdit.JoinStyle)EditorGUILayout.EnumPopup(
            new GUIContent("曲线衔接方式",
                "Smooth（默认）：进入/离开停顿有 ease in/out，观感自然，但行走段速度不严格匀速。\n" +
                "Linear：所有衔接为直线，行走段是纯匀速（适合需要严格按动画速度匹配的场景）。"),
            PauseJoinStyle);
        if (EditorGUI.EndChangeCheck()) PauseJoinStyle = newStyle;

        EditorGUILayout.Space(2);

        // ── 是否延长 Clip 总时长 ──
        EditorGUI.BeginChangeCheck();
        bool extend = EditorGUILayout.ToggleLeft(
            new GUIContent(
                "延长 Clip 总时长以保持速度不变",
                "勾选（推荐）：插入停顿时把 Clip 时长 +N 秒，停顿前后段速度不变（【暂停物体】语义）。\n" +
                "不勾选：保持 Clip 时长固定，停顿挤压剩余时间，后段速度会加快（【挤进固定时长】语义）。"),
            ExtendClipOnInsert);
        if (EditorGUI.EndChangeCheck()) ExtendClipOnInsert = extend;

        if (ExtendClipOnInsert && info.valid && pauseDuration > 0)
        {
            EditorGUILayout.LabelField(
                $"Clip 时长：{TimedCurveDrawer.FormatTime(info.duration)} → " +
                $"{TimedCurveDrawer.FormatTime(info.duration + pauseDuration)}",
                EditorStyles.miniLabel);
        }
        else if (!ExtendClipOnInsert && info.valid && pauseDuration > 0)
        {
            // 计算后段速度倍数提示
            double remainSec = System.Math.Max(1e-6, info.duration - PauseStartSec);
            double remainAfterPause = System.Math.Max(0.0, remainSec - pauseDuration);
            double speedMultiplier = remainAfterPause > 1e-6 ? remainSec / remainAfterPause : double.PositiveInfinity;
            EditorGUILayout.LabelField(
                $"后段速度倍数：×{speedMultiplier:F2}（Clip 时长不变）",
                EditorStyles.miniLabel);
        }

        // 警告：越界
        if (info.valid && (PauseStartSec + pauseDuration) > info.duration + 1e-3 && !ExtendClipOnInsert)
        {
            EditorGUILayout.HelpBox(
                $"结束时间 {TimedCurveDrawer.FormatTime(PauseStartSec + pauseDuration)} 超过了 Clip 时长 " +
                $"{TimedCurveDrawer.FormatTime(info.duration)}，将被截断。",
                MessageType.Warning);
        }

        EditorGUILayout.Space(4);

        // ── 操作按钮 ──
        EditorGUILayout.BeginHorizontal();
        bool canInsert = info.valid && info.duration > 0 && pauseDuration > 1e-4;
        EditorGUI.BeginDisabledGroup(!canInsert);
        string btnLabel = ExtendClipOnInsert
            ? $"插入停顿并延长 Clip（{TimedCurveDrawer.FormatTime(pauseDuration)}）"
            : $"在 {TimedCurveDrawer.FormatTime(PauseStartSec)} 处插入 {TimedCurveDrawer.FormatTime(pauseDuration)} 停顿";
        if (GUILayout.Button(btnLabel))
        {
            DoInsertPause(clip, info, PauseStartSec, pauseDuration, ExtendClipOnInsert);
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        // ── 现有停顿管理：单独删除 + 清除所有 ──
        EditorGUILayout.Space(4);
        DrawExistingPausesSubsection(clip, info);
    }

    /// <summary>
    /// 列出当前曲线检测到的所有停顿，提供单独删除 + 一键清除全部。
    /// 删除会缩短 Clip 时长（减去删除停顿的总时长），后续停顿自动前移。
    /// </summary>
    private void DrawExistingPausesSubsection(SplineCurveMoveClip clip, TimelineClipContext.Info info)
    {
        if (curveProp == null || !info.valid) return;

        var curve = curveProp.animationCurveValue;
        var pauses = CurvePauseQuickEdit.DetectPauses(curve, info.duration);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            $"现有停顿（{pauses.Count} 个）",
            EditorStyles.miniBoldLabel);

        if (pauses.Count == 0)
        {
            EditorGUILayout.LabelField("当前曲线没有停顿段。", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            return;
        }

        // 列出每个停顿 + 单独删除按钮
        for (int i = 0; i < pauses.Count; i++)
        {
            var p = pauses[i];
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"#{i + 1}  {TimedCurveDrawer.FormatTime(p.startSec)} → " +
                $"{TimedCurveDrawer.FormatTime(p.startSec + p.durationSec)}  " +
                $"({TimedCurveDrawer.FormatTime(p.durationSec)})",
                EditorStyles.label);

            double clipAfter = info.duration - p.durationSec;
            if (GUILayout.Button(
                    new GUIContent($"× 删除（Clip → {TimedCurveDrawer.FormatTime(clipAfter)}）",
                        "删除此停顿。Clip 时长减少该停顿时长，后续停顿前移，曲线该位置变成线性。"),
                    GUILayout.Width(180f)))
            {
                if (EditorUtility.DisplayDialog(
                        "确认删除停顿",
                        $"删除停顿 #{i + 1}（{TimedCurveDrawer.FormatTime(p.durationSec)}），" +
                        $"Clip 从 {TimedCurveDrawer.FormatTime(info.duration)} 缩短到 " +
                        $"{TimedCurveDrawer.FormatTime(clipAfter)}。",
                        "删除", "取消"))
                {
                    RemovePauseAt(i, pauses, clip, info);
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(4);

        // 总时长统计
        double totalPause = 0;
        for (int i = 0; i < pauses.Count; i++) totalPause += pauses[i].durationSec;
        double clipAfterAll = info.duration - totalPause;

        EditorGUILayout.LabelField(
            $"总停顿: {TimedCurveDrawer.FormatTime(totalPause)}   |   " +
            $"清除全部后 Clip: {TimedCurveDrawer.FormatTime(clipAfterAll)}",
            EditorStyles.miniLabel);

        // 全部清除 + 重置匀速线性
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(
                new GUIContent($"清除所有停顿（Clip → {TimedCurveDrawer.FormatTime(clipAfterAll)}）",
                    "删除所有停顿。Clip 时长减少所有停顿的总时长，曲线变为纯匀速线性。"),
                GUILayout.Height(22f)))
        {
            if (EditorUtility.DisplayDialog(
                    "确认清除所有停顿",
                    $"将清除 {pauses.Count} 个停顿（总时长 {TimedCurveDrawer.FormatTime(totalPause)}）。\n" +
                    $"Clip 从 {TimedCurveDrawer.FormatTime(info.duration)} 缩短到 " +
                    $"{TimedCurveDrawer.FormatTime(clipAfterAll)}，曲线变为纯线性匀速。",
                    "清除", "取消"))
            {
                ClearAllPauses(clip, info, clipAfterAll);
                GUIUtility.ExitGUI();
            }
        }
        if (GUILayout.Button(
                new GUIContent("重置为线性（保持 Clip 时长）",
                    "曲线变为纯线性 (0,0)→(1,1)，Clip 时长不变。如果 Clip 时长 = 现在含停顿的时长，速度会变慢。"),
                GUILayout.Width(200f), GUILayout.Height(22f)))
        {
            Undo.RecordObject(clip, "Reset Curve");
            curveProp.animationCurveValue = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            EditorUtility.SetDirty(clip);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 删除指定停顿：后续停顿前移该停顿的时长，Clip 时长减少该停顿时长。
    /// 用 Linear 重建曲线（删除位置变成线性行走段）。
    /// </summary>
    private void RemovePauseAt(int index, System.Collections.Generic.List<CurvePauseQuickEdit.PauseSegment> pauses,
                               SplineCurveMoveClip clip, TimelineClipContext.Info info)
    {
        if (curveProp == null || index < 0 || index >= pauses.Count) return;

        var deleted = pauses[index];
        // 构造删除后的停顿列表：移除目标，后续 startSec 前移 deleted.durationSec
        var remaining = new System.Collections.Generic.List<CurvePauseQuickEdit.PauseSegment>();
        for (int i = 0; i < pauses.Count; i++)
        {
            if (i == index) continue;
            var p = pauses[i];
            if (p.startSec > deleted.startSec) p.startSec -= deleted.durationSec;
            remaining.Add(p);
        }

        double newDuration = info.duration - deleted.durationSec;
        if (newDuration < 0.001) newDuration = 0.001;

        var undoTargets = info.timeline != null
            ? new UnityEngine.Object[] { clip, info.timeline }
            : new UnityEngine.Object[] { clip };
        Undo.RecordObjects(undoTargets, "Remove Pause");

        var newCurve = CurvePauseQuickEdit.RebuildFromScratch(
            newDuration, remaining, CurvePauseQuickEdit.JoinStyle.Linear);
        curveProp.animationCurveValue = newCurve;
        SetClipDurationSynced(clip, info, newDuration);

        // 关键：必须主动 apply。调用方紧接着会 GUIUtility.ExitGUI()，
        // 它抛 ExitGUIException 跳出 OnInspectorGUI，导致末尾的 ApplyModifiedProperties() 不会执行。
        // 不 apply 的话，SerializedProperty 的 curve 改动会在下次 serializedObject.Update() 时被丢弃，
        // 而 info.clip.duration 是直接改对象会生效——这就是"Clip 缩短了但曲线没变"的根因。
        serializedObject.ApplyModifiedProperties();

        EditorUtility.SetDirty(clip);
        if (info.timeline != null) EditorUtility.SetDirty(info.timeline);

        UnityEditor.Timeline.TimelineEditor.Refresh(
            UnityEditor.Timeline.RefreshReason.ContentsModified);

        // 新 Clip 时长成为新基线
        clip.LastBaselineDuration = newDuration;

        // 同步 Pause Adjustment 缓存
        _editPauses = null;
    }

    /// <summary>
    /// 清除所有停顿：Clip 时长减去所有停顿总时长，曲线变为纯线性匀速。
    /// </summary>
    private void ClearAllPauses(SplineCurveMoveClip clip, TimelineClipContext.Info info, double newDuration)
    {
        if (curveProp == null) return;
        if (newDuration < 0.001) newDuration = 0.001;

        var undoTargets = info.timeline != null
            ? new UnityEngine.Object[] { clip, info.timeline }
            : new UnityEngine.Object[] { clip };
        Undo.RecordObjects(undoTargets, "Clear All Pauses");

        curveProp.animationCurveValue = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        SetClipDurationSynced(clip, info, newDuration);

        // 同 RemovePauseAt：调用方会 ExitGUI()，必须主动 apply
        serializedObject.ApplyModifiedProperties();

        EditorUtility.SetDirty(clip);
        if (info.timeline != null) EditorUtility.SetDirty(info.timeline);

        UnityEditor.Timeline.TimelineEditor.Refresh(
            UnityEditor.Timeline.RefreshReason.ContentsModified);

        // 新 Clip 时长成为新基线
        clip.LastBaselineDuration = newDuration;

        // 同步 Pause Adjustment 缓存
        _editPauses = null;
    }

    /// <summary>
    /// 执行插入停顿。根据 extendClip 选择算法：
    /// - extendClip = true：重映射所有关键帧到新时长，同时修改 TimelineClip.duration
    /// - extendClip = false：在固定时长内挤入水平段（老行为）
    /// </summary>
    private void DoInsertPause(SplineCurveMoveClip clip, TimelineClipContext.Info info,
                               float startSec, float durationSec, bool extendClip)
    {
        if (curveProp == null || !info.valid) return;

        var oldCurve = curveProp.animationCurveValue;
        var joinStyle = PauseJoinStyle;

        if (extendClip)
        {
            // 同时修改 TimelineClip 和 PlayableAsset，要记录两个 Undo target
            var undoTargets = info.timeline != null
                ? new UnityEngine.Object[] { clip, info.timeline }
                : new UnityEngine.Object[] { clip };
            Undo.RecordObjects(undoTargets, "Insert Pause (Extend Clip)");

            var newCurve = CurvePauseQuickEdit.InsertPausePreservingSpeed(
                oldCurve, info.duration, startSec, durationSec, out double newDuration, joinStyle);

            curveProp.animationCurveValue = newCurve;

            // 修改 Clip duration —— 必须在 PlayableAsset 写完后做
            SetClipDurationSynced(clip, info, newDuration);

            EditorUtility.SetDirty(clip);
            if (info.timeline != null) EditorUtility.SetDirty(info.timeline);

            // 通知 Timeline 编辑器刷新图（duration 变化需要 rebuild）
            UnityEditor.Timeline.TimelineEditor.Refresh(
                UnityEditor.Timeline.RefreshReason.ContentsModified);

            // 新 Clip 时长成为新基线（防止此操作触发自身的"漂移"提示）
            clip.LastBaselineDuration = newDuration;
        }
        else
        {
            Undo.RecordObject(clip, "Insert Pause");
            float startNorm = (float)(startSec    / info.duration);
            float durNorm   = (float)(durationSec / info.duration);
            if (CurvePauseQuickEdit.InsertPause(oldCurve, startNorm, durNorm, joinStyle))
            {
                curveProp.animationCurveValue = oldCurve;
                EditorUtility.SetDirty(clip);
                clip.LastBaselineDuration = info.duration;
            }
        }
    }

    /// <summary>
    /// 动画速度同步面板。勾选 useAnimationSpeedSync 才显示完整功能。
    /// 让用户输入"角色行走速度（m/s）"，根据曲线总长 splineLength 自动算 Clip 应该多长。
    /// 提供两种应用方式：保留停顿 / 重置为匀速线性。
    /// </summary>
    private void DrawAnimationSpeedSyncSection(SplineCurveMoveClip clip, TimelineClipContext.Info info)
    {
        if (useAnimSpeedSyncProp == null) return;

        // 顶部 toggle：是否启用此功能（默认关）
        EditorGUI.BeginChangeCheck();
        bool enabled = EditorGUILayout.ToggleLeft(
            new GUIContent(
                "启用动画速度同步（Animation Speed Sync）",
                "勾选后显示\"按角色行走速度调整 Clip 时长\"按钮。\n" +
                "未勾选时此区块隐藏，不会影响其他配置。"),
            useAnimSpeedSyncProp.boolValue);
        if (EditorGUI.EndChangeCheck()) useAnimSpeedSyncProp.boolValue = enabled;

        if (!enabled) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("动画速度同步", EditorStyles.miniBoldLabel);

        // 行走速度 + 打开 Analyzer 工具
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        float newSpeed = EditorGUILayout.FloatField(
            new GUIContent("角色行走速度（m/s）",
                "等效的【原地动画行走速度】。如不知道，请用 Gait Speed Analyzer 工具测量。"),
            animWalkSpeedProp.floatValue);
        if (EditorGUI.EndChangeCheck()) animWalkSpeedProp.floatValue = Mathf.Max(0.001f, newSpeed);

        if (GUILayout.Button(
                new GUIContent("打开 Analyzer ↗",
                    "打开 Gait Speed Analyzer 工具，从原地走路动画推算等效速度"),
                GUILayout.Width(120f)))
        {
            GaitSpeedAnalyzerWindow.Open();
        }
        EditorGUILayout.EndHorizontal();

        // 曲线总长 + 建议时长
        float speed = animWalkSpeedProp.floatValue;
        float splineLength = TryReadSplineLength(clip);
        bool lengthValid = splineLength > 1e-3f && speed > 1e-3f;

        if (!lengthValid)
        {
            EditorGUILayout.HelpBox(
                splineLength <= 1e-3f
                    ? "曲线总长未知（控制点不足或 LUT 未构建）。在 Timeline 上播放一帧让曲线构建后再来。"
                    : "速度值无效（必须 > 0）。",
                MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        float targetWalkDuration = splineLength / speed;
        EditorGUILayout.LabelField($"曲线总长：    {splineLength:F3} m");
        EditorGUILayout.LabelField($"应行走时长：  {TimedCurveDrawer.FormatTime(targetWalkDuration)}");

        // 当前 Clip 行走/停顿统计 + 速度对比
        if (info.valid && curveProp != null)
        {
            var curve = curveProp.animationCurveValue;
            AnalyzeCurveSegments(curve, info.duration, out double curWalkAbs, out double curPauseAbs);
            EditorGUILayout.LabelField($"当前 Clip：    {TimedCurveDrawer.FormatTime(info.duration)}");
            EditorGUILayout.LabelField(
                $"  ├ 行走段：   {TimedCurveDrawer.FormatTime(curWalkAbs)}",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"  └ 停顿段：   {TimedCurveDrawer.FormatTime(curPauseAbs)}",
                EditorStyles.miniLabel);

            // 速度差异提示（反算 Animator.speed 建议值）
            if (curWalkAbs > 1e-3)
            {
                double actualSpeed = splineLength / curWalkAbs;
                double speedRatio = actualSpeed / speed;
                if (System.Math.Abs(speedRatio - 1.0) > 0.005)
                {
                    string note = speedRatio > 1.0
                        ? $"当前 Clip 行走段比目标快 {(speedRatio - 1.0) * 100.0:F1}%"
                        : $"当前 Clip 行走段比目标慢 {(1.0 - speedRatio) * 100.0:F1}%";
                    EditorGUILayout.LabelField(
                        $"⚠ {note}（实际 {actualSpeed:F3} m/s，或把 Animator.speed 设为 {speedRatio:F3}）",
                        EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField(
                        $"✓ 当前 Clip 行走速度已匹配（{actualSpeed:F3} m/s）",
                        EditorStyles.miniLabel);
                }
            }
        }

        EditorGUILayout.Space(4);

        // 应用按钮：两种模式
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(!info.valid);
        if (GUILayout.Button(
                new GUIContent("按速度调整（保留停顿）",
                    "保留现有的停顿段时长不变，把行走段总时长缩放成 splineLength / walkSpeed。\n" +
                    "Clip 时长 = 新行走时长 + 原停顿时长。"),
                GUILayout.Height(22f)))
        {
            ApplyWalkSpeed(clip, info, targetWalkDuration, preservePauses: true);
        }
        if (GUILayout.Button(
                new GUIContent("按速度重置（匀速线性）",
                    "displacementCurve 推平成线性 (0,0)-(1,1)，Clip 时长 = splineLength / walkSpeed。\n" +
                    "所有现有的停顿和加减速会丢失，仅适合空白或要重头来的 Clip。"),
                GUILayout.Height(22f)))
        {
            if (EditorUtility.DisplayDialog(
                    "确认重置",
                    "这会清除现有 displacementCurve 的所有关键帧（包括停顿）并恢复线性。继续吗？",
                    "确认", "取消"))
            {
                ApplyWalkSpeedReset(clip, info, targetWalkDuration);
            }
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    /// <summary>读取 Clip 的 spline 总长。如果 LUT 还没构建，会触发一次构建。</summary>
    private static float TryReadSplineLength(SplineCurveMoveClip clip)
    {
        if (clip == null || clip.Template == null) return 0f;
        var spline = clip.Template.Spline;
        if (spline == null || spline.ControlPoints == null || spline.ControlPoints.Count < 2)
            return 0f;
        return spline.TotalLength;
    }

    /// <summary>把 displacementCurve 拆成行走段（非水平）和停顿段（水平），分别累计绝对时长。</summary>
    private static void AnalyzeCurveSegments(AnimationCurve curve, double duration,
                                             out double walkAbs, out double pauseAbs)
    {
        walkAbs = 0; pauseAbs = 0;
        if (curve == null || curve.keys.Length < 2) return;
        var keys = curve.keys;
        const float PAUSE_EPS = 0.001f;
        for (int i = 0; i < keys.Length - 1; i++)
        {
            double segDur = (keys[i + 1].time - keys[i].time) * duration;
            bool isPause = Mathf.Abs(keys[i].value - keys[i + 1].value) < PAUSE_EPS;
            if (isPause) pauseAbs += segDur;
            else         walkAbs  += segDur;
        }
    }

    /// <summary>应用：保留停顿，按目标行走时长缩放行走段。</summary>
    private void ApplyWalkSpeed(SplineCurveMoveClip clip, TimelineClipContext.Info info,
                                float targetWalkDuration, bool preservePauses)
    {
        if (curveProp == null || !info.valid) return;
        var oldCurve = curveProp.animationCurveValue;

        var undoTargets = info.timeline != null
            ? new UnityEngine.Object[] { clip, info.timeline }
            : new UnityEngine.Object[] { clip };
        Undo.RecordObjects(undoTargets, "Apply Walk Speed");

        var newCurve = CurvePauseQuickEdit.RescaleWalkPreservingPauses(
            oldCurve, info.duration, targetWalkDuration, out double newDuration);

        curveProp.animationCurveValue = newCurve;
        SetClipDurationSynced(clip, info, newDuration);

        EditorUtility.SetDirty(clip);
        if (info.timeline != null) EditorUtility.SetDirty(info.timeline);

        UnityEditor.Timeline.TimelineEditor.Refresh(
            UnityEditor.Timeline.RefreshReason.ContentsModified);
    }

    /// <summary>应用：曲线重置为线性，Clip 时长直接等于目标行走时长。</summary>
    private void ApplyWalkSpeedReset(SplineCurveMoveClip clip, TimelineClipContext.Info info,
                                     float targetWalkDuration)
    {
        if (curveProp == null || !info.valid) return;

        var undoTargets = info.timeline != null
            ? new UnityEngine.Object[] { clip, info.timeline }
            : new UnityEngine.Object[] { clip };
        Undo.RecordObjects(undoTargets, "Apply Walk Speed (Reset)");

        curveProp.animationCurveValue = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        SetClipDurationSynced(clip, info, targetWalkDuration);

        EditorUtility.SetDirty(clip);
        if (info.timeline != null) EditorUtility.SetDirty(info.timeline);

        UnityEditor.Timeline.TimelineEditor.Refresh(
            UnityEditor.Timeline.RefreshReason.ContentsModified);
    }

    // ════════════════════════════════════════════════════════════════════════════════
    //  停顿调整（Pause Adjustment）—— 只负责平移，不负责删除
    //
    //  设计语义（关键）：
    //   - Clip 时长 T 是用户在 Clip 创建时确定的"速度基线"，本面板【不改 Clip 时长】
    //   - 移动速度 = 曲线长 / (T - 总停顿时长)。用户只改 stop.start（位置），
    //     duration 由系统【锁定】，总停顿不变 → 速度自然不变
    //   - 用户的 start 改动可能导致两个停顿重叠 —— 系统会自动合并（用户原话场景），
    //     合并后总停顿时长可能小于原值，UI 会用速度对比警告用户
    //   - 超出 [0, T] 的停顿会被截断；clamp 后宽度 ≤ 0 的停顿被丢弃（"全部超出 = 没有停顿"）
    //
    //  编辑模型：
    //   - OnInspectorGUI 检测 curve hash，变化时从 curve 重载 _editPauses
    //   - 用户改 _editPauses[i].startSec（duration 锁定不可编辑）
    //   - 点应用：调用 RebuildFromScratch(T, _editPauses, Linear)
    // ════════════════════════════════════════════════════════════════════════════════

    private void DrawPauseAdjustmentSection(SplineCurveMoveClip clip, TimelineClipContext.Info info)
    {
        if (curveProp == null || !info.valid) return;

        EditorGUI.BeginChangeCheck();
        bool show = EditorGUILayout.Foldout(ShowPauseManager,
            new GUIContent("停顿调整（Pause Adjustment）",
                "精确调整每个停顿的起始时间和持续时长。\n" +
                "两种模式（顶部开关切换）：\n" +
                "  • 保持速度（默认）：用 RebuildFromScratch 重建，所有行走段统一速度\n" +
                "  • 普通模式：保留每个停顿的 path 进度，停顿水平平移，前后段速度变化\n" +
                "停顿不能跨越相邻停顿——A 的末尾不能超过 B 的开头。"),
            true);
        if (EditorGUI.EndChangeCheck()) ShowPauseManager = show;
        if (!show) return;

        var curve = curveProp.animationCurveValue;

        // 检测 curve 是否被外部修改了 —— 是则重新加载 _editPauses
        int curHash = ComputeCurveHash(curve);
        if (_editPauses == null || curHash != _editPausesSourceKeyHash)
        {
            ReloadEditPauses(curve, info.duration);
            // 确保按起始时间排序（邻居 clamp 依赖于这个顺序）
            _editPauses.Sort((a, b) => a.startSec.CompareTo(b.startSec));
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // ── 模式开关 ──
        EditorGUI.BeginChangeCheck();
        bool preserveSpeed = EditorGUILayout.ToggleLeft(
            new GUIContent("保持速度不变（重建模式）",
                "✔ 勾选（默认）：应用时基于\"无停顿\"基线重建，所有行走段速度统一。" +
                "适合\"动画速度匹配\"的场景。\n" +
                "✘ 不勾选（普通模式）：保留每个停顿的 path 进度位置，仅在时间轴上水平平移。" +
                "停顿前后行走段斜率会改变（这段更快/更慢），其它段不变。"),
            PauseAdjustPreserveSpeed);
        if (EditorGUI.EndChangeCheck()) PauseAdjustPreserveSpeed = preserveSpeed;

        // ── 顶部信息条 + 速度基线 ──
        double currentTotalPause = 0;
        for (int i = 0; i < _editPauses.Count; i++) currentTotalPause += _editPauses[i].durationSec;
        double currentWalkSec = info.duration - currentTotalPause;
        float splineLength = TryReadSplineLength(clip);
        bool hasSpeed = splineLength > 1e-3f && currentWalkSec > 1e-3f;
        double baselineSpeed = hasSpeed ? splineLength / currentWalkSec : 0;

        EditorGUILayout.LabelField(
            $"Clip 时长: {TimedCurveDrawer.FormatTime(info.duration)}   |   " +
            $"检测到 {_editPauses.Count} 个停顿",
            EditorStyles.miniBoldLabel);
        if (hasSpeed)
        {
            EditorGUILayout.LabelField(
                $"基线速度: {baselineSpeed:F3} m/s    (行走 {TimedCurveDrawer.FormatTime(currentWalkSec)} / 长 {splineLength:F2} m)",
                EditorStyles.miniLabel);
        }

        if (GUILayout.Button("↻ 从曲线重新加载", GUILayout.Width(150f)))
        {
            ReloadEditPauses(curve, info.duration);
            _editPauses.Sort((a, b) => a.startSec.CompareTo(b.startSec));
        }

        if (_editPauses.Count == 0)
        {
            EditorGUILayout.HelpBox("当前曲线没有检测到水平段（停顿）。", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.Space(2);

        // 邻居 clamp 用的小缝隙：避免两个停顿端点重合（曲线会有垂直跳跃）
        const float NEIGHBOR_GAP = 0.001f;
        const float MIN_PAUSE_DUR = 0.05f;

        // ── 列出每个停顿，允许编辑 start 和 duration ──
        for (int i = 0; i < _editPauses.Count; i++)
        {
            var p = _editPauses[i];

            // 邻居约束（左右边界）
            float minStart = (i == 0)
                ? 0f
                : (_editPauses[i - 1].startSec + _editPauses[i - 1].durationSec + NEIGHBOR_GAP);
            float maxEnd = (i == _editPauses.Count - 1)
                ? (float)info.duration
                : (_editPauses[i + 1].startSec - NEIGHBOR_GAP);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                $"停顿 #{i + 1}    可调范围 [{TimedCurveDrawer.FormatTime(minStart)} ~ {TimedCurveDrawer.FormatTime(maxEnd)}]",
                EditorStyles.miniBoldLabel);

            // 起始时间（clamp 到 [minStart, maxEnd - currentDuration]）
            float startMax = Mathf.Max(minStart, maxEnd - p.durationSec);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            float newStart = EditorGUILayout.FloatField(
                new GUIContent(
                    $"起始（秒）  ∈ [{minStart:F2}, {startMax:F2}]",
                    "停顿在 Clip 内的起始时间。会被自动 clamp 到合法范围（不能跨越相邻停顿）。"),
                p.startSec);
            if (EditorGUI.EndChangeCheck())
            {
                p.startSec = Mathf.Clamp(newStart, minStart, startMax);
                _editPauses[i] = p;
            }
            EditorGUI.BeginDisabledGroup(!info.playheadInClip);
            if (GUILayout.Button(
                    new GUIContent("⏱", $"用当前播放头时间 {TimedCurveDrawer.FormatTime(info.clipLocalTime)} 填入"),
                    GUILayout.Width(28f)))
            {
                p.startSec = Mathf.Clamp((float)info.clipLocalTime, minStart, startMax);
                _editPauses[i] = p;
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            // 持续时间（clamp 到 [MIN_PAUSE_DUR, maxEnd - currentStart]）
            float durMax = Mathf.Max(MIN_PAUSE_DUR, maxEnd - p.startSec);
            EditorGUI.BeginChangeCheck();
            float newDur = EditorGUILayout.FloatField(
                new GUIContent(
                    $"持续时长（秒）  ≤ {durMax:F2}",
                    "停顿的持续时间。会被自动 clamp，确保末尾不超过下一个停顿的起始（或 Clip 末尾）。"),
                p.durationSec);
            if (EditorGUI.EndChangeCheck())
            {
                p.durationSec = Mathf.Clamp(newDur, MIN_PAUSE_DUR, durMax);
                _editPauses[i] = p;
            }

            EditorGUILayout.LabelField(
                $"结束（自动）= {TimedCurveDrawer.FormatTime(p.startSec + p.durationSec)}",
                EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(4);

        // ── 应用后的预览（总停顿/行走/速度对比）──
        double previewTotalPause = 0;
        for (int i = 0; i < _editPauses.Count; i++) previewTotalPause += _editPauses[i].durationSec;
        double previewWalkSec = info.duration - previewTotalPause;
        bool walkChanged = System.Math.Abs(previewWalkSec - currentWalkSec) > 1e-3;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("应用后预览", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField(
            $"停顿: {_editPauses.Count} 个    |    总停顿 {TimedCurveDrawer.FormatTime(previewTotalPause)}    |    行走 {TimedCurveDrawer.FormatTime(previewWalkSec)}",
            EditorStyles.miniLabel);

        if (PauseAdjustPreserveSpeed)
        {
            if (hasSpeed && previewWalkSec > 1e-3)
            {
                double newSpeed = splineLength / previewWalkSec;
                if (walkChanged)
                {
                    EditorGUILayout.HelpBox(
                        $"【保持速度模式】总停顿时长变了：行走时间 {TimedCurveDrawer.FormatTime(currentWalkSec)} → " +
                        $"{TimedCurveDrawer.FormatTime(previewWalkSec)}，速度也会从 {baselineSpeed:F3} → {newSpeed:F3} m/s。",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.LabelField($"✓ 速度不变 ({newSpeed:F3} m/s)", EditorStyles.miniLabel);
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "【普通模式】停顿水平平移，前后行走段斜率会按各自的距离/时间各自变化（各段速度不一致）。",
                MessageType.Info);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // ── 应用按钮 ──
        string btnLabel = PauseAdjustPreserveSpeed
            ? "应用所有更改（重建 - 保持速度）"
            : "应用所有更改（平移 - 保留 path 进度）";
        if (GUILayout.Button(btnLabel, GUILayout.Height(24f)))
        {
            ApplyPauseEdits(clip, info);
        }

        EditorGUILayout.LabelField(
            "Clip 时长不变。应用模式取决于上方开关。",
            EditorStyles.miniLabel);

        EditorGUILayout.EndVertical();

        // ─── 子区块：绝对位置快照 ───
        EditorGUILayout.Space(4);
        DrawPauseSnapshotSubsection(clip, info);
    }

    /// <summary>
    /// "绝对位置快照"子区块：保存当前停顿在 Timeline 上的绝对时间，方便 Clip 被移动/拉伸后一键还原。
    /// 工作原理：
    ///   保存：abs = clip.start + pause.startSecLocal，存到 SplineCurveMoveClip 的 savedPauseSnapshots
    ///   恢复：new local = saved abs - currentClipStart，clamp 到 [0, currentDuration]，超出丢弃
    ///         用 RebuildPreservingProgress 重建（保留每个停顿的 progressValue → 同样的 path 进度位置）
    /// </summary>
    private void DrawPauseSnapshotSubsection(SplineCurveMoveClip clip, TimelineClipContext.Info info)
    {
        if (!info.valid) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("绝对位置快照（Snapshot）", EditorStyles.miniBoldLabel);

        var snapshots = clip.SavedPauseSnapshots;
        int snapshotCount = snapshots?.Count ?? 0;
        double currentClipStart = info.clip.start;

        if (snapshotCount == 0)
        {
            EditorGUILayout.LabelField(
                "状态：尚未保存任何快照",
                EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField(
                $"状态：已保存 {snapshotCount} 个停顿快照",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"保存时 Clip 起始时间：{TimedCurveDrawer.FormatTime(clip.SavedSnapshotClipStartTime)}    " +
                $"|    当前 Clip 起始：{TimedCurveDrawer.FormatTime(currentClipStart)}",
                EditorStyles.miniLabel);

            // 显示快照详情（每个停顿的绝对时间范围）
            for (int i = 0; i < snapshotCount; i++)
            {
                var s = snapshots[i];
                double absEnd = s.absoluteStartSec + s.durationSec;

                // 计算恢复时在当前 Clip 上的局部范围（带 clamp 标识）
                double localStart = s.absoluteStartSec - currentClipStart;
                double localEnd = absEnd - currentClipStart;
                bool willClampLeft  = localStart < -1e-3;
                bool willClampRight = localEnd > info.duration + 1e-3;
                bool willDrop = (System.Math.Min(localEnd, info.duration) - System.Math.Max(localStart, 0)) <= 1e-3;

                string status = willDrop ? "（恢复后将完全超出 Clip，丢弃）"
                              : (willClampLeft || willClampRight) ? "（恢复后部分超出，将被截断）"
                              : "";
                EditorGUILayout.LabelField(
                    $"  #{i + 1}  abs [{TimedCurveDrawer.FormatTime(s.absoluteStartSec)} → " +
                    $"{TimedCurveDrawer.FormatTime(absEnd)}] " +
                    $"(时长 {TimedCurveDrawer.FormatTime(s.durationSec)})  {status}",
                    EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.Space(4);

        // ── 操作按钮 ──
        EditorGUILayout.BeginHorizontal();

        // 保存按钮
        if (GUILayout.Button(
                new GUIContent("💾 保存当前为绝对位置",
                    "把当前每个停顿的 (Clip 起始 + 局部起始) 记录为绝对时间。\n" +
                    "之后即使 Clip 在 Timeline 上被移动或拉伸，也能一键恢复到这些原始时刻。"),
                GUILayout.Height(22f)))
        {
            SavePauseSnapshots(clip, info);
        }

        // 恢复按钮（无快照时禁用）
        EditorGUI.BeginDisabledGroup(snapshotCount == 0);
        if (GUILayout.Button(
                new GUIContent("↻ 恢复到保存位置",
                    "把保存的绝对位置转换成当前 Clip 上的局部位置（用 abs - clip.start 计算），" +
                    "超出当前 Clip 时长的部分被截断，完全超出的停顿被丢弃。\n" +
                    "用 RebuildPreservingProgress 重建（保留每个停顿的 path 进度位置）。"),
                GUILayout.Height(22f)))
        {
            if (EditorUtility.DisplayDialog(
                    "确认恢复",
                    $"将用 {snapshotCount} 个保存的快照覆盖当前停顿配置。\n" +
                    "当前的所有停顿编辑会被丢弃。继续？",
                    "恢复", "取消"))
            {
                RestorePauseSnapshots(clip, info);
                GUIUtility.ExitGUI();
            }
        }
        EditorGUI.EndDisabledGroup();

        // 清除按钮
        EditorGUI.BeginDisabledGroup(snapshotCount == 0);
        if (GUILayout.Button(
                new GUIContent("🗑 清除", "清除保存的所有快照。"),
                GUILayout.Width(60f), GUILayout.Height(22f)))
        {
            if (EditorUtility.DisplayDialog(
                    "确认清除快照",
                    $"将清除 {snapshotCount} 个已保存的快照。当前停顿配置不受影响。",
                    "清除", "取消"))
            {
                Undo.RecordObject(clip, "Clear Pause Snapshots");
                clip.SavedPauseSnapshots = new List<SavedPauseSnapshot>();
                clip.SavedSnapshotClipStartTime = -1;
                EditorUtility.SetDirty(clip);
            }
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(
            "建议在【调整 Clip 在 Timeline 上的位置或时长之前】保存一次，操作完成后再恢复。",
            EditorStyles.miniLabel);

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 保存：把当前每个停顿的绝对时间（clip.start + 局部 startSec）存到 asset。
    /// </summary>
    private void SavePauseSnapshots(SplineCurveMoveClip clip, TimelineClipContext.Info info)
    {
        if (curveProp == null || !info.valid) return;

        var curve = curveProp.animationCurveValue;
        var currentPauses = CurvePauseQuickEdit.DetectPauses(curve, info.duration);
        double clipStart = info.clip.start;

        Undo.RecordObject(clip, "Save Pause Snapshots");

        var snapshots = new List<SavedPauseSnapshot>(currentPauses.Count);
        for (int i = 0; i < currentPauses.Count; i++)
        {
            var p = currentPauses[i];
            snapshots.Add(new SavedPauseSnapshot
            {
                absoluteStartSec = clipStart + p.startSec,
                durationSec      = p.durationSec,
                progressValue    = p.progressValue,
            });
        }
        clip.SavedPauseSnapshots = snapshots;
        clip.SavedSnapshotClipStartTime = clipStart;
        EditorUtility.SetDirty(clip);
    }

    /// <summary>
    /// 恢复：把保存的绝对位置转换成当前 Clip 上的局部位置，超出范围的截断/丢弃，
    /// 用 RebuildPreservingProgress 重建（保留 progressValue）。
    /// </summary>
    private void RestorePauseSnapshots(SplineCurveMoveClip clip, TimelineClipContext.Info info)
    {
        if (curveProp == null || !info.valid) return;

        var snapshots = clip.SavedPauseSnapshots;
        if (snapshots == null || snapshots.Count == 0) return;

        double currentClipStart = info.clip.start;
        double currentClipDuration = info.duration;
        const double MIN_PAUSE_DUR = 0.05;
        const double NEIGHBOR_GAP = 0.001;

        // 1. 把每个 snapshot 转换成局部停顿，clamp 到 [0, currentDuration]，丢弃完全超出的
        var localPauses = new List<CurvePauseQuickEdit.PauseSegment>();
        int droppedCount = 0;
        for (int i = 0; i < snapshots.Count; i++)
        {
            var s = snapshots[i];
            double localStart = s.absoluteStartSec - currentClipStart;
            double localEnd   = localStart + s.durationSec;

            localStart = System.Math.Max(0.0, localStart);
            localEnd   = System.Math.Min(currentClipDuration, localEnd);

            if (localEnd - localStart < MIN_PAUSE_DUR)
            {
                droppedCount++;
                continue;
            }

            localPauses.Add(new CurvePauseQuickEdit.PauseSegment
            {
                startSec      = (float)localStart,
                durationSec   = (float)(localEnd - localStart),
                progressValue = s.progressValue,
            });
        }

        // 2. 按 start 排序
        localPauses.Sort((a, b) => a.startSec.CompareTo(b.startSec));

        // 3. 解决相邻冲突（如果两个停顿恢复后重叠，保留前一个完整的，截断后一个的 start）
        for (int i = 1; i < localPauses.Count; i++)
        {
            var prev = localPauses[i - 1];
            var cur  = localPauses[i];
            float prevEnd = prev.startSec + prev.durationSec;
            float minStart = prevEnd + (float)NEIGHBOR_GAP;
            if (cur.startSec < minStart)
            {
                float curEnd = cur.startSec + cur.durationSec;
                float newStart = minStart;
                float newDur = curEnd - newStart;
                if (newDur < MIN_PAUSE_DUR)
                {
                    localPauses.RemoveAt(i);
                    droppedCount++;
                    i--;
                    continue;
                }
                cur.startSec = newStart;
                cur.durationSec = newDur;
                localPauses[i] = cur;
            }
        }

        // 4. 应用：用 RebuildPreservingProgress 重建（保留 path 进度）
        Undo.RecordObject(clip, "Restore Pause Snapshots");
        var newCurve = CurvePauseQuickEdit.RebuildPreservingProgress(
            currentClipDuration, localPauses, CurvePauseQuickEdit.JoinStyle.Linear);
        curveProp.animationCurveValue = newCurve;

        // ExitGUI 会跳过末尾的 ApplyModifiedProperties，必须主动 apply
        serializedObject.ApplyModifiedProperties();

        clip.LastBaselineDuration = currentClipDuration;
        EditorUtility.SetDirty(clip);

        // 同步 _editPauses
        _editPauses = null;

        if (droppedCount > 0)
        {
            Debug.LogWarning($"[Spline Pause Restore] {droppedCount} 个停顿因超出当前 Clip 范围或与邻居冲突被丢弃。");
        }
    }

    private void ReloadEditPauses(AnimationCurve curve, double duration)
    {
        _editPauses = CurvePauseQuickEdit.DetectPauses(curve, duration);
        _editPausesSourceKeyHash = ComputeCurveHash(curve);
    }

    /// <summary>简单 hash：把所有 key 的 (time, value) 加起来，用来检测 curve 是否被外部改动。</summary>
    private static int ComputeCurveHash(AnimationCurve curve)
    {
        if (curve == null) return 0;
        unchecked
        {
            int h = curve.keys.Length * 397;
            foreach (var k in curve.keys)
            {
                h = h * 31 + k.time.GetHashCode();
                h = h * 31 + k.value.GetHashCode();
            }
            return h;
        }
    }

    private void ApplyPauseEdits(SplineCurveMoveClip clip, TimelineClipContext.Info info)
    {
        if (curveProp == null) return;

        Undo.RecordObject(clip, "Apply Pause Adjustment");

        // 排序（确保 RebuildPreservingProgress 拿到正确顺序）
        _editPauses.Sort((a, b) => a.startSec.CompareTo(b.startSec));

        AnimationCurve newCurve;
        if (PauseAdjustPreserveSpeed)
        {
            // 模式 1：基于无停顿基线重建，所有行走段统一速度（progressValue 被重算）
            newCurve = CurvePauseQuickEdit.RebuildFromScratch(
                info.duration, _editPauses, CurvePauseQuickEdit.JoinStyle.Linear);
        }
        else
        {
            // 模式 2：水平平移，保留每个停顿的 progressValue（path 进度不变，前后段斜率变化）
            newCurve = CurvePauseQuickEdit.RebuildPreservingProgress(
                info.duration, _editPauses, CurvePauseQuickEdit.JoinStyle.Linear);
        }

        curveProp.animationCurveValue = newCurve;

        // Clip 时长没变，baseline 仍然对齐
        clip.LastBaselineDuration = info.duration;
        EditorUtility.SetDirty(clip);

        // 重新加载 _editPauses，反映应用后的实际状态
        ReloadEditPauses(newCurve, info.duration);
        _editPauses.Sort((a, b) => a.startSec.CompareTo(b.startSec));
    }

    /// <summary>
    /// 程序化修改 Clip 时长（插入/删除停顿、速度同步等）的统一入口。
    /// 关键：改完后把【端点拖拽 remap 基线】对齐到新的 start/duration，
    /// 否则下次用户拖端点时，OnClipChanged 会用过期的基线算出错误的位移量。
    ///
    /// 必须【同时】用两种方式写基线：
    ///   1) 直接写内存字段（SyncTrimBaseline）—— 覆盖"本方法返回后、ApplyModifiedProperties 之前"
    ///      这段窗口里可能补发的 OnClipChanged。
    ///   2) 写进 serializedObject —— 否则随后的 serializedObject.ApplyModifiedProperties() 会用
    ///      Update() 时的旧快照把上面的内存写【覆盖回旧值】，导致停顿被二次 remap（如 4s→2.9s）。
    /// 再加一个"程序化改时长发生在第几帧"的时间戳，作为 OnClipChanged 的兜底判据。
    ///
    /// 这些操作都只改 duration、不改 start，所以基线 start 用当前 info.clip.start。
    /// </summary>
    private void SetClipDurationSynced(SplineCurveMoveClip clip, TimelineClipContext.Info info, double newDuration)
    {
        if (clip == null || info.clip == null) return;
        clip.SuppressTrimRemap = true;
        try
        {
            info.clip.duration = newDuration;
            double newStart = info.clip.start;

            // (1) 立刻写内存
            clip.SyncTrimBaseline(newStart, newDuration);
            clip.LastProgrammaticDurationFrame = Time.frameCount;

            // (2) 写进 serializedObject —— 让 ApplyModifiedProperties 应用的是新值而非旧快照
            var bs = serializedObject.FindProperty("trimBaselineStart");
            var bd = serializedObject.FindProperty("trimBaselineDuration");
            if (bs != null) bs.doubleValue = newStart;
            if (bd != null) bd.doubleValue = newDuration;
        }
        finally { clip.SuppressTrimRemap = false; }
    }

    /// <summary>渲染 template 内除 excludeName 之外的所有可见字段。</summary>
    private void DrawTemplateExcept(string excludeName)
    {
        if (templateProp == null) return;
        var iterator = templateProp.Copy();
        var endProp  = iterator.GetEndProperty();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProp))
        {
            enterChildren = false;
            if (iterator.name == excludeName) continue;
            EditorGUILayout.PropertyField(iterator, true);
        }
    }
}
