using UnityEditor;
using UnityEngine;

namespace LiangZhu.Editor
{
    public sealed class DissolveLitGUI : ShaderGUI
    {
        MaterialEditor editor;
        MaterialProperty[] properties;
        bool surface = true, maps = true, dissolve = true, fog = true;
        bool preview = false, advanced;
        static readonly string[] ToggleProperties = { "_UseMaps", "_UseNormal", "_UseDetailMap", "_FogEnable", "_AlphaClip", "_BlinnPhongLight" };
        static readonly string[] Keywords = { "_USE_PBR_MAPS", "_USE_NORMAL_MAP", "_USE_DETAIL_MAP", "_FOG_AFFECTS", "_ALPHATEST_ON", "_BLINNPHONE_LIGHT" };

        MaterialProperty P(string name) => FindProperty(name, properties);
        bool Enabled(string name) => P(name).hasMixedValue || P(name).floatValue > 0.5f;
        void Field(string name, string label) => editor.DefaultShaderProperty(P(name), label);
        void Texture(string name, string label, string extra = null)
        {
            editor.TexturePropertySingleLine(new GUIContent(label), P(name), extra == null ? null : P(extra));
        }
        bool Toggle(string name, string label)
        {
            var p = P(name);
            EditorGUI.showMixedValue = p.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            bool value = EditorGUILayout.Toggle(label, p.floatValue > 0.5f);
            if (EditorGUI.EndChangeCheck()) { editor.RegisterPropertyChangeUndo(label); p.floatValue = value ? 1 : 0; }
            EditorGUI.showMixedValue = false;
            return p.hasMixedValue || value;
        }
        void Popup(string name, string label, params string[] choices)
        {
            var p = P(name);
            EditorGUI.showMixedValue = p.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            int value = EditorGUILayout.Popup(label, Mathf.Clamp(Mathf.RoundToInt(p.floatValue), 0, choices.Length - 1), choices);
            if (EditorGUI.EndChangeCheck()) { editor.RegisterPropertyChangeUndo(label); p.floatValue = value; }
            EditorGUI.showMixedValue = false;
        }
        static bool Section(ref bool open, string label)
        {
            EditorGUILayout.Space(6);
            open = EditorGUILayout.Foldout(open, label, true, EditorStyles.foldoutHeader);
            return open;
        }

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] materialProperties)
        {
            editor = materialEditor; properties = materialProperties;
            float oldWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(EditorGUIUtility.currentViewWidth * 0.42f, 145, 230);
            EditorGUILayout.LabelField("Dissolve Lit", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            if (Section(ref surface, "Surface & Lighting"))
            {
                Popup("_BlinnPhongLight", "Blinn Phong Light", "PBR", "Blinn-Phong");
                Texture("_BaseMap", "Base Map", "_BaseColor");
                editor.TextureScaleOffsetProperty(P("_BaseMap"));
                if (!Enabled("_UseMaps") || P("_UseMaps").hasMixedValue) Field("_Metallic", "Metallic");
                Field("_Smoothness", Enabled("_UseMaps") ? "Smoothness Multiplier" : "Smoothness");
                Texture("_EmissionMap", "Emission Map", "_EmissionColor");
                if (Toggle("_AlphaClip", "Alpha Clip")) Field("_Cutoff", "Cutoff");
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Fresnel", EditorStyles.boldLabel);
                Field("_FresnelIntensity", "Fresnel Intensity");
                if (P("_FresnelIntensity").hasMixedValue || P("_FresnelIntensity").floatValue > 0)
                {
                    Field("_FresnelColor", "Fresnel Color"); Field("_FresnelPower", "Fresnel Power"); Field("_FresnelBias", "Fresnel Bias");
                }

            }
            if (Section(ref maps, "PBR Maps & Surface Detail"))
            {
                if (Toggle("_UseMaps", "Use Maps"))
                {
                    Texture("_MaskMap", "Mask Map");
                    EditorGUILayout.HelpBox("Mask channels: R = Metallic, G = AO, A = Smoothness multiplied by the Smoothness value.", MessageType.None);
                }
                if (Toggle("_UseNormal", "Use Normal")) Texture("_NormalMap", "Normal Map", "_NormalScale");
                if (Toggle("_UseDetailMap", "Use Detail Map"))
                {
                    Texture("_DetailBaseMap", "Detail Base Map");
                    editor.TextureScaleOffsetProperty(P("_DetailBaseMap"));
                    Texture("_DetailNormalMap", "Detail Normal Map", "_DetailNormalScale");
                    editor.TextureScaleOffsetProperty(P("_DetailNormalMap"));
                    Texture("_DetailMask", "Detail Mask");
                }
            }
            if (Section(ref dissolve, "Dissolve"))
            {
                if (Toggle("_DissolveEnabled", "Enable Dissolve"))
                {
                    Field("_DissolveCoverageWidth", "Dissolve Coverage Width");
                    Field("_DissolveAAPixels", "Dissolve AAPixels");
                    EditorGUILayout.HelpBox("控制器可通过 PropertyBlock 覆盖边缘宽度和 HDR 颜色；材质值仅在未覆盖时生效。World 空间裁切由控制器的 Origin / Radius 决定。", MessageType.None);
                    Field("_DissolveEdgeColor", "Dissolve Edge Color");
                    Field("_DissolveEdgeIntensity", "Dissolve Edge Intensity");
                    Field("_DissolveEdgeWidth", "Dissolve Edge Width");
                    if (Toggle("_DissolveBrightnessFade", "Dissolve Brightness Fade")) Field("_DissolveBrightnessPower", "Dissolve Brightness Power");
                    EditorGUI.indentLevel++;
                    if (Section(ref preview, "Dissolve Parameters"))
                    {
                        EditorGUILayout.HelpBox("本分组中的参数：如果物体由 DissolveController 脚本控制，将由 Controller 接管；只有未使用控制器时，才可在此手动调整。", MessageType.Info);
                        Field("_DissolveAmount", "Dissolve Amount");
                        Popup("_DissolveMode", "Dissolve Mode", "Noise", "Direction", "Radial");
                        Popup("_DissolveSpace", "Dissolve Space", "Local", "World");
                        var mode = P("_DissolveMode");
                        bool directional = mode.hasMixedValue || mode.floatValue == 1 || mode.floatValue == 2;
                        if (directional) Field("_DissolveEdgeNoiseStrength", "Dissolve Edge Noise Strength");
                        if (mode.hasMixedValue || mode.floatValue == 0 || P("_DissolveEdgeNoiseStrength").hasMixedValue || P("_DissolveEdgeNoiseStrength").floatValue > 0)
                        {
                            Popup("_DissolveUseNoiseTex", "Dissolve Use Noise Tex", "Procedural Noise", "Shared Noise Texture");
                            if (Enabled("_DissolveUseNoiseTex")) Texture("_DissolveNoiseTex", "Dissolve Noise Tex");
                            Field("_DissolveNoiseScale", "Dissolve Noise Scale");
                        }
                        if (mode.hasMixedValue || mode.floatValue == 1)
                        {
                            Field("_DissolveAxis", "Dissolve Axis");
                            Field("_DissolveAxisCenter", "Dissolve Axis Center");
                        }
                        if (mode.hasMixedValue || mode.floatValue == 2)
                        {
                            Field("_DissolveRadial", "Dissolve Radial");
                            Toggle("_DissolveRadialReverse", "Dissolve Radial Reverse");
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }
            if (Section(ref fog, "Clear Zone Fog"))
            {
                Toggle("_FogEnable", "Fog Enable");
                EditorGUILayout.HelpBox("此效果需要配合 ClearZoneFogController 脚本使用：在场景中配置全局控制器，并添加有效的 SphereFogVolume 清晰区域。雾色、密度和交接权重由控制器统一设置；关闭此开关时使用 Unity 内置距离雾。", MessageType.None);
            }
            if (Section(ref advanced, "Advanced Rendering"))
            {
                Popup("_Cull", "Cull", "Off (Double Sided)", "Front", "Back");
                Toggle("_DissolveDepthA2C", "Depth A2C");
                EditorGUILayout.HelpBox("Enable Depth A2C only for multisampled depth targets. Keep it off for ordinary URP depth textures.", MessageType.None);
                editor.RenderQueueField(); editor.EnableInstancingField(); editor.DoubleSidedGIField();
            }
            if (EditorGUI.EndChangeCheck())
                foreach (Object target in editor.targets) ValidateMaterial((Material)target);
            EditorGUIUtility.labelWidth = oldWidth;
        }

        public override void ValidateMaterial(Material material)
        {
            for (int i = 0; i < Keywords.Length; i++)
            {
                if (material.GetFloat(ToggleProperties[i]) > 0.5f) material.EnableKeyword(Keywords[i]);
                else material.DisableKeyword(Keywords[i]);
            }
        }
    }
}
