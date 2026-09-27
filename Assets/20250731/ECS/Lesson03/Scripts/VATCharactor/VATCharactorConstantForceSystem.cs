using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Physics.Extensions;
using Unity.Mathematics;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PhysicsSystemGroup))]
partial struct VATCharactorConstantForceSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ConstantForce>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float fixedDeltaTime = SystemAPI.Time.DeltaTime;

        foreach(var (velocity,mass,force,surfaceInfo,grounded)in SystemAPI.Query<
            RefRW<PhysicsVelocity>,
            RefRO<PhysicsMass>,
            RefRO<ConstantForce>,
            RefRO<GroundSurfaceInfo>,
            EnabledRefRO<GroundedTag>
            >())
        {
           float3 surfaceNormal = math.normalizesafe(surfaceInfo.ValueRO.Normal, math.up());
            
            float3 forceAlongGround = force.ValueRO.Force - math.dot(force.ValueRO.Force, surfaceNormal) * surfaceNormal;

            var impulse = forceAlongGround * fixedDeltaTime;

           velocity.ValueRW.ApplyLinearImpulse(mass.ValueRO, impulse);
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
