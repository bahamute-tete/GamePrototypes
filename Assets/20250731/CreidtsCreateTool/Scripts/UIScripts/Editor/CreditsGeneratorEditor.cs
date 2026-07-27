using UnityEngine;
using UnityEditor;


[CustomEditor(typeof(CreditsGenerator))]
public class CreditsGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        CreditsGenerator CreditsSetting = (CreditsGenerator)target;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generat"))
        {
            CreditsSetting.GeneratUI();
        }

        if (GUILayout.Button("Delet"))
        {
            CreditsSetting.DeleteUI();
        }

        EditorGUILayout.EndHorizontal();
    }
}
