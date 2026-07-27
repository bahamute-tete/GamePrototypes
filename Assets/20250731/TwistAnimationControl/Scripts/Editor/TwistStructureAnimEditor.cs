using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


[CustomEditor(typeof(TwistStructureAnim))]
public class TwistStructureAnimEditor : Editor
{
    // Base
    private SerializedProperty structurePrefab;
    
    // TwistShapeParameters
    private SerializedProperty numSegments;
    private SerializedProperty depthStep;
    private SerializedProperty twistAngle;

    // AnimationParameters
    private SerializedProperty activeAnimation;
    private SerializedProperty easeType;
    private SerializedProperty movmentType;
    private SerializedProperty rule;
    private SerializedProperty duration;
    private SerializedProperty addtionAngle;
    private SerializedProperty waitTime;
    private SerializedProperty delayTime;

    //ColorSetting
    private SerializedProperty lightMode;
    private SerializedProperty shaderPropertyName;
    private SerializedProperty solidDistributionMode;
    private SerializedProperty solidColor;
    private SerializedProperty randomColors;
    private SerializedProperty gradient;
    private SerializedProperty idleLightAnimation;
    private SerializedProperty idleColorType;
    private SerializedProperty idleFrequency;
    private SerializedProperty waveSpeed;

    string tip;
    private void OnEnable()
    {
        // Base
        structurePrefab = serializedObject.FindProperty("structurePrefab");

        // TwistShape
        numSegments = serializedObject.FindProperty("numSegments");
        depthStep = serializedObject.FindProperty("depthStep");
        twistAngle = serializedObject.FindProperty("twistAngle");

        // Animation
        activeAnimation = serializedObject.FindProperty("activeAnimation");
        easeType = serializedObject.FindProperty("easeType");
        movmentType = serializedObject.FindProperty("movmentType");
        rule = serializedObject.FindProperty("rule");
        duration = serializedObject.FindProperty("duration");
        addtionAngle = serializedObject.FindProperty("addtionAngle");
        waitTime = serializedObject.FindProperty("waitTime");
        delayTime = serializedObject.FindProperty("delayTime");

        tip = "+(a):Roll clockwise ‹a› degrees. \r\n\r\n-(a):Roll counter-clockwise ‹a› degrees.\r\n\r\n " +
            "Example:+(45) -(60) + - -(-30),Use comma or space to separate";

        //ColorSetting
        lightMode= serializedObject.FindProperty("lightMode");
        shaderPropertyName= serializedObject.FindProperty("shaderPropertyName");
        solidDistributionMode= serializedObject.FindProperty("solidDistributionMode");
        solidColor= serializedObject.FindProperty("solidColor");
        randomColors= serializedObject.FindProperty("randomColors");
        gradient= serializedObject.FindProperty("gradient");
        idleLightAnimation= serializedObject.FindProperty("idleLightAnimation");
        idleColorType= serializedObject.FindProperty("idleColorType");
        idleFrequency= serializedObject.FindProperty("idleFrequency");
        waveSpeed= serializedObject.FindProperty("waveSpeed");

    }



    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        TwistStructureAnim twistStructureAnim = (TwistStructureAnim)target;

        // ==================== Base ====================
        GUI.backgroundColor = new Color(0.7f, 0.9f, 1.0f);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Base Setting", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(structurePrefab, new GUIContent("Structure Prefab"));
        EditorGUILayout.PropertyField(shaderPropertyName, new GUIContent("Shader Property Name"));
        EditorGUI.indentLevel--;
        EditorGUILayout.EndVertical();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);

        // ==================== TwistShapeParameters ====================
        GUI.backgroundColor = new Color(1.0f, 0.9f, 0.7f);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Shape Control", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(numSegments, new GUIContent("Num Segments"));
        EditorGUILayout.PropertyField(depthStep, new GUIContent("Depth Step"));
        EditorGUILayout.PropertyField(twistAngle, new GUIContent("Twist Angle"));
        EditorGUI.indentLevel--;
        EditorGUILayout.EndVertical();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);

        // ==================== AnimationParameters ====================
        GUI.backgroundColor = new Color(0.9f, 1.0f, 0.7f);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Animation Control", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        EditorGUI.indentLevel++;
        
        EditorGUILayout.PropertyField(activeAnimation, new GUIContent("Active Animation"));
        EditorGUILayout.PropertyField(duration, new GUIContent("Duration"));
        EditorGUILayout.PropertyField(addtionAngle, new GUIContent("Addition Angle"));
        EditorGUILayout.PropertyField(waitTime, new GUIContent("Wait Time"));
        EditorGUILayout.PropertyField(delayTime, new GUIContent("Delay Time"));
        
        EditorGUILayout.PropertyField(easeType, new GUIContent("Ease Type"));
        EditorGUILayout.PropertyField(movmentType, new GUIContent("Movement Mode"));
        
    
        if ((TwistStructureAnim.MovmentMode)movmentType.enumValueIndex == TwistStructureAnim.MovmentMode.Custom)
        {
            
            EditorGUILayout.HelpBox(tip, MessageType.Info);
            EditorGUILayout.PropertyField(rule, new GUIContent("Custom Rule"));
        }
        
        EditorGUILayout.EndVertical();
        GUI.backgroundColor = Color.white;
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(5);
        // ==================== Color ====================
        GUI.backgroundColor = new Color(1.0f, 0.9f, 0.7f);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Color Control", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(lightMode, new GUIContent("Light Mode"));
        
        
        // 根据 lightMode 显示不同的设置
        if (lightMode.enumValueIndex == 0) // Solid
        {
            EditorGUILayout.LabelField("Solid Mode Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(solidDistributionMode, new GUIContent("Distribution Mode"));
            
            // 根据 solidDistributionMode 显示颜色设置
            if (solidDistributionMode.enumValueIndex == 0) // 假设 0 是单色模式
            {
                EditorGUILayout.PropertyField(solidColor, new GUIContent("Solid Color"));
            }
            else // 随机颜色模式
            {
                EditorGUILayout.PropertyField(randomColors, new GUIContent("Random Colors"));
            }
        }
        else if (lightMode.enumValueIndex == 1) // Gradient
        {
            EditorGUILayout.LabelField("Gradient Mode Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(gradient, new GUIContent("Gradient"));
        }
        else if (lightMode.enumValueIndex == 2) // Wave
        {
            EditorGUILayout.LabelField("Wave Mode Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(gradient, new GUIContent("Gradient"));
            EditorGUILayout.PropertyField(waveSpeed, new GUIContent("Wave Speed"));
        }
        else if (lightMode.enumValueIndex == 3) // Idle
        {
            EditorGUILayout.LabelField("Idle Mode Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(idleLightAnimation, new GUIContent("Idle Light Animation"));
            EditorGUILayout.PropertyField(idleFrequency, new GUIContent("Idle Frequency"));
            EditorGUILayout.PropertyField(idleColorType, new GUIContent("Idle Color Type"));
            
            // 根据 idleColorType 显示颜色设置
            if (idleColorType.enumValueIndex == 0) 
            {
                EditorGUILayout.PropertyField(solidColor, new GUIContent("Solid Color"));
            }
            else 
            {
                EditorGUILayout.PropertyField(gradient, new GUIContent("Gradient"));
            }
        }
        
        EditorGUI.indentLevel--;
        EditorGUILayout.EndVertical();
        GUI.backgroundColor = Color.white;

        // ==================== Tips ====================
        // EditorGUILayout.HelpBox("Tips: You can use negative values for counter-clockwise rotation.", MessageType.Info);
        
        serializedObject.ApplyModifiedProperties();
    }
}

