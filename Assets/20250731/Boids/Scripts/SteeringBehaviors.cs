
using System.Collections.Generic;
using UnityEngine;


public abstract class SteeringBehaviors
{

   public abstract Vector3 Behavior(Boid boid, List<Boid> boids);
}


public class AlignmentBehavior : SteeringBehaviors
{
    public override Vector3 Behavior(Boid self, List<Boid> boids)
    {
        Vector3 steering = Vector3.zero;
        int total = 0;
        
        foreach (Boid other in boids)
        {
            float distance = Vector3.Distance(self.position, other.position);
            if (!ReferenceEquals(self,other) && distance <self.radius)
            {
                steering += other.velocity;
                total++;
            }
        }

        if (total > 0)
        {
            steering /= total;
            if (steering.magnitude > 0)
            {
                steering = Vector3.Normalize(steering) * self.maxSpeed;
            }

            steering -=self.velocity;

            if (steering.magnitude >self.maxForce)
            {
                steering = Vector3.Normalize(steering) * self.maxForce;
            }
        }

        return steering;
    }
}


public class CohesionBehavior : SteeringBehaviors
{
    public override Vector3 Behavior(Boid self, List<Boid> boids)
    {
        Vector3 steering = Vector3.zero;
        int total = 0;

        foreach (Boid other in boids)
        {
            float distance = Vector3.Distance(self.position, other.position);
            if (!ReferenceEquals(self, other) && distance < self.radius)
            {
                steering += other.position;
                total++;
            }
        }

        if (total > 0)
        {
            steering /= total;

            //desired direction
            steering -= self.position;

            if (steering.magnitude > 0)
            {
                steering = Vector3.Normalize(steering) * self.maxSpeed;
            }
            steering -= self.velocity;

            if (steering.magnitude > self.maxForce)
            {
                steering = Vector3.Normalize(steering) * self.maxForce;
            }
        }

        return steering;
    }
}


public class SeparationBehavior : SteeringBehaviors
{
    public override Vector3 Behavior(Boid self, List<Boid> boids)
    {
        Vector3 steering = Vector3.zero;
        int total = 0;

        foreach (Boid other in boids)
        {
            float distance = Vector3.Distance(self.position, other.position);
            if (!ReferenceEquals(self, other) && distance < self.radius)
            {
                Vector3 diff = self.position - other.position;
                
                if(distance > 0)
                    diff /= distance ; //weight by distance
                    
                steering += diff;
                total++;
            }
        }

        if (total > 0)
        {
            steering /= total;

            if (steering.magnitude > 0)
            {
                steering = Vector3.Normalize(steering) * self.maxSpeed;
            }
            steering -= self.velocity;

            if (steering.magnitude > self.maxForce)
            {
                steering = Vector3.Normalize(steering) * self.maxForce;
            }
        }

        return steering;
    }

}