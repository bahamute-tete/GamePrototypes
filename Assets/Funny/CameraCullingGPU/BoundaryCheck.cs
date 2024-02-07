using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoundaryCheck : MonoBehaviour
{

    Camera camera => GetComponent<Camera>();

    public GameObject prefab;
    public Vector2 size =Vector2.one;
    private List<GameObject> gameObjects = new List<GameObject>();
    // Start is called before the first frame update
    void Start()
    {
        for (int i = 0; i <size.y; i++)
        {
            for (int j = 0; j < size.x; j++)
            {
                GameObject temp = Instantiate(prefab);
                temp.transform.position =new  Vector3(j-size.x/2, i-size.y/2, 10.0f);
                temp.transform.rotation = Quaternion.identity;
                gameObjects.Add(temp);
            }
        }



    }

    // Update is called once per frame
    void Update()
    {
        if (camera == null) return;

        CameraFrusturmCullingUnityMethod(camera, gameObjects.ToArray());



    }


    void CameraFrusturmCulling(Camera camera, GameObject[] gameObjects)
    {
        float a = 1.0f / camera.aspect;//heigh divid width
        float e = 1.0f / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);//horizontal FOV
        float near = camera.nearClipPlane;
        float far = camera.farClipPlane;

        Vector4 _Nnear = new Vector4(0.0f, 0.0f, -1.0f, -near);
        Vector4 _Nfar = new Vector4(0.0f, 0.0f, 1.0f, far);
        Vector4 _Nleft = new Vector4(e / Mathf.Sqrt(e * e + 1), 0.0f, -1.0f / Mathf.Sqrt(e * e + 1), 0.0f);
        Vector4 _Nright = new Vector4(-e / Mathf.Sqrt(e * e + 1), 0.0f, -1.0f / Mathf.Sqrt(e * e + 1), 0.0f);
        Vector4 _Nbottom = new Vector4(0.0f, e / Mathf.Sqrt(e * e + a * a), -a / Mathf.Sqrt(e * e + a * a), 0.0f);
        Vector4 _Ntop = new Vector4(0.0f, -e / Mathf.Sqrt(e * e + a * a), -a / Mathf.Sqrt(e * e + a * a), 0.0f);

        Vector4[] cameraPlane = new Vector4[6] { _Nleft, _Nright, _Ntop, _Nbottom, _Nnear, _Nfar };



        foreach (GameObject go in gameObjects) 
        {
            float r = go.transform.localScale.x * 0.5f;
            Vector3 positonWS = go.transform.position;
            Vector4 positionVS = camera.worldToCameraMatrix * new Vector4(positonWS.x, positonWS.y, positonWS.z, 1.0f);

            go.SetActive(true);
            for (int i = 0; i < cameraPlane.Length; i++)
            {
                float res = Vector4.Dot(positionVS, cameraPlane[i]);

                if (res <= -r)
                {
                    go.SetActive(false);
                }
            }

        }  
    }


    void CameraFrusturmCullingUnityMethod(Camera camera, GameObject[] gameObjects)
    {
        


        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);//Ordering: [0] = Left, [1] = Right, [2] = Down, [3] = Up, [4] = Near, [5] = Far

        Vector4 left = new Vector4(frustumPlanes[0].normal.x, frustumPlanes[0].normal.y, frustumPlanes[0].normal.z, frustumPlanes[0].distance);
        Vector4 right = new Vector4(frustumPlanes[1].normal.x, frustumPlanes[1].normal.y, frustumPlanes[1].normal.z, frustumPlanes[1].distance);
        Vector4 down = new Vector4(frustumPlanes[2].normal.x, frustumPlanes[2].normal.y, frustumPlanes[2].normal.z, frustumPlanes[2].distance);
        Vector4 up = new Vector4(frustumPlanes[3].normal.x, frustumPlanes[3].normal.y, frustumPlanes[3].normal.z, frustumPlanes[3].distance);
        Vector4 nearP = new Vector4(frustumPlanes[4].normal.x, frustumPlanes[4].normal.y, frustumPlanes[4].normal.z, frustumPlanes[4].distance);
        Vector4 farP = new Vector4(frustumPlanes[5].normal.x, frustumPlanes[5].normal.y, frustumPlanes[5].normal.z, frustumPlanes[5].distance);


        Vector4[] cameraPlane = new Vector4[6] { left, right, down, up, nearP, farP };


        foreach (GameObject go in gameObjects)
        {
            float r = go.transform.localScale.x * 0.5f;
            Vector3 positonWS = go.transform.position;
            Vector4 positionHWS = new Vector4(positonWS.x, positonWS.y, positonWS.z, 1.0f);

            go.SetActive(true);
            for (int i = 0; i < cameraPlane.Length; i++)
            {
                float res = Vector4.Dot(positionHWS, cameraPlane[i]);

                if (res <= -r)
                {
                    go.SetActive(false);
                }


                Debug.Log("r:" + r);
            }

        }



    }
}
