using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class NormalTools : MonoBehaviour
{

    public float lineLength = 1.0f;
    private float _lineLenCache = 0.01f;
    private Mesh _mesh;
    private Vector3[] _normalCache;


    struct NormalLine
    {
        public Vector3 posF;
        public Vector3 posT;
    }

    private List<NormalLine> normalLines ;


    void CalculateNormalLine()
    {
        normalLines.Clear();

        if (_mesh != null)
        {
            for (int i = 0; i < _mesh.normals.Length; i++)
            {
                var  normalLine = new NormalLine();
                var mat = transform.localToWorldMatrix;
                normalLine.posF = mat.MultiplyPoint(_mesh.vertices[i]);
                normalLine.posT = mat.MultiplyPoint(_mesh.vertices[i] + _mesh.normals[i] * lineLength);
                normalLines.Add(normalLine);

            }

            _lineLenCache = lineLength;
            _normalCache = _mesh.normals;
        }
    }


    private void OnEnable()
    {
        normalLines = new List<NormalLine>();
        if (TryGetComponent<MeshFilter>(out MeshFilter filter))
        {
            _mesh = filter.sharedMesh;
        }
        else
        {
            _mesh = GetComponent<SkinnedMeshRenderer>().sharedMesh;
        }

        CalculateNormalLine();
    }
    // Start is called before the first frame update
    void Start()
    {
        for (int i = 0; i < _mesh.triangles.Length; i+= 3)
        {
            Debug.Log(_mesh.triangles[i]);
            Debug.Log(_mesh.triangles[i+1]);
            Debug.Log(_mesh.triangles[i+2]);
        }
    }

    // Update is called once per frame
    void Update()
    {
        //if (Mathf.Abs(lineLength - _lineLenCache) > 0 || _normalCache != _mesh.normals)
        //{
        //    CalculateNormalLine();
        //}
    }

    private void OnDisable()
    {
        _mesh = null;
        normalLines = null;
    }

    private Dictionary<Vector3, List<Vector3>> CreatsurfaceNormal(Mesh mesh)
    {
        Dictionary<Vector3, List<Vector3>> surfaceNormalDic = new Dictionary<Vector3, List<Vector3>>();

        for (int i = 0; i < mesh.triangles.Length-3; i+=3)
        {
            Vector3 a = mesh.vertices[mesh.triangles[i + 1]] - mesh.vertices[mesh.triangles[i]];

            Vector3 b = mesh.vertices[mesh.triangles[i + 2]] - mesh.vertices[mesh.triangles[i]];

            Vector3 normal = Vector3.Cross(a, b);

            for (int j = 0; j < 3; j++)
            {
                int tri = mesh.triangles[i + j];
                if (!surfaceNormalDic.ContainsKey(mesh.vertices[tri]))
                {
                    List<Vector3> noramls = new List<Vector3>();
                    surfaceNormalDic.Add(mesh.vertices[tri], noramls);
                }

                bool containsNormal = false;

                for (int k = 0; k < surfaceNormalDic[mesh.vertices[tri]].Count; k++)
                {
                    if (surfaceNormalDic[mesh.vertices[tri]][k].normalized.Equals(normal.normalized))
                    {
                        surfaceNormalDic[mesh.vertices[tri]][k] += normal;
                        containsNormal = true;
                        break;
                    }
                }

                if (!containsNormal)
                {
                    surfaceNormalDic[mesh.vertices[tri]].Add(normal);
                }
            }


        }
            return surfaceNormalDic;
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;


        if (_mesh != null)
        {
            //for (int i = 0; i < _mesh.normals.Length; i++)
            //{
            //    var normalLine = new NormalLine();
            //    var mat = transform.localToWorldMatrix;
            //    normalLine.posF = mat.MultiplyPoint(_mesh.vertices[i]);
            //    normalLine.posT = mat.MultiplyPoint(_mesh.vertices[i] + _mesh.normals[i] * lineLength);
            //   // normalLines.Add(normalLine);

            //    Gizmos.DrawLine(normalLine.posF, normalLine.posT);

            //}


            foreach (var normalLine in normalLines)
            {
                Gizmos.DrawLine(normalLine.posF, normalLine.posT);
            }
        }
    }
}
