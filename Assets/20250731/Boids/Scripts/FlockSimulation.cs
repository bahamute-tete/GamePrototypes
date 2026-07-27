using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlockSimulation : MonoBehaviour
{
     public enum FlockType
    {
        Fish,      
        Bird,      
        Insect     
    }

    [Header("Flock Type")]
    public FlockType flockType;
    private FlockType previousFlockType;

    List<Boid> boids = new();

    public Transform boidPrefab;
    public int maxNumBoids = 100;
    public Bounds bounds= new Bounds(Vector3.zero,Vector3.one*20f);


    [Header("Boid Parameters")]
    public float maxSpeed=3f;
    public float maxForce=1f;
    public float radius=50f;

    [Header("Behavior Weights")]
    public float alignmentWeight = 1.0f;
    public float cohesionWeight = 1.0f;
    public float separationWeight = 1.5f;

    [Header("Debug")]
    public bool showRadius = true;

    // 缓存行为对象
    private AlignmentBehavior alignmentBehavior;
    private CohesionBehavior cohesionBehavior;
    private SeparationBehavior separationBehavior;
    // Start is called before the first frame update
    void Start()
    {

        alignmentBehavior = new AlignmentBehavior();
        cohesionBehavior = new CohesionBehavior();
        separationBehavior = new SeparationBehavior();

        previousFlockType = flockType;

        ApplyFlockTypePreset();
        CreateBoids();
    }

    private void ApplyFlockTypePreset()
    {
        switch (flockType)
        {
            case FlockType.Fish:
                // 鱼群：平滑游动，中等聚合，较强避障
                maxSpeed = 2.5f;
                maxForce = 0.5f;
                radius = 3.0f;
                alignmentWeight = 1.2f;
                cohesionWeight = 0.8f;
                separationWeight = 1.5f;
                break;

            case FlockType.Bird:
                // 鸟群：快速飞行，强对齐，较松散聚合
                maxSpeed = 5.0f;
                maxForce = 1.0f;
                radius = 5.0f;
                alignmentWeight = 1.5f;
                cohesionWeight = 0.6f;
                separationWeight = 1.2f;
                break;

            case FlockType.Insect:
                // 昆虫群：快速随机，弱对齐，强聚合
                maxSpeed = 3.5f;
                maxForce = 1.5f;
                radius = 2.5f;
                alignmentWeight = 0.8f;
                cohesionWeight = 1.5f;
                separationWeight = 2.0f;
                break;
        }
    }

    private void CreateBoids()
    {
        foreach (var boid in boids)
        {
            if (boid.transform != null)
            {
                Destroy(boid.transform.gameObject);
            }
        }
        boids.Clear();

        if (boidPrefab)
        {
            for (int i = 0; i < maxNumBoids; i++)
            {
                Vector3 pos = new Vector3(  UnityEngine.Random.Range(bounds.min.x, bounds.max.x), 
                                            UnityEngine.Random.Range(bounds.min.y, bounds.max.y), 
                                            UnityEngine.Random.Range(bounds.min.z, bounds.max.z));

                Boid boid = new Boid(pos, maxSpeed, maxForce, radius);
                boid.alignmentWeight = alignmentWeight;
                boid.cohesionWeight = cohesionWeight;
                boid.separationWeight = separationWeight;


                Transform boidInstance = Instantiate(boidPrefab, pos, Quaternion.identity,transform);
                boidInstance.localScale = Vector3.one;

                boid.transform = boidInstance;

                boids.Add(boid);
            }

        }
       
    }

    // Update is called once per frame
    void Update()
    {
         if (flockType != previousFlockType)
        {
            previousFlockType = flockType;
            ApplyFlockTypePreset();
           
            foreach (Boid boid in boids)
            {
                UpdateBoidsParameters(boid);
            }
        }

        foreach (Boid boid in boids)
        {
            UpdateBoidsParameters(boid);

            boid.UpdateBoids(boids,alignmentBehavior,cohesionBehavior,separationBehavior);
            boid.Boundlimits(bounds.min, bounds.max);
        }
    }


    private void UpdateBoidsParameters(Boid boid)
    {

        boid.maxSpeed = maxSpeed;
        boid.maxForce = maxForce;
        boid.radius = radius;
        boid.alignmentWeight = alignmentWeight;
        boid.cohesionWeight = cohesionWeight;
        boid.separationWeight = separationWeight;
        if (boid.transform != null)
        {
            BoidDebug debugComponent = boid.transform.GetComponent<BoidDebug>();
            if (showRadius && debugComponent != null)
            {
                debugComponent.radius = radius;

            }
            else
            {
                if (debugComponent != null)
                {
                    debugComponent.radius = 0;
                }
            }
           
       
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
