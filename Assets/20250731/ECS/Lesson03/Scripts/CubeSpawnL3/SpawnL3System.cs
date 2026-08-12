using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
partial struct SpawnL3System : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpawnL3Component>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
        var spawnParameters = SystemAPI.QueryBuilder().WithAll<SpawnL3Component,SpawnedL3Tag>().Build();


        state.Dependency = new SpawnCubeL3Job
        {
            ecb = ecb,
        }.ScheduleParallel(spawnParameters, state.Dependency);

    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
