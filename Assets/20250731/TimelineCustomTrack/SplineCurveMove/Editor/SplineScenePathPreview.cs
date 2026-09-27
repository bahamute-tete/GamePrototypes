using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Timeline;

/// <summary>Clip selection, not GameObject selection, owns this preview.</summary>
[InitializeOnLoad]
public static class SplineScenePathPreview
{
    private const string Preference = "SplineMove.SelectedClipPreview";
    private static readonly Vector3[] Points = new Vector3[101];
    private static double nextRepaint;
    public static bool Enabled
    {
        get => EditorPrefs.GetBool(Preference, true);
        set { EditorPrefs.SetBool(Preference, value); SceneView.RepaintAll(); }
    }
    static SplineScenePathPreview()
    {
        SceneView.duringSceneGui += Draw;
        EditorApplication.update += Refresh;
    }
    public static void InspectorControls()
    {
        bool enabled = EditorGUILayout.ToggleLeft("Scene 中显示所选 Clip 路径", Enabled);
        if (enabled != Enabled) Enabled = enabled;
        if (enabled) EditorGUILayout.HelpBox("Scene 打开 Gizmos 后，橙线=当前参考系中的完整路径；绿/红=起终点；青色=当前轨道预期位置与朝向（含 Clip 混合，不含额外 LookOverride）。", MessageType.None);
    }
    private static bool IsMove(TimelineClip clip)
        => clip != null && (clip.asset is SplineCurveMoveClip || clip.asset is SplineTransitionClip);
    private static void Refresh()
    {
        if (!Enabled || EditorApplication.timeSinceStartup < nextRepaint) return;
        nextRepaint = EditorApplication.timeSinceStartup + 1.0 / 30;
        // Only repaint while a supported Timeline clip is selected. No player/Director evaluation.
        foreach (var clip in TimelineEditor.selectedClips)
            if (IsMove(clip)) { SceneView.RepaintAll(); return; }
    }
    private static void Draw(SceneView view)
    {
        if (!Enabled || !view.drawGizmos || Event.current.type != EventType.Repaint) return;
        var director = TimelineEditor.inspectedDirector;
        if (director == null) return;
        Color oldColor = Handles.color;
        CompareFunction oldDepth = Handles.zTest;
        Matrix4x4 oldMatrix = Handles.matrix;
        try
        {
            Handles.matrix = Matrix4x4.identity;
            Handles.zTest = CompareFunction.Always;
            foreach (var clip in TimelineEditor.selectedClips)
            {
                if (!IsMove(clip)) continue;
                if (!SplineScenePreview.Sample(director, clip, Points, out var data))
                {
                    Handles.color = Color.yellow;
                    Handles.Label(data.target != null ? data.target.position : director.transform.position,
                        clip.displayName + "：" + data.message);
                    continue;
                }
                Handles.color = new Color(1f, .55f, .12f, .95f);
                Handles.DrawAAPolyLine(3.5f, Points);
                // Three evenly spaced direction hints keep the full route readable.
                for (int arrow = 1; arrow <= 3; arrow++)
                {
                    int i = arrow * (Points.Length - 1) / 4;
                    Vector3 tangent = Points[i+1] - Points[i-1];
                    if (tangent.sqrMagnitude < 1e-8f) continue;
                    Handles.ArrowHandleCap(0, Points[i], Quaternion.LookRotation(tangent),
                        HandleUtility.GetHandleSize(Points[i]) * .28f, EventType.Repaint);
                }
                Mark(Points[0], Color.green, clip.displayName + " 起点");
                Mark(Points[Points.Length-1], new Color(1,.25f,.25f), "终点");
                if (data.hasExpected)
                {
                    Mark(data.position, Color.cyan, "轨道预期位置");
                    float size = HandleUtility.GetHandleSize(data.position) * .65f;
                    if (data.hasRotation)
                    {
                        Handles.ArrowHandleCap(0, data.position, data.rotation, size, EventType.Repaint);
                        Handles.DrawLine(data.position, data.position + data.rotation * Vector3.up * size * .5f);
                    }
                    if (data.target != null && Vector3.Distance(data.position, data.target.position) > .01f)
                    {
                        Handles.color = Color.magenta;
                        Handles.DrawDottedLine(data.position, data.target.position, 4);
                        Handles.Label(data.target.position, "实际位置 · 偏差 " + Vector3.Distance(data.position, data.target.position).ToString("F3") + " m");
                    }
                }
                else Handles.Label(Points[0] + Vector3.up * HandleUtility.GetHandleSize(Points[0]) * .2f, data.message);
            }
        }
        finally { Handles.color = oldColor; Handles.zTest = oldDepth; Handles.matrix = oldMatrix; }
    }
    private static void Mark(Vector3 position, Color color, string label)
    {
        Handles.color = color;
        float size = HandleUtility.GetHandleSize(position) * .11f;
        Handles.SphereHandleCap(0, position, Quaternion.identity, size, EventType.Repaint);
        Handles.Label(position + Vector3.up * size, label);
    }
}
