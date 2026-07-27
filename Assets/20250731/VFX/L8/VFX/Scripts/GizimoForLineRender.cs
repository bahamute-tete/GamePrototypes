using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class GizimoForLineRender : MonoBehaviour
{

    public List<GameObject> objectPoints = new List<GameObject>(); // 新增：存储游戏对象
    public Color gizmoColor = Color.red;
    public float sphereRadius = 0.1f; // 点位置显示的球体半径
    public bool showSpheres = true; // 是否显示点位置
    public bool showLines = true; // 是否显示连线
    public bool showLocalSpace = false; // 是否在局部空间中显示



    
    // 新增：添加游戏对象作为点
    public void AddObject(GameObject obj)
    {
        if (objectPoints == null)
            objectPoints = new List<GameObject>();
            
        if (obj != null)
            objectPoints.Add(obj);
    }
    
    // 新增：获取所有物体的位置
    private List<Vector3> GetObjectPositions()
    {
        List<Vector3> positions = new List<Vector3>();
        
        foreach (GameObject obj in objectPoints)
        {
            if (obj != null)
            {
                positions.Add(obj.transform.position);
            }
        }
        
        return positions;
    }


    // 新增：清除对象点
    public void ClearObjects()
    {
        if (objectPoints != null)
            objectPoints.Clear();
    }

    private void OnDrawGizmos() 
    {
        if (objectPoints == null || objectPoints.Count == 0)
            return;
            
        Gizmos.color = gizmoColor;
        
        List<Vector3> drawPoints = GetObjectPositions();
        
        // 显示连接线
        if (showLines && drawPoints.Count > 1)
        {
            for (int i = 0; i < drawPoints.Count - 1; i++)
            {
                Vector3 startPoint = showLocalSpace ? transform.TransformPoint(drawPoints[i]) : drawPoints[i];
                Vector3 endPoint = showLocalSpace ? transform.TransformPoint(drawPoints[i + 1]) : drawPoints[i + 1];
                Gizmos.DrawLine(startPoint, endPoint);
            }
        }
        
        // 显示点位置
        if (showSpheres)
        {
            for (int i = 0; i < drawPoints.Count; i++)
            {
                Vector3 point = showLocalSpace ? transform.TransformPoint(drawPoints[i]) : drawPoints[i];
                Gizmos.DrawSphere(point, sphereRadius);
            }
        }
    }
    

    public void DrawDebugLines()
    {
        List<Vector3> drawPoints = GetObjectPositions();
        
        if (drawPoints == null || drawPoints.Count < 2)
            return;
            
        for (int i = 0; i < drawPoints.Count - 1; i++)
        {
            Vector3 startPoint = showLocalSpace ? transform.TransformPoint(drawPoints[i]) : drawPoints[i];
            Vector3 endPoint = showLocalSpace ? transform.TransformPoint(drawPoints[i + 1]) : drawPoints[i + 1];
            Debug.DrawLine(startPoint, endPoint, gizmoColor);
        }
    }
}
