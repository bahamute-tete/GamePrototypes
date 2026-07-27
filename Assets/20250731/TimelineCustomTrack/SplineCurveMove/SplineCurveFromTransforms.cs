using UnityEngine;
using UnityEngine.Timeline;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Playables;

public enum RotationKeyframeMode
{
    [Tooltip("每个关键点都使用自身的 rotation（默认行为）")]
    EveryPoint,
    [Tooltip("只使用首尾两个点的 rotation，中间点按序号比例 Slerp 过渡")]
    EndpointsOnly,
    [Tooltip("只使用 IsKey 勾选的点的 rotation。曲线在第一个 key 之前/最后一个 key 之后会 clamp。")]
    MarkedKeyframes,
}

[System.Serializable]
public class CVPointsForClip
{
    public int clipIndex;
    public List<Transform> controlPointsTransforms = new List<Transform>();
    public int resamplePoints = 30;
    [Tooltip("仅在 MarkedKeyframes 模式下生效。长度自动同步到 controlPointsTransforms。")]
    public List<bool> rotationKeyFlags = new List<bool>();

    [Tooltip("贴地用的地面模型（需含 MeshFilter，且 Mesh 勾选 Read/Write）。留空 = 该 Clip 不投影。")]
    public GameObject groundModel;
    [Tooltip("贴地后沿世界 Y 抬升的偏移，用于微调脚底离地。")]
    public float groundOffset = 0f;
}

[System.Serializable]
public class SplineCurveSetting
{
    public string trackName;
    public List<CVPointsForClip> splineCurveParametersPerClip = new List<CVPointsForClip>();

    public bool debugCurve = true;
    public bool showOriginalCurve = true;
    public Color curveColor = Color.white;

    public bool displayLable = false;
    public bool displayCurveAixe = false;
    public Vector3 visualRotationOffset = Vector3.zero;

    public bool showResampledCurve = false;
    public int resamplePoints = 30;
    public Color resampledCurveColor = Color.green;

    public CatmullRomSpline.Alpha alphaMode = CatmullRomSpline.Alpha.Centripetal;
    public CatmullRomSpline.RotationMode rotationMode = CatmullRomSpline.RotationMode.TangentWithRoll;
    public bool useKeypointRotation = true;
    public RotationKeyframeMode rotationKeyframeMode = RotationKeyframeMode.EveryPoint;

    [Range(20, 500)] public int pathResolution = 200;
    [Range(1f, 8f)] public float pathLineWidth = 3f;
    public bool showDirectionArrows = false;
    [Range(2, 30)] public int directionArrowCount = 8;
    [Range(0.05f, 0.5f)] public float directionArrowSize = 0.15f;

    // ---- 阶段 4 新增 ---- //
    [Tooltip("在曲线上等时间间隔投影 displacementCurve 的采样点。点密 = 慢，点疏 = 快。")]
    public bool showEasingPreview = false;
    [Range(10, 100)] public int easingPreviewCount = 30;
    public Color easingPreviewColor = Color.cyan;
    [Range(0.02f, 0.2f)] public float easingPreviewSize = 0.06f;

    [Tooltip("显示路径事件触发位置（沿曲线 arcLengthRatio 处）。")]
    public bool showPathEvents = true;
    [Range(0.1f, 1f)] public float pathEventSize = 0.3f;

    // ---- Phase 4 新增：Stored Spline Preview ---- //
    // 当 Clip 绑定了 referenceFrame 时，绘制【已 bake 进 Clip 的曲线数据】（应用 refFrame
    // 变换后）。这是 SceneView 里能看到的、与运行时一致的曲线——载具一动它就跟着动。
    // 与基于 Transforms 的常规预览并存：两条曲线重合 = 一切正常；分离 = bake 过期或
    // Transforms 没 parent 到 refFrame 下，需要重新 bake 或修正 parenting。
    [Tooltip("当 Clip 绑定了 referenceFrame 时，绘制已 bake 数据应用 refFrame 后的曲线。\n" +
             "这条曲线代表运行时实际路径，与载具实时同步。")]
    public bool showStoredSplinePreview = true;
    public Color storedPreviewColor = new Color(1f, 0.55f, 0.2f, 0.9f);
    [Range(1f, 8f)] public float storedPreviewLineWidth = 3f;

    // ---- UX 优化：控制点编号标签 ---- //
    // 在 Scene 视图每个控制点位置上方画"C{clipIndex}/P{pointIndex}"标号，
    // 让你在多曲线杂多场景里能肉眼快速识别"这是哪个 Clip 的第几个点"。
    [Tooltip("在 Scene 视图每个控制点上方显示 \"C{clipIndex}/P{pointIndex}\" 标号。\n" +
             "对于场景里同时存在多条曲线、控制点很多时的快速识别非常有用。\n" +
             "点位密集时可能视觉拥挤，可关闭。")]
    public bool showPointLabels = true;
    public Color pointLabelColor = new Color(1f, 1f, 0.4f, 0.95f);
    [Range(8, 24)] public int pointLabelFontSize = 11;
    [Range(0f, 2f)] public float pointLabelOffsetY = 0.3f;
}

public class SplineCurveFromTransforms : MonoBehaviour
{
    [Tooltip("要修改的 TimelineAsset 资源。")]
    public TimelineAsset timelineAsset;

    [Tooltip("Bake 前临时把 Timeline scrub 到 t=0，让所有 refFrame 回到各自的初始姿态再 bake。\n" +
             "\n" +
             "为什么需要：bake 计算 storedLocal = refFrame.InverseTransformPoint(controlPoint.worldPos)，\n" +
             "取的是 refFrame 当前那一刻的世界姿态。如果 refFrame 自己被 Timeline 驱动（典型如沿曲线\n" +
             "移动的载具），bake 时载具可能正处于动画中间状态，存储数据就是【相对于那个中间姿态】的\n" +
             "局部坐标，运行时回到 t=0 时角色看起来漂移了。\n" +
             "\n" +
             "开启后：bake 变成幂等操作——不论 playhead 在哪儿点 Update Timeline Track，结果一致。\n" +
             "推荐保持开启。仅在你确实想从【当前 playhead 状态】捕捉时关闭。")]
    public bool bakeAtTimelineStart = true;

    [Tooltip("进入 PlayMode 之前自动 bake 一次，确保运行时数据是最新的。\n" +
             "\n" +
             "为什么需要：你在 Edit 模式下移动了控制点 Transform，但忘了点 Update Timeline Track，\n" +
             "然后直接进 PlayMode 验证——结果跑的是旧 bake 数据，与你看到的 Scene 预览不一致。\n" +
             "\n" +
             "开启后：进入 PlayMode 前自动跑一遍 ApplyControlPointsToTrack，免去手动触发。\n" +
             "推荐保持开启。bake 本身很快（毫秒级），不会明显延迟 PlayMode 启动。")]
    public bool autoBakeOnPlay = true;

    public List<SplineCurveSetting> settings = new List<SplineCurveSetting>();

    public void ApplyControlPointsToTrack()
    {
        if (timelineAsset == null) { Debug.LogError("尚未分配 TimelineAsset。", this); return; }
        if (settings == null || settings.Count == 0) { Debug.LogWarning("Settings 列表为空。", this); return; }

        // ════════════════════════════════════════════════════════════════════════════════
        // Phase 4 hotfix：在 bake 前把 Timeline scrub 到 t=0，让所有 refFrame 回到初始姿态。
        //
        // 原理：bake 的核心计算是 storedLocal = refFrame.InverseTransformPoint(controlPoint.worldPos)，
        // 取的是 refFrame 在 bake 那一刻的世界姿态。当 refFrame 自身被 Timeline 驱动时，它的姿态
        // 是 time 的函数——bake 时 Director.time 在哪里，存储数据就以那一刻的姿态为参照系。
        //
        // 不做这件事的后果：用户在 Timeline 中间某帧（refFrame 处于动画过程中）点 bake，存储
        // 数据是"相对于中间姿态的局部坐标"，运行时回到 t=0 时角色看似漂移。
        //
        // 做了之后：bake 变成幂等操作——不论 playhead 在哪儿，结果都一样。仅在 Edit 模式且
        // 设置允许时生效；PlayMode 下不动 Director（避免干扰运行中的 Timeline）。
        // ════════════════════════════════════════════════════════════════════════════════
        PlayableDirector director = FindMatchingDirector();
        double savedTime = -1;
        bool didScrubToZero = false;
#if UNITY_EDITOR
        if (bakeAtTimelineStart && !Application.isPlaying && director != null && director.time > 1e-6)
        {
            savedTime = director.time;
            director.time = 0;
            director.Evaluate();   // 强制立刻按 t=0 重算所有 Track，让 refFrame Transform 落到初始姿态
            didScrubToZero = true;
        }
#endif

        try
        {
            ApplyControlPointsToTrackInternal();
        }
        finally
        {
#if UNITY_EDITOR
            if (didScrubToZero && director != null)
            {
                director.time = savedTime;
                director.Evaluate();   // 恢复原 playhead 位置（避免 bake 看起来"跳到 t=0 没回来"）
            }
#endif
        }
    }

    /// <summary>
    /// ApplyControlPointsToTrack 的实际 bake 逻辑（已被外层包了 Director scrub 处理）。
    /// 拆分出来保持 try/finally 结构清晰。
    /// </summary>
    private void ApplyControlPointsToTrackInternal()
    {
        foreach (var setting in settings)
        {
            if (string.IsNullOrEmpty(setting.trackName))
            {
                Debug.LogError("Settings 中有一项未指定轨道名称。", this);
                continue;
            }

            TrackAsset targetTrack = null;
            foreach (var track in timelineAsset.GetOutputTracks())
            {
                if (track.name == setting.trackName) { targetTrack = track; break; }
            }
            if (targetTrack == null)
            {
                Debug.LogError($"未找到轨道 '{setting.trackName}'。", this);
                continue;
            }
            if (!(targetTrack is SplineCurveMoveTrack splineTrack))
            {
                Debug.LogError($"轨道 '{setting.trackName}' 不是 SplineCurveMoveTrack。", this);
                continue;
            }

            var clipArray = splineTrack.GetClips().ToArray();
            for (int i = 0; i < clipArray.Length; i++)
            {
                var splineClip = clipArray[i].asset as SplineCurveMoveClip;
                if (splineClip == null) continue;

                var behaviour = splineClip.Template;
                var clipSettings = setting.splineCurveParametersPerClip.FirstOrDefault(cp => cp.clipIndex == i);
                if (clipSettings == null) continue;

                SyncKeyFlagsLength(clipSettings);

                behaviour.Spline.ControlPoints.Clear();
                behaviour.Spline.ControlRotations.Clear();
                behaviour.Spline.AlphaMode = setting.alphaMode;
                behaviour.RotationMode = setting.rotationMode;

                // ============================================================
                // Phase 4：解析此 Clip 的参考系 Transform
                // ============================================================
                // 若 Clip 的 referenceFrame 已在 PlayableDirector 的 Scene Bindings 中绑定，
                // bake 时把 Transform 的世界坐标 → refFrame 局部坐标存储。
                // 运行时 SplineCurveMoveMixerBehaviour 会自动反向变换回世界。
                // refFrame=null 时退化为世界坐标存储——完全等价于原行为。
                Transform refFrame = TryResolveClipReferenceFrame(splineClip);
                bool useLocal = refFrame != null;
                Quaternion refFrameInvRot = useLocal ? Quaternion.Inverse(refFrame.rotation) : Quaternion.identity;

                var validTransforms = new List<Transform>();
                if (clipSettings.controlPointsTransforms != null)
                {
                    foreach (var t in clipSettings.controlPointsTransforms)
                    {
                        if (t == null) continue;
                        Vector3 storedPos = useLocal
                            ? refFrame.InverseTransformPoint(t.position)
                            : t.position;
                        behaviour.Spline.AddPoint(storedPos);
                        validTransforms.Add(t);
                    }
                }

                if (setting.useKeypointRotation && validTransforms.Count > 0)
                {
                    var rotations = ResolveValidRotations(
                        clipSettings.controlPointsTransforms,
                        clipSettings.rotationKeyFlags,
                        setting.rotationKeyframeMode);

                    for (int k = 0; k < rotations.Count && k < validTransforms.Count; k++)
                    {
                        Quaternion storedRot = useLocal
                            ? refFrameInvRot * rotations[k]
                            : rotations[k];
                        behaviour.Spline.SetRotation(k, storedRot);
                    }
                }

                behaviour.Spline.InvalidateCache();
                behaviour.RotationOffset = setting.visualRotationOffset;

                // 贴地投影：指定了地面模型则烘高度 LUT，否则清除
                ApplyGroundProjection(behaviour.Spline, clipSettings);

#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(splineClip);
#endif
            }
        }
    }

    /// <summary>
    /// 指定了 groundModel 则把该 clip 的样条行走路径向下投影到地面、烘成高度 LUT；否则清除。
    /// 用 LiangZhu.Geometry.RayMesh（BVH），不依赖 Collider / 物理场景。
    /// </summary>
    private static void ApplyGroundProjection(CatmullRomSpline spline, CVPointsForClip clipSettings)
    {
        if (spline == null || clipSettings == null) return;

        if (clipSettings.groundModel == null) { spline.ClearGroundProjection(); return; }

        if (!TryBuildGroundSampler(clipSettings.groundModel, out var sampler))
        {
            spline.ClearGroundProjection();   // 模型不可用 → 不投影（内部已 LogError）
            return;
        }
        spline.BakeGroundProjection(sampler, clipSettings.groundOffset);
    }

    /// <summary>
    /// 把地面模型包成"向下射线"采样闭包：(世界点)->(是否命中, 命中世界 Y)。
    /// 射线起点取地面 AABB 顶部之上，保证无论采样点在地面上方还是下方都能从上往下打中。
    /// </summary>
    private static bool TryBuildGroundSampler(GameObject ground, out System.Func<Vector3, (bool hit, float y)> sampler)
    {
        sampler = null;
        var mf = ground.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        { Debug.LogError($"[GroundProjection] '{ground.name}' 下找不到带 Mesh 的 MeshFilter。", ground); return false; }

        var mesh = mf.sharedMesh;
        if (!mesh.isReadable)
        { Debug.LogError($"[GroundProjection] Mesh '{mesh.name}' 未开启 Read/Write，请在导入设置里勾选。", ground); return false; }

        var rayMesh = LiangZhu.Geometry.RayMesh.FromMesh(mesh, mf.transform);

        var rend = mf.GetComponent<Renderer>();
        bool hasB = rend != null;
        float top = hasB ? rend.bounds.max.y : 0f;
        float bottom = hasB ? rend.bounds.min.y : 0f;
        const float margin = 1f, fallbackUp = 50f;

        sampler = (p) =>
        {
            float startY = hasB ? Mathf.Max(p.y, top) + margin : p.y + fallbackUp;
            float maxDist = hasB ? (startY - bottom) + margin : fallbackUp * 2f;
            if (rayMesh.Raycast(new Vector3(p.x, startY, p.z), Vector3.down, maxDist, out var h, cullBackface: false))
                return (true, h.point.y);
            return (false, 0f);
        };
        return true;
    }

    /// <summary>
    /// Phase 4：解析指定 Clip 的 referenceFrame ExposedReference 为运行时 Transform。
    /// 返回 null 时 bake 退化为世界坐标存储（向后兼容原行为）。
    /// </summary>
    private Transform TryResolveClipReferenceFrame(SplineCurveMoveClip clip)
    {
        if (clip == null) return null;
        var dir = FindMatchingDirector();
        if (dir == null) return null;
        return clip.referenceFrame.Resolve(dir);
    }

    /// <summary>
    /// 在场景里查找 playableAsset == 本 timelineAsset 的 PlayableDirector。
    /// 用作 ExposedReference 解析上下文，以及 bake 前 scrub 到 t=0 的目标 Director。
    ///
    /// 注意事项：
    ///   - 场景里可能有多个 Director 引用同一 TimelineAsset（少见但可能），此实现返回首个找到的
    ///   - FindObjectsOfType 不包含 inactive 物体；Director 所在 GameObject 必须 enabled 才能被找到
    /// </summary>
    private UnityEngine.Playables.PlayableDirector FindMatchingDirector()
    {
        if (timelineAsset == null) return null;
        var directors = UnityEngine.Object.FindObjectsOfType<UnityEngine.Playables.PlayableDirector>();
        foreach (var d in directors)
        {
            if (d != null && d.playableAsset == timelineAsset)
                return d;
        }
        return null;
    }

    public void RedistributeClipControlPoints(int settingIndex, int clipIndex, int targetCount)
    {
        if (settingIndex < 0 || settingIndex >= settings.Count) return;
        var setting = settings[settingIndex];
        var clipSettings = setting.splineCurveParametersPerClip.FirstOrDefault(cp => cp.clipIndex == clipIndex);
        if (clipSettings == null || clipSettings.controlPointsTransforms.Count < 2) return;

        SyncKeyFlagsLength(clipSettings);

        var temp = new CatmullRomSpline { AlphaMode = setting.alphaMode };
        var validTransforms = new List<Transform>();
        foreach (var t in clipSettings.controlPointsTransforms)
        {
            if (t == null) continue;
            temp.AddPoint(t.position);
            validTransforms.Add(t);
        }

        if (setting.useKeypointRotation)
        {
            var rotations = ResolveValidRotations(
                clipSettings.controlPointsTransforms,
                clipSettings.rotationKeyFlags,
                setting.rotationKeyframeMode);
            for (int i = 0; i < rotations.Count && i < validTransforms.Count; i++)
                temp.SetRotation(i, rotations[i]);
        }

        targetCount = Mathf.Clamp(targetCount, 2, validTransforms.Count);

#if UNITY_EDITOR
        UnityEditor.Undo.RecordObjects(validTransforms.Take(targetCount).Cast<UnityEngine.Object>().ToArray(),
            "Redistribute Spline Points");
#endif

        for (int i = 0; i < targetCount; i++)
        {
            float s = (float)i / (targetCount - 1);
            float tCurve = temp.ArcLengthToT(s);
            validTransforms[i].position = temp.GetPoint(tCurve);
            if (setting.useKeypointRotation)
                validTransforms[i].rotation = temp.GetRotation(tCurve, CatmullRomSpline.RotationMode.KeyframeOnly);
        }
    }

    public static void SyncKeyFlagsLength(CVPointsForClip clip)
    {
        if (clip == null) return;
        if (clip.rotationKeyFlags == null) clip.rotationKeyFlags = new List<bool>();
        int target = clip.controlPointsTransforms?.Count ?? 0;
        while (clip.rotationKeyFlags.Count < target) clip.rotationKeyFlags.Add(false);
        while (clip.rotationKeyFlags.Count > target) clip.rotationKeyFlags.RemoveAt(clip.rotationKeyFlags.Count - 1);
    }

    private static void FilterValid(
        List<Transform> transforms, List<bool> keyFlags,
        out List<Transform> valid, out List<bool> validFlags)
    {
        valid = new List<Transform>();
        validFlags = new List<bool>();
        if (transforms == null) return;
        for (int i = 0; i < transforms.Count; i++)
        {
            if (transforms[i] == null) continue;
            valid.Add(transforms[i]);
            validFlags.Add(keyFlags != null && i < keyFlags.Count && keyFlags[i]);
        }
    }

    private static List<int> ComputeKeyIndices(int n, List<bool> validFlags, RotationKeyframeMode mode)
    {
        var keyIndices = new List<int>();
        if (n == 0) return keyIndices;

        if (mode == RotationKeyframeMode.EveryPoint)
        {
            for (int i = 0; i < n; i++) keyIndices.Add(i);
        }
        else if (mode == RotationKeyframeMode.EndpointsOnly)
        {
            keyIndices.Add(0);
            if (n > 1) keyIndices.Add(n - 1);
        }
        else
        {
            for (int i = 0; i < n; i++)
                if (validFlags[i]) keyIndices.Add(i);

            if (keyIndices.Count == 0)
            {
                keyIndices.Add(0);
                if (n > 1) keyIndices.Add(n - 1);
            }
        }
        return keyIndices;
    }

    public static List<int> ResolveValidKeyIndices(
        List<Transform> transforms, List<bool> keyFlags, RotationKeyframeMode mode)
    {
        FilterValid(transforms, keyFlags, out var valid, out var validFlags);
        return ComputeKeyIndices(valid.Count, validFlags, mode);
    }

    public static List<Quaternion> ResolveValidRotations(
        List<Transform> transforms, List<bool> keyFlags, RotationKeyframeMode mode)
    {
        var result = new List<Quaternion>();
        FilterValid(transforms, keyFlags, out var valid, out var validFlags);

        int n = valid.Count;
        if (n == 0) return result;
        if (n == 1) { result.Add(valid[0].rotation); return result; }

        var keyIndices = ComputeKeyIndices(n, validFlags, mode);
        if (keyIndices.Count == 0)
        {
            for (int i = 0; i < n; i++) result.Add(Quaternion.identity);
            return result;
        }

        int firstKey = keyIndices[0];
        int lastKey = keyIndices[keyIndices.Count - 1];

        for (int i = 0; i < n; i++)
        {
            if (i <= firstKey) { result.Add(valid[firstKey].rotation); continue; }
            if (i >= lastKey) { result.Add(valid[lastKey].rotation); continue; }

            int prevKey = firstKey;
            int nextKey = lastKey;
            for (int j = 0; j < keyIndices.Count - 1; j++)
            {
                if (keyIndices[j] <= i && keyIndices[j + 1] >= i)
                {
                    prevKey = keyIndices[j];
                    nextKey = keyIndices[j + 1];
                    break;
                }
            }

            if (prevKey == nextKey)
                result.Add(valid[prevKey].rotation);
            else
            {
                float frac = (float)(i - prevKey) / (nextKey - prevKey);
                result.Add(Quaternion.Slerp(valid[prevKey].rotation, valid[nextKey].rotation, frac));
            }
        }

        return result;
    }

#if UNITY_EDITOR
    private struct GizmoCache
    {
        public CatmullRomSpline spline;
        public Vector3[] pathPoints;
        public Vector3[] samplePoints;
        public int controlPointHash;
        public int pathResolution;
    }

    private readonly Dictionary<(int, int), GizmoCache> _gizmoCaches = new Dictionary<(int, int), GizmoCache>();

    private static int HashTransforms(List<Transform> list, List<bool> flags,
                                      CatmullRomSpline.Alpha alpha, CatmullRomSpline.RotationMode rotMode,
                                      RotationKeyframeMode keyMode, bool useRot, int sampleRes, int pathRes)
    {
        unchecked
        {
            int h = (int)alpha * 31 + (int)rotMode;
            h = h * 31 + (int)keyMode;
            h = h * 31 + (useRot ? 1 : 0);
            h = h * 31 + sampleRes;
            h = h * 31 + pathRes;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t == null) { h = h * 31; continue; }
                h = h * 31 + t.position.GetHashCode();
                if (useRot) h = h * 31 + t.rotation.GetHashCode();
            }
            if (flags != null && useRot)
                for (int i = 0; i < flags.Count; i++) h = h * 31 + (flags[i] ? 1 : 0);
            return h;
        }
    }

    /// <summary>
    /// 阶段 4：在 timelineAsset 中找到指定 trackName 的指定 clipIndex 对应的 SplineCurveMoveClip。
    /// </summary>
    private SplineCurveMoveClip FindSplineClip(string trackName, int clipIndex)
    {
        if (timelineAsset == null) return null;
        foreach (var track in timelineAsset.GetOutputTracks())
        {
            if (track.name != trackName) continue;
            if (!(track is SplineCurveMoveTrack st)) continue;
            int idx = 0;
            foreach (var clip in st.GetClips())
            {
                if (idx == clipIndex) return clip.asset as SplineCurveMoveClip;
                idx++;
            }
        }
        return null;
    }

    /// <summary>
    /// 反查：给定一个 SplineCurveMoveClip asset，找出本组件里管理它的那一组控制点 Transform 列表。
    ///
    /// 关联机制：bake 是用 FindSplineClip(trackName, clipIndex) 把每个 (setting, item) 正向映射到
    /// 一个 Clip asset 的。这里用【同一个映射函数】反向匹配——遍历所有 (setting, item)，对每个
    /// 调用 FindSplineClip，若返回的 asset 实例 == 传入的 clipAsset，即命中。
    ///
    /// 用对象引用相等判断（==），不依赖 trackName 拼写或 clipIndex 数值的"看起来对不对"：
    /// 只要 bake 当初能写进这个 Clip，现在就能反查回来，逻辑自洽。
    ///
    /// 返回值：
    ///   - true + controlPoints：找到了（controlPoints 可能含已删除的 null 元素，调用方需判空）
    ///   - false：本组件不管理这个 Clip（它可能由别的组件管理，或根本不是本工具 bake 的）
    ///
    /// 边角情况：理论上同一组件内多个 item 可能映射到同一 Clip（异常配置）。此实现返回首个命中，
    /// 并在发现重复时 Console 警告。
    /// </summary>
    public bool TryFindControlPointsForClip(SplineCurveMoveClip clipAsset, out List<Transform> controlPoints)
    {
        controlPoints = null;
        if (clipAsset == null || timelineAsset == null || settings == null) return false;

        int hitCount = 0;
        foreach (var setting in settings)
        {
            if (setting == null || setting.splineCurveParametersPerClip == null) continue;
            foreach (var item in setting.splineCurveParametersPerClip)
            {
                if (item == null) continue;
                var mapped = FindSplineClip(setting.trackName, item.clipIndex);
                if (mapped == clipAsset)
                {
                    if (hitCount == 0)
                        controlPoints = item.controlPointsTransforms;
                    hitCount++;
                }
            }
        }

        if (hitCount > 1)
            Debug.LogWarning($"[Spline] Clip【{clipAsset.name}】在 {name} 里有 {hitCount} 处重复映射，定位用首个。检查 settings 是否有重复 trackName/clipIndex 配置。", this);

        return controlPoints != null;
    }


    // ─── 贴地预览：按 groundModel 缓存 RayMesh，避免拖控制点时每帧重建 BVH ───
    private struct GroundRayCache
    {
        public Mesh mesh;
        public Matrix4x4 l2w;
        public LiangZhu.Geometry.RayMesh rayMesh;
        public bool hasBounds;
        public float top, bottom;
    }
    private readonly Dictionary<int, GroundRayCache> _groundRayCaches = new Dictionary<int, GroundRayCache>();

    /// <summary>
    /// 取（或重建）groundModel 的向下射线采样闭包，供 gizmo 预览贴地曲线。
    /// RayMesh(BVH) 按 groundModel 缓存，仅在 Mesh 或地面世界变换变化时重建；
    /// 拖控制点时复用。Mesh 不可读 / 无 MeshFilter 时静默失败（不刷 Log）。
    /// </summary>
    private bool TryGetGroundSamplerCached(GameObject ground, out System.Func<Vector3, (bool hit, float y)> sampler)
    {
        sampler = null;
        if (ground == null) return false;

        var mf = ground.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) return false;

        int key = ground.GetInstanceID();
        Matrix4x4 l2w = mf.transform.localToWorldMatrix;

        if (!_groundRayCaches.TryGetValue(key, out var e) || e.mesh != mf.sharedMesh || e.l2w != l2w)
        {
            var rend = mf.GetComponent<Renderer>();
            e = new GroundRayCache
            {
                mesh = mf.sharedMesh,
                l2w = l2w,
                rayMesh = LiangZhu.Geometry.RayMesh.FromMesh(mf.sharedMesh, mf.transform),
                hasBounds = rend != null,
                top = rend != null ? rend.bounds.max.y : 0f,
                bottom = rend != null ? rend.bounds.min.y : 0f,
            };
            _groundRayCaches[key] = e;
        }

        var entry = e;
        const float margin = 1f, fallbackUp = 50f;
        sampler = (p) =>
        {
            float startY = entry.hasBounds ? Mathf.Max(p.y, entry.top) + margin : p.y + fallbackUp;
            float maxDist = entry.hasBounds ? (startY - entry.bottom) + margin : fallbackUp * 2f;
            if (entry.rayMesh.Raycast(new Vector3(p.x, startY, p.z), Vector3.down, maxDist, out var h, cullBackface: false))
                return (true, h.point.y);
            return (false, 0f);
        };
        return true;
    }

    private void OnDrawGizmos()
    {
        if (settings == null) return;

        for (int sIdx = 0; sIdx < settings.Count; sIdx++)
        {
            var setting = settings[sIdx];
            // settings 列表的基本健全性（无 Clip 配置无法往下走）
            if (setting.splineCurveParametersPerClip == null) continue;

            for (int cIdx = 0; cIdx < setting.splineCurveParametersPerClip.Count; cIdx++)
            {
                var item = setting.splineCurveParametersPerClip[cIdx];

                // ────────────────────────────────────────────────────────────────
                // Phase 4：Stored Spline Preview——直接读 Clip 已 bake 数据并应用 refFrame 变换。
                //
                // 独立于 debugCurve 主开关：
                //   - debugCurve 控制的是【基于 Transforms 的创作调试可视化】（曲线本体 / 采样球 /
                //     方向箭头 / 路径事件等），属于创作流程的中间产物
                //   - Stored Preview 是【运行时实际路径的可视化】，读的是 Clip 自身数据，
                //     与创作中间产物无关，由独立的 showStoredSplinePreview 控制
                //
                // 也独立于 controlPointsTransforms.Count<2 早退：Stored Preview 不需要任何
                // 控制点 Transforms 存在，只要 Clip 自身 bake 过数据就能画。
                // ────────────────────────────────────────────────────────────────
                if (setting.showStoredSplinePreview)
                    DrawStoredSplinePreview(setting, item);

                // ────────────────────────────────────────────────────────────────
                // UX 优化：控制点编号标签——同样独立于 debugCurve 主开关。
                // 这是"识别"性质的辅助标记，多曲线场景里快速看出"哪个点是哪个 Clip 的第几个"。
                // 不依赖 controlPointsTransforms.Count<2 早退——只要列表里有点就标。
                // ────────────────────────────────────────────────────────────────
                if (setting.showPointLabels && item.controlPointsTransforms != null && item.controlPointsTransforms.Count > 0)
                    DrawPointLabels(setting, item);

                // 以下所有可视化由 debugCurve 主开关控制（基于 Transforms 的创作调试）
                if (!setting.debugCurve) continue;
                if (item.controlPointsTransforms.Count < 2) continue;

                SyncKeyFlagsLength(item);

                var key = (sIdx, cIdx);
                int pathRes = Mathf.Max(20, setting.pathResolution);
                int sampleRes = Mathf.Max(2, item.resamplePoints);

                int hash = HashTransforms(item.controlPointsTransforms, item.rotationKeyFlags,
                                          setting.alphaMode, setting.rotationMode,
                                          setting.rotationKeyframeMode, setting.useKeypointRotation,
                                          sampleRes, pathRes);

                // 贴地字段纳入 hash：改 groundModel / groundOffset / 移动地面时刷新预览
                unchecked
                {
                    hash = hash * 31 + (item.groundModel != null ? item.groundModel.GetInstanceID() : 0);
                    hash = hash * 31 + item.groundOffset.GetHashCode();
                    if (item.groundModel != null)
                    {
                        var gmf = item.groundModel.GetComponentInChildren<MeshFilter>();
                        if (gmf != null) hash = hash * 31 + gmf.transform.localToWorldMatrix.GetHashCode();
                    }
                }


                if (!_gizmoCaches.TryGetValue(key, out var cache) || cache.controlPointHash != hash)
                {
                    cache.spline = new CatmullRomSpline { AlphaMode = setting.alphaMode };

                    var validTransforms = new List<Transform>();
                    foreach (var t in item.controlPointsTransforms)
                    {
                        if (t == null) continue;
                        cache.spline.AddPoint(t.position);
                        validTransforms.Add(t);
                    }

                    if (setting.useKeypointRotation && validTransforms.Count > 0)
                    {
                        var rotations = ResolveValidRotations(
                            item.controlPointsTransforms,
                            item.rotationKeyFlags,
                            setting.rotationKeyframeMode);
                        for (int k = 0; k < rotations.Count; k++)
                            cache.spline.SetRotation(k, rotations[k]);
                    }

                    // 贴地预览：指定了地面模型则把 gizmo 曲线投影到地面（路径/采样点/箭头/事件一起贴地）
                    if (item.groundModel != null &&
                        TryGetGroundSamplerCached(item.groundModel, out var gizmoGroundSampler))
                    {
                        cache.spline.BakeGroundProjection(gizmoGroundSampler, item.groundOffset);
                    }

                    cache.pathPoints = new Vector3[pathRes];
                    for (int i = 0; i < pathRes; i++)
                    {
                        float s = (float)i / (pathRes - 1);
                        cache.pathPoints[i] = cache.spline.GetPointByArcLength(s);
                    }

                    cache.samplePoints = new Vector3[sampleRes];
                    for (int i = 0; i < sampleRes; i++)
                    {
                        float s = (float)i / (sampleRes - 1);
                        cache.samplePoints[i] = cache.spline.GetPointByArcLength(s);
                    }

                    cache.controlPointHash = hash;
                    cache.pathResolution = pathRes;
                    _gizmoCaches[key] = cache;
                }

                if (setting.showOriginalCurve)
                {
                    UnityEditor.Handles.color = setting.curveColor;
                    UnityEditor.Handles.DrawAAPolyLine(setting.pathLineWidth, cache.pathPoints);
                }

                if (setting.showResampledCurve)
                {
                    UnityEditor.Handles.color = setting.resampledCurveColor;
                    foreach (var p in cache.samplePoints)
                        UnityEditor.Handles.SphereHandleCap(0, p, Quaternion.identity, 0.05f, EventType.Repaint);
                }

                if (setting.showDirectionArrows)
                    DrawDirectionArrows(cache.spline, setting);

                if (setting.showResampledCurve && setting.displayCurveAixe)
                    DrawAxes(cache.spline, sampleRes, setting);

                if (setting.showResampledCurve && setting.displayLable)
                    DrawLabels(cache.spline, cache.samplePoints, setting, item.clipIndex);

                // 阶段 4：缓动曲线投影
                if (setting.showEasingPreview)
                    DrawEasingPreview(cache.spline, setting, item.clipIndex);

                // 阶段 4：路径事件 Gizmo
                if (setting.showPathEvents)
                    DrawPathEvents(cache.spline, setting, item.clipIndex);

                if (setting.displayLable)
                {
                    UnityEditor.Handles.color = setting.resampledCurveColor;
                    for (int i = 0; i < item.controlPointsTransforms.Count; i++)
                    {
                        var t = item.controlPointsTransforms[i];
                        if (t == null) continue;
                        UnityEditor.Handles.SphereHandleCap(0, t.position, Quaternion.identity, 0.2f, EventType.Repaint);
                        UnityEditor.Handles.Label(t.position,
                            $"Point_{i}\n({t.position.x:F2}, {t.position.y:F2}, {t.position.z:F2})");
                    }
                }
            }
        }
    }

    private void DrawDirectionArrows(CatmullRomSpline spline, SplineCurveSetting setting)
    {
        int count = Mathf.Max(2, setting.directionArrowCount);
        float size = setting.directionArrowSize;
        UnityEditor.Handles.color = setting.curveColor;
        for (int i = 0; i < count; i++)
        {
            float s = Mathf.Lerp(0.05f, 0.95f, (float)i / (count - 1));
            float t = spline.ArcLengthToT(s);
            Vector3 pos = spline.GetPoint(t);
            Vector3 tangent = spline.GetTangent(t);
            if (tangent.sqrMagnitude < 1e-8f) continue;
            UnityEditor.Handles.ConeHandleCap(0, pos, Quaternion.LookRotation(tangent), size, EventType.Repaint);
        }
    }

    private void DrawAxes(CatmullRomSpline spline, int resolution, SplineCurveSetting setting)
    {
        for (int i = 0; i < resolution; i++)
        {
            float s = (float)i / (resolution - 1);
            float t = spline.ArcLengthToT(s);
            Vector3 p = spline.GetPoint(t);
            Quaternion rot = spline.GetRotation(t, setting.rotationMode, setting.visualRotationOffset);
            Vector3 fwd = rot * Vector3.forward;
            Vector3 up = rot * Vector3.up;
            Vector3 right = rot * Vector3.right;

            UnityEditor.Handles.color = Color.blue;
            UnityEditor.Handles.ArrowHandleCap(0, p, Quaternion.LookRotation(fwd), 0.15f, EventType.Repaint);
            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.ArrowHandleCap(0, p, Quaternion.LookRotation(up), 0.15f, EventType.Repaint);
            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.ArrowHandleCap(0, p, Quaternion.LookRotation(right), 0.15f, EventType.Repaint);
        }
    }

    private static GUIStyle _labelStyle;
    private void DrawLabels(CatmullRomSpline spline, Vector3[] points, SplineCurveSetting setting, int clipIndex)
    {
        if (_labelStyle == null) _labelStyle = new GUIStyle { fontSize = 10 };
        _labelStyle.normal.textColor = setting.resampledCurveColor;

        float clipDuration = 0f;
        if (timelineAsset != null)
        {
            foreach (var track in timelineAsset.GetOutputTracks())
            {
                if (track.name != setting.trackName) continue;
                if (!(track is SplineCurveMoveTrack st)) continue;
                int idx = 0;
                foreach (var clip in st.GetClips())
                {
                    if (idx == clipIndex) { clipDuration = (float)clip.duration; break; }
                    idx++;
                }
                break;
            }
        }

        for (int i = 0; i < points.Length; i++)
        {
            float t = (float)i / (points.Length - 1);
            UnityEditor.Handles.Label(points[i] + new Vector3(0, -0.1f, 0),
                $"({clipDuration * t:F2},{t:F2})", _labelStyle);
        }
    }

    /// <summary>
    /// 阶段 4：投影 displacementCurve 的等时间隔采样到曲线上。
    /// 点密表示运动慢，点疏表示运动快。
    /// </summary>
    private void DrawEasingPreview(CatmullRomSpline spline, SplineCurveSetting setting, int clipIndex)
    {
        var splineClip = FindSplineClip(setting.trackName, clipIndex);
        if (splineClip == null) return;
        var curve = splineClip.Template.DisplacementCurve;
        if (curve == null) return;

        UnityEditor.Handles.color = setting.easingPreviewColor;
        int N = Mathf.Max(2, setting.easingPreviewCount);
        for (int i = 0; i <= N; i++)
        {
            float tn = (float)i / N;
            float s = Mathf.Clamp01(curve.Evaluate(tn));
            Vector3 pos = spline.GetPointByArcLength(s);
            UnityEditor.Handles.SphereHandleCap(0, pos, Quaternion.identity, setting.easingPreviewSize, EventType.Repaint);
        }
    }

    /// <summary>
    /// 阶段 4：在 arcLengthRatio 处显示路径事件标记。
    /// </summary>
    private void DrawPathEvents(CatmullRomSpline spline, SplineCurveSetting setting, int clipIndex)
    {
        var splineClip = FindSplineClip(setting.trackName, clipIndex);
        if (splineClip == null || splineClip.PathEvents == null) return;

        for (int i = 0; i < splineClip.PathEvents.Count; i++)
        {
            var ev = splineClip.PathEvents[i];
            if (ev == null) continue;

            float s = Mathf.Clamp01(ev.arcLengthRatio);
            Vector3 pos = spline.GetPointByArcLength(s);
            Vector3 tan = spline.GetTangentByArcLength(s);
            if (tan.sqrMagnitude < 1e-8f) tan = Vector3.forward;

            UnityEditor.Handles.color = ev.gizmoColor;
            UnityEditor.Handles.ConeHandleCap(0, pos + Vector3.up * 0.2f,
                Quaternion.LookRotation(Vector3.down), setting.pathEventSize, EventType.Repaint);
            UnityEditor.Handles.DrawLine(pos, pos + Vector3.up * 0.2f);

            UnityEditor.Handles.Label(pos + Vector3.up * 0.3f,
                $"<b>{ev.eventName}</b>\n<size=9>s={s:F2}</size>",
                new GUIStyle { richText = true, normal = { textColor = ev.gizmoColor }, fontSize = 11 });
        }
    }

    /// <summary>
    /// Phase 4：直接读取 Clip 已 bake 数据，应用 refFrame 变换后绘制曲线。
    ///
    /// 与基于 Transforms 的常规预览不同：
    ///   - 常规预览：每帧读控制点 Transform 的世界位置，建临时 spline 画线。
    ///     若 Transforms 没 parent 到 refFrame 下，载具移动时它不会跟随。
    ///   - 此预览：读 Clip 的【已存储数据】，应用当前 refFrame 实时变换。
    ///     refFrame 一动它就动——这是运行时实际看到的路径。
    ///
    /// 视觉对比：
    ///   - 两条曲线重合 ＝ 一切正常（stored 数据是最新的 + Transforms 已 parent）
    ///   - 两条曲线分离 ＝ bake 过期 或 Transforms 未 parent 到 refFrame——需要修正
    ///
    /// 失败时的诊断：以前任何前置条件失败都静默 return，新场景里用户不知道为什么没显示。
    /// 现在改为：refFrame 解析失败 + 用户显有绑定意图 → 画警告标签；
    /// stored 数据为空（Clip 还没 bake 过）→ 画警告标签提示去点 Update Timeline Track。
    /// </summary>
    private void DrawStoredSplinePreview(SplineCurveSetting setting, CVPointsForClip item)
    {
        var splineClip = FindSplineClip(setting.trackName, item.clipIndex);
        if (splineClip == null) return;  // trackName/clipIndex 配置问题，留给其他诊断处理

        Transform refFrame = TryResolveClipReferenceFrame(splineClip);

        if (refFrame == null)
        {
            // refFrame=null 在两种情况下出现：
            //   ① 用户主动不设（合理的世界空间模式）→ 静默，stored 预览本应等于 Transforms 预览，无需绘制
            //   ② 用户设了字段但 Director/绑定有问题导致解析不出 → 画警告
            if (HasReferenceFrameIntent(splineClip))
            {
                DrawDiagnosticLabel(setting, item,
                    "⚠ Reference Frame 字段已设置但解析失败\n" +
                    "可能原因：\n" +
                    "  • 场景里没有指向本 TimelineAsset 的 PlayableDirector\n" +
                    "  • 绑定保存在错误的 Director 上（在正确 Director 的 Timeline 窗口里重新拖一遍）\n" +
                    "  • SplineCurveFromTransforms.timelineAsset 与 Director 的 playableAsset 不是同一个");
            }
            return;
        }

        var stored = splineClip.Template != null ? splineClip.Template.Spline : null;
        if (stored == null || stored.ControlPoints.Count < 2)
        {
            DrawDiagnosticLabel(setting, item,
                $"⚠ Stored Spline 数据为空或不足 2 个控制点\n" +
                $"refFrame 已绑定到【{refFrame.name}】，但 Clip 里还没 bake 过数据。\n" +
                $"点【Update Timeline Track】生成 stored 数据。");
            return;
        }

        int previewRes = Mathf.Max(20, setting.pathResolution);
        var pts = new Vector3[previewRes];
        for (int p = 0; p < previewRes; p++)
        {
            float s = (float)p / (previewRes - 1);
            float t = stored.ArcLengthToT(s);
            Vector3 localPt = stored.GetPoint(t);
            pts[p] = refFrame.TransformPoint(localPt);
        }

        UnityEditor.Handles.color = setting.storedPreviewColor;
        UnityEditor.Handles.DrawAAPolyLine(setting.storedPreviewLineWidth, pts);
    }

    /// <summary>
    /// 检查 Clip 的 referenceFrame ExposedReference 是否被用户"赋过值"。
    /// 区分两种 refFrame=null 的情况，避免在世界空间模式下画无意义的警告。
    ///
    /// ExposedReference 内部：exposedName (PropertyName) + defaultValue (Object)。
    /// 任一非默认 → 用户尝试过绑定。
    /// </summary>
    private static bool HasReferenceFrameIntent(SplineCurveMoveClip clip)
    {
        if (clip == null) return false;
        var refFrame = clip.referenceFrame;
        if (refFrame.defaultValue != null) return true;
        // PropertyName 是 struct，默认状态对应内部 id = 0。
        // != default(PropertyName) 在 Unity 实现里能区分"未赋值"和"已赋 GUID"。
        if (refFrame.exposedName != default(PropertyName)) return true;
        return false;
    }

    /// <summary>
    /// 在 SplineCurveFromTransforms 的位置上方画一个诊断文字标签。
    /// 不同 Clip 的标签按 clipIndex 错开，避免叠在一起。
    /// </summary>
    private void DrawDiagnosticLabel(SplineCurveSetting setting, CVPointsForClip item, string message)
    {
        Vector3 pos = transform.position + Vector3.up * (1.5f + item.clipIndex * 0.7f);
        var style = new GUIStyle
        {
            normal = { textColor = new Color(1f, 0.7f, 0.3f) },
            fontSize = 11,
            richText = false,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false
        };
        UnityEditor.Handles.Label(pos,
            $"[{setting.trackName} → Clip {item.clipIndex}]\n{message}",
            style);
    }

    /// <summary>
    /// UX 优化：在 Scene 视图每个控制点上方画 "C{clipIndex}/P{pointIndex}" 编号标签。
    ///
    /// 用途：多曲线场景里靠肉眼快速识别"这是哪个 Clip 的第几个点"。和 Inspector 的 🔍 Frame
    /// 按钮配合：你在 Scene 里扫一眼看到目标点是 C0/P3 → 在 Inspector 里点对应行的 🔍 → 一秒命中。
    ///
    /// 性能：每帧重建 GUIStyle 略浪费但可读性最好；缓存留作后续优化。OnDrawGizmos 在 Editor 下
    /// 重绘频率不高（只在 Scene 视图刷新时调用），实测无可感卡顿。
    ///
    /// 多列表（多个 Setting 对应多条曲线）时编号在视觉上的辨识度由 setting.pointLabelColor 区分。
    /// </summary>
    private void DrawPointLabels(SplineCurveSetting setting, CVPointsForClip item)
    {
        var style = new GUIStyle
        {
            normal = { textColor = setting.pointLabelColor },
            fontSize = setting.pointLabelFontSize,
            fontStyle = FontStyle.Bold,
            richText = false,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false
        };

        Vector3 offset = Vector3.up * setting.pointLabelOffsetY;
        for (int k = 0; k < item.controlPointsTransforms.Count; k++)
        {
            var t = item.controlPointsTransforms[k];
            if (t == null) continue;
            // 格式 "C0/P3"——紧凑足够，5-7 字符宽度，密集场景下也不至于刷屏
            UnityEditor.Handles.Label(t.position + offset, $"C{item.clipIndex}/P{k}", style);
        }
    }
#endif
}
