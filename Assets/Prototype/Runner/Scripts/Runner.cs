using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Runner : MonoBehaviour
{
    [SerializeField]
    Light pointLight;

    [SerializeField]
    ParticleSystem explosionSystem, trailSystem;

    [SerializeField, Min(0f)]
    float startSpeedX = 5f,maxSpeedX =40f, jumpAcceleration = 100f, gravity = 40f;

    [SerializeField]
    FloatRange jumpDuration = new FloatRange(0.1f, 0.2f);

    [SerializeField]
    AnimationCurve runAcclerationCurve;

    MeshRenderer meshRenderer;

    Vector2 position, velocity;

    public Vector2 Position => position;

    [SerializeField, Min(0f)]
    float extents = 0.5f;

    SkylineObject currentObstacle;

    bool grounded, transitioning;

    float jumpTimeRemaining;

    //��Ծʱִ�������ת
    [SerializeField, Min(0f)]
    float spinDuration = 0.75f;
    float spinTimeRemaining;
    Vector3 spinRotation;

    public float SpeedX {
        get => velocity.x; 
        set => velocity.x = value;
    }

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.enabled = true;
        pointLight.enabled = true;
    }
    /// <summary>
    /// ��λ������Ϊ�㣬������Ⱦ���͵ƹ⣬�����ըϵͳ��������á�����Ͳ��Ź켣ϵͳ��
    /// </summary>
    public void StartNewGame( SkylineObject obstacle)
    {
        currentObstacle = obstacle;

        while (currentObstacle.MaxX < extents)
        { 
            currentObstacle =currentObstacle.Next;
        }

        position = new Vector2(0f,currentObstacle.GapY.min+extents);
        //transform.localPosition = position;
        transform.SetPositionAndRotation(position, Quaternion.identity);
        meshRenderer.enabled = true;
        pointLight.enabled = true;
        explosionSystem.Clear();
        SetTrailEmission(true);
        trailSystem.Clear();
        trailSystem.Play();

        transitioning = false;

        grounded = true;
        jumpTimeRemaining = 0f;
        spinTimeRemaining = 0f;
        velocity = new Vector2(startSpeedX, 0f);



    }
    /// <summary>
    /// �÷���������Ⱦ�����ƹ��β�����䣬������Ϸ�����λ�ã�
    /// ��������ըϵͳ����������������ӡ�
    /// �������ֵ����Ϊ 100
    /// </summary>
    void Explode()
    { 
        meshRenderer.enabled = false;
        pointLight.enabled = false;
        SetTrailEmission(false);
        transform.localPosition = position;
        explosionSystem.Emit(explosionSystem.main.maxParticles);
    }

    private void SetTrailEmission(bool enable)
    {
        ParticleSystem.EmissionModule emission = trailSystem.emission;
        emission.enabled = enable;
    }

    public bool Run(float dt)
    {
        //if (position.x > 25f)
        //{
        //    Explode();
        //    return false;
        //}
        Move(dt);
        //position.x += startSpeedX * dt;

        if (position.x + extents < currentObstacle.MaxX)
        {
            ConstrainY(currentObstacle);
        }
        else
        {
            bool stillInsideCurrent = position.x - extents < currentObstacle.MaxX;
            if (stillInsideCurrent)
            {
                ConstrainY(currentObstacle);
            }

            if (!transitioning)
            {
                if (CheckCollision())
                {
                    return false;
                }
                transitioning = true;
            }

            ConstrainY(currentObstacle.Next);

            if (!stillInsideCurrent)
            {
                currentObstacle = currentObstacle.Next;
                transitioning = false;
            }
        }
        return true;
    }

    public void UpdateVisualiztion()
    {
        transform.localPosition = position;

        if (spinTimeRemaining > 0f)
        {
            // 当有旋转时间的时候，旋转时间逐渐减小直到0
            spinTimeRemaining = Mathf.Max(spinTimeRemaining - Time.deltaTime, 0f);
            // 对旋转角度插值
            transform.localRotation = Quaternion.Euler(
                Vector3.Lerp(spinRotation, Vector3.zero, spinTimeRemaining / spinDuration));
        }
    }

    void ConstrainY(SkylineObject obstacle)
    {
        FloatRange openY = obstacle.GapY;
        if (position.y - extents <= openY.min)
        {
            position.y = openY.min + extents;
            velocity.y = Mathf.Max(velocity.y, 0f);
            jumpTimeRemaining =0f;
            grounded = true;
        }
        else if (position.y + extents >= openY.max)
        {
            position.y = openY.max - extents;
            velocity.y = Mathf.Min(velocity.y, 0f);
            jumpTimeRemaining = 0f;
        }
        obstacle.Check(this);
   
    }

    bool CheckCollision()
    {
        Vector2 transitionPoint;
        transitionPoint.x = currentObstacle.MaxX -extents;
        transitionPoint.y = position.y - velocity.y*(position.x-transitionPoint.x)/velocity.x;
        float shrunkExtents = extents - 0.01f;
        FloatRange gapY = currentObstacle.Next.GapY;

        if (transitionPoint.y - shrunkExtents < gapY.min ||
            transitionPoint.y + shrunkExtents > gapY.max
            )
        {
            position = transitionPoint;
            Explode();
            return true;
        }
        return false;
    }

    public void StartJumping()
    {
        if (grounded)
        {
            jumpTimeRemaining = jumpDuration.max;

            if (spinTimeRemaining <= 0f)
            {   
                //如果没有剩余的旋转时间则设置旋转时间
                spinTimeRemaining = spinDuration;
                spinRotation = Vector3.zero;
                //三个轴随机选取，并保证每次旋转的都是90度
                spinRotation[UnityEngine.Random.Range(0, 3)] = UnityEngine.Random.value < 0.5f ? -90 : 90f;
            }
        }
    }

    public void EndJumping() 
    {
        //跳跃结束后，加上一个负值保证剩余时间最小
        jumpTimeRemaining += jumpDuration.min - jumpDuration.max;
    }

    void Move(float dt)
    {
        if (jumpTimeRemaining > 0f)
        {
            //有跳跃时间的时候，让时间减少
            jumpTimeRemaining -= dt;
            //计算速度，忽略重力，限定一个最大的加速为dt,然后逐渐减小
            velocity.y += jumpAcceleration * Mathf.Min(dt, jumpTimeRemaining);
        }
        else
        {
            // 重力
            velocity.y -= gravity * dt;
        }

        if (grounded)
        {
            // 水平方向上有一个加速度，但是收到动画曲线控制，
            // 速度逐渐加大到最大加速度的时候，动画曲线加速度最小，
            velocity.x = Mathf.Min(velocity.x + runAcclerationCurve.Evaluate(velocity.x / maxSpeedX) * dt, 
                maxSpeedX);

            grounded =false;
        }


        grounded = false;

        position += velocity * dt;
    }
}
