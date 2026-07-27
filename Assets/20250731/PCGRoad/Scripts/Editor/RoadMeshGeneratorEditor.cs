using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RoadMeshGenerator))]
public class RoadMeshGeneratorEditor : Editor
{
    // 定义序列化属性以便访问
    private SerializedProperty samplingModeProp;
    private SerializedProperty pointsPerSegmentProp;
    private SerializedProperty densityFactorProp;
    private SerializedProperty totalCurvePointsProp;
    private SerializedProperty autoUpdateProp;
    
    // UI分组标签
    private GUIContent segmentModeLabel;
    private GUIContent globalModeLabel;

    private void OnEnable()
    {
        // 获取序列化属性
        samplingModeProp = serializedObject.FindProperty("samplingMode");
        pointsPerSegmentProp = serializedObject.FindProperty("pointsPerSegment");
        densityFactorProp = serializedObject.FindProperty("densityFactor");
        totalCurvePointsProp = serializedObject.FindProperty("totalCurvePoints");
        autoUpdateProp = serializedObject.FindProperty("autoUpdate");
        
        // 初始化UI标签
        segmentModeLabel = new GUIContent("每段点数", "每段曲线的采样点数，值越大曲线越平滑");
        globalModeLabel = new GUIContent("总点数", "曲线上的总采样点数");
    }

    public override void OnInspectorGUI()
    {
        RoadMeshGenerator generator = (RoadMeshGenerator)target;
        serializedObject.Update();
        
        // 缓存当前自动更新状态，以便在临时关闭后恢复
        bool wasAutoUpdate = generator.autoUpdate;
        
        // 标记是否有任何参数改变
        EditorGUI.BeginChangeCheck();
        
        // 在Property修改过程中临时关闭自动更新以提高性能
        generator.autoUpdate = false;
        
        // 绘制Road Settings部分
        DrawRoadSettings();
        
        // 绘制采样模式和相应参数
        DrawSamplingSettings();
        
        // 绘制其他设置
        DrawOtherSettings();
        
        // 检查是否有属性更改
        bool changed = EditorGUI.EndChangeCheck();
        
        // 应用更改
        serializedObject.ApplyModifiedProperties();
        
        // 如果有属性更改，并且原来是自动更新模式，则立即更新网格
        if (changed)
        {
            // 恢复自动更新状态
            generator.autoUpdate = wasAutoUpdate;
            
            // 如果自动更新开启，立即更新网格
            if (wasAutoUpdate && generator.enabled)
            {
                generator.GenerateRoadMesh();
                SceneView.RepaintAll();
            }
        }
        
        // 在非自动更新模式下显示更新按钮
        if (!generator.autoUpdate)
        {
            EditorGUILayout.Space(10);
            if (GUILayout.Button("更新道路网格", GUILayout.Height(25)))
            {
                Undo.RecordObject(generator.GetComponent<MeshFilter>(), "Update Road Mesh");
                generator.GenerateRoadMesh();
                SceneView.RepaintAll();
            }
        }
    }

    private void DrawRoadSettings()
    {
        EditorGUILayout.LabelField("道路设置", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pathManager"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("roadWidth"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("roadDepth")); // 添加厚度控制
        EditorGUILayout.PropertyField(autoUpdateProp);
        EditorGUILayout.Space(5);
        
        // 材质设置
        EditorGUILayout.LabelField("材质设置", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("useMultipleMaterials"));
        
        bool useMultiMat = serializedObject.FindProperty("useMultipleMaterials").boolValue;
        float roadDepth = serializedObject.FindProperty("roadDepth").floatValue;
        
        // 顶面材质总是显示
        EditorGUILayout.PropertyField(serializedObject.FindProperty("topMaterial"), new GUIContent("顶面材质"));
        
        // 只有当启用多材质且有厚度时，才显示其他材质
        if (useMultiMat && roadDepth > 0)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sideMaterial"), new GUIContent("侧面材质"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bottomMaterial"), new GUIContent("底面材质"));
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.Space(5);
    }

    private void DrawSamplingSettings()
    {
        // 曲线采样设置标题
        EditorGUILayout.LabelField("曲线采样设置", EditorStyles.boldLabel);
        
        // 采样模式下拉菜单
        EditorGUILayout.PropertyField(samplingModeProp, new GUIContent("采样模式", "选择采样点分布方式"));
        
        // 根据当前选择的采样模式显示相应选项
        RoadMeshGenerator.SamplingMode currentMode = (RoadMeshGenerator.SamplingMode)samplingModeProp.enumValueIndex;
        
        EditorGUI.indentLevel++;
        
        if (currentMode == RoadMeshGenerator.SamplingMode.UniformPerSegment)
        {
            // 每段固定点数模式的设置
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("每段固定点数模式", EditorStyles.miniLabel);
            EditorGUILayout.PropertyField(pointsPerSegmentProp, segmentModeLabel);
            EditorGUILayout.EndVertical();
        }
        else // UniformGlobal
        {
            // 全局均匀模式的设置
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("全局均匀分布模式", EditorStyles.miniLabel);
            EditorGUILayout.PropertyField(totalCurvePointsProp, globalModeLabel);
            EditorGUILayout.PropertyField(densityFactorProp, new GUIContent("密度因子", "采样密度调整因子，值越大采样越密集"));
            EditorGUILayout.EndVertical();
        }
        
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(5);
    }

    private void DrawOtherSettings()
    {
        // UV设置
        EditorGUILayout.LabelField("UV设置", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("uvRepeat"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("flipUV"));
        EditorGUILayout.Space(5);
        
        // 调试可视化设置
        EditorGUILayout.LabelField("调试可视化", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("showDebugVisuals"));
        
        // 只在启用调试可视化时显示相关选项
        if (serializedObject.FindProperty("showDebugVisuals").boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("leftCurveColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rightCurveColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("debugPointSize"));
            EditorGUI.indentLevel--;
        }
    }
}