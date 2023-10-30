using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrackingCamera : MonoBehaviour
{
    Vector3 offset, position;

    //距相机每单位距离的 X 维度视图的半宽
    float viewFactorX;

    ParticleSystem stars;

    [SerializeField]
    AnimationCurve yCurve;

    private void Awake()
    {
        offset = transform.position;


        Camera c = GetComponent<Camera>();
        float viewFactorY = Mathf.Tan(c.fieldOfView * 0.5f * Mathf.Deg2Rad);
        viewFactorX = viewFactorY * c.aspect;


        stars = GetComponent<ParticleSystem>();
        ParticleSystem.ShapeModule shape = stars.shape;
        Vector3 position = shape.position;
        position.y = viewFactorY * position.z * 0.5f;
        shape.position = position;
        shape.scale = new Vector3(2f * viewFactorX, viewFactorY) * position.z;
    }

    public void StartNewGame()
    {
        Track(Vector3.zero);
        stars.Clear();
        stars.Emit(stars.main.maxParticles);
    }

    public void Track(Vector3 forcusPoint)
    {
        position = forcusPoint+offset;
        position.y = yCurve.Evaluate(position.y);
        transform.localPosition = position;
    }

    //用相机的 X 位置和按相对 Z 距离缩放的视图因子 X 作为其范围而形成的范围
    public FloatRange VisibleX(float z)
    {
        return FloatRange.PositionExtents(position.x, viewFactorX * (z - position.z));
    }
}
