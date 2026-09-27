using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RoadMeshGenerator))]
public class RoadMeshGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("道路设置", EditorStyles.boldLabel);
        Field("pathManager");
        Field("roadWidth");
        Field("roadDepth");
        Field("autoUpdate");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("材质设置", EditorStyles.boldLabel);
        Field("useMultipleMaterials");
        Field("topMaterial");
        if (serializedObject.FindProperty("useMultipleMaterials").boolValue &&
            serializedObject.FindProperty("roadDepth").floatValue > 0f)
        {
            Field("sideMaterial");
            Field("bottomMaterial");
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("网格采样设置", EditorStyles.boldLabel);
        Field("samplingMode");
        if (serializedObject.FindProperty("samplingMode").enumValueIndex ==
            (int)RoadMeshGenerator.SamplingMode.UniformPerSegment)
            Field("pointsPerSegment");
        else
        {
            Field("totalCurvePoints");
            Field("densityFactor");
        }
        EditorGUILayout.HelpBox("使用与路径预览相同的中心线算法，网格采样精度独立设置。", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("UV 设置", EditorStyles.boldLabel);
        Field("uvRepeat");
        Field("flipUV");
        Field("showDebugVisuals");
        if (serializedObject.FindProperty("showDebugVisuals").boolValue)
        {
            Field("leftCurveColor");
            Field("rightCurveColor");
            Field("debugPointSize");
        }
        // Serialized properties own Undo and validation. Drawing never changes autoUpdate.
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.Space();
        if (GUILayout.Button("更新道路网格", GUILayout.Height(25)))
        {
            var generator = (RoadMeshGenerator)target;
            generator.GenerateRoadMesh();
            EditorUtility.SetDirty(generator);
            if (generator.gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            SceneView.RepaintAll();
        }
    }

    private void Field(string name) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
}
