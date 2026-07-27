using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(RoadPathManager))]
public class RoadPathManagerEditor : Editor
{
    private RoadPathManager pathManager;
    private bool showControlPointList = true;

    private void OnEnable()
    {
        pathManager = (RoadPathManager)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 绘制原有属性
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("控制点管理", EditorStyles.boldLabel);

        // 控制点操作按钮
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("添加控制点", GUILayout.Height(30)))
        {
            Undo.RecordObject(pathManager, "添加控制点");
            pathManager.CreateControlPoint();
            EditorUtility.SetDirty(pathManager);
        }
        
        GUI.enabled = pathManager.controlPoints != null && pathManager.controlPoints.Count > 0;
        if (GUILayout.Button("删除最后一个控制点", GUILayout.Height(30)))
        {
            DeleteLastControlPoint();
        }
        GUI.enabled = true;
        
        EditorGUILayout.EndHorizontal();

        // 控制点列表折叠面板
        EditorGUILayout.Space(5);
        showControlPointList = EditorGUILayout.Foldout(showControlPointList, "控制点列表", true);
        
        if (showControlPointList && pathManager.controlPoints != null)
        {
            EditorGUI.indentLevel++;
            
            // 显示控制点列表
            for (int i = 0; i < pathManager.controlPoints.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                // 显示控制点引用
                Transform oldPoint = pathManager.controlPoints[i];
                Transform newPoint = (Transform)EditorGUILayout.ObjectField($"点 {i}", oldPoint, typeof(Transform), true);
                
                // 如果引用变化，更新控制点列表
                if (newPoint != oldPoint)
                {
                    Undo.RecordObject(pathManager, "修改控制点");
                    pathManager.controlPoints[i] = newPoint;
                    if (pathManager.autoUpdate)
                    {
                        pathManager.UpdatePath();
                    }
                    EditorUtility.SetDirty(pathManager);
                }
                
                // 删除特定控制点的按钮
                if (GUILayout.Button("删除", GUILayout.Width(60)))
                {
                    DeleteControlPointAt(i);
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUI.indentLevel--;
        }

        // 只在autoUpdate为false时显示"更新路径"按钮
        if (!pathManager.autoUpdate)
        {
            EditorGUILayout.Space(10);
            if (GUILayout.Button("更新路径", GUILayout.Height(25)))
            {
                pathManager.UpdatePath();
                SceneView.RepaintAll();
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DeleteLastControlPoint()
    {
        if (pathManager.controlPoints == null || pathManager.controlPoints.Count == 0)
            return;

        int lastIndex = pathManager.controlPoints.Count - 1;
        DeleteControlPointAt(lastIndex);
    }

    private void DeleteControlPointAt(int index)
    {
        if (pathManager.controlPoints == null || index < 0 || index >= pathManager.controlPoints.Count)
            return;

        Undo.RecordObject(pathManager, "删除控制点");
        
        // 获取要删除的控制点
        Transform pointToDelete = pathManager.controlPoints[index];
        
        // 从列表中移除
        pathManager.controlPoints.RemoveAt(index);
        
        // 如果是一个子物体且没有其他组件，则删除该物体
        if (pointToDelete != null && pointToDelete.parent == pathManager.transform)
        {
            // 检查是否有其他重要组件
            if (!HasImportantComponents(pointToDelete.gameObject))
            {
                Undo.DestroyObjectImmediate(pointToDelete.gameObject);
            }
        }
        
        // 更新路径
        if (pathManager.autoUpdate)
        {
            pathManager.UpdatePath();
        }
        
        EditorUtility.SetDirty(pathManager);
        SceneView.RepaintAll();
    }

    // 检查游戏对象是否有重要组件(除Transform外)
    private bool HasImportantComponents(GameObject obj)
    {
        Component[] components = obj.GetComponents<Component>();
        return components.Length > 1; // 只有Transform组件时长度为1
    }
}
