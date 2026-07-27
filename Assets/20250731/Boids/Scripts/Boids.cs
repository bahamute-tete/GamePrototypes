
using System.Collections.Generic;
using UnityEngine;

public class Boid
{
    public Transform transform;

    public Vector3 position;
    public Vector3 velocity;
    public Vector3 acceleration;
    public float maxSpeed;
    public float maxForce;
    public float radius;

    public float alignmentWeight = 1.0f;
    public float cohesionWeight = 1.0f;
    public float separationWeight = 1.5f;
    public Boid(Vector3 position, float maxSpeed=1f, float maxForce = 1f, float radius = 1f)
    { 
        this.position = position;
        this.velocity = Random.insideUnitSphere;
        this.acceleration = Vector3.zero;
        this.maxSpeed = maxSpeed;
        this.maxForce = maxForce;
        this.radius = radius;
    }

    public Vector3 Flock(List<Boid> boids,SteeringBehaviors alignmentBehavior,
        SteeringBehaviors cohesionBehavior,SteeringBehaviors separationBehavior)
    {
        Vector3 alignment = alignmentBehavior.Behavior(this, boids);
        Vector3 cohesion = cohesionBehavior.Behavior(this, boids);
        Vector3 separation = separationBehavior.Behavior(this, boids);

        alignment *= alignmentWeight;
        cohesion *= cohesionWeight;
        separation *= separationWeight;

        Vector3 acceleration = alignment + cohesion + separation;

        return acceleration;
    }

    public void UpdateBoids(List<Boid> boids,SteeringBehaviors alignmentBehavior,
        SteeringBehaviors cohesionBehavior,SteeringBehaviors separationBehavior)
    {
        acceleration = Flock(boids, alignmentBehavior, cohesionBehavior, separationBehavior);
        velocity += acceleration * Time.deltaTime;

        if (velocity.magnitude > maxSpeed)
        {
            velocity = Vector3.ClampMagnitude(velocity, maxSpeed);
        }
        
        position += velocity * Time.deltaTime;


        if (transform != null)
        {
            transform.position = position;

            if (velocity.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(velocity.normalized);
            }
        }

        acceleration = Vector3.zero;
    }


    public void Boundlimits(Vector3 min, Vector3 max)
    {
        if (position.x < min.x) position.x = max.x;
        if (position.y < min.y) position.y = max.y;
        if (position.z < min.z) position.z = max.z;
        if (position.x > max.x) position.x = min.x;
        if (position.y > max.y) position.y = min.y;
        if (position.z > max.z) position.z = min.z;
    }



}
