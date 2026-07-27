using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridCellVisualizer : MonoBehaviour
{

    public Renderer renderer;

    public void CreateCell(Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector2 uv4, JacobianFunction function)
    { 
        Vector3 p1 = function.Transform(uv1);
        Vector3 p2 = function.Transform(uv2);
        Vector3 p3 = function.Transform(uv3);
        Vector3 p4 = function.Transform(uv4);

        Vector2 centerUV = (uv1 + uv2 + uv3 + uv4) / 4f;
        float det = function.Determinant(centerUV);

        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[] { p1, p2, p3, p4 };
        
        // 根据雅可比行列式的符号决定三角形顺序
        // 1. det > 0: 变换保持方向（右手系 → 右手系） 
        // 2. det < 0: 变换翻转方向（右手系 → 左手系） 
        // 3. det = 0: 变换退化（降维）
        if (det >= 0)
        {
            // 正常顺序（逆时针）
            mesh.triangles = new int[] { 0, 3, 2, 2, 1, 0 };
        }
        else
        {
            // 翻转顺序（顺时针 → 逆时针）
            mesh.triangles = new int[] { 0, 1, 2, 2, 3, 0 };
        }

        UpdateColor(det);

        GetComponent<MeshFilter>().mesh = mesh;
    }

    private void UpdateColor(float det)
    {
        float absDet = Mathf.Abs(det);
        
        if (absDet < 0.1f) renderer.material.color = Color.red;
        else if (absDet < 1f) renderer.material.color = Color.Lerp(Color.red, Color.green, absDet);
        else if (absDet < 10f) renderer.material.color = Color.Lerp(Color.green, Color.blue, (absDet - 1f) / 9f);
        else renderer.material.color = Color.blue;
        
        // 可选：用不同颜色标记负det
        if (det < 0)
        {
            renderer.material.color = Color.Lerp(renderer.material.color, Color.magenta, 0.3f);
        }
    }
}
