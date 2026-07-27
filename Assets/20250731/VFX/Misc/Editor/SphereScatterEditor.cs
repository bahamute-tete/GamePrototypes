using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SphereScatter))]
public class SphereScatterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("操作按钮", EditorStyles.boldLabel);
        
        SphereScatter scatterScript = (SphereScatter)target;
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("生成撒点", GUILayout.Height(30)))
        {
            scatterScript.GenerateScatter();
        }
        
        if (GUILayout.Button("清除撒点", GUILayout.Height(30)))
        {
            scatterScript.ClearScatter();
        }
        
        EditorGUILayout.EndHorizontal();
    }
}
