using Unity.Entities;
using Unity.Physics;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Physics.Systems;


[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
public partial struct VATCharactorLaunchSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<LanchOnceTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        foreach (var (velocity,lanchTag) in  SystemAPI.Query<RefRW<PhysicsVelocity>, EnabledRefRW<LanchOnceTag>>()) 
        { 
            velocity.ValueRW.Linear = new float3(0, 10.0f, 0);

            lanchTag.ValueRW = false;
        }
    }

    public void OnDestroy() { }
}
