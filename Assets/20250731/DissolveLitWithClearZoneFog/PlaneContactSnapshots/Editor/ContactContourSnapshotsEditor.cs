using UnityEditor;
using UnityEngine;

/// <summary>仅服务新的快照组件，不替换原接触组件的 Inspector。</summary>
[CustomEditor(typeof(ContactContourSnapshots))]
public sealed class ContactContourSnapshotsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var snapshots = (ContactContourSnapshots)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("开启 Editor Preview：编辑模式自动捕获、淡出。关闭后冻结已有快照。手动捕获不要求开启预览。",MessageType.Info);
        using (new EditorGUI.DisabledScope(!snapshots.isActiveAndEnabled))
        {
            if (GUILayout.Button("手动捕获当前轮廓")) snapshots.CaptureSnapshotNow();
            if (GUILayout.Button("清空快照"))
            {
                snapshots.ClearSnapshots();
                SceneView.RepaintAll();
            }
        }
        EditorGUILayout.LabelField("当前快照数",snapshots.ActiveCount.ToString());
        EditorGUILayout.LabelField("累计捕获数",snapshots.CapturedCount.ToString());
        if (!string.IsNullOrEmpty(snapshots.Status)) EditorGUILayout.HelpBox(snapshots.Status,MessageType.None);
        if (!string.IsNullOrEmpty(snapshots.SdfDiagnostic)) EditorGUILayout.HelpBox(snapshots.SdfDiagnostic,MessageType.Info);
        EditorGUILayout.LabelField("上次 SDF 捕获耗时",snapshots.LastSdfMilliseconds.ToString("F2")+" ms");
        // 预览关闭时，修改检查模式或颜色也立即反映到冻结的快照，年龄不推进。
        if (GUI.changed && !Application.isPlaying && !snapshots.editorPreview) snapshots.Step(0);
        // 仅在预览/运行期间刷新计数，按钮不创建持久资产，也不把 RT 写入场景。
        if (snapshots.editorPreview || Application.isPlaying) Repaint();
    }
}
