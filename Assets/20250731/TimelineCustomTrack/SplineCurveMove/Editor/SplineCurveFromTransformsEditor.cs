using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using UnityEditor.Timeline;

[CustomEditor(typeof(SplineCurveFromTransforms))]
public class SplineCurveFromTransformsEditor : Editor
{
    private TimelineClip[] MovementClips(SerializedProperty setting)
    {
        var timeline = timelineAssetProp.objectReferenceValue as TimelineAsset;
        if (timeline == null) return new TimelineClip[0];
        var tracks = timeline.GetOutputTracks().OfType<SplineCurveMoveTrack>().ToArray();
        var track = setting.FindPropertyRelative("track").objectReferenceValue as SplineCurveMoveTrack;
        if (track == null)
        {
            var matches = tracks.Where(t => t.name == setting.FindPropertyRelative("trackName").stringValue).ToArray();
            if (matches.Length == 1) track = matches[0];
        }
        return track != null && tracks.Contains(track)
            ? track.GetClips().Where(c => c.asset is SplineCurveMoveClip).OrderBy(c => c.start).ToArray() : new TimelineClip[0];
    }
    private void DrawClipSelector(SerializedProperty setting, SerializedProperty item)
    {
        var clips = MovementClips(setting);
        var reference = item.FindPropertyRelative("clip");
        var pinned = item.FindPropertyRelative("hasClipBinding");
        var legacy = item.FindPropertyRelative("clipIndex");
        var labels = new List<string> { "请选择移动 Clip" };
        foreach (var clip in clips) labels.Add($"{labels.Count}. {clip.displayName}  ({clip.start:F2}–{clip.end:F2}s)");
        int selected = System.Array.FindIndex(clips, c => c.asset == reference.objectReferenceValue) + 1;
        if (selected == 0 && (!pinned.boolValue || reference.objectReferenceValue != null || reference.objectReferenceInstanceIDValue != 0))
        {
            labels.Add(!pinned.boolValue ? $"旧索引 {legacy.intValue}（请选择或迁移）" : "原 Clip 已删除或移出轨道，请重新选择");
            selected = labels.Count - 1;
        }
        EditorGUI.BeginChangeCheck();
        int choice = EditorGUILayout.Popup("绑定移动 Clip", selected, labels.ToArray());
        if (EditorGUI.EndChangeCheck() && choice <= clips.Length)
        {
            reference.objectReferenceValue = choice > 0 ? clips[choice-1].asset : null;
            pinned.boolValue = true;
        }
    }
    private void DrawClipMigration(SerializedProperty setting, SerializedProperty items)
    {
        bool legacy = false;
        for (int i = 0; i < items.arraySize; i++)
            legacy |= !items.GetArrayElementAtIndex(i).FindPropertyRelative("hasClipBinding").boolValue;
        if (!legacy) return;
        EditorGUILayout.HelpBox("旧索引尚未转换。若原来按移动 Clip 的 0、1、2… 配置，可使用下方按钮忽略过渡 Clip 迁移；如果已手动调整过索引，请逐行选择正确的移动 Clip。迁移后无需再维护编号。", MessageType.Info);
        if (!GUILayout.Button("迁移旧索引（忽略过渡 Clip）")) return;
        var clips = MovementClips(setting);
        var chosen = new HashSet<Object>();
        for (int i = 0; i < items.arraySize; i++)
        {
            var item = items.GetArrayElementAtIndex(i);
            var clip = item.FindPropertyRelative("clip").objectReferenceValue;
            if (!item.FindPropertyRelative("hasClipBinding").boolValue && clip == null)
            {
                int index = item.FindPropertyRelative("clipIndex").intValue;
                if (index >= 0 && index < clips.Length) clip = clips[index].asset;
            }
            if (clip == null || !chosen.Add(clip))
            {
                Debug.LogWarning("旧索引越界或绑定重复，本次未迁移。请逐行选择对应的移动 Clip。", target);
                return;
            }
        }
        for (int i = 0; i < items.arraySize; i++)
        {
            var item = items.GetArrayElementAtIndex(i);
            if (item.FindPropertyRelative("hasClipBinding").boolValue) continue;
            var reference = item.FindPropertyRelative("clip");
            if (reference.objectReferenceValue == null) reference.objectReferenceValue = clips[item.FindPropertyRelative("clipIndex").intValue].asset;
            item.FindPropertyRelative("hasClipBinding").boolValue = true;
        }
    }
    private PlayableDirector ResolveNamingDirector(TimelineAsset timeline)
    {
        if (timeline == null) return null;
        var inspected = TimelineEditor.inspectedDirector;
        if (inspected != null && inspected.playableAsset == timeline) return inspected;
        var local = ((SplineCurveFromTransforms)target).GetComponentInParent<PlayableDirector>();
        if (local != null && local.playableAsset == timeline) return local;
        var candidates = Resources.FindObjectsOfTypeAll<PlayableDirector>()
            .Where(d => !EditorUtility.IsPersistent(d) && d.gameObject.scene.IsValid() && d.playableAsset == timeline).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static string TrackLabel(SplineCurveMoveTrack track, PlayableDirector director)
    {
        var binding = director != null ? director.GetGenericBinding(track) : null;
        string path = track.name;
        for (var parent = track.parent as TrackAsset; parent != null; parent = parent.parent as TrackAsset)
            path = parent.name + " / " + path;
        return binding != null ? binding.name + "  [" + path + "]" : path + "（未找到绑定物体）";
    }

    private static void NameTrackAfterBinding(SplineCurveMoveTrack track, PlayableDirector director, TimelineAsset timeline)
    {
        var binding = director.GetGenericBinding(track);
        if (binding == null || track.name == binding.name) return;
        // Preserve old scene mappings before the shared TrackAsset changes its name.
        foreach (var author in Resources.FindObjectsOfTypeAll<SplineCurveFromTransforms>())
        {
            if (EditorUtility.IsPersistent(author) || !author.gameObject.scene.IsValid() || author.timelineAsset != timeline) continue;
            var settings = author.settings.Where(s => s != null && s.track == null && author.ResolveTrack(s) == track).ToArray();
            if (settings.Length == 0) continue;
            Undo.RecordObject(author, "Migrate track binding");
            foreach (var setting in settings) { setting.track = track; setting.trackName = ""; }
            PrefabUtility.RecordPrefabInstancePropertyModifications(author);
            EditorUtility.SetDirty(author);
        }
        Undo.RecordObject(track, "Name track after bound object");
        track.name = binding.name;
        EditorUtility.SetDirty(track);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
    }

    private void DrawTrackSelector(SerializedProperty setting)
    {
        var reference = setting.FindPropertyRelative("track");
        var legacy = setting.FindPropertyRelative("trackName");
        var timeline = timelineAssetProp.objectReferenceValue as TimelineAsset;
        var director = ResolveNamingDirector(timeline);
        var tracks = timeline == null ? new List<SplineCurveMoveTrack>() : timeline.GetOutputTracks().OfType<SplineCurveMoveTrack>().ToList();
        // Migrate only an unambiguous old name, using SerializedProperty so Undo and prefab overrides work.
        if (reference.objectReferenceValue == null && !string.IsNullOrEmpty(legacy.stringValue))
        {
            var matches = tracks.Where(t => t.name == legacy.stringValue).ToList();
            if (matches.Count == 1) { reference.objectReferenceValue = matches[0]; legacy.stringValue = ""; }
        }
        var labels = new List<string> { "请选择移动轨道" };
        foreach (var track in tracks)
        {
            labels.Add($"{labels.Count}. {TrackLabel(track, director)}");
        }
        int selected = tracks.IndexOf(reference.objectReferenceValue as SplineCurveMoveTrack) + 1;
        if (selected == 0 && (reference.objectReferenceValue != null || !string.IsNullOrEmpty(legacy.stringValue)))
        {
            labels.Add("旧绑定未找到或不属于当前 Timeline，请重新选择");
            selected = labels.Count - 1;
        }
        EditorGUI.BeginChangeCheck();
        int choice = EditorGUILayout.Popup("绑定轨道", selected, labels.ToArray());
        if (EditorGUI.EndChangeCheck() && choice <= tracks.Count)
        {
            reference.objectReferenceValue = choice == 0 ? null : tracks[choice - 1];
            legacy.stringValue = "";
        }
        if (tracks.Count == 0) EditorGUILayout.HelpBox("请先指定 Timeline，并在其中创建 Spline Curve Move Track。", MessageType.Info);
        var selectedTrack = reference.objectReferenceValue as SplineCurveMoveTrack;
        using (new EditorGUI.DisabledScope(director == null || selectedTrack == null || !tracks.Contains(selectedTrack) || director.GetGenericBinding(selectedTrack) == null))
        {
            if (GUILayout.Button("用绑定物体名命名此轨道"))
            {
                serializedObject.ApplyModifiedProperties();
                NameTrackAfterBinding(selectedTrack, director, timeline);
                serializedObject.Update();
            }
        }
        if (tracks.Count > 0 && director == null)
            EditorGUILayout.HelpBox("请在 Timeline 窗口打开对应的场景 Director，以显示绑定物体名。", MessageType.Info);
    }

    private SerializedProperty timelineAssetProp;
    private SerializedProperty bakeAtTimelineStartProp;  // Phase 4 hotfix
    private SerializedProperty autoBakeOnPlayProp;       // UX 优化
    private SerializedProperty settingsProp;
    private Dictionary<int, bool> settingsFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, bool> splineCurveParametersPerClipFoldouts = new Dictionary<int, bool>();
    private Dictionary<int, bool> visualizeCurveFoldouts = new Dictionary<int, bool>();

    public Texture texture;

    private const string PREF_SHOW_DEBUG = "SCFT.ShowKeypointDebug";
    private const string PREF_AXIS_LEN = "SCFT.DebugAxisLength";
    private const string PREF_LABEL_OFFSET = "SCFT.DebugLabelOffset";
    private const string PREF_LABEL_FONT = "SCFT.DebugLabelFontSize";

    private bool ShowKeypointDebug
    {
        get => EditorPrefs.GetBool(PREF_SHOW_DEBUG, false);
        set => EditorPrefs.SetBool(PREF_SHOW_DEBUG, value);
    }
    private float DebugAxisLength
    {
        get => EditorPrefs.GetFloat(PREF_AXIS_LEN, 0.5f);
        set => EditorPrefs.SetFloat(PREF_AXIS_LEN, value);
    }
    private float DebugLabelOffset
    {
        get => EditorPrefs.GetFloat(PREF_LABEL_OFFSET, 0.3f);
        set => EditorPrefs.SetFloat(PREF_LABEL_OFFSET, value);
    }
    private int DebugLabelFontSize
    {
        get => EditorPrefs.GetInt(PREF_LABEL_FONT, 11);
        set => EditorPrefs.SetInt(PREF_LABEL_FONT, value);
    }

    private void OnEnable()
    {
        timelineAssetProp = serializedObject.FindProperty("timelineAsset");
        bakeAtTimelineStartProp = serializedObject.FindProperty("bakeAtTimelineStart");
        autoBakeOnPlayProp = serializedObject.FindProperty("autoBakeOnPlay");
        settingsProp = serializedObject.FindProperty("settings");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var script = (SplineCurveFromTransforms)target;

        // ════════════════════════════════════════════════════════════════════════════════
        //  顶部"🚀 Update Now"大按钮——避免用户为找 Update Timeline Track 按钮在 Inspector
        //  下方翻找。这里始终可见、固定位置；功能等同底部按钮，复用同一份逻辑。
        //  快捷键 Ctrl/Cmd + Shift + U 也指向同一个动作（见 MenuItem 定义）。
        // ════════════════════════════════════════════════════════════════════════════════
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.55f, 1.0f, 0.55f);
        if (GUILayout.Button(new GUIContent("🚀 Update Now",
                "立即烘焙曲线（等同底部按钮 / 快捷键 Ctrl+Shift+U）"),
                GUILayout.Height(28f)))
        {
            script.ApplyControlPointsToTrack();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(6);

        GUI.backgroundColor = new Color(0.7f, 0.9f, 1.0f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Timeline", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(timelineAssetProp);

        // Phase 4 hotfix：bake 防呆开关——避免 refFrame 在动画中间状态时 bake 导致漂移
        if (bakeAtTimelineStartProp != null)
        {
            EditorGUILayout.PropertyField(bakeAtTimelineStartProp,
                new GUIContent("Bake At Timeline Start",
                    "Bake 前把 Timeline scrub 到 t=0，让所有 refFrame 回到初始姿态再 bake。\n" +
                    "推荐保持开启——避免在 refFrame 处于动画中间状态时 bake 导致漂移。"));
            if (bakeAtTimelineStartProp.boolValue == false)
            {
                EditorGUILayout.HelpBox(
                    "⚠ 已关闭防呆：bake 会按当前 playhead 状态采样 refFrame 姿态。\n" +
                    "若 refFrame 自身被 Timeline 驱动（如沿曲线移动的载具），\n" +
                    "请确认 playhead 处于你想用的\"锚定姿态\"对应的时间。",
                    MessageType.Warning);
            }
        }

        // UX 优化：进 PlayMode 前自动 bake 一次
        if (autoBakeOnPlayProp != null)
        {
            EditorGUILayout.PropertyField(autoBakeOnPlayProp,
                new GUIContent("Auto Bake On Play",
                    "进入 PlayMode 前自动 bake，确保运行时数据与 Scene 预览一致。\n" +
                    "推荐保持开启——免去手动 Update。"));
        }

        EditorGUILayout.EndVertical();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(8);

        // ============================================================ //
        // Scene 调试
        // ============================================================ //
        GUI.backgroundColor = new Color(1.0f, 0.92f, 0.7f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Scene Debug", EditorStyles.boldLabel);

        bool prev = ShowKeypointDebug;
        bool now = EditorGUILayout.Toggle(
            new GUIContent("Show Keypoint RPY",
                "在 Scene 视图显示每个关键点的世界欧拉角与相对切线的 Roll/Pitch/Yaw"), prev);
        if (now != prev) { ShowKeypointDebug = now; SceneView.RepaintAll(); }

        if (now)
        {
            EditorGUI.indentLevel++;
            float al = EditorGUILayout.Slider("Axis Length", DebugAxisLength, 0.1f, 2f);
            if (!Mathf.Approximately(al, DebugAxisLength)) { DebugAxisLength = al; SceneView.RepaintAll(); }

            float lo = EditorGUILayout.Slider("Label Offset", DebugLabelOffset, 0f, 1f);
            if (!Mathf.Approximately(lo, DebugLabelOffset)) { DebugLabelOffset = lo; SceneView.RepaintAll(); }

            int fs = EditorGUILayout.IntSlider("Label Font Size", DebugLabelFontSize, 8, 20);
            if (fs != DebugLabelFontSize) { DebugLabelFontSize = fs; SceneView.RepaintAll(); }

            EditorGUILayout.HelpBox(
                "红 = 关键点 right 轴\n绿 = 关键点 up 轴\n蓝 = 关键点 forward 轴\n绿色虚线 = 曲线切线方向（参考基准）\n" +
                "Local RPY = 关键点 rotation 相对于切线参考帧的偏移。",
                MessageType.None);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(10);

        // ============================================================ //
        // Settings
        // ============================================================ //
        EditorGUI.indentLevel++;
        if (settingsProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No Curve, Please Add Curve Setting", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < settingsProp.arraySize; i++)
            {
                SerializedProperty settingProp = settingsProp.GetArrayElementAtIndex(i);

                if (!settingsFoldouts.ContainsKey(i)) settingsFoldouts[i] = true;
                if (!visualizeCurveFoldouts.ContainsKey(i)) visualizeCurveFoldouts[i] = false;
                if (!splineCurveParametersPerClipFoldouts.ContainsKey(i)) splineCurveParametersPerClipFoldouts[i] = true;

                GUI.backgroundColor = new Color(0.85f, 0.9f, 1.0f);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();
                SerializedProperty trackNameProp = settingProp.FindPropertyRelative("trackName");
                var boundTrack = settingProp.FindPropertyRelative("track").objectReferenceValue as SplineCurveMoveTrack;
                string trackName = boundTrack != null ? TrackLabel(boundTrack, ResolveNamingDirector(timelineAssetProp.objectReferenceValue as TimelineAsset)) : trackNameProp.stringValue;
                string settingTitle = string.IsNullOrEmpty(trackName) ? $"曲线设置 {i + 1}" : $"轨道: {trackName}";
                settingsFoldouts[i] = EditorGUILayout.Foldout(settingsFoldouts[i], settingTitle, true, EditorStyles.foldoutHeader);
                GUI.backgroundColor = new Color(1.0f, 0.3f, 0.0f);
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    if (EditorUtility.DisplayDialog("确认删除", "确定要删除这个曲线设置吗？", "删除", "取消"))
                    {
                        settingsProp.DeleteArrayElementAtIndex(i);
                        serializedObject.ApplyModifiedProperties();
                        continue;
                    }
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
                EditorGUILayout.Separator();

                if (settingsFoldouts[i])
                {
                    EditorGUI.indentLevel++;
                    DrawTrackSelector(settingProp);

                    // ============================================ //
                    // Rotation Config
                    // ============================================ //
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    EditorGUILayout.LabelField("Rotation Config", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(settingProp.FindPropertyRelative("visualRotationOffset"), new GUIContent("Rotation Offset"));

                    SerializedProperty alphaModeProp = settingProp.FindPropertyRelative("alphaMode");
                    if (alphaModeProp != null)
                        EditorGUILayout.PropertyField(alphaModeProp, new GUIContent("Alpha Mode"));

                    SerializedProperty rotationModeProp = settingProp.FindPropertyRelative("rotationMode");
                    if (rotationModeProp != null)
                        EditorGUILayout.PropertyField(rotationModeProp, new GUIContent("Rotation Mode"));

                    SerializedProperty useKeyRotProp = settingProp.FindPropertyRelative("useKeypointRotation");
                    bool usesRoll = rotationModeProp != null &&
                        rotationModeProp.intValue == (int)CatmullRomSpline.RotationMode.TangentWithRoll;
                    if (usesRoll && useKeyRotProp != null)
                        EditorGUILayout.PropertyField(useKeyRotProp, new GUIContent("Use Control Point Roll",
                            "仅提取控制点旋转中的 Roll；前进方向始终由路径切线决定。"));

                    SerializedProperty rotKeyModeProp = settingProp.FindPropertyRelative("rotationKeyframeMode");
                    if (usesRoll && rotKeyModeProp != null && (useKeyRotProp == null || useKeyRotProp.boolValue))
                    {
                        EditorGUILayout.PropertyField(rotKeyModeProp, new GUIContent("Roll Key Mode"));

                        var mode = (RotationKeyframeMode)rotKeyModeProp.enumValueIndex;
                        switch (mode)
                        {
                            case RotationKeyframeMode.EveryPoint:
                                EditorGUILayout.HelpBox(
                                    "EveryPoint：每个控制点的旋转都用于提供 Roll。",
                                    MessageType.None);
                                break;
                            case RotationKeyframeMode.EndpointsOnly:
                                EditorGUILayout.HelpBox(
                                    "EndpointsOnly：由首尾两点旋转插值得到 Roll 参考，朝向仍沿切线。",
                                    MessageType.Info);
                                break;
                            case RotationKeyframeMode.MarkedKeyframes:
                                EditorGUILayout.HelpBox(
                                    "MarkedKeyframes：勾选 IsKey 的点提供 Roll 参考。\n" +
                                    "• 第一个 key 之前的点：clamp 用第一个 key 的 rotation\n" +
                                    "• 最后一个 key 之后的点：clamp 用最后一个 key 的 rotation\n" +
                                    "• 不勾任何点时：自动 fallback 到 EndpointsOnly 行为",
                                    MessageType.Info);
                                break;
                        }
                    }

                    EditorGUILayout.Space(4);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUI.backgroundColor = new Color(1f, 0.85f, 0.5f);
                    if (GUILayout.Button(
                        new GUIContent("Align All Rotations to Tangent",
                            "将本 Setting 下所有 Clip 的所有关键点 Transform.rotation 设置为 LookRotation(切线方向)。"),
                        GUILayout.Width(240), GUILayout.Height(22)))
                    {
                        if (i < script.settings.Count)
                            AlignAllRotationsToTangent(script.settings[i]);
                    }
                    GUI.backgroundColor = Color.white;
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();

                    // ============================================ //
                    // Parameters Per Clip
                    // ============================================ //
                    SerializedProperty splineCurveParametersPerClip = settingProp.FindPropertyRelative("splineCurveParametersPerClip");
                    EditorGUILayout.BeginVertical(GUI.skin.box);

                    EditorGUILayout.BeginHorizontal();
                    splineCurveParametersPerClipFoldouts[i] = EditorGUILayout.Foldout(
                        splineCurveParametersPerClipFoldouts[i],
                        $"Parameters Per Clip ({splineCurveParametersPerClip.arraySize})",
                        true, EditorStyles.foldoutHeader);

                    GUI.backgroundColor = new Color(0.3f, 1.0f, 0.3f);
                    if (GUILayout.Button(new GUIContent("Add Clip"), GUILayout.Width(80)))
                    {
                        splineCurveParametersPerClip.arraySize++;
                        var newItem = splineCurveParametersPerClip.GetArrayElementAtIndex(splineCurveParametersPerClip.arraySize - 1);
                        newItem.FindPropertyRelative("clipIndex").intValue = splineCurveParametersPerClip.arraySize - 1;
                        newItem.FindPropertyRelative("clip").objectReferenceValue = null;
                        newItem.FindPropertyRelative("hasClipBinding").boolValue = true;
                        splineCurveParametersPerClipFoldouts[i] = true;
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(2);

                    if (splineCurveParametersPerClipFoldouts[i])
                    {
                        DrawClipMigration(settingProp, splineCurveParametersPerClip);
                        EditorGUI.indentLevel++;

                        bool showIsKey = false;
                        if (rotKeyModeProp != null && (useKeyRotProp == null || useKeyRotProp.boolValue))
                        {
                            showIsKey = (RotationKeyframeMode)rotKeyModeProp.enumValueIndex == RotationKeyframeMode.MarkedKeyframes;
                        }

                        for (int j = 0; j < splineCurveParametersPerClip.arraySize; j++)
                        {
                            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                            SerializedProperty clipProp = splineCurveParametersPerClip.GetArrayElementAtIndex(j);

                            SerializedProperty clipcontrolPointsProp = clipProp.FindPropertyRelative("controlPointsTransforms");
                            SerializedProperty keyFlagsProp = clipProp.FindPropertyRelative("rotationKeyFlags");
                            

                            SyncKeyFlagsLengthSerialized(keyFlagsProp, clipcontrolPointsProp.arraySize);

                            EditorGUILayout.BeginHorizontal();
                            DrawClipSelector(settingProp, clipProp);
                            GUILayout.FlexibleSpace();
                            GUI.backgroundColor = new Color(1.0f, 0.3f, 0.0f);
                            if (GUILayout.Button("Delete", GUILayout.Width(60)))
                            {
                                splineCurveParametersPerClip.DeleteArrayElementAtIndex(j);
                                break;
                            }
                            GUI.backgroundColor = Color.white;
                            EditorGUILayout.EndHorizontal();

                            EditorGUILayout.Space(2);
                            EditorGUILayout.LabelField("Control Points:", EditorStyles.boldLabel);
                            EditorGUI.indentLevel++;

                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField($"Points: {clipcontrolPointsProp.arraySize}");
                            GUI.backgroundColor = new Color(0.3f, 1.0f, 0.3f);
                            if (GUILayout.Button("Add Point", GUILayout.Width(80)))
                            {
                                clipcontrolPointsProp.arraySize++;
                                keyFlagsProp.arraySize = clipcontrolPointsProp.arraySize;
                            }
                            GUI.backgroundColor = Color.white;
                            EditorGUILayout.EndHorizontal();

                            // ──────────────────────────────────────────────
                            // UX 优化：批量添加 —— 把 Selection 中的 GameObjects 一次性追加
                            // ──────────────────────────────────────────────
                            // Unity 的 Selection.gameObjects 按【场景层级顺序】返回（不是点击顺序）。
                            // 想要可预测的顺序：在 Hierarchy 里把控制点作为 sibling 排好，然后从上到下
                            // 选中（Shift 多选）—— 这就是层级顺序，与 Hierarchy 视觉一致。
                            int selCount = Selection.gameObjects != null ? Selection.gameObjects.Length : 0;
                            EditorGUILayout.BeginHorizontal();
                            EditorGUI.BeginDisabledGroup(selCount == 0);
                            GUI.backgroundColor = new Color(0.55f, 0.85f, 1f);
                            if (GUILayout.Button(new GUIContent(
                                    selCount > 0 ? $"+ Add Selected ({selCount})" : "+ Add Selected",
                                    "把当前在 Hierarchy / Scene 中选中的 GameObjects 按【层级顺序】依次追加到本 Clip。\n" +
                                    "想要可预测顺序：让它们成为同一父级的 sibling，从上到下 Shift 多选。"),
                                    GUILayout.Height(20f)))
                            {
                                var selected = Selection.gameObjects;
                                int added = 0;
                                for (int s = 0; s < selected.Length; s++)
                                {
                                    if (selected[s] == null) continue;
                                    int newIdx = clipcontrolPointsProp.arraySize;
                                    clipcontrolPointsProp.arraySize = newIdx + 1;
                                    var elem = clipcontrolPointsProp.GetArrayElementAtIndex(newIdx);
                                    elem.objectReferenceValue = selected[s].transform;
                                    added++;
                                }
                                if (added > 0)
                                {
                                    keyFlagsProp.arraySize = clipcontrolPointsProp.arraySize;
                                    Debug.Log($"[Spline] Added {added} control point(s) to control-point group {j + 1}");
                                }
                            }
                            GUI.backgroundColor = Color.white;
                            EditorGUI.EndDisabledGroup();
                            EditorGUILayout.EndHorizontal();

                            // 在 MarkedKeyframes 模式下，统计当前已勾选数量做提示
                            if (showIsKey)
                            {
                                int markedCount = 0;
                                for (int kk = 0; kk < keyFlagsProp.arraySize; kk++)
                                    if (keyFlagsProp.GetArrayElementAtIndex(kk).boolValue) markedCount++;

                                if (markedCount == 0)
                                {
                                    EditorGUILayout.HelpBox("未勾选任何 IsKey，本 Clip 将 fallback 为 EndpointsOnly 行为。",
                                        MessageType.Warning);
                                }
                                else
                                {
                                    EditorGUILayout.HelpBox($"{markedCount} 个点被标记为 Key。这些点的 rotation 将参与插值。",
                                        MessageType.None);
                                }
                            }

                            for (int k = 0; k < clipcontrolPointsProp.arraySize; k++)
                            {
                                EditorGUILayout.BeginHorizontal();
                                SerializedProperty pointProp = clipcontrolPointsProp.GetArrayElementAtIndex(k);
                                EditorGUILayout.PropertyField(pointProp, new GUIContent($"Point {k}"));

                                // ────────────────────────────────────────────────────
                                // UX 优化：🔍 Frame & Select 按钮
                                // ────────────────────────────────────────────────────
                                // 一键定位：把 Scene 相机飞到该控制点，并把它在 Hierarchy 里选中。
                                // 选中后 Unity 标准的 Move Gizmo 立刻可用，省去"找 GameObject → 按 F"两步。
                                // 仅在 Transform 引用有效时启用。
                                Transform pointTransform = pointProp.objectReferenceValue as Transform;
                                EditorGUI.BeginDisabledGroup(pointTransform == null);
                                if (GUILayout.Button(new GUIContent("🔍",
                                        pointTransform != null
                                            ? $"Frame & Select：把相机飞到【{pointTransform.name}】并选中"
                                            : "未指定 Transform"),
                                        GUILayout.Width(28)))
                                {
                                    Selection.activeGameObject = pointTransform.gameObject;
                                    // SceneView.FrameLastActiveSceneView 会基于 Selection 调用 Frame——
                                    // 等价于在 Scene 视图按 F 键的行为
                                    SceneView.FrameLastActiveSceneView();
                                }
                                EditorGUI.EndDisabledGroup();

                                if (showIsKey && k < keyFlagsProp.arraySize)
                                {
                                    SerializedProperty flagProp = keyFlagsProp.GetArrayElementAtIndex(k);
                                    flagProp.boolValue = GUILayout.Toggle(flagProp.boolValue,
                                        new GUIContent("Key", "标记此点为旋转关键帧"), GUILayout.Width(50));
                                }

                                if (GUILayout.Button("×", GUILayout.Width(20)))
                                {
                                    clipcontrolPointsProp.DeleteArrayElementAtIndex(k);
                                    if (k < keyFlagsProp.arraySize)
                                        keyFlagsProp.DeleteArrayElementAtIndex(k);
                                    break;
                                }
                                EditorGUILayout.EndHorizontal();
                            }

                            EditorGUILayout.BeginVertical(GUI.skin.box);
                            GUI.backgroundColor = new Color(0.3799198f, 0.9716981f, 0.2077844f);
                            SerializedProperty resamplePointsProp = clipProp.FindPropertyRelative("resamplePoints");
                            EditorGUILayout.PropertyField(resamplePointsProp, new GUIContent("Sample Point Count"));
                           
                            if (resamplePointsProp.intValue < 5)
                            {
                                EditorGUILayout.HelpBox("采样点数应不少于 5 个", MessageType.Warning);
                            }
                            GUI.backgroundColor = Color.white;

                            // ── 贴地投影 (Ground Projection) ──
                            SerializedProperty groundModelProp = clipProp.FindPropertyRelative("groundModel");
                            SerializedProperty groundOffsetProp = clipProp.FindPropertyRelative("groundOffset");
                            EditorGUILayout.Space(4);
                            EditorGUILayout.LabelField("Ground Projection", EditorStyles.boldLabel);
                            EditorGUILayout.PropertyField(groundModelProp,
                                new GUIContent("Ground Model",
                                    "贴地用的地面模型（需含 MeshFilter，且 Mesh 勾选 Read/Write）。留空 = 该 Clip 不投影。"));
                            EditorGUILayout.PropertyField(groundOffsetProp,
                                new GUIContent("Ground Offset",
                                    "贴地后沿世界 Y 抬升的偏移，用于微调脚底离地。"));
                            if (groundModelProp.objectReferenceValue == null)
                            {
                                EditorGUILayout.HelpBox("未指定地面模型：点 Update 时此 Clip 不做贴地投影（路径保持解析曲线）。",
                                    MessageType.None);
                            }

                            EditorGUILayout.EndVertical();

                            EditorGUI.indentLevel--;
                            EditorGUILayout.EndVertical();
                            EditorGUILayout.Space(5);
                        }

                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();

                    // ============================================ //
                    // Visualization
                    // ============================================ //
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    visualizeCurveFoldouts[i] = EditorGUILayout.Foldout(visualizeCurveFoldouts[i], "CurveVisualization", true);

                    if (visualizeCurveFoldouts[i])
                    {
                        EditorGUI.indentLevel++;
                        SerializedProperty debugCurveProp = settingProp.FindPropertyRelative("debugCurve");
                        EditorGUILayout.PropertyField(debugCurveProp, new GUIContent("Display Curve"));

                        EditorGUILayout.BeginVertical(GUI.skin.box);
                        EditorGUI.indentLevel++;
                        if (debugCurveProp.boolValue)
                        {
                            EditorGUILayout.LabelField("Movement Path", EditorStyles.boldLabel);
                            EditorGUILayout.PropertyField(settingProp.FindPropertyRelative("showOriginalCurve"),
                                new GUIContent("Display Path Curve", "显示物体实际运动路径（高分辨率，按弧长采样）"));
                            EditorGUILayout.PropertyField(settingProp.FindPropertyRelative("curveColor"),
                                new GUIContent("Path Color"));
                            EditorGUILayout.PropertyField(settingProp.FindPropertyRelative("pathResolution"),
                                new GUIContent("Path Resolution", "曲线绘制分辨率，越大越光滑"));
                            EditorGUILayout.PropertyField(settingProp.FindPropertyRelative("pathLineWidth"),
                                new GUIContent("Path Line Width"));

                            EditorGUILayout.Space(4);
                            EditorGUILayout.PropertyField(settingProp.FindPropertyRelative("displayLable"),
                                new GUIContent("Display Labels"));

                            // ==================================================== //
                            // 阶段 4：Path Events
                            // ==================================================== //
                            EditorGUILayout.Space();
                            EditorGUILayout.Separator();
                            EditorGUILayout.LabelField("Path Events", EditorStyles.boldLabel);

                            SerializedProperty showEventsProp = settingProp.FindPropertyRelative("showPathEvents");
                            EditorGUILayout.PropertyField(showEventsProp,
                                new GUIContent("Show Path Events", "在曲线上显示路径事件触发位置（在 Clip Inspector 的 Path Events 列表配置）。"));
                            if (showEventsProp.boolValue)
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(settingProp.FindPropertyRelative("pathEventSize"),
                                    new GUIContent("Marker Size"));
                                EditorGUILayout.HelpBox(
                                    "事件在 SplineCurveMoveClip 的 Path Events 列表中配置。\n" +
                                    "目标 Transform 上需挂 SplineEventReceiver 组件接收事件。",
                                    MessageType.None);
                                EditorGUI.indentLevel--;
                            }

                            // ==================================================== //
                            // UX 优化：Point Labels（控制点编号标签）
                            // ==================================================== //
                            EditorGUILayout.Space();
                            EditorGUILayout.Separator();
                            EditorGUILayout.LabelField("Point Labels", EditorStyles.boldLabel);

                            SerializedProperty showLabelsProp = settingProp.FindPropertyRelative("showPointLabels");
                            EditorGUILayout.PropertyField(showLabelsProp,
                                new GUIContent("Show Point Labels",
                                    "在 Scene 视图每个控制点上方显示 \"C{clipIndex}/P{pointIndex}\" 标号。\n" +
                                    "多曲线杂多场景下肉眼识别控制点用——配合 Inspector 里 🔍 Frame 按钮使用。"));
                            if (showLabelsProp.boolValue)
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(settingProp.FindPropertyRelative("pointLabelColor"),
                                    new GUIContent("Label Color"));
                                EditorGUILayout.PropertyField(settingProp.FindPropertyRelative("pointLabelFontSize"),
                                    new GUIContent("Font Size"));
                                EditorGUILayout.PropertyField(settingProp.FindPropertyRelative("pointLabelOffsetY"),
                                    new GUIContent("Vertical Offset"));
                                EditorGUILayout.HelpBox(
                                    "工作流：在 Scene 看到目标点是【C0/P3】 →  Inspector 里点对应行的 🔍  →  一秒命中。",
                                    MessageType.None);
                                EditorGUI.indentLevel--;
                            }
                        }
                        EditorGUI.indentLevel--;
                        EditorGUILayout.EndVertical();
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUI.indentLevel--;
                }

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
        }
        EditorGUI.indentLevel--;

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.backgroundColor = new Color(0.3f, 1.0f, 0.3f);
        if (GUILayout.Button("Add SplineCurve Setting", GUILayout.Width(200), GUILayout.Height(24)))
        {
            settingsProp.arraySize++;
            int newIndex = settingsProp.arraySize - 1;
            settingsProp.GetArrayElementAtIndex(newIndex).FindPropertyRelative("track").objectReferenceValue = null;
            settingsProp.GetArrayElementAtIndex(newIndex).FindPropertyRelative("trackName").stringValue = "";
            settingsProp.GetArrayElementAtIndex(newIndex).FindPropertyRelative("curveColor").colorValue = new Color(
                Random.value, Random.value, Random.value, 1.0f);
            settingsProp.GetArrayElementAtIndex(newIndex).FindPropertyRelative("debugCurve").boolValue = true;
            settingsProp.GetArrayElementAtIndex(newIndex).FindPropertyRelative("pathResolution").intValue = 200;
            settingsProp.GetArrayElementAtIndex(newIndex).FindPropertyRelative("pathLineWidth").floatValue = 3f;
            // 阶段 4 默认值
            settingsProp.GetArrayElementAtIndex(newIndex).FindPropertyRelative("showPathEvents").boolValue = true;
            settingsProp.GetArrayElementAtIndex(newIndex).FindPropertyRelative("pathEventSize").floatValue = 0.3f;
            settingsFoldouts[newIndex] = true;
        }
        GUI.backgroundColor = Color.white;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(15);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        Texture2D buttonIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ArtResource/Generic/TimelineCustomTrack/SplineCurveMove/Icon/rocket.png");
        GUIContent buttonContent = new GUIContent("  Update Timeline Track", buttonIcon);

        GUIStyle customButtonStyle = new GUIStyle(GUI.skin.button);
        customButtonStyle.imagePosition = ImagePosition.ImageLeft;
        customButtonStyle.fontSize = 12;
        customButtonStyle.fontStyle = FontStyle.Bold;
        customButtonStyle.padding = new RectOffset(10, 10, 6, 6);
        customButtonStyle.margin = new RectOffset(0, 0, 0, 0);
        customButtonStyle.fixedHeight = 30;
        customButtonStyle.alignment = TextAnchor.MiddleCenter;

        GUI.backgroundColor = new Color(0.8f, 0.74f, 1.0f);
        if (GUILayout.Button(buttonContent, customButtonStyle, GUILayout.Width(250)))
        {
            script.ApplyControlPointsToTrack();
        }
        GUI.backgroundColor = Color.white;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);
        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }

    private static void SyncKeyFlagsLengthSerialized(SerializedProperty flagsProp, int targetSize)
    {
        if (flagsProp == null) return;
        while (flagsProp.arraySize < targetSize) flagsProp.arraySize++;
        while (flagsProp.arraySize > targetSize) flagsProp.arraySize--;
    }

    // ====================================================================== //
    // 批量对齐切线
    // ====================================================================== //
    private void AlignAllRotationsToTangent(SplineCurveSetting setting)
    {
        if (setting?.splineCurveParametersPerClip == null) return;

        var allObjectsToRecord = new List<Object>();
        foreach (var item in setting.splineCurveParametersPerClip)
        {
            if (item?.controlPointsTransforms == null) continue;
            foreach (var t in item.controlPointsTransforms)
                if (t != null) allObjectsToRecord.Add(t);
        }

        if (allObjectsToRecord.Count == 0)
        {
            Debug.LogWarning("[SplineCurveFromTransforms] 没有可对齐的关键点 Transform。", target);
            return;
        }

        Undo.RecordObjects(allObjectsToRecord.ToArray(), "Align Rotations to Tangent");

        int totalAligned = 0;
        foreach (var item in setting.splineCurveParametersPerClip)
        {
            if (item?.controlPointsTransforms == null) continue;

            var transforms = new List<Transform>();
            foreach (var t in item.controlPointsTransforms)
                if (t != null) transforms.Add(t);

            if (transforms.Count < 2) continue;

            var spline = new CatmullRomSpline { AlphaMode = setting.alphaMode };
            foreach (var t in transforms) spline.AddPoint(t.position);

            int n = transforms.Count;
            for (int k = 0; k < n; k++)
            {
                float curveT = (n > 1) ? (float)k / (n - 1) : 0f;
                Vector3 tangent = spline.GetTangent(curveT);
                if (tangent.sqrMagnitude > 1e-8f)
                {
                    transforms[k].rotation = SafeLookRotation(tangent);
                    totalAligned++;
                }
            }
        }

        SceneView.RepaintAll();
        Debug.Log($"[SplineCurveFromTransforms] 已将 {totalAligned} 个关键点的 rotation 对齐到曲线切线方向。", target);
    }

    // ====================================================================== //
    // Scene RPY
    // ====================================================================== //
    private void OnSceneGUI()
    {
        if (!ShowKeypointDebug) return;
        var script = (SplineCurveFromTransforms)target;
        if (script.settings == null) return;

        GUIStyle labelStyle = new GUIStyle();
        labelStyle.fontSize = DebugLabelFontSize;
        labelStyle.normal.textColor = Color.white;
        labelStyle.alignment = TextAnchor.UpperLeft;
        labelStyle.richText = true;

        foreach (var setting in script.settings)
        {
            if (setting == null || setting.splineCurveParametersPerClip == null) continue;

            foreach (var item in setting.splineCurveParametersPerClip)
            {
                if (item?.controlPointsTransforms == null) continue;
                DrawClipKeypointDebug(item, setting, labelStyle);
            }
        }
    }

    private void DrawClipKeypointDebug(CVPointsForClip item, SplineCurveSetting setting, GUIStyle labelStyle)
    {
        var transforms = new List<Transform>();
        foreach (var t in item.controlPointsTransforms)
            if (t != null) transforms.Add(t);

        if (transforms.Count < 2) return;

        var spline = new CatmullRomSpline { AlphaMode = setting.alphaMode };
        foreach (var t in transforms) spline.AddPoint(t.position);

        SplineCurveFromTransforms.SyncKeyFlagsLength(item);
        List<Quaternion> resolvedRotations = null;
        List<int> resolvedKeyIndices = null;
        if (setting.useKeypointRotation)
        {
            resolvedRotations = SplineCurveFromTransforms.ResolveValidRotations(
                item.controlPointsTransforms,
                item.rotationKeyFlags,
                setting.rotationKeyframeMode);
            resolvedKeyIndices = SplineCurveFromTransforms.ResolveValidKeyIndices(
                item.controlPointsTransforms,
                item.rotationKeyFlags,
                setting.rotationKeyframeMode);
        }

        int n = transforms.Count;
        float axisLen = DebugAxisLength;

        var validToOrigin = new List<int>();
        for (int i = 0; i < item.controlPointsTransforms.Count; i++)
            if (item.controlPointsTransforms[i] != null) validToOrigin.Add(i);

        for (int k = 0; k < n; k++)
        {
            var t = transforms[k];
            Vector3 pos = t.position;

            float curveT = (n > 1) ? (float)k / (n - 1) : 0f;
            Vector3 tangent = spline.GetTangent(curveT);
            Quaternion tangentRef = SafeLookRotation(tangent);

            Quaternion drawRot = resolvedRotations != null && k < resolvedRotations.Count
                ? resolvedRotations[k]
                : t.rotation;

            Handles.color = new Color(1f, 0.3f, 0.3f);
            Handles.DrawLine(pos, pos + drawRot * Vector3.right * axisLen, 2f);
            Handles.color = new Color(0.3f, 1f, 0.3f);
            Handles.DrawLine(pos, pos + drawRot * Vector3.up * axisLen, 2f);
            Handles.color = new Color(0.3f, 0.5f, 1f);
            Handles.DrawLine(pos, pos + drawRot * Vector3.forward * axisLen, 2f);

            bool rotDiff = Quaternion.Angle(drawRot, t.rotation) > 0.5f;
            if (rotDiff)
            {
                Handles.color = new Color(1f, 1f, 1f, 0.4f);
                Handles.DrawDottedLine(pos, pos + t.right * axisLen * 0.7f, 2f);
                Handles.DrawDottedLine(pos, pos + t.up * axisLen * 0.7f, 2f);
                Handles.DrawDottedLine(pos, pos + t.forward * axisLen * 0.7f, 2f);
            }

            Handles.color = new Color(0.6f, 1f, 0.6f, 0.9f);
            Handles.DrawDottedLine(pos, pos + tangent * axisLen * 1.3f, 4f);

            Vector3 worldEuler = NormalizeEulers(drawRot.eulerAngles);
            Quaternion localQ = Quaternion.Inverse(tangentRef) * drawRot;
            Vector3 localEuler = NormalizeEulers(localQ.eulerAngles);

            int originIdx = k < validToOrigin.Count ? validToOrigin[k] : k;

            // [KEY]/[interp] 标签：用解析后的 keyIndices 决定（valid 索引空间）
            string keyTag = "";
            if (setting.useKeypointRotation && resolvedKeyIndices != null)
            {
                bool isResolvedKey = resolvedKeyIndices.Contains(k);
                keyTag = isResolvedKey
                    ? "<color=#FFD080>[KEY]</color>"
                    : "<color=#888888>[interp]</color>";
            }

            string label =
                $"<b>P{originIdx}</b> {keyTag}\n" +
                $"<color=#A0A0A0>World:</color> ({worldEuler.x:F1}, {worldEuler.y:F1}, {worldEuler.z:F1})\n" +
                $"<color=#FF8080>Pitch (X):</color> {localEuler.x:F1}°\n" +
                $"<color=#80FF80>Yaw   (Y):</color> {localEuler.y:F1}°\n" +
                $"<color=#80B0FF>Roll  (Z):</color> {localEuler.z:F1}°";

            Handles.Label(pos + Vector3.up * DebugLabelOffset, label, labelStyle);
        }
    }

    private static Quaternion SafeLookRotation(Vector3 tangent)
    {
        if (tangent.sqrMagnitude < 1e-8f) return Quaternion.identity;
        Vector3 fwd = tangent.normalized;
        Vector3 up = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(fwd, up)) > 0.99f) up = Vector3.right;
        return Quaternion.LookRotation(fwd, up);
    }

    private static Vector3 NormalizeEulers(Vector3 e)
    {
        return new Vector3(NormalizeAngle(e.x), NormalizeAngle(e.y), NormalizeAngle(e.z));
    }

    private static float NormalizeAngle(float a)
    {
        a %= 360f;
        if (a > 180f) a -= 360f;
        else if (a < -180f) a += 360f;
        return a;
    }
}

// ════════════════════════════════════════════════════════════════════════════════
//  UX 优化辅助：热键快捷触发 Update + 进 PlayMode 前自动 bake
//
//  与 SplineCurveFromTransformsEditor 协同工作的静态工具类：
//   1. [MenuItem] 提供 Ctrl/Cmd+Shift+U 热键，从场景任何状态下都能一键 bake
//   2. [InitializeOnLoad] 监听 PlayMode 切换，进入前自动 bake 所有 autoBakeOnPlay=true 的实例
// ════════════════════════════════════════════════════════════════════════════════
[InitializeOnLoad]
internal static class SplineCurveUpdateUtilities
{
    // 热键：Ctrl+Shift+U（Windows）/ Cmd+Shift+U（macOS）
    // 触发逻辑：优先用 Selection 里的 SplineCurveFromTransforms；没选中时找场景里第一个
    private const string MENU_PATH = "LiangZhu/Spline Curve/Update Active SplineCurveFromTransforms %#u";

    static SplineCurveUpdateUtilities()
    {
        // 监听 PlayMode 切换事件，注册一次即可
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem(MENU_PATH)]
    private static void UpdateActiveSpline()
    {
        // 1. 优先用 Selection 中的 SplineCurveFromTransforms
        SplineCurveFromTransforms target = null;
        if (Selection.activeGameObject != null)
            target = Selection.activeGameObject.GetComponent<SplineCurveFromTransforms>();

        // 2. 兜底：场景里找第一个
        if (target == null)
        {
            var all = Object.FindObjectsOfType<SplineCurveFromTransforms>();
            if (all.Length > 0) target = all[0];
        }

        if (target == null)
        {
            Debug.LogWarning("[Spline] 未找到 SplineCurveFromTransforms 组件。选中带有该组件的 GameObject 或确保场景里至少存在一个。");
            return;
        }

        target.ApplyControlPointsToTrack();
        Debug.Log($"[Spline] 已更新 → {target.name}");
    }

    [MenuItem(MENU_PATH, validate = true)]
    private static bool UpdateActiveSplineValidate()
    {
        // 菜单只在 Edit 模式可点（PlayMode 下 bake 没意义，且我们在 ApplyControlPointsToTrack
        // 里也跳过了 PlayMode 路径的 scrub 逻辑）
        return !Application.isPlaying;
    }

    /// <summary>
    /// PlayMode 状态切换回调：在 ExitingEditMode 阶段（即将进入 Play）自动 bake。
    ///
    /// 时机选择 ExitingEditMode 的原因：
    ///   - 此刻 Application.isPlaying 仍为 false，bakeAtTimelineStart 的 scrub-to-zero
    ///     逻辑仍能正常工作（PlayMode 下被 isPlaying 守门排除）
    ///   - 此刻还在 Edit 模式，Director 操作不会干扰真正运行中的 Timeline
    ///   - Play 开始时数据已经是最新 bake 状态——避免"Scene 里看到的曲线和运行时不一致"
    /// </summary>
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;

        var all = Object.FindObjectsOfType<SplineCurveFromTransforms>();
        int bakedCount = 0;
        foreach (var t in all)
        {
            if (t == null || !t.autoBakeOnPlay) continue;
            if (t.timelineAsset == null) continue;   // 没配 TimelineAsset 的实例跳过

            try
            {
                t.ApplyControlPointsToTrack();
                bakedCount++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Spline] 自动 bake 失败 ({t.name})：{e.Message}", t);
            }
        }

        if (bakedCount > 0)
            Debug.Log($"[Spline] 进入 PlayMode 前自动 bake 了 {bakedCount} 个 SplineCurveFromTransforms 实例");
    }
}
