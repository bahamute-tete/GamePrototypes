using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

partial struct CubeSphereAnimationSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CubeAnimationComponent>();
        state.RequireForUpdate<CubeDragInput>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var deltaTime = SystemAPI.Time.DeltaTime;

        var dragInput = SystemAPI.GetSingletonRW<CubeDragInput>();
        float3 targetOffset = dragInput.ValueRW.IsDragging>0f ? dragInput.ValueRW.TrargetPos : float3.zero;
 
        // 指数插值：不同帧率下仍能保持近似一致的跟随手感。
        float followSharpness = 10f;
        float blend = 1f - math.exp(-followSharpness * deltaTime);


        dragInput.ValueRW.CurrentOffset = math.lerp(dragInput.ValueRW.CurrentOffset, dragInput.ValueRW.TrargetPos, blend);   


        var animConfig = SystemAPI.GetSingleton<CubeAnimationComponent>();
        var colorConfig = SystemAPI.GetSingleton<CubeOrbitTrapColor>();
        var query = SystemAPI.QueryBuilder().WithAll<LocalTransform,CubeComponentL2,CubeColor,OrbitTrapColorFactor>().Build();

        var animJob = new CubeAniamtionJob
        {
            time = (float)SystemAPI.Time.ElapsedTime,
            amplitude = animConfig.amplitude,
            frequency = animConfig.frequency,
            speed = animConfig.speed,
            followOffset = targetOffset,

        }.ScheduleParallel(query, state.Dependency);

        var colorJob = new CubeColorOrbitTrabsJob
        {
            baseColor = colorConfig.baseColor,
            amplitudeColor = colorConfig.amplitudeColor,
            frequencyColor = colorConfig.frequencyColor,
            phaseColor = colorConfig.phaseColor
        }.ScheduleParallel(query, state.Dependency);

        state.Dependency = Unity.Jobs.JobHandle.CombineDependencies(animJob, colorJob);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
