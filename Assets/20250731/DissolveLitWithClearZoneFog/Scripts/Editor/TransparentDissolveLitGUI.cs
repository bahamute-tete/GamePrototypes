using UnityEditor;
using UnityEngine;

namespace LiangZhu.Editor
{
    public sealed class TransparentDissolveLitGUI : ShaderGUI
    {
        MaterialEditor editor;
        MaterialProperty[] properties;
        bool surface = true, maps = true, dissolve = true, fog = true;
        bool preview = false, advanced;
        static readonly string[] ToggleProperties = { "_UseMaps", "_UseNormalMap", "_FogEnable", "_UsePBR", "_RefractionEnable" };
        static readonly string[] Keywords = { "_USE_PBR_MAPS", "_USE_NORMAL_MAP", "_FOG_AFFECTS_GLASS", "_USE_PBR", "_REFRACTION_ON" };

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
            EditorGUILayout.LabelField("Transparent Dissolve Lit", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            if (Section(ref surface, "Surface & Lighting"))
            {
                Popup("_UsePBR", "Lighting Model", "Blinn-Phong", "PBR");
                Texture("_MainTex", "Main Texture / Color", "_Color");
                editor.TextureScaleOffsetProperty(P("_MainTex"));
                EditorGUILayout.HelpBox("Texture alpha multiplied by Color alpha controls base opacity. Fresnel Color alpha can add rim opacity.", MessageType.None);
                Field("_Smoothness", Enabled("_UseMaps") ? "Smoothness Multiplier" : "Smoothness");
                if (Enabled("_UsePBR") && (!Enabled("_UseMaps") || P("_UseMaps").hasMixedValue)) Field("_Metallic", "Metallic");
                if (P("_UsePBR").hasMixedValue || P("_UsePBR").floatValue <= 0.5f) Field("_SpecIntensity", "Spec Intensity");
                Field("_ReflectIntensity", "Reflect Intensity");
                if (Toggle("_RefractionEnable", "Refraction Enable"))
                {
                    Field("_RefractStrength", "Refract Strength");
                    EditorGUILayout.HelpBox("折射需要在 URP 或相机中开启 Opaque Texture，只采样不透明物体与天空。透明度过低会减弱效果；PBR 金属度为 1 时没有透射贡献。", MessageType.Info);
                }
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Fresnel", EditorStyles.boldLabel);
                Field("_FresnelIntensity", "Fresnel Intensity");
                if (P("_FresnelIntensity").hasMixedValue || P("_FresnelIntensity").floatValue > 0)
                {
                    Field("_FresnelColor", "Fresnel Color");
                    Field("_FresnelPower", "Fresnel Power");
                }
            }
            if (Section(ref maps, "PBR Maps & Normal"))
            {
                if (Toggle("_UseMaps", "Use Maps"))
                {
                    Texture("_MaskMap", "Mask Map");
                    if (!P("_MaskMap").hasMixedValue && P("_MaskMap").textureValue == null)
                        EditorGUILayout.HelpBox("未指定 Mask Map：默认白贴图会令金属度为 1，PBR 模式下折射将不可见。请指定贴图，或关闭 Use Maps 后调整 Metallic。", MessageType.Warning);
                    EditorGUILayout.HelpBox("R = Metallic, G = AO, A = Smoothness multiplied by Smoothness.", MessageType.None);
                }
                if (Toggle("_UseNormalMap", "Use Normal Map"))
                {
                    Texture("_NormalMap", "Normal Map / Scale", "_NormalScale");
                    editor.TextureScaleOffsetProperty(P("_NormalMap"));
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
                Field("_FogColorStrength", "Fog Color Strength");
                Field("_FogFadeStrength", "Fog Fade Strength");
                EditorGUILayout.HelpBox("Fog Color Strength controls tint; Fog Fade Strength controls opacity loss. Both apply to the selected fog source, including Unity fog fallback.", MessageType.None);
                EditorGUILayout.HelpBox("此效果需要配合 ClearZoneFogController 脚本使用：在场景中配置全局控制器，并添加有效的 SphereFogVolume 清晰区域。雾色、密度和交接权重由控制器统一设置；关闭此开关时使用 Unity 内置距离雾。", MessageType.None);
            }
            if (Section(ref advanced, "Advanced Rendering"))
            {
                Popup("_Cull", "Cull", "Off (Double Sided)", "Front", "Back");
                EditorGUILayout.HelpBox("Alpha Blend; ZWrite Off; A2C Off. Dissolve uses continuous alpha coverage.", MessageType.None);
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
