using GK;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{


    private List<Vector3> poses = new List<Vector3>();
    private List<Vector3> hullPoses = new List<Vector3>();
    private List<GameObject> cubes = new List<GameObject>();

    private List<int>tris= new List<int>();
    private List<Vector3>normals= new List<Vector3>();

    // Start is called before the first frame update
    void Start()
    {
        for (int i = 0; i < 200; i++)
        { 
            Vector3 pos = Random.insideUnitSphere * 5f;
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = pos;
            cube.transform.localScale = Vector3.one * 0.1f;
            cube.transform.SetParent(transform);
            poses.Add(pos);
            cubes.Add(cube);
        }

        ConvexHullCalculator ca = new ConvexHullCalculator();

       
        ca.GenerateHull(poses, false, ref hullPoses, ref tris, ref normals);

        Mesh mesh = new Mesh();
        transform.GetComponent<MeshFilter>().mesh= mesh;
        mesh.SetVertices(hullPoses);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

      
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDrawGizmos()
    {
        foreach (var p in hullPoses)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(p, 0.1f);

        }
    }
}
