// Assets/Scripts/Timeline/Editor/TimelineNameBinderEditor.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

[CustomEditor(typeof(TimelineNameBinder))]
public class TimelineNameBinderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var binder = (TimelineNameBinder)target;

        DrawDefaultInspector();   // 同时展示已记录的两张表,方便核对

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            $"已记录  轨道绑定 {binder.TrackBindings.Count}  /  Exposed {binder.ExposedBindings.Count}",
            EditorStyles.miniBoldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Capture(从当前 Director 记录)"))
            {
                Undo.RecordObject(binder, "Capture Timeline Bindings");
                binder.Capture();
                EditorUtility.SetDirty(binder);
            }

            if (GUILayout.Button("Apply(按名字重绑)"))
            {
                var director = binder.GetComponent<PlayableDirector>();
                Undo.RecordObject(director, "Apply Timeline Bindings");
                binder.Apply();
                EditorUtility.SetDirty(director);
            }
        }

        if (GUILayout.Button("Clear(清空记录)"))
        {
            serializedObject.Update();
            serializedObject.FindProperty("_trackBindings").ClearArray();
            serializedObject.FindProperty("_exposedBindings").ClearArray();
            serializedObject.ApplyModifiedProperties();
        }

        EditorGUILayout.HelpBox(
            "工作流:在绑定正确的 Director 上点 Capture → 复制 GameObject(数据随之带走)→ 换上复制出来的 .playable → 点 Apply。",
            MessageType.Info);
    }
}
#endif
