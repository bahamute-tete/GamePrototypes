using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class RayBoxIntersectionCheck : MonoBehaviour
{

    public Vector3 startPos = new Vector3(0, 0, -5);
    public Vector3 rayDir = new Vector3(-1, 1, 2);
    public float distance = 1f;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireCube(new Vector3(0, 0, 0), new Vector3(4, 2, 2));

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(startPos, 0.1f);
        Gizmos.DrawLine(startPos, startPos+ Vector3.Normalize(rayDir) * distance);

        Vector3 rd = Vector3.Normalize(rayDir);
        Vector3 ro = startPos;
        Vector3 boxMin = new Vector3(-2.0f, -1.0f, -1f);
        Vector3 boxMax = new Vector3(2.0f, 1.0f,1f);


        Vector2 res = RayBoxDst(boxMin, boxMax, ro, rd);

        Vector3 nearDis = ro + rd * res.x;
        Vector3 farDis = ro+ rd * (res.y+res.x);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(nearDis, 0.1f);
        Gizmos.DrawLine(ro, nearDis);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(farDis, 0.1f);
        Gizmos.DrawLine(ro, farDis);






    }

    Vector2 RayBoxDst(Vector3 boxMin, Vector3 boxMax, Vector3 pos, Vector3 rayDir)
    {

        Vector3 p1 = boxMin - pos;
        Vector3 p2 = boxMax - pos;


        Vector3 t0 =new  Vector3 (p1.x / rayDir.x , p1.y / rayDir.y, p1.z / rayDir.z);
        Vector3 t1 = new Vector3(p2.x / rayDir.x, p2.y / rayDir.y, p2.z / rayDir.z);

        Vector3 tmin =Vector3.Min(t0, t1);
        Vector3 tmax = Vector3.Max(t0, t1);

        //射线到box两个相交点的距离, dstA最近距离， dstB最远距离
        float dstA = Mathf.Max(Mathf.Max(tmin.x, tmin.y), tmin.z);
        float dstB = Mathf.Min(Mathf.Min(tmax.x, tmax.y), tmax.z);

        float dstToBox = Mathf.Max(0, dstA);
        float dstInBox = Mathf.Max(0, dstB - dstToBox);

        // x 到包围盒最近的距离， y 穿过包围盒的距离
        return new Vector2(dstToBox, dstInBox);
    }
}
