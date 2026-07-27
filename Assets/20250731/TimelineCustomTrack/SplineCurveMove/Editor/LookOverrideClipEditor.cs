using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LookOverrideClip))]
public class LookOverrideClipEditor : Editor
{
    private SerializedProperty templateProp;
    private SerializedProperty yawProp;
    private SerializedProperty pitchProp;
    private SerializedProperty rollProp;

    // 转头模板参数 —— 全部用【秒】存储
    private const string PREF_TURN_START_SEC = "LOC.TurnStartSec";
    private const string PREF_TURN_DUR_SEC   = "LOC.TurnDurationSec";
    private const string PREF_HOLD_DUR_SEC   = "LOC.HoldDurationSec";
    private const string PREF_TURN_ANGLE     = "LOC.TurnAngle";
    private const string PREF_SHOW_PITCH     = "LOC.ShowPitch";
    private const string PREF_SHOW_ROLL      = "LOC.ShowRoll";

    private float TurnStartSec    { get => EditorPrefs.GetFloat(PREF_TURN_START_SEC, 0f);   set => EditorPrefs.SetFloat(PREF_TURN_START_SEC, value); }
    private float TurnDurationSec { get => EditorPrefs.GetFloat(PREF_TURN_DUR_SEC,   2f);   set => EditorPrefs.SetFloat(PREF_TURN_DUR_SEC,   value); }
    private float HoldDurationSec { get => EditorPrefs.GetFloat(PREF_HOLD_DUR_SEC,   0f);   set => EditorPrefs.SetFloat(PREF_HOLD_DUR_SEC,   value); }
    private float TurnAngle       { get => EditorPrefs.GetFloat(PREF_TURN_ANGLE,    -90f);  set => EditorPrefs.SetFloat(PREF_TURN_ANGLE,    value); }
    private bool  ShowPitch       { get => EditorPrefs.GetBool (PREF_SHOW_PITCH,     false); set => EditorPrefs.SetBool (PREF_SHOW_PITCH,    value); }
    private bool  ShowRoll        { get => EditorPrefs.GetBool (PREF_SHOW_ROLL,      false); set => EditorPrefs.SetBool (PREF_SHOW_ROLL,     value); }

    private double _lastObservedDirectorTime = double.MinValue;

    private void OnEnable()
    {
        templateProp = serializedObject.FindProperty("template");
        if (templateProp != null)
        {
            yawProp   = templateProp.FindPropertyRelative("yawCurve");
            pitchProp = templateProp.FindPropertyRelative("pitchCurve");
            rollProp  = templateProp.FindPropertyRelative("rollCurve");
        }
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

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
        var clip = target as LookOverrideClip;
        if (clip == null) { base.OnInspectorGUI(); return; }

        var info = TimelineClipContext.Resolve(clip);

        EditorGUILayout.LabelField("Look Override Clip", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "此 Clip 所在 Track 必须放在 SplineCurveMoveTrack 【下方】。\n" +
            "曲线 Y 轴单位：度。Unity 中 Yaw 正值 = 右转，负值 = 左转。",
            MessageType.Info);

        DrawClipInfoBar(info);
        EditorGUILayout.Space(4);

        // Yaw —— 默认展开
        DrawCurveBlock("Yaw（左右转头）", yawProp, new Color(0.4f, 1f, 0.5f), new Vector2(-180f, 180f), info);

        EditorGUILayout.Space(8);
        DrawLookSequenceTemplate(clip, info);

        EditorGUILayout.Space(10);

        // Pitch / Roll —— 折叠（不常用）
        EditorGUI.BeginChangeCheck();
        bool sp = EditorGUILayout.Foldout(ShowPitch, "Pitch（抬头低头）", true);
        if (EditorGUI.EndChangeCheck()) ShowPitch = sp;
        if (ShowPitch) DrawCurveBlock(null, pitchProp, new Color(1f, 0.7f, 0.3f), new Vector2(-90f, 90f), info);

        EditorGUI.BeginChangeCheck();
        bool sr = EditorGUILayout.Foldout(ShowRoll, "Roll（左右倾斜）", true);
        if (EditorGUI.EndChangeCheck()) ShowRoll = sr;
        if (ShowRoll) DrawCurveBlock(null, rollProp, new Color(1f, 0.5f, 0.8f), new Vector2(-180f, 180f), info);

        serializedObject.ApplyModifiedProperties();
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
                "未在打开的 Timeline 中找到此 Clip —— 秒数 UI 与 playhead 联动需要 Clip 在 Timeline 上才能生效。",
                MessageType.Warning);
        }
    }

    private void DrawCurveBlock(string label, SerializedProperty curveProp, Color curveColor, Vector2 yRange, TimelineClipContext.Info info)
    {
        if (curveProp == null) return;
        if (!string.IsNullOrEmpty(label))
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);

        AnimationCurve curve = curveProp.animationCurveValue;

        // 大编辑区
        Rect bigRect = GUILayoutUtility.GetRect(0f, 150f, GUILayout.ExpandWidth(true));
        EditorGUI.BeginChangeCheck();
        // CurveField Ranges：X 始终 [0,1] 归一化；Y 显示用 yRange
        Rect range = new Rect(0f, yRange.x, 1f, yRange.y - yRange.x);
        curve = EditorGUI.CurveField(bigRect, GUIContent.none, curve, curveColor, range);
        if (EditorGUI.EndChangeCheck())
            curveProp.animationCurveValue = curve;

        // playhead 红线
        TimedCurveDrawer.DrawPlayhead(bigRect, info, new Color(1f, 0.25f, 0.25f, 0.9f));

        // 时间轴刻度
        Rect timeAxisRect = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
        TimedCurveDrawer.DrawTimeAxis(timeAxisRect, info.valid ? info.duration : 1.0);

        // 分析栏 + playhead（标注水平段 = 朝向锁定段）
        Rect analysisRect = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
        CurvePauseAnalysisDrawer.Draw(analysisRect, curve);
        TimedCurveDrawer.DrawPlayhead(analysisRect, info, new Color(1f, 0.3f, 0.3f, 0.95f));

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("置零", GUILayout.Width(60f)))
            curveProp.animationCurveValue = AnimationCurve.Constant(0f, 1f, 0f);
        if (GUILayout.Button("反转", GUILayout.Width(60f)))
        {
            var c = curveProp.animationCurveValue;
            var keys = c.keys;
            for (int i = 0; i < keys.Length; i++)
                keys[i].value = -keys[i].value;
            c.keys = keys;
            curveProp.animationCurveValue = c;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawLookSequenceTemplate(LookOverrideClip clip, TimelineClipContext.Info info)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Yaw 模板生成器：转 → 停 → 转回（单位：秒）", EditorStyles.miniBoldLabel);

        // 起始时间（秒） + ⏱ 一键填入
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        float ns = EditorGUILayout.FloatField(new GUIContent("起始时间（秒）"), TurnStartSec);
        if (EditorGUI.EndChangeCheck()) TurnStartSec = Mathf.Max(0f, ns);

        EditorGUI.BeginDisabledGroup(!info.playheadInClip);
        if (GUILayout.Button(new GUIContent("⏱ 用当前播放头", "把 Timeline 当前播放头时间填入起始"), GUILayout.Width(110f)))
            TurnStartSec = (float)info.clipLocalTime;
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        float nd = EditorGUILayout.FloatField(new GUIContent("单边转动时长（秒）"), TurnDurationSec);
        if (EditorGUI.EndChangeCheck()) TurnDurationSec = Mathf.Max(0.001f, nd);

        EditorGUI.BeginChangeCheck();
        float nh = EditorGUILayout.FloatField(new GUIContent("中间保持时长（秒）"), HoldDurationSec);
        if (EditorGUI.EndChangeCheck()) HoldDurationSec = Mathf.Max(0f, nh);

        EditorGUI.BeginChangeCheck();
        float na = EditorGUILayout.FloatField(new GUIContent("目标角度（度）"), TurnAngle);
        if (EditorGUI.EndChangeCheck()) TurnAngle = na;

        if (info.valid)
        {
            double totalSec = TurnStartSec + 2.0 * TurnDurationSec + HoldDurationSec;
            string summary = $"序列总跨度 {TimedCurveDrawer.FormatTime(totalSec - TurnStartSec)}，结束于 {TimedCurveDrawer.FormatTime(totalSec)}";
            if (totalSec > info.duration + 1e-3)
                EditorGUILayout.HelpBox(summary + $"，超出 Clip 时长 {TimedCurveDrawer.FormatTime(info.duration)}，末段将被截断。", MessageType.Warning);
            else
                EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(!(info.valid && info.duration > 0));
        if (GUILayout.Button("应用到 Yaw 曲线", GUILayout.Height(22f)))
        {
            Undo.RecordObject(clip, "Apply Look Sequence");
            float startNorm = (float)(TurnStartSec    / info.duration);
            float turnNorm  = (float)(TurnDurationSec / info.duration);
            float holdNorm  = (float)(HoldDurationSec / info.duration);
            yawProp.animationCurveValue = CurvePauseQuickEdit.BuildLookSequence(startNorm, turnNorm, holdNorm, TurnAngle);
            EditorUtility.SetDirty(clip);
        }
        EditorGUI.EndDisabledGroup();
        if (GUILayout.Button("反转角度", GUILayout.Width(80f), GUILayout.Height(22f)))
            TurnAngle = -TurnAngle;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }
}
