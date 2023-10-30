using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer),typeof(MeshFilter))]
public class SimpleProceduralMesh : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    
    private void OnEnable()
    {
        var mesh = new Mesh();
        mesh.name = "Procedural Mesh";


        mesh.vertices = new Vector3[] { Vector3.zero, Vector3.right, Vector3.up,
                                        new Vector3(1f,1f,0f)};

        mesh.triangles = new int[] { 0,2,1,1,2,3 };//cw 方向才可见

        //如果没有设定法向量 Unity默认为前向量
        mesh.normals = new Vector3[] {Vector3.back,Vector3.back,Vector3.back ,
                                       Vector3.back};

        //可以最多设定8组UV在一个顶点上 但是只能通过方向访问
        mesh.uv = new Vector2[] { Vector2.zero, Vector2.right, Vector2.up,
                                  Vector2.one};

        //默认URP不支持顶点色
        //mesh.colors = new Color[] { Color.blue, Color.cyan, Color.red };

        //切线方向是一个Vector4 默认是（1，0，0，1）向右，但是Unity需要设置为-1 才正确
        mesh.tangents = new Vector4[] {
                                       new Vector4(1,0,0,-1),
                                       new Vector4(1,0,0,-1),
                                       new Vector4(1,0,0,-1),
                                       new Vector4(1,0,0,-1),
                                       
        };
        GetComponent<MeshFilter>().mesh = mesh;

    }
}
