using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SetStencilBeMaskV3))]
public class SetStencilBeMaskV3Editor : Editor
{
    // 第一个Pass的属性
    SerializedProperty _stencilMask;
    SerializedProperty _stencilCompFunction;
    SerializedProperty _stencilPassOperation;
    SerializedProperty _stencilFailOperation;
    SerializedProperty _stencilZFailOperation;

    //// 第二个Pass的启用开关和属性
    //SerializedProperty _enableSecondPassStencil;
    //SerializedProperty _stencilMask2;
    //SerializedProperty _stencilCompFunction2;
    //SerializedProperty _stencilPassOperation2;
    //SerializedProperty _stencilFailOperation2;
    //SerializedProperty _stencilZFailOperation2;

    // UI分组折叠状态
    private bool showFirstPassSettings = true;

    void OnEnable()
    {
        // 第一个Pass的属性
        _stencilMask = serializedObject.FindProperty("_StencilMask");
        _stencilCompFunction = serializedObject.FindProperty("_StencilCompFunction");
        _stencilPassOperation = serializedObject.FindProperty("_StencilPassOperation");
        _stencilFailOperation = serializedObject.FindProperty("_StencilFailOperation");
        _stencilZFailOperation = serializedObject.FindProperty("_StencilZFailOperation");

        //// 第二个Pass的启用开关和属性
        //_enableSecondPassStencil = serializedObject.FindProperty("_EnableSecondPassStencil");
        //_stencilMask2 = serializedObject.FindProperty("_StencilMask2");
        //_stencilCompFunction2 = serializedObject.FindProperty("_StencilCompFunction2");
        //_stencilPassOperation2 = serializedObject.FindProperty("_StencilPassOperation2");
        //_stencilFailOperation2 = serializedObject.FindProperty("_StencilFailOperation2");
        //_stencilZFailOperation2 = serializedObject.FindProperty("_StencilZFailOperation2");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        SetStencilBeMaskV3 script = (SetStencilBeMaskV3)target;
        
        // 第一个Pass的模板设置
        EditorGUILayout.Space(5);
        DrawSeparator(Color.gray);
        
        showFirstPassSettings = EditorGUILayout.Foldout(showFirstPassSettings, "第一个Pass模板设置", true, EditorStyles.foldoutHeader);
        
        if (showFirstPassSettings)
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(_stencilMask, new GUIContent("模板值"));
            EditorGUILayout.PropertyField(_stencilCompFunction, new GUIContent("模板比较函数"));
            EditorGUILayout.PropertyField(_stencilPassOperation, new GUIContent("通过操作"));
            EditorGUILayout.PropertyField(_stencilFailOperation, new GUIContent("失败操作"));
            EditorGUILayout.PropertyField(_stencilZFailOperation, new GUIContent("深度失败操作"));

            EditorGUILayout.Space();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("应用第一Pass设置", GUILayout.Height(25)))
            {
                script.ApplyFirstPassSettings();
                EditorUtility.SetDirty(target);
            }
            
            if (GUILayout.Button("重置第一Pass设置", GUILayout.Height(25)))
            {
                script.ResetFirstPassSettings();
                EditorUtility.SetDirty(target);
                serializedObject.Update();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.indentLevel--;
        }
        
        //// 第二个Pass的模板设置
        //EditorGUILayout.Space(5);
        //DrawSeparator(Color.gray);
        
        //// 只显示第二个Pass的启用开关
        //EditorGUILayout.PropertyField(_enableSecondPassStencil, new GUIContent("启用第二个Pass模板设置"));
        
        //// 只有在启用第二个Pass时才显示设置
        //if (_enableSecondPassStencil.boolValue)
        //{
        //    EditorGUI.indentLevel++;
            
        //    EditorGUILayout.PropertyField(_stencilMask2, new GUIContent("模板值"));
        //    EditorGUILayout.PropertyField(_stencilCompFunction2, new GUIContent("模板比较函数"));
        //    EditorGUILayout.PropertyField(_stencilPassOperation2, new GUIContent("通过操作"));
        //    EditorGUILayout.PropertyField(_stencilFailOperation2, new GUIContent("失败操作"));
        //    EditorGUILayout.PropertyField(_stencilZFailOperation2, new GUIContent("深度失败操作"));

        //    EditorGUILayout.Space();
            
        //    EditorGUILayout.BeginHorizontal();
        //    if (GUILayout.Button("应用第二Pass设置", GUILayout.Height(25)))
        //    {
        //        script.ApplySecondPassSettings();
        //        EditorUtility.SetDirty(target);
        //    }
            
        //    if (GUILayout.Button("重置第二Pass设置", GUILayout.Height(25)))
        //    {
        //        script.ResetSecondPassSettings();
        //        EditorUtility.SetDirty(target);
        //        serializedObject.Update();
        //    }
        //    EditorGUILayout.EndHorizontal();
            
        //    EditorGUI.indentLevel--;
        //}

        serializedObject.ApplyModifiedProperties();
    }
    
    // 绘制分隔线
    private void DrawSeparator(Color color)
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, color);
    }
}
