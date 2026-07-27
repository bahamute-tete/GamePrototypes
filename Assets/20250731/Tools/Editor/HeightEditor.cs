using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
[CustomEditor(typeof(Height))]
public class HeightEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        Height height = target as Height;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("AABB"))
        {
            height.SetAABB();
        }

        EditorGUILayout.EndHorizontal();


    }
}
