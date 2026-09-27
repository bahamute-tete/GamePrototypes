using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(DissolvePlaneContact))]
public sealed class DissolvePlaneContactEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var contact = (DissolvePlaneContact)target;
        EditorGUILayout.HelpBox("平面读取 Controller 的 World / Direction、Origin 和 Plane Offset。Size、Line Width、Glow Radius 均以米为单位。Contact Renderers 留空时读取 Controller 的目标。", MessageType.Info);
        if (contact.controller != null && contact.controller.edgeNoiseStrength > 0)
            EditorGUILayout.HelpBox("当前溶解有边缘噪声。接触特效沿真实平面显示；设 Edge Noise Strength = 0 可让两者精确对齐。", MessageType.Warning);
        if (GUILayout.Button("刷新接触轮廓")) { contact.RefreshContact(); SceneView.RepaintAll(); }
        EditorGUILayout.LabelField("接触线段", contact.SegmentCount.ToString());
        EditorGUILayout.LabelField("状态", contact.Status ?? "Waiting");
        if (contact.ContactTexture != null)
        {
            var rect = GUILayoutUtility.GetAspectRect(1, GUILayout.MaxWidth(240));
            EditorGUI.DrawPreviewTexture(rect, contact.ContactTexture, null, ScaleMode.ScaleToFit);
        }
    }

    [MenuItem("Tools/LiangZhu/Plane Contact/Create For Selected Controller")]
    public static void CreateForSelected()
    {
        var controller = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<DissolveController>() : null;
        if (controller == null) { Debug.LogWarning("Select the GameObject containing DissolveController first."); return; }
        var go = new GameObject(controller.name + " — Contact Plane");
        Undo.RegisterCreatedObjectUndo(go, "Create dissolve contact plane");
        var contact = go.AddComponent<DissolvePlaneContact>();
        contact.controller = controller;
        contact.maskShader = Shader.Find("Hidden/LiangZhu/ContactMask");
        contact.surfaceShader = Shader.Find("Custom/LiangZhu/ContactPlane");
        contact.RefreshContact();
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }
}
