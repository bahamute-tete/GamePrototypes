using SlotSystem.Timeline;
using UnityEditor;
using UnityEngine;

namespace SlotSystem.EditorTools
{
    [CustomEditor(typeof(SlotInteractionClip))]
    public sealed class SlotInteractionClipEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var mode = serializedObject.FindProperty("mode");
            EditorGUILayout.PropertyField(mode, new GUIContent("动作"));
            if (mode.enumValueIndex == (int)SlotInteractionMode.Transfer)
            {
                DrawTarget("source", "起点");
                DrawTarget("destination", "终点");
                EditorGUILayout.PropertyField(serializedObject.FindProperty("arcHeight"), new GUIContent("抬升高度"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ease"), new GUIContent("过渡曲线"));
            }
            else DrawTarget("destination", "跟随目标");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("endMode"), new GUIContent("结束后"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("道具抓握点（道具本地坐标）", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gripPosition"), new GUIContent("位置"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gripEulerAngles"), new GUIContent("旋转"));
            EditorGUILayout.HelpBox("Transfer 用于拾取、递交和放下；Follow 用于携带。World 目标固定在世界坐标，Slot / Transform 目标持续跟随。重叠片段以较晚开始者优先。", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTarget(string name, string label)
        {
            var target = serializedObject.FindProperty(name);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            var kind = target.FindPropertyRelative("kind");
            EditorGUILayout.PropertyField(kind, new GUIContent("类型"));
            if (kind.enumValueIndex == (int)SlotTargetKind.Slot)
            {
                EditorGUILayout.PropertyField(target.FindPropertyRelative("manager"), new GUIContent("挂点管理器"));
                EditorGUILayout.PropertyField(target.FindPropertyRelative("slotId"), new GUIContent("挂点 ID"));
            }
            else if (kind.enumValueIndex == (int)SlotTargetKind.Transform)
                EditorGUILayout.PropertyField(target.FindPropertyRelative("target"), new GUIContent("目标物体"));
            EditorGUILayout.PropertyField(target.FindPropertyRelative("position"), new GUIContent(kind.enumValueIndex == 2 ? "世界位置" : "本地偏移"));
            EditorGUILayout.PropertyField(target.FindPropertyRelative("eulerAngles"), new GUIContent(kind.enumValueIndex == 2 ? "世界旋转" : "旋转偏移"));
        }
    }
}
