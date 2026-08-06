using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class CubeSpawnAuthoring : MonoBehaviour
{
    [Header("Spawner")]
    public GameObject prefab;
    public int spawnCount;

    public float radius=10f;

    public float spawnInterval = 1f;
    public int maxWaveCount = 10;


    public float cubeLifeTime = 5f;


    [Header("Animation")]
    public float amplitude;
    public float frequency;
    public float speed;
    public float2 direction;

    [Header("Color")]
    public Color baseColor;
    public Color amplitudeColor;
    public Color frequencyColor;
    public Color phaseColor;

}

class CubeSpawnAuthoringBaker : Baker<CubeSpawnAuthoring>
{
    public override void Bake(CubeSpawnAuthoring authoring)
    {
        var spawner = GetEntity(TransformUsageFlags.None);


        AddComponent(spawner, new CubeSpawnComponet 
        {
            prefab = GetEntity(authoring.prefab, TransformUsageFlags.None),
            spawnCount = authoring.spawnCount,
            radius = authoring.radius,
            spawnInterval = authoring.spawnInterval,
            timer = 0f,
            waveCount = 0,
            maxWaveCount=authoring.maxWaveCount,
           

        });

        AddComponent(spawner, new CubeAnimationComponent
        {
            amplitude = authoring.amplitude,
            frequency = authoring.frequency,
            speed = authoring.speed,
            direction =math.normalizesafe(authoring.direction,new float2(1,0)),
            
        });

        AddComponent(spawner, new CubeOrbitTrapColor
        {
            baseColor = new float3(authoring.baseColor.r, authoring.baseColor.g, authoring.baseColor.b),
            amplitudeColor = new float3(authoring.amplitudeColor.r, authoring.amplitudeColor.g, authoring.amplitudeColor.b),
            frequencyColor = new float3(authoring.frequencyColor.r, authoring.frequencyColor.g, authoring.frequencyColor.b),
            phaseColor = new float3(authoring.phaseColor.r, authoring.phaseColor.g, authoring.phaseColor.b),
        });


        AddComponent<SpawnerActiveTagL2>(spawner);


    }
}
