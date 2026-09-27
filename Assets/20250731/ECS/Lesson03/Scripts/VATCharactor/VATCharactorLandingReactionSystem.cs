using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
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
        foreach(var (velocity,mass,reactionTag,config) in SystemAPI.Query<
            RefRW<PhysicsVelocity>,
            RefRO<PhysicsMass>,
            EnabledRefRW<LandingReactionTag>,
            RefRO<LandingReactionConfig>
            >())
        {
            //var linerVelocity = velocity.ValueRW.Linear;
            //linerVelocity.y = config.ValueRO.BounceImpulse;
            //velocity.ValueRW.Linear = linerVelocity;
            velocity.ValueRW.ApplyLinearImpulse(mass.ValueRO,new float3(0, config.ValueRO.BounceImpulse, 0));

            reactionTag.ValueRW = false;
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
