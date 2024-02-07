using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;


struct HitRecord
{
    public bool isHited;
    public float t;
    public Vector3 hitPos;
    public float gama;
    public float beta;
}

struct Triangle
{
    public Vector3 vertex0;
    public Vector3 vertex1;
    public Vector3 vertex2;

    public void SetTriangle(Vector3 vertex0, Vector3 vertex1, Vector3 vertex2)
    {
        this.vertex0 = vertex0;
        this.vertex1 = vertex1;
        this.vertex2 = vertex2;
    }

    public Vector3 GetNormal() 
    {
        Vector3 ab = (vertex1 - vertex0).normalized;
        Vector3 ac = (vertex2 - vertex0).normalized;
        Vector3 n = Vector3.Cross(ac, ab);
        return n;
    }
}
struct Surface
{
    public Vector3[] normals;

    public Vector3[] vertices;

    public int[] triangles;

    public void Initialized(Vector3[] vertices,Vector3[] normals, int[] triangles )
    {
        this.normals = normals;
        this.vertices = vertices;
        this.triangles = triangles;
    }
    public Vector3[] GetFaceNormals()
    {
        Vector3[] faceNormals = new Vector3[triangles.Length/3];

        for (int j = 0; j < triangles.Length; j += 3)
        {
            int a0 = triangles[j];
            int a1 = triangles[j + 1];
            int a2 = triangles[j + 2];

            Vector3 n0 = normals[a0];
            Vector3 n1 = normals[a1];
            Vector3 n2 = normals[a2];

            Vector3 faceNormal = (n0 + n1 + n2).normalized;
            faceNormals[j/3] = faceNormal;
        }

        return faceNormals;
    }

    public HitRecord Hit(Ray r, Triangle tri, float tmin=0.0f, float tmax =100.0f)
    { 
        HitRecord hitRecord = new HitRecord();
        float t = 0;



        float3x3 A = new float3x3(tri.vertex0.x - tri.vertex1.x, tri.vertex0.x - tri.vertex2.x, r.direction.x,
                                   tri.vertex0.y - tri.vertex1.y, tri.vertex0.y - tri.vertex2.y, r.direction.y,
                                   tri.vertex0.z - tri.vertex1.z, tri.vertex0.z - tri.vertex2.z, r.direction.z);

        float3x3 BETA = new float3x3(tri.vertex0.x - r.origin.x, tri.vertex0.x - tri.vertex2.x, r.direction.x,
                                      tri.vertex0.y - r.origin.y, tri.vertex0.y - tri.vertex2.y, r.direction.y,
                                      tri.vertex0.z - r.origin.z, tri.vertex0.z - tri.vertex2.z, r.direction.z
                                     );

        float3x3 GAMA = new float3x3(tri.vertex0.x - tri.vertex1.x, tri.vertex0.x - r.origin.x, r.direction.x,
                                      tri.vertex0.y - tri.vertex1.y, tri.vertex0.y - r.origin.y, r.direction.y,
                                      tri.vertex0.z - tri.vertex1.z, tri.vertex0.z - r.origin.z, r.direction.z
                                     );

        float3x3 T = new float3x3(tri.vertex0.x - tri.vertex1.x, tri.vertex0.x - tri.vertex2.x, tri.vertex0.x - r.origin.x,
                                  tri.vertex0.y - tri.vertex1.y, tri.vertex0.y - tri.vertex2.y, tri.vertex0.y - r.origin.y,
                                  tri.vertex0.z - tri.vertex1.z, tri.vertex0.z - tri.vertex2.z, tri.vertex0.z - r.origin.z
                                  );


        t =(determinant(A)==0)? 0.0f: determinant(T) / determinant(A);

        Vector3 faceNormal = tri.GetNormal();

        hitRecord.isHited = (Vector3.Dot(r.direction, faceNormal) > 0.0f) ? true : false;

        if (t < tmin || t > tmax)
        {
            hitRecord.isHited =false; 
        }

        float gama = determinant(GAMA) / determinant(A);
        if (gama < 0.0 || gama > 1.0f)
        {
            hitRecord.isHited = false;
        }
           

        float beta = determinant(BETA) / determinant(A);
        if (beta < 0.0f || beta > 1.0f- gama)
        {
            hitRecord.isHited = false;
        }


        if (hitRecord.isHited)
        {
            hitRecord.t = t;
            hitRecord.gama = gama;
            hitRecord.beta = beta;
            hitRecord.hitPos = r.origin + r.direction * t;
        }
        else
        {
            hitRecord.t = 0;
            hitRecord.gama = 0f;
            hitRecord.beta = 0f;
            hitRecord.hitPos = r.origin;
        }

        return hitRecord;
    }

   


}


[ExecuteInEditMode]
public class SphereRayIntersection : MonoBehaviour
{

    public GameObject SurfaceObject;
    [Range(0.01f,0.1f),Min(0.01f)]
    public float normalLength = 0.1f;

    public Transform raySource;

    private Mesh mesh;

 
    private Vector3 ro;
    private Vector3 rd;
    // Start is called before the first frame update

    Surface surface = new Surface();



    ComputeShader computeShader;
    ComputeBuffer buffer;


    private void Initialized()
    {
        

        if (raySource != null)
        { 
            ro = raySource.transform.position;
            rd = raySource.transform.forward;
        }

        if (SurfaceObject != null)
        {

            mesh = SurfaceObject.GetComponent<MeshFilter>().sharedMesh;
            if (mesh != null)
            {

                Vector3[] vertices = new Vector3[mesh.vertexCount];
                Vector3[] normals = new Vector3[mesh.normals.Length];
                int[]  tris = new int[mesh.triangles.Length];
              
                vertices = mesh.vertices;
                normals = mesh.normals;
                tris = mesh.triangles;

                surface.Initialized(vertices, normals, tris);
            }
        }
    }
    private void OnEnable()
    {
        Initialized();

    }
    private void Awake()
    {
      
    }

    

    Vector2 SphereIntersectionFunction(Vector3 ro, Vector3 rd, Vector3 center, float r)
    {
        Vector3 oc = ro - center;
        float b =Vector3.Dot(oc, rd);
        float c = Vector3.Dot(oc, oc) - r * r;
        float h = b * b - c;
        if (h < 0.0) return Vector2.zero; // no intersection
        h = Mathf.Sqrt(h);
        return new  Vector2(-b - h, -b + h);
    }

    float PlaneIntersectionFunction(Vector3 ro, Vector3 rd, Vector3 n,float d)
    {
        float t = -(d + Vector3.Dot(n, ro)) / (Vector3.Dot(n,rd));
        return t;
    }
    void Start()
    {
        buffer = new ComputeBuffer(surface.triangles.Length , 4);

        buffer.SetData(surface.triangles);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDrawGizmos()
    {
        Initialized();

        if (surface.triangles.Length>0 && SurfaceObject != null)
        {

            for (int j = 0; j < surface.triangles.Length; j += 3)
            {
                int a0 = surface.triangles[j];
                int a1 = surface.triangles[j+1];
                int a2 = surface.triangles[j+2];

                Vector3 p0 = surface.vertices[a0];
                Vector3 n0 = surface.normals[a0];

                p0 = SurfaceObject.transform.TransformPoint(p0);
                n0 = SurfaceObject.transform.TransformDirection(n0);


                Vector3 p1 = surface.vertices[a1];
                Vector3 n1 = surface.normals[a1];

                p1 = SurfaceObject.transform.TransformPoint(p1);
                n1 = SurfaceObject.transform.TransformDirection(n1);

                Vector3 p2 = surface.vertices[a2];
                Vector3 n2 = surface.normals[a2];

                p2 = SurfaceObject.transform.TransformPoint(p2);
                n2 = SurfaceObject.transform.TransformDirection(n2);

                Gizmos.color = Color.gray;
                Gizmos.DrawLine(p0, p1);
                Gizmos.DrawLine(p1, p2);
                Gizmos.DrawLine(p2, p0);

                Triangle triangle = new Triangle();
                triangle.SetTriangle(p0, p1, p2);

                Vector3[] triangles = { p0, p1, p2 };
                Vector3[] normals = { n0, n1, n2 };



                HitRecord hit = surface.Hit(new Ray(ro, rd), triangle);
                
                Vector3 faceNormal = Vector3.zero;
                for (int k = 0; k < triangles.Length; k++)
                {
                    Gizmos.color = Color.black;
                    Gizmos.DrawSphere(triangles[k], 0.01f);

                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(triangles[k], triangles[k] + normals[k] * normalLength);

                    //faceNormal += normals[k];
                    //faceNormal = faceNormal.normalized;
                }

                if (hit.isHited)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(hit.hitPos, 0.05f);
                    Gizmos.DrawRay(new Ray(ro, rd));

                    faceNormal = ((1.0f - hit.beta - hit.gama) * normals[0] + hit.beta * normals[1] + hit.gama * normals[2]).normalized;
                    Gizmos.DrawLine(hit.hitPos, hit.hitPos+faceNormal* normalLength*2);
                }
            }



        }

        
    }
}
