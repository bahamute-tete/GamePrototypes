#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(MoveableTypePrintingAttributes)),RequireComponent(typeof(InteractionMoveableTypePrintig))]
public class MoveableTypePrintingAttributesEditor : Editor
{
    private Color testGlowColor = Color.green;
    private string selectedCategory = "";
    private bool showTestingOptions = false;
    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        MoveableTypePrintingAttributes script = (MoveableTypePrintingAttributes)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.LabelField("收集数据", EditorStyles.boldLabel);
        
        // 更新数据按钮
        if (GUILayout.Button("更新物体数据", GUILayout.Height(30)))
        {
            script.UpdateAllObjectsData();
            EditorUtility.SetDirty(script);
        }
        
        // 更新位置按钮
        if (GUILayout.Button("更新所有物体位置", GUILayout.Height(25)))
        {
            script.UpdateAllPositions();
            EditorUtility.SetDirty(script);
        }
        
        EditorGUILayout.Space();
        
        // 测试选项
        showTestingOptions = EditorGUILayout.Foldout(showTestingOptions, "公共方法测试", true);
        if (showTestingOptions)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox("请先更新物体数据", MessageType.Info);
            // 分类列表
            string[] categories = script.GetAllCategoryNames();
            if(categories.Length > 0)
            {
                int categoryIndex = 0;
                for (int i = 0; i < categories.Length; i++)
                {
                    if (categories[i] == selectedCategory)
                    {
                        categoryIndex = i;
                        break;
                    }
                }
                
                categoryIndex = EditorGUILayout.Popup("选择分类", categoryIndex, categories);
                selectedCategory = categories[categoryIndex];
                
                testGlowColor = EditorGUILayout.ColorField(new GUIContent("测试发光颜色"), testGlowColor, true, true, true);
                
                // 设置分类颜色
                if (GUILayout.Button("设置该分类发光颜色"))
                {
                    script.SetCategoryGlowColor(selectedCategory, testGlowColor);
                }


                // 更新位置按钮
                if (GUILayout.Button("测试随机位置"))
                {
                    script.SetRandomDepthPosition(selectedCategory);
                    script.UpdateAllPositions();
                    EditorUtility.SetDirty(script);
                }

                 // 更新位置按钮
                if (GUILayout.Button("复位"))
                {
                    script.RestoreCategoryOriginalPositions(selectedCategory);
                    script.UpdateAllPositions();
                    EditorUtility.SetDirty(script);
                }

             }
            EditorGUILayout.Space();

           


            EditorGUI.indentLevel--;
        }
       
        if (GUI.changed)
        {
            EditorUtility.SetDirty(script);
        }
    }
}
#endif
