using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class livingCamera : MonoBehaviour
{
    [SerializeField, Min(0f)]
    float jostleStrenth = 40f,//推挤Y方向
            pushStrenth = 1f,//推XZ方向
            springStrenth = 100f,//弹簧
            dampingStrenth = 10f,//阻力
            maxDeltaTime = 1f / 60f;

    Vector3 velocity, anchorPosition;


    private void Awake()
    {
        anchorPosition = transform.localPosition;
    }

    public void JostleY()
    {
        velocity.y += jostleStrenth;

    }

    public void PushXZ(Vector2 impulse)
    {
        velocity.x += pushStrenth * impulse.x;
        velocity.z += pushStrenth * impulse.y;
    }

    private void LateUpdate()
    {

        float dt = Time.deltaTime;
        while (dt > maxDeltaTime)
        {
            TimeStep(maxDeltaTime);
            dt -= maxDeltaTime;
        }

        TimeStep(dt);
        
    }

    private void TimeStep(float dt)
    {
        //当前相机位置和锚点的位移
        Vector3 displacement = anchorPosition - transform.localPosition;
        //正方向的移动距离-负方向的距离
        Vector3 accleration = springStrenth * displacement - dampingStrenth * velocity;
        velocity += accleration * dt;

        transform.localPosition += velocity * dt;
    }
}
