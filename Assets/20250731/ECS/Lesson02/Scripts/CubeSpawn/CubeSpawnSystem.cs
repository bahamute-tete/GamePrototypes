using Unity.Burst;
using Unity.Mathematics;
using Unity.Entities;

partial struct CubeSpawnSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
       state.RequireForUpdate<CubeSpawnComponet>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        var query = SystemAPI.QueryBuilder().WithAll<CubeSpawnComponet,SpawnerActiveTagL2,CubeOrbitTrapColor>().Build();

        state.Dependency = new SpawnJobL2
        {
            ecb = ecb,
            deltaTime = SystemAPI.Time.DeltaTime
           
        }.ScheduleParallel(query,state.Dependency);




    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
