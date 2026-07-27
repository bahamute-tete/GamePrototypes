using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class TestUIToolKit : EditorWindow
{
    private Button btn;

    [MenuItem("Tools/UIToolKit")]
    public static void ShowWindow()
    {
        var window = GetWindow<TestUIToolKit>("UIToolKit");
        window.titleContent = new GUIContent();
        window.minSize = new Vector2(300, 200);
    }

    public void CreateGUI()
    {
        VisualElement root = rootVisualElement;

        string uxmlPath = "Assets/20250731/UIToolKit/resources/test1.uxml";
        VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
        if (uxmlPath is null)
        {
            return;
        }

        visualTree.CloneTree(root);
    }
}
