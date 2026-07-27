using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridPoint : MonoBehaviour
{
    public Vector2 uvCoord;
    public Renderer renderer;
    public TextMesh textMesh;


    public Transform uArrow;
    public Transform vArrow;

    public void UpdateVisualization(JacobianFunction function, bool showVectors)
    { 
        Vector2 xy = function.Transform(uvCoord);
        transform.position = new Vector3(xy.x, 0, xy.y);

        Matrix4x4 J = function.GetjacobianMatrix(uvCoord);
        float det = function.Determinant(uvCoord);

        UpdateColor(det);

        textMesh.text = $"det: {det:F2}";

        uArrow.gameObject.SetActive(showVectors);
        vArrow.gameObject.SetActive(showVectors);

        if (showVectors)
        {
            UpdateBasicVectors(J);
        }
    }

    private void UpdateBasicVectors(Matrix4x4 j)
    {
        // u方向的雅可比向量 (∂x/∂u, ∂y/∂u)
        Vector3 uDir = new Vector3(j.m00, 0, j.m10);
        float uMagnitude = uDir.magnitude;
        
        if (uMagnitude > 0.001f)
        {
            uArrow.forward = uDir.normalized;
            uArrow.localScale = new Vector3(1, 1, uMagnitude);
        }

        // v方向的雅可比向量 (∂x/∂v, ∂y/∂v)
        Vector3 vDir = new Vector3(j.m01, 0, j.m11);
        float vMagnitude = vDir.magnitude;
        
        if (vMagnitude > 0.001f)
        {
            vArrow.forward = vDir.normalized;
            vArrow.localScale = new Vector3(1, 1, vMagnitude);
        }
    }

    private void UpdateColor(float det)
    {
        if (det < 0.1f) renderer.material.color = Color.red;
        else if (det < 1f) renderer.material.color = Color.Lerp(Color.red, Color.green, det);
        else if (det <10f) renderer.material.color = Color.Lerp(Color.green, Color.blue, (det - 1f) / 9f);
        else renderer.material.color = Color.blue;
    }


  
}
