using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class ObjectAutoRotateWithAixe : MonoBehaviour
{
    public enum RotationAxis
    {
        X,
        Y,
        Z
    }

    [Header("旋转设置")]
    [Tooltip("选择旋转轴向")]
    public RotationAxis rotationAxis = RotationAxis.Y;

    [Tooltip("旋转速度（度/秒）")]
    public float rotationSpeed = 30.0f;

    [Tooltip("旋转方向（勾选为顺时针，不勾选为逆时针）")]
    public bool clockwise = true;

    // Start is called before the first frame update
    void Start()
    {
        // 初始化代码（如果需要）
    }

    // Update is called once per frame
    void Update()
    {
        // 根据选择的方向调整速度符号
        float direction = clockwise ? 1.0f : -1.0f;
        float rotationAmount = rotationSpeed * direction * Time.deltaTime;
        
        // 根据选择的轴向应用旋转
        switch (rotationAxis)
        {
            case RotationAxis.X:
                transform.Rotate(rotationAmount, 0, 0, Space.Self);
                break;
            case RotationAxis.Y:
                transform.Rotate(0, rotationAmount, 0, Space.Self);
                break;
            case RotationAxis.Z:
                transform.Rotate(0, 0, rotationAmount, Space.Self);
                break;
        }
    }
}
