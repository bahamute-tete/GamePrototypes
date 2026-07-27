using UnityEditor;
using UnityEngine;

namespace VRFade.Editor
{
    /// <summary>
    /// FadeClip 自定义 Inspector：根据 type 隐藏无关字段，让面板始终干净。
    /// </summary>
    [CustomEditor(typeof(FadeClip))]
    public class FadeClipEditor : UnityEditor.Editor
    {
        // 通用
        SerializedProperty typeProp;
        SerializedProperty colorProp;
        SerializedProperty startAlphaProp;
        SerializedProperty endAlphaProp;
        SerializedProperty curveProp;

        // Iris
        SerializedProperty irisCenterProp;
        SerializedProperty irisSoftnessProp;
        SerializedProperty irisAspectCorrectProp;

        // Desaturate
        SerializedProperty desatAmountProp;
        SerializedProperty brightnessMultProp;

        // DepthFade
        SerializedProperty depthNearProp;
        SerializedProperty depthFarProp;
        SerializedProperty depthInvertProp;

        private void OnEnable()
        {
            typeProp                = serializedObject.FindProperty("type");
            colorProp               = serializedObject.FindProperty("color");
            startAlphaProp          = serializedObject.FindProperty("startAlpha");
            endAlphaProp            = serializedObject.FindProperty("endAlpha");
            curveProp               = serializedObject.FindProperty("curve");

            irisCenterProp          = serializedObject.FindProperty("irisCenter");
            irisSoftnessProp        = serializedObject.FindProperty("irisSoftness");
            irisAspectCorrectProp   = serializedObject.FindProperty("irisAspectCorrect");

            desatAmountProp         = serializedObject.FindProperty("desaturationAmount");
            brightnessMultProp      = serializedObject.FindProperty("brightnessMultiplier");

            depthNearProp           = serializedObject.FindProperty("depthNear");
            depthFarProp            = serializedObject.FindProperty("depthFar");
            depthInvertProp         = serializedObject.FindProperty("depthInvert");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // ========== Type 选择 ==========
            EditorGUILayout.PropertyField(typeProp);

            FadeType type = (FadeType)typeProp.enumValueIndex;

            EditorGUILayout.Space(4);
            DrawSectionHeader("Common");

            // Color：Desaturate 不需要
            if (type != FadeType.Desaturate)
            {
                EditorGUILayout.PropertyField(colorProp);
            }

            EditorGUILayout.PropertyField(startAlphaProp);
            EditorGUILayout.PropertyField(endAlphaProp);
            EditorGUILayout.PropertyField(curveProp);

            // ========== Type-specific ==========
            EditorGUILayout.Space(4);

            switch (type)
            {
                case FadeType.SolidColor:
                    DrawHelpBox("纯色覆盖：整个画面 lerp 到 Color。最简单也最稳定。\n" +
                                "黑场: Color = 黑；白场: Color = 白。");
                    break;

                case FadeType.Iris:
                    DrawSectionHeader("Iris");
                    EditorGUILayout.PropertyField(irisCenterProp);
                    EditorGUILayout.PropertyField(irisSoftnessProp);
                    EditorGUILayout.PropertyField(irisAspectCorrectProp);
                    DrawHelpBox("圆形虹膜遮罩：从屏幕中心向外 lerp 到 Color。\n" +
                                "VR 中是 head-locked 的 2D 遮罩，无视差冲突，是最舒适的过渡形式。");
                    break;

                case FadeType.Desaturate:
                    DrawSectionHeader("Desaturate");
                    EditorGUILayout.PropertyField(desatAmountProp);
                    EditorGUILayout.PropertyField(brightnessMultProp);
                    DrawHelpBox("色彩降饱和 + 压暗：alpha=1 时画面变成 Brightness × 灰阶。\n" +
                                "不会完全遮挡画面，最温和。适合情绪转折、时间慢镜。");
                    break;

                case FadeType.DepthFade:
                    DrawSectionHeader("DepthFade");
                    EditorGUILayout.PropertyField(depthNearProp);
                    EditorGUILayout.PropertyField(depthFarProp);
                    EditorGUILayout.PropertyField(depthInvertProp);
                    DrawHelpBox("深度感应淡入：远处先被 Color 覆盖（或反过来）。\n" +
                                "比纯黑场多一层空间感。需要 URP 启用 Depth Texture（RenderFeature 已通过 ConfigureInput 自动请求）。");
                    break;

                case FadeType.Flash:
                    DrawHelpBox("闪白：白色 + 尖峰曲线 预设。\n" +
                                "建议片段总时长 ≤ 0.3s，用于剧情高潮（爆炸、能量激发）。");
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawSectionHeader(string text)
        {
            var style = new GUIStyle(EditorStyles.boldLabel);
            EditorGUILayout.LabelField(text, style);
        }

        private static void DrawHelpBox(string text)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.HelpBox(text, MessageType.Info);
        }
    }
}
