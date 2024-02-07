using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class BoundingBox2DIntersection : MonoBehaviour
{
    public Transform rayPoint;
    
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
        if (rayPoint == null) return;


        Gizmos.color = Color.yellow;
        Rect rect = new Rect(Vector2.zero, new Vector2(2, 1));
        Gizmos.DrawWireCube(rect.center,rect.size);

        Gizmos.color = Color.green;
        Ray ray = new Ray(rayPoint.transform.position, rayPoint.transform.right);
        Gizmos.DrawRay(ray);

        float t = 0f;
        if (RayIntersectionRect(ray, rect,out t ))
        {
            Gizmos.color = Color.green;
            Vector3 intersectionPoint = ray.origin + ray.direction * t;
            Gizmos.DrawSphere(intersectionPoint, 0.05f);
        }
    }


    bool RayIntersectionRect(Ray r,Rect rect ,out float t)
    {


        float tminX = (rect.min.x - r.origin.x) / r.direction.x;
        float tmaxX = (rect.max.x - r.origin.x) / r.direction.x;
        float tminY = (rect.min.y - r.origin.y) / r.direction.y;
        float tmaxY = (rect.max.y - r.origin.y) / r.direction.y;

        float tmin = Mathf.Max(tminX, tminY);
        float tmax = Mathf.Min(tmaxX, tmaxY);

        t = tmin;

        return tmin < tmax ? true : false;
    }
}
