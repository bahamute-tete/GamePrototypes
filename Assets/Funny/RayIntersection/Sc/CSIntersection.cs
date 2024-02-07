using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using System.Linq;
using UnityEditor.PackageManager;

public class CSIntersection : MonoBehaviour
{

    public ComputeShader computeShader;
    ComputeBuffer hitInfoBuffer, trianglesBuffer;
    ComputeBuffer hitIndexBuffer;

    public Transform raySource;
    public GameObject SurfaceObject;
    public GameObject pointPrefab;
    private GameObject pointObj;
    private Mesh mesh;

    Vector3[] vertices, normals;
    int[] tris;

    List<GameObject> cubes = new List<GameObject>();
    Vector3[] poses;


    struct Triangle
    {
        public Vector3 vertex0;
        public Vector3 normal0;

        public Vector3 vertex1;
        public Vector3 normal1;

        public Vector3 vertex2;
        public Vector3 normal2;
    }

    Triangle[] triangles;


    struct HitInfo
    {
        public Vector3 hitPos;
        public float t;
        public Vector3 normal;
        public float beta;
        public float gama;
    }
    HitInfo[] hitInfo;

    int[] hitedIndex;

    int kernel;
    Ray r;

    private Vector3 hitPos;
    private Vector3 hitNormal = Vector3.forward;

    private void Awake()
    {
        if (SurfaceObject != null)
            mesh = SurfaceObject.GetComponent<MeshFilter>().sharedMesh;



        if (raySource != null)
        {
            r = new Ray(raySource.transform.position, raySource.transform.forward);
            hitPos = r.origin;
        }

        if (pointPrefab != null)
            pointObj = Instantiate(pointPrefab);


    }
    // Start is called before the first frame update
    void Start()
    {

       

        kernel = computeShader.FindKernel("CSMain");

        if (mesh != null)
        {

            vertices = new Vector3[mesh.vertexCount];
            normals = new Vector3[mesh.normals.Length];

            vertices = mesh.vertices;
            tris = mesh.triangles;
            normals = mesh.normals;

            triangles = new Triangle[tris.Length / 3];
            hitInfo = new HitInfo[tris.Length / 3];
            hitedIndex = new int[tris.Length / 3];

            hitInfoBuffer = new ComputeBuffer(tris.Length / 3, 4*9);
            trianglesBuffer = new ComputeBuffer(tris.Length / 3, 12 * 3 *2);
            hitIndexBuffer = new ComputeBuffer(tris.Length / 3, sizeof(float));

            for (int i = 0, a = 0; i < tris.Length; i += 3, a++)
            {
                int a0 = tris[i];
                int a1 = tris[i + 1];
                int a2 = tris[i + 2];

                Triangle triangle = new Triangle();
                triangle.vertex0 = vertices[a0];
                triangle.vertex1 = vertices[a1];
                triangle.vertex2 = vertices[a2];

                triangle.normal0 = normals[a0];
                triangle.normal1 = normals[a1];
                triangle.normal2 = normals[a2];

                triangles[a] = triangle;

                hitedIndex[a] = -1;
            }


            trianglesBuffer.SetData(triangles);

            hitIndexBuffer.SetData(hitedIndex);


            computeShader.SetVector("ro", r.origin);
            computeShader.SetVector("rd", r.direction);
            computeShader.SetMatrix("localToWorld", SurfaceObject.transform.localToWorldMatrix);
            computeShader.SetMatrix("worldToLocal", SurfaceObject.transform.worldToLocalMatrix);


            computeShader.SetBuffer(kernel, "Triangles", trianglesBuffer);


            computeShader.SetBuffer(kernel, "hitInfo", hitInfoBuffer);




            computeShader.Dispatch(kernel, Mathf.CeilToInt(trianglesBuffer.count / 64.0f), 1, 1);


            //hitInfoBuffer.GetData(hitInfo);
            //hitIndexBuffer.GetData(hitedIndex);


            //var info = hitInfo.OrderByDescending(x => x.t);

            //foreach (var h in info)
            //{
            //    Debug.Log(h.t);
            //}



            //pointObj.transform.position = (r.origin + r.direction * info.t);
            //pointObj.transform.forward = info.normal;
        }


    }



    // Update is called once per frame
    void Update()
    {
        if (mesh == null && pointObj == null) return;

        r = new Ray(raySource.transform.position, raySource.transform.forward);
        computeShader.SetVector("ro", r.origin);
        computeShader.SetVector("rd", r.direction);


        for (int i = 0; i < hitIndexBuffer.count; i++)
        {
            hitedIndex[i] = -1;
        }

        computeShader.SetBuffer(kernel, "hitInfo", hitInfoBuffer);

        computeShader.SetMatrix("localToWorld", SurfaceObject.transform.localToWorldMatrix);
        computeShader.SetMatrix("worldToLocal", SurfaceObject.transform.worldToLocalMatrix);

        computeShader.SetBuffer(kernel, "hitedIndex", hitIndexBuffer);


        computeShader.Dispatch(kernel, Mathf.CeilToInt(trianglesBuffer.count / 64.0f), 1, 1);



        hitInfoBuffer.GetData(hitInfo);
        hitIndexBuffer.GetData(hitedIndex);



        var info = hitInfo.OrderByDescending(x => x.t).First();

        hitPos = r.origin + r.direction * info.t;
        hitNormal = info.normal;


        pointObj.transform.position = hitPos;
        pointObj.transform.forward = hitNormal;





    }

    private void OnAssemblyReload()
    {
        BufferRelease();
    }
    private void OnEnable()
    {
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnAssemblyReload;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnAssemblyReload;
#endif
        BufferRelease();
    }

    private void OnDestroy()
    {
        BufferRelease();
        trianglesBuffer = null;
        hitInfoBuffer = null;
        hitInfoBuffer = null;
    }

    void BufferRelease()
    {

        trianglesBuffer.Dispose();
        hitInfoBuffer.Dispose();
        hitIndexBuffer.Dispose();
        Debug.Log("releaseBuffer");
    }


    private void OnDrawGizmos()
    {
        if (SurfaceObject != null && raySource != null && pointObj!=null)
        {

            Gizmos.color = Color.red;
            Gizmos.DrawLine(raySource.transform.position, hitPos);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(hitPos, hitPos + hitNormal * 0.1f);
            Gizmos.matrix = Matrix4x4.TRS(hitPos,pointObj.transform.rotation,Vector3.one*0.1f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(1f, 1f, 1f));

        }
    }

}
