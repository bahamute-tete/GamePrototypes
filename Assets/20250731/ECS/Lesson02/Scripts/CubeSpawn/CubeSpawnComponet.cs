using Unity.Entities;
using Unity.Mathematics;

public struct CubeSpawnComponet : IComponentData
{
    public Entity prefab;
    public int spawnCount;

    public float radius;

    public float spawnInterval;
    public float timer;
    public int waveCount;
    public int maxWaveCount;

    public float cubeLifeTime;


}

public struct CubeOrbitTrapColor : IComponentData
{
    public float3 baseColor;
    public float3 amplitudeColor;
    public float3 frequencyColor;
    public float3 phaseColor;
    public float facctor;
}

public struct CubeLifeTimeComponent : IComponentData
{
    public float lifeTime;
}


public struct SpawnerActiveTagL2 : IComponentData,IEnableableComponent
{

}
