using UnityEngine;

/// <summary>
/// 简单的环绕相机：左/右键拖拽旋转，滚轮缩放，中键拖拽平移。
/// 挂在相机上即可，target 默认为 Fox 数据中心。
/// </summary>
public class GsplatOrbitCamera : MonoBehaviour
{
    [Tooltip("环绕中心（Fox 数据几何中心约为 -2.9, -1.0, 3.2）")]
    public Vector3 target = new Vector3(-2.9f, -1.0f, 3.2f);
    public float distance = 12f;
    public float rotateSpeed = 4f;
    public float zoomSpeed = 6f;
    public float panSpeed = 0.4f;
    public float minDistance = 0.5f;
    public float maxDistance = 200f;

    float _yaw;
    float _pitch;

    void OnEnable()
    {
        Vector3 e = transform.eulerAngles;
        _yaw = e.y;
        _pitch = e.x > 180f ? e.x - 360f : e.x;
        float d = Vector3.Distance(transform.position, target);
        if (d > 0.01f) distance = d;
    }

    void Update()
    {
        // 旋转：左键或右键拖拽
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            _yaw   += Input.GetAxis("Mouse X") * rotateSpeed;
            _pitch -= Input.GetAxis("Mouse Y") * rotateSpeed;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);
        }

        // 平移：中键拖拽
        if (Input.GetMouseButton(2))
        {
            target -= transform.right * Input.GetAxis("Mouse X") * panSpeed * distance * 0.1f;
            target -= transform.up    * Input.GetAxis("Mouse Y") * panSpeed * distance * 0.1f;
        }

        // 缩放：滚轮
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (!Mathf.Approximately(scroll, 0f))
            distance = Mathf.Clamp(distance * (1f - scroll * zoomSpeed * 0.1f), minDistance, maxDistance);

        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        transform.position = target - transform.forward * distance;
    }
}
