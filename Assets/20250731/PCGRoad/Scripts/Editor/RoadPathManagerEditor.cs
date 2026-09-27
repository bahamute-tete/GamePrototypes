using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RoadPathManager))]
public class RoadPathManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var manager = (RoadPathManager)target;
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("控制点的旋转用于道路倾斜参考，不会被自动改写。无效引用会清空生成结果；相邻重合点会跳过。", MessageType.Info);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("控制点管理", EditorStyles.boldLabel);
        if (GUILayout.Button("添加控制点", GUILayout.Height(28)))
        {
            manager.CreateControlPoint();
            EditorUtility.SetDirty(manager);
            SceneView.RepaintAll();
        }
        if (manager.controlPoints != null)
        {
            for (int i = 0; i < manager.controlPoints.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"点 {i}", GUILayout.Width(45));
                Transform point = manager.controlPoints[i];
                if (GUILayout.Button(point != null ? point.name : "空引用"))
                {
                    if (point != null) Selection.activeTransform = point;
                }
                bool delete = GUILayout.Button("删除", GUILayout.Width(55));
                EditorGUILayout.EndHorizontal();
                if (!delete) continue;
                Undo.RecordObject(manager, "删除道路控制点");
                manager.controlPoints.RemoveAt(i);
                // Preserve externally supplied controls and objects with other components.
                if (point != null && point.parent == manager.transform &&
                    point.GetComponents<Component>().Length == 1 && !manager.controlPoints.Contains(point))
                    Undo.DestroyObjectImmediate(point.gameObject);
                EditorUtility.SetDirty(manager);
                if (manager.autoUpdate) manager.UpdatePath();
                SceneView.RepaintAll();
                break;
            }
        }
        if (GUILayout.Button("更新路径", GUILayout.Height(25)))
        {
            manager.UpdatePath();
            SceneView.RepaintAll();
        }
    }
}
