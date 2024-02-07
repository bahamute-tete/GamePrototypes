using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraParameter : MonoBehaviour
{
    public Material material;
    public Camera camera;

    static int rightID = Shader.PropertyToID("_Right");
    static int upID = Shader.PropertyToID("_Up");
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //Vector3 forward = camera.transform.forward;
        //Vector3 right = camera.transform.right;
        //Vector3 up = camera.transform.up;

        Vector3 forward =(Vector3.zero -camera.transform.position).normalized;
        Vector3 right = Vector3.Cross(forward,Vector3.up).normalized;
        Vector3 up = Vector3.Cross(forward,right);

        material.SetVector(rightID, new Vector4(right.x, right.y, right.z, 0.0f));
        material.SetVector(upID, new Vector4(up.x, up.y, up.z, 0.0f));

        Debug.Log("R: "+right);
        Debug.Log("U: " + up);


    }
}
