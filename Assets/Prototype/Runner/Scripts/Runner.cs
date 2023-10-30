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
    /// 将位置设置为零，启用渲染器和灯光，清除爆炸系统，最后启用、清除和播放轨迹系统。
    /// </summary>
    public void StartNewGame( SkylineObject obstacle)
    {
        currentObstacle = obstacle;

        while (currentObstacle.MaxX < extents)
        { 
            currentObstacle =currentObstacle.Next;
        }

        position = new Vector2(0f,currentObstacle.GapY.min+extents);
        transform.localPosition = position;
        meshRenderer.enabled = true;
        pointLight.enabled = true;
        explosionSystem.Clear();
        SetTrailEmission(true);
        trailSystem.Clear();
        trailSystem.Play();

        transitioning = false;

        grounded = true;
        jumpTimeRemaining = 0f;
        velocity = new Vector2(startSpeedX, 0f);
    }
    /// <summary>
    /// 该方法禁用渲染器、灯光和尾迹发射，更新游戏对象的位置，
    /// 并触发爆炸系统发射最大数量的粒子。
    /// 将此最大值设置为 100
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
        }
    }

    public void EndJumping() 
    {
        //确保了始终达到最小值
        jumpTimeRemaining += jumpDuration.min - jumpDuration.max;
    }

    void Move(float dt)
    {
        if (jumpTimeRemaining > 0f)
        {
            jumpTimeRemaining -= dt;
            velocity.y += jumpAcceleration * Mathf.Min(dt, jumpTimeRemaining);
        }
        else
        {
            velocity.y -= gravity * dt;
        }

        if (grounded)
        {
            velocity.x = Mathf.Min(velocity.x + runAcclerationCurve.Evaluate(velocity.x / maxSpeedX) * dt, 
                maxSpeedX);

            grounded =false;
        }


        grounded = false;

        position += velocity * dt;
    }
}
