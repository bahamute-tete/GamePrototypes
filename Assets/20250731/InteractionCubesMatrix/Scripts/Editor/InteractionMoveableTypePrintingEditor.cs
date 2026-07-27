using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(InteractionMoveableTypePrintig))]
public class InteractionMoveableTypePrintingEditor : Editor
{
    private Vector2Int texIndexZW = new Vector2Int(8, 8); // _TexIndex.zw 参数的默认值
    private Vector2Int texIndexXY = new Vector2Int(0, 0); // _TexIndex.xy 参数的默认值
    private bool useRandomTexIndexXY = false; // 是否使用随机值
    private bool showTextureSettings = false; // 折叠菜单状态
    
    // 添加分类可视化开关
    private bool visualizeCategoriesWithGizmo = true;

    private void OnEnable()
    {
        // 从EditorPrefs加载visualizeCategoriesWithGizmo的值
        visualizeCategoriesWithGizmo = EditorPrefs.GetBool("MoveableTypePrinting_VisualizeCategories", true);
    }

    public override void OnInspectorGUI()
    {
        // 显示默认的检查器属性
        DrawDefaultInspector();

        // 获取当前选中的目标脚本
        InteractionMoveableTypePrintig script = (InteractionMoveableTypePrintig)target;

        // 添加分隔线
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.LabelField("编辑器工具", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // 添加分类可视化开关
        bool newVisualize = EditorGUILayout.Toggle("可视化分类 (Gizmo)", visualizeCategoriesWithGizmo);
        if (newVisualize != visualizeCategoriesWithGizmo)
        {
            visualizeCategoriesWithGizmo = newVisualize;
            EditorPrefs.SetBool("MoveableTypePrinting_VisualizeCategories", visualizeCategoriesWithGizmo);
            SceneView.RepaintAll(); // 刷新场景视图以更新Gizmo显示
        }
        
        EditorGUILayout.Space();

        // 添加创建按钮
        if (GUILayout.Button("创建物体网格", GUILayout.Height(30)))
        {
            // 调用脚本中的重新生成方法
            script.RegenerateGrid();
            
            // 提示已完成创建
            SceneView.RepaintAll(); 
            Undo.RegisterCompleteObjectUndo(script, "Generate Object Grid");
            EditorUtility.SetDirty(script); 
        }

        // 添加删除按钮
        if (GUILayout.Button("删除所有生成的物体", GUILayout.Height(30)))
        {
            Undo.RegisterCompleteObjectUndo(script, "Clear Generated Objects");
            script.ClearGeneratedObjects();
            SceneView.RepaintAll(); 
            EditorUtility.SetDirty(script);
        }

         if (GUILayout.Button("读取纹理分组数据", GUILayout.Height(30)))
        {
            Undo.RegisterCompleteObjectUndo(script, "Reset TextureTileID");
            script.ResetTextureIndices();
            SceneView.RepaintAll(); 
            EditorUtility.SetDirty(script);
        }
    }
}

