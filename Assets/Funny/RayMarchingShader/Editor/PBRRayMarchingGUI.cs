
using UnityEngine;
using UnityEditor;
using System;

public class PBRRayMarchingGUI : ShaderGUI
{

    MaterialEditor editor;
    MaterialProperty[] property;
    Material target;

    MaterialProperty FindProperty(string name)
    {
        return FindProperty(name, property);
    }


    static GUIContent staticLable = new GUIContent();
    static GUIContent MakeLabel(string text, string tooltip = null)
    {
        staticLable.text = text;
        staticLable.tooltip = tooltip;

        return staticLable;
    }


    static GUIContent MakeLabel(MaterialProperty property, string tooltip = null)
    {
        staticLable.text = property.displayName;
        staticLable.tooltip = tooltip;

        return staticLable;
    }
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        this.editor = materialEditor;
        this.property = properties;
        this.target = editor.target as Material;
        TriplannerGUI();

        if (!IsKeywordEnabled("_TRIPLANNAR"))
         PBRGUI();

        OtherSetting();

    }

    private void OtherSetting()
    {
        GUILayout.Label("OtherSetting", EditorStyles.boldLabel);
        if (IsKeywordEnabled("_TRIPLANNAR"))
        {
            MaterialProperty _Shiness = FindProperty("_Shiness");
            EditorGUI.indentLevel += 2;
            EditorGUI.BeginChangeCheck();
            editor.ShaderProperty(_Shiness, MakeLabel(_Shiness));
            EditorGUI.indentLevel -= 2;


            MaterialProperty _SpecularScaler = FindProperty("_SpecularScaler");
            EditorGUI.indentLevel += 2;
            EditorGUI.BeginChangeCheck();
            editor.ShaderProperty(_SpecularScaler, MakeLabel(_SpecularScaler));
            EditorGUI.indentLevel -= 2;
        }
     


        MaterialProperty _GlowColor = FindProperty("_GlowColor");
        EditorGUI.indentLevel += 2;
        EditorGUI.BeginChangeCheck();
        editor.ShaderProperty(_GlowColor, MakeLabel(_GlowColor));
        EditorGUI.indentLevel -= 2;


        MaterialProperty _GlowIntensity = FindProperty("_GlowIntensity");
        EditorGUI.indentLevel += 2;
        EditorGUI.BeginChangeCheck();
        editor.ShaderProperty(_GlowIntensity, MakeLabel(_GlowIntensity));
        EditorGUI.indentLevel -= 2;


        MaterialProperty _Reflection = FindProperty("_Reflection");
        EditorGUI.indentLevel += 2;
        EditorGUI.BeginChangeCheck();
        editor.ShaderProperty(_Reflection, MakeLabel(_Reflection));
        EditorGUI.indentLevel -= 2;

        if (_Reflection.floatValue>0.0f)
            SetKeyword("_RAYMARCHING_REFLECTION", true);
        else
            SetKeyword("_RAYMARCHING_REFLECTION", false);


        MaterialProperty _FullImage = FindProperty("_FullImage");
        EditorGUI.indentLevel += 2;
        EditorGUI.BeginChangeCheck();
        editor.ShaderProperty(_FullImage, MakeLabel(_FullImage));
        EditorGUI.indentLevel -= 2;

        if (_FullImage.floatValue > 0.0f)
            SetKeyword("_FULLIMAGE", true);
        else
            SetKeyword("_FULLIMAGE", false);


    }

    private void PBRGUI()
    {
        
        GUILayout.Label("PBRShading", EditorStyles.boldLabel);
        DoMetallic();
        DoRRoughness();
    }

    private void DoRRoughness()
    {
        MaterialProperty metallic = FindProperty("_Roughness");
        EditorGUI.indentLevel += 2;
        EditorGUI.BeginChangeCheck();
        editor.ShaderProperty(metallic, MakeLabel(metallic));
        EditorGUI.indentLevel -= 2;
    }

    private void DoMetallic()
    {
        MaterialProperty roughness = FindProperty("_Metallic");
        EditorGUI.indentLevel += 2;
        EditorGUI.BeginChangeCheck();
        editor.ShaderProperty(roughness, MakeLabel(roughness));
        EditorGUI.indentLevel -= 2;
    }



    private void TriplannerGUI()
    {
        GUILayout.Label("TriplannerMaps",EditorStyles.boldLabel);



        DoMain();
        if (IsKeywordEnabled("_TRIPLANNAR"))
        {
            DoUVScale();
            DoControll();
            DoNormals();
     
            DoTopMainTex();
            DoTopMosMap();
            DoTopNormal();
        }

        //editor.TextureScaleOffsetProperty(mainTex);
    }

    private void SetKeyword(string keyword,bool state)
    {
        if (state)
        {
            target.EnableKeyword(keyword);
        }
        else
        {
            target.DisableKeyword(keyword);
        } 
    }

    bool IsKeywordEnabled(string keyword)
    {
        return target.IsKeywordEnabled(keyword);
    }

    private void DoTopNormal()
    {
        MaterialProperty map = FindProperty("_TopNormalMap");
        editor.TexturePropertySingleLine(MakeLabel(map), map);
    }

    private void DoTopMosMap()
    {
        MaterialProperty map = FindProperty("_TopMOHSMap");
        editor.TexturePropertySingleLine(MakeLabel(map), map);
    }

    private void DoTopMainTex()
    {
        MaterialProperty mainTex = FindProperty("_TopMainTex");
        editor.TexturePropertySingleLine(MakeLabel(mainTex), mainTex);

        if (EditorGUI.EndChangeCheck())
        {
            if (mainTex.textureValue && IsKeywordEnabled("_TRIPLANNAR"))
            {

                SetKeyword("_SEPARATE_TOP_MAPS", true);
            }
            else
            {
                SetKeyword("_SEPARATE_TOP_MAPS", false);
            }
        }
    }

    private void DoUVScale()
    {
        MaterialProperty triplannerUVScale = FindProperty("_MapScale");
        EditorGUI.indentLevel += 2;
        editor.FloatProperty(triplannerUVScale, triplannerUVScale.displayName);
        EditorGUI.indentLevel -= 2;
    }

    private void DoMain()
    {
        MaterialProperty mainTex = FindProperty("_MainTex");
        MaterialProperty tintColor = FindProperty("_Color");

        editor.TexturePropertySingleLine(MakeLabel(mainTex,"TriplannerMaping while has a texture,vice PBR Shading"), mainTex, tintColor);


        if (EditorGUI.EndChangeCheck())
        {
            if (mainTex.textureValue)
            {

                SetKeyword("_TRIPLANNAR", true);
            }
            else
            {
                SetKeyword("_TRIPLANNAR", false);

               
            }
        }






        //MaterialProperty uvTilling = FindProperty("_MapScale");
        //editor.TexturePropertySingleLine(MakeLabel(uvTilling), uvTilling);

    }

    void DoNormals()
    {
        MaterialProperty map = FindProperty("_NormalMap");
        editor.TexturePropertySingleLine(MakeLabel(map), map);
    }
    private void DoControll()
    {
        MaterialProperty map = FindProperty("_MOSMap");
        editor.TexturePropertySingleLine(MakeLabel(map), map);
    }



}
