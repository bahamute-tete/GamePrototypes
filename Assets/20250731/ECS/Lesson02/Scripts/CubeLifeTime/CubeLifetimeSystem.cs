using Unity.Burst;
using Unity.Entities;

partial struct CubeLifetimeSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CubeLifeTimeComponent>();   
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
        var query = SystemAPI.QueryBuilder().WithAll<CubeLifeTimeComponent>().Build();

        state.Dependency = new CubeLifeTimeJob
        {
            ecb = ecb,
            deltaTime = SystemAPI.Time.DeltaTime
        }.ScheduleParallel(query, state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
