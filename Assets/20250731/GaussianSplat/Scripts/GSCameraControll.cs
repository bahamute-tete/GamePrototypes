using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GSCameraControll : MonoBehaviour
{
    public Camera camera;
    public Transform target;
    public float rotationSpeed = 5.0f;
    
    private float distance;
    private float rotationX;
    private float rotationY;

    // Start is called before the first frame update
    void Start()
    {
        if (camera == null) camera = Camera.main;
        if (target != null)
        {
            // 初始化距离和角度
            Vector3 direction = camera.transform.position - target.position;
            distance = direction.magnitude;
            rotationX = camera.transform.eulerAngles.y;
            rotationY = camera.transform.eulerAngles.x;
        }
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (target == null || camera == null) return;

        // 按住鼠标右键旋转
        if (Input.GetMouseButton(1))
        {
            rotationX += Input.GetAxis("Mouse X") * rotationSpeed;
            rotationY -= Input.GetAxis("Mouse Y") * rotationSpeed;

            // 限制Y轴角度，防止翻转
            rotationY = Mathf.Clamp(rotationY, -89f, 89f);
        }

        Quaternion rotation = Quaternion.Euler(rotationY, rotationX, 0);
        Vector3 position = rotation * new Vector3(0.0f, 0.0f, -distance) + target.position;

        camera.transform.rotation = rotation;
        camera.transform.position = position;
    }
}
