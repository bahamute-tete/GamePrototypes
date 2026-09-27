using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(DissolveController)), CanEditMultipleObjects]
public sealed class DissolveControllerEditor : Editor
{
    SerializedProperty P(string n) => serializedObject.FindProperty(n);
    void Field(string n, string label) => EditorGUILayout.PropertyField(P(n), new GUIContent(label), true);
    void Section(string s) { EditorGUILayout.Space(7); EditorGUILayout.LabelField(s, EditorStyles.boldLabel); }
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        Section("空间与模式 / Space & Mode"); Field("space", "Space"); Field("mode", "Mode");
        bool mixed=P("space").hasMultipleDifferentValues || P("mode").hasMultipleDifferentValues;
        bool world=P("space").intValue==1, noise=P("mode").intValue==0, spatial=world&&!noise;
        if (!mixed)
        {
            if (!spatial) { Section("进度 / Progress"); Field("amount","Amount / 消失进度"); }
            if (spatial)
            {
                Section("世界定位 / World Origin"); Field("worldOrigin","Origin / 定位物体");
                EditorGUILayout.HelpBox("只读取世界位置，忽略旋转和缩放。",MessageType.None);
                if (!P("worldOrigin").objectReferenceValue) EditorGUILayout.HelpBox("缺少 Origin：保持显示。",MessageType.Warning);
            }
            if (P("mode").intValue==1)
            {
                Section("平面 / Direction"); Field("axisDirection",world?"Direction / 世界方向":"Direction / 局部方向");
                if (spatial) Field("planeOffset","Plane Offset / 偏移（米）");
                Field("directionReverse","Flip Side / 反转消失侧");
                EditorGUILayout.HelpBox(spatial?"平面位置 = Origin + Direction × Offset。Offset 可为负数；反转不改变平面位置。":"沿每个 Renderer 的 Bounds，由 Amount 推进。",MessageType.None);
            }
            if (P("mode").intValue==2)
            {
                Section("球体 / Radial"); if(spatial) Field("radius","Radius / 半径（米）"); Field("radialReverse","Reverse / 反转消失侧");
                EditorGUILayout.HelpBox(spatial?"默认消去球内，反转后保留球内。":"球心为各自 Bounds 中心，由 Amount 推进。",MessageType.None);
            }
        }
        Section("Shared Noise"); Field("noiseTexture","Noise Texture"); Field("noiseScale","Noise Scale");
        if(!mixed&&!noise) Field("edgeNoiseStrength",spatial?"Edge Noise Strength（米）":"Edge Noise Strength（归一化）");
        Section("溶解边缘 / Dissolve Edge"); Field("DissolveEdgeWidth",noise?"Edge Width（阈值）":world?"Edge Width（米）":"Edge Width（局部长度）"); Field("DissolveEdgeColor","Edge Color / HDR"); Field("DissolveEdgeIntensity","Edge Intensity / 强度倍数");
        Section("Controlled Renderers"); Field("controlledRenderers","受控 Renderer");
        Section("Final State"); Field("autoToggleRenderer","Auto Toggle Renderer");
        if (!mixed && spatial) {
            Field("worldCullPadding","World Cull Padding（米）");
            EditorGUILayout.HelpBox("完全进入消失侧后关闭不透明 Renderer；回退、移动物体或 Origin 时自动恢复。保留噪声、Coverage 和额外 AA 安全余量。SkinnedMeshRenderer 的 Local Bounds 必须覆盖动画范围。",MessageType.None);
        } else using(new EditorGUI.DisabledScope(mixed)) Field("hideThreshold","Hide Threshold（仅 Amount）");
        Section("Bounds Refresh"); using(new EditorGUI.DisabledScope(mixed||noise||spatial)) Field("refreshAnimatedBounds","Refresh Animated Bounds");
        if(serializedObject.ApplyModifiedProperties()) foreach(DissolveController c in targets)c.ForceRefresh();
    }
}
