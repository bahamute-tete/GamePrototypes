using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;


[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
[UpdateAfter(typeof(VATCharactorCollisionSystem))]
partial struct VATCharactorLandingReactionSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach(var (velocity,reactionTag) in SystemAPI.Query<RefRW<PhysicsVelocity>,EnabledRefRW<LandingReactionTag>>())
        {
            var linerVelocity = velocity.ValueRW.Linear;
            linerVelocity.y = 5f;
            velocity.ValueRW.Linear = linerVelocity;

            reactionTag.ValueRW = false;
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
