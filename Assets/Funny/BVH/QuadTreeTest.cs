using System.Collections;
using System.Collections.Generic;
using UnityEditor.SearchService;
using UnityEngine;
using CameraCullingGPU;
using static BoundaryCheckCS;
using System.Security.Cryptography;

public class QuadTreeTest : MonoBehaviour
{
    [Range(1,1000),Min(1)]
    public int PointNumber = 10;
    public GameObject prefab;
    [SerializeField] List<GameObject> objs = new List<GameObject>();


    [SerializeField] GameObject CameraTrans;
    Rect boundary = new Rect();

    [SerializeField] float boundWidth;
    [SerializeField] float boundHeigh;

    QuadTree quadTree;
    public Rect range = new(0, 0, 1, 1);


    [SerializeField] List<Vector3> pointPos = new List<Vector3>();
    List<Vector3> found = new List<Vector3>();

    GameObject content;

    LineRenderer rangelr = new LineRenderer();
    GameObject rangeObj;


    [SerializeField] List<Vector3> res = new List<Vector3>();



    // Start is called before the first frame update
    void Start()
    {


        boundary.x = -boundWidth * 0.5f; boundary.y = -boundWidth*0.5f; boundary.width = boundWidth; boundary.height = boundHeigh;
        quadTree = new QuadTree(boundary, 10);

        pointPos.Clear();
        objs.Clear();
        found.Clear();
        res.Clear();

        content = new GameObject("content");


        rangeObj = new GameObject("range");
        rangelr = rangeObj.AddComponent<LineRenderer>();
        rangelr.positionCount = 5;
        rangelr.startWidth = rangelr.endWidth = 0.02f;
        rangelr.material = new Material(Shader.Find("Particles/Standard Unlit"));
        rangelr.startColor = rangelr.endColor = Color.cyan;



        for (int i = 0; i < PointNumber; i++)
        {
            Vector3 p = new Vector3(Random.Range(-boundWidth*0.5f, boundWidth*0.5f), Random.Range(-boundHeigh*0.5f,boundHeigh * 0.5f), 0);
            GameObject temp = Instantiate(prefab, p, Quaternion.identity,content.transform);
            quadTree.Insert(p);
            pointPos.Add(p);
            objs.Add(temp);

        }
        quadTree.Show();



    }

    // Update is called once per frame
    void Update()
    {

        Vector3 wordPos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z));
        range.center = wordPos;

        

        Vector3 v1 = new Vector3(range.x, range.y, 0);
        Vector3 v2 = new Vector3(range.x, range.y + range.height, 0);
        Vector3 v3 = new Vector3(range.x + range.width, range.y + range.height, 0);
        Vector3 v4 = new Vector3(range.x + range.width, range.y, 0);
        rangelr.SetPosition(0, v1);
        rangelr.SetPosition(1, v2);
        rangelr.SetPosition(2, v3);
        rangelr.SetPosition(3, v4);
        rangelr.SetPosition(4, v1);




        found.Clear();
        res.Clear();
        quadTree.QueryRange(range, found);
        for (int i = 0; i < found.Count; i++)
        {
            if (found[i] != Vector3.zero)
                res.Add(found[i]);
        }

        foreach (var o in objs)
        {
            o.transform.GetComponent<MeshRenderer>().material.color = Color.white;
            o.transform.localScale = new Vector3(0.05f, .05f, .05f);

            foreach (var p in res)
            {
                if (o.transform.position == p)
                {
                    o.transform.GetComponent<MeshRenderer>().material.color = Color.cyan;
                    o.transform.localScale = new Vector3(0.1f, .1f, .1f);
                }
            }

        }

        
    }

        private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(boundary.center, boundary.size);


        var p = Camera.main.ScreenToWorldPoint(new  Vector3(Input.mousePosition.x, Input.mousePosition.y, 10));
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(p, 0.1f);
    }



}
