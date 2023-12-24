using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class TargetVisual : MonoBehaviour
{

    public Transform target;
    private Camera camera => GetComponent<Camera>();
    // Start is called before the first frame update
    void Start()
    {
        camera.transform.LookAt(target);
    }

    // Update is called once per frame
    void Update()
    {
        if (camera.transform.hasChanged)
            camera.transform.LookAt(target);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(target.transform.position, 0.2f);
    }
}
