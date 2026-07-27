using System.ComponentModel;
using UnityEditor;
using UnityEngine;
using MaterialEditor = UnityEditor.MaterialEditor;

public class CelShadingURP_V1GUI : ShaderGUI
{
    // 折叠区域控制变量
    private bool showBaseSetting = true;
    private bool showRampMap = true;
    private bool showRim = true;
    private bool showEdge = true;
    private bool showSpecularSetting = true;
    private bool showHairSpecular = true;
    private bool showMask = true;
    private bool showDisslove = true;
    private bool showClipPlane = true; // 新增折叠区域控制变量

    // 用于标题样式
    private GUIStyle titleStyle;
    private GUIStyle subTitleStyle;
    private GUIStyle sectionHeaderStyle;
    
    // 用于缩进控制
    private const float BaseIndentWidth = 15f;
    private const float PropertyIndentWidth = 20f;

    enum OPTIONS
    {
        On = 0,
        Off = 1
    }

    OPTIONS op = OPTIONS.Off;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        // 初始化样式
        InitStyles();
        
        Material material = materialEditor.target as Material;

        MaterialProperty faceShadowToggle = FindProperty("_FACE_SHADOW_TEX", properties);
        MaterialProperty rampMapToggle = FindProperty("_RAMP_MAP", properties);
        MaterialProperty hairSpecularToggle = FindProperty("_HAIR_SPECULAR", properties);
        MaterialProperty maskMapToggle = FindProperty("_MASK_MAP", properties);
        MaterialProperty dissloveToggle = FindProperty("_DISSLOVE", properties);
        MaterialProperty clipPlaneToggle = FindProperty("_UseClipPlane", properties); 
        MaterialProperty secondClipPlaneToggle = FindProperty("_UseSecondClipPlane", properties); 
        MaterialProperty planeNormalOSToggle = FindProperty("_PlaneNormalOS", properties); 

        EditorGUILayout.Space(10);
        
        EditorGUILayout.LabelField("卡通渲染设置", titleStyle);
        EditorGUILayout.Space(5);
        
        // 基础设置区域
        DrawBaseSetting(materialEditor, properties, material, rampMapToggle, faceShadowToggle);
        
        // 只在_RAMP_MAP_ON启用时绘制RampMap设置
        if (material.IsKeywordEnabled("_RAMP_MAP_ON"))
        {
            DrawRampMap(materialEditor, properties, material);
        }
        
        DrawRimSettings(materialEditor, properties);
        DrawEdgeSettings(materialEditor, properties);
        DrawMaskSettings(materialEditor, properties, material, maskMapToggle);
        DrawDissloveSettings(materialEditor, properties, material, dissloveToggle);
        DrawClipPlaneSettings(materialEditor, properties, material, clipPlaneToggle, secondClipPlaneToggle, planeNormalOSToggle);

        // 绘制高光设置(包含HairSpecular)
        DrawSpecularSettings(materialEditor, properties, material, hairSpecularToggle);
    }

    // 初始化GUI样式
    private void InitStyles()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
        }
        
        if (subTitleStyle == null)
        {
            subTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
        }
        
        if (sectionHeaderStyle == null)
        {
            sectionHeaderStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
        }
    }

    void DrawKeywordToggle(MaterialEditor editor, Material material, MaterialProperty prop, string label)
    {
        EditorGUI.BeginChangeCheck();
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(PropertyIndentWidth);
        bool state = EditorGUILayout.Toggle(label, prop.floatValue > 0.5f);
        EditorGUILayout.EndHorizontal();
        
        string keyword = prop.name.ToUpper() + "_ON";
        if (EditorGUI.EndChangeCheck())
        {
            prop.floatValue = state ? 1 : 0;
            if (state) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
        }
    }

    void DrawKeywordPop(MaterialEditor editor, Material material, MaterialProperty prop, string label)
    {
        EditorGUI.BeginChangeCheck();
        
        // 根据材质的关键字状态设置当前选项
        string keyword = prop.name.ToUpper() + "_ON";
        string nokeyword = prop.name.ToUpper() + "_OFF";
        op = material.IsKeywordEnabled(keyword) ? OPTIONS.On : OPTIONS.Off;
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(PropertyIndentWidth);
        op = (OPTIONS)EditorGUILayout.EnumPopup(label, op);
        EditorGUILayout.EndHorizontal();

        if (EditorGUI.EndChangeCheck())
        {
            switch (op)
            {
                case OPTIONS.On:
                    material.SetFloat(prop.name.ToUpper(),1);
                    material.EnableKeyword(keyword);
                    material.DisableKeyword(nokeyword);
                    break;

                case OPTIONS.Off:
                    material.SetFloat(prop.name.ToUpper(), 0);
                    material.EnableKeyword(nokeyword);
                    material.DisableKeyword(keyword);
                    break;
            }

            EditorUtility.SetDirty(editor.target);
        }
    }

    void DrawBaseSetting(MaterialEditor editor, MaterialProperty[] props, Material mat, 
                          MaterialProperty rampMapToggle, MaterialProperty faceShadowToggle)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        Rect rect = EditorGUILayout.GetControlRect(false, 18f);
        rect.x += 2f;
        showBaseSetting = EditorGUI.Foldout(rect, showBaseSetting, "基础设置", true, sectionHeaderStyle);

        if (showBaseSetting)
        {
            EditorGUILayout.Space(3);
            
            // 使用水平布局和缩进来提高可读性
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(BaseIndentWidth);
            EditorGUILayout.BeginVertical();
            
            editor.ShaderProperty(FindProperty("_MainTex", props), "主纹理");
            
            // 将RampMap和FaceShadowTexture开关移至这里
            DrawKeywordPop(editor, mat, rampMapToggle, "渐变光照");
            DrawKeywordPop(editor, mat, faceShadowToggle, "面部阴影");
            
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("基本颜色设置", subTitleStyle);
            
            editor.ShaderProperty(FindProperty("_BaseColor", props), "基础颜色");
            editor.ShaderProperty(FindProperty("_ShadowColor", props), "阴影颜色");
            editor.RangeProperty(FindProperty("_ShadowRange", props), "阴影范围");
            editor.RangeProperty(FindProperty("_Brightness", props), "Brightness");

            // 仅在Face Shadow Texture启用时显示对应属性
            if (mat.IsKeywordEnabled("_FACE_SHADOW_TEX_ON"))
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("面部阴影设置", subTitleStyle);
                
                editor.ShaderProperty(FindProperty("_FaceShadowTex", props), "面部阴影贴图");
                editor.RangeProperty(FindProperty("_LerpMax", props), "过渡最大值");
            }

            editor.RangeProperty(FindProperty("_ShadowSmooth", props), "阴影平滑度");
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    void DrawRampMap(MaterialEditor editor, MaterialProperty[] props, Material mat)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        Rect rect = EditorGUILayout.GetControlRect(false, 18f);
        rect.x += 2f;
        showRampMap = EditorGUI.Foldout(rect, showRampMap, "渐变光照设置", true, sectionHeaderStyle);
        
        if (showRampMap)
        {
            EditorGUILayout.Space(3);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(BaseIndentWidth);
            EditorGUILayout.BeginVertical();
            
            editor.ShaderProperty(FindProperty("_RampTex", props), "渐变贴图");
            
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("渐变颜色设置", subTitleStyle);
            
            editor.ShaderProperty(FindProperty("_Color1", props), "颜色 1");
            editor.ShaderProperty(FindProperty("_Color2", props), "颜色 2");
            editor.ShaderProperty(FindProperty("_Color3", props), "颜色 3");
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    void DrawRimSettings(MaterialEditor editor, MaterialProperty[] props)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        Rect rect = EditorGUILayout.GetControlRect(false, 18f);
        rect.x += 2f;
        showRim = EditorGUI.Foldout(rect, showRim, "边缘光设置", true, sectionHeaderStyle);
        
        if (showRim)
        {
            EditorGUILayout.Space(3);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(BaseIndentWidth);
            EditorGUILayout.BeginVertical();
            
            editor.FloatProperty(FindProperty("_RimMin", props), "边缘光最小值");
            editor.FloatProperty(FindProperty("_RimMax", props), "边缘光最大值");
            editor.RangeProperty(FindProperty("_RimSmooth", props), "边缘光平滑度");
            editor.ShaderProperty(FindProperty("_RimColor", props), "边缘光颜色");
            editor.RangeProperty(FindProperty("_RimBloomExp", props), "边缘光强度");
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    void DrawEdgeSettings(MaterialEditor editor, MaterialProperty[] props)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        Rect rect = EditorGUILayout.GetControlRect(false, 18f);
        rect.x += 2f;
        showEdge = EditorGUI.Foldout(rect, showEdge, "描边设置", true, sectionHeaderStyle);
        
        if (showEdge)
        {
            EditorGUILayout.Space(3);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(BaseIndentWidth);
            EditorGUILayout.BeginVertical();
            editor.ShaderProperty(FindProperty("_TurnOnThickness", props), "开启边缘");	//新增
            editor.RangeProperty(FindProperty("_Thickness", props), "边缘厚度");
            editor.ShaderProperty(FindProperty("_EdgeColor", props), "边缘颜色");
           
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }


    void DrawMaskSettings(MaterialEditor editor, MaterialProperty[] props,Material material, MaterialProperty maskMapToggle)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        Rect rect = EditorGUILayout.GetControlRect(false, 18f);
        rect.x += 2f;
        showMask = EditorGUI.Foldout(rect, showMask, "Mask设置", true, sectionHeaderStyle);

        if (showMask)
        {
            EditorGUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(BaseIndentWidth);
            EditorGUILayout.BeginVertical();

            DrawKeywordPop(editor, material, maskMapToggle, "Mask贴图");

            editor.RangeProperty(FindProperty("_Cutoff", props), "MaskVaule");
            editor.TextureProperty(FindProperty("_MaskMap", props), "Mask Texture");

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);

    }

    void DrawDissloveSettings(MaterialEditor editor, MaterialProperty[] props, Material material, MaterialProperty dissloveToggle)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        Rect rect = EditorGUILayout.GetControlRect(false, 18f);
        rect.x += 2f;
        showDisslove = EditorGUI.Foldout(rect, showDisslove, "溶解", true, sectionHeaderStyle);

        if (showDisslove)
        {
            EditorGUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(BaseIndentWidth);
            EditorGUILayout.BeginVertical();

            DrawKeywordPop(editor, material, dissloveToggle, "溶解开关");

            editor.FloatProperty(FindProperty("_EdgeWidth", props), "Edge Width");
            editor.ColorProperty(FindProperty("_DissloveEdgeColor", props), "Edge Color");
            editor.RangeProperty(FindProperty("_CutoffHeight1", props), "Disslove Cutoff");
            editor.TextureProperty(FindProperty("_NoiseMap", props), "Disslove Texture");

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);

    }

    void DrawClipPlaneSettings(MaterialEditor editor, MaterialProperty[] props, Material material, 
                            MaterialProperty clipPlaneToggle, MaterialProperty secondClipPlaneToggle, MaterialProperty planeNormalOSToggle)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        Rect rect = EditorGUILayout.GetControlRect(false, 18f);
        rect.x += 2f;
        showClipPlane = EditorGUI.Foldout(rect, showClipPlane, "剪切平面设置", true, sectionHeaderStyle);
        
        if (showClipPlane)
        {
            EditorGUILayout.Space(3);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(BaseIndentWidth);
            EditorGUILayout.BeginVertical();
            
            // 剪切平面开关
            DrawKeywordToggle(editor, material, clipPlaneToggle, "使用剪切平面");
            
            // 仅在启用剪切平面时显示其他设置
            if (material.IsKeywordEnabled("_CLIP_PLANE"))
            {
                // 法线空间设置
                DrawKeywordToggle(editor, material, planeNormalOSToggle, "剪切平面法线使用对象空间");
                
                // 第二剪切平面开关
                DrawKeywordToggle(editor, material, secondClipPlaneToggle, "使用第二剪切平面");
                
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("剪切平面参数", subTitleStyle);
                
                // 剪切平面参数设置
                editor.RangeProperty(FindProperty("_LineWidth", props), "线宽");
                editor.ShaderProperty(FindProperty("_LineColor", props), "线颜色");
                editor.ShaderProperty(FindProperty("_ClipPlane", props), "剪切平面参数");
                editor.ShaderProperty(FindProperty("_EdgeColorInside", props), "内部边缘颜色");
                
                // 仅在启用第二剪切平面时显示
                if (material.IsKeywordEnabled("_SECOND_CLIP_PLANE_ON"))
                {
                    editor.ShaderProperty(FindProperty("_ClipPlane2", props), "第二剪切平面参数");
                }
                
                editor.ShaderProperty(FindProperty("_ColorInside", props), "内部颜色");
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    void DrawSpecularSettings(MaterialEditor editor, MaterialProperty[] props, Material mat, 
                              MaterialProperty hairSpecularToggle)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        Rect rect = EditorGUILayout.GetControlRect(false, 18f);
        rect.x += 2f;
        showSpecularSetting = EditorGUI.Foldout(rect, showSpecularSetting, "高光设置", true, sectionHeaderStyle);
        
        if (showSpecularSetting)
        {
            EditorGUILayout.Space(3);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(BaseIndentWidth);
            EditorGUILayout.BeginVertical();
            
            // 基础高光设置
            editor.ShaderProperty(FindProperty("_Glossiness", props), "光泽度");
            editor.ShaderProperty(FindProperty("_SpecColor", props), "高光颜色");
            

            DrawKeywordPop(editor, mat, hairSpecularToggle, "头发高光");
            

            if (mat.IsKeywordEnabled("_HAIR_SPECULAR_ON"))
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("头发高光设置", subTitleStyle);
                
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(PropertyIndentWidth);
                EditorGUILayout.BeginVertical();
                
                editor.ShaderProperty(FindProperty("_StretchedNoiseTex", props), "噪声贴图");
                editor.RangeProperty(FindProperty("_ShiftTangent", props), "切线偏移");
                editor.ShaderProperty(FindProperty("_AnisotropicPowerScale", props), "高光强度");
                editor.RangeProperty(FindProperty("_AnisotropicPowerValue", props), "高光锐度");
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }
}