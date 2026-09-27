using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SplineTransitionClip))]
public class SplineTransitionClipEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SplineScenePathPreview.InspectorControls();
        var automatic = serializedObject.FindProperty("autoMatchAdjacentPaths");
        EditorGUILayout.PropertyField(automatic, new GUIContent("自动匹配前后路径"));
        if (automatic.boolValue)
        {
            EditorGUILayout.HelpBox("自动接上前段终点与后段起点的位置、切线和速度。无需手动摆锚点或填写端点速度。两端须紧邻且无淡入淡出；移动参考系的平移、旋转和缩放均参与计算。", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("手动模式：使用锚点位置、+Z 方向和端点速度。不会自动保证与前后路径连续。", MessageType.Info);
            foreach (string name in new[] { "departure", "arrival", "departureSpeed", "arrivalSpeed" })
                EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty("arcHeight"), new GUIContent("中段抬高"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("applyRotation"), new GUIContent("应用朝向"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("endMode"), new GUIContent("结束后"));
        serializedObject.ApplyModifiedProperties();
        var bridge = (SplineTransitionClip)target;
        if (bridge.autoMatchAdjacentPaths)
        {
            var info = TimelineClipContext.Resolve(bridge);
            if (bridge.TryGetAdjacentPaths(info.clip, out var previous, out var next, out var error))
                EditorGUILayout.HelpBox($"{previous.displayName}（结束 {previous.end:F2}s） → 过渡 → {next.displayName}（开始 {next.start:F2}s）", MessageType.None);
            else EditorGUILayout.HelpBox(error, MessageType.Warning);
        }
        EditorGUILayout.HelpBox("自动模式匹配交接处的位置与速度，不保证加速度连续。大间距或短时长仍可能使中段速度较高；请按路线长度设置时长。参考系自身须连续运动。", MessageType.None);
    }
}
