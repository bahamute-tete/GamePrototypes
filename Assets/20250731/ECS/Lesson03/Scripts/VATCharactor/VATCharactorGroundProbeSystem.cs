using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Physics.Extensions;
using Unity.Transforms;

[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
[UpdateAfter(typeof(VATCharactorLandingReactionSystem))]
partial struct VATCharactorGroundProbeSystem : ISystem
{

    private const uint ChractoerCategory = 1u << 0;
    private const uint GroundCategory = 1u << 1;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<GroundProbeConfig>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;

        var groundTags = SystemAPI.GetComponentLookup<PhysicsGroundTag>(true);

        var groundedTags = SystemAPI.GetComponentLookup<GroundedTag>();

        foreach (var (transform, surfaceInfo, probe, entity) in SystemAPI.Query<
            RefRW<LocalTransform>,
            RefRW<GroundSurfaceInfo>,
            RefRO<GroundProbeConfig>
            >().WithAll<GroundedTag>().WithEntityAccess().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState))
        { 

            float3 start = transform.ValueRO.Position + math.up()* probe.ValueRO.StartOffset;

            float3 end = start - math.up() * probe.ValueRO.Distance;

            var raycastInput = new RaycastInput
            {
                Start = start,
                End = end,
                Filter = new CollisionFilter
                {
                    BelongsTo = ChractoerCategory,
                    CollidesWith = GroundCategory,
                    GroupIndex = 0
                }
            };

            bool hitGround = collisionWorld.CastRay(raycastInput, out RaycastHit hit);

            bool isGrounded = hitGround && groundTags.HasComponent(hit.Entity) && hit.SurfaceNormal.y>0.5f;

            if (isGrounded)
            { 
                surfaceInfo.ValueRW = new GroundSurfaceInfo
                {
                    GroundEntity = hit.Entity,
                    Point = hit.Position,
                    Normal = hit.SurfaceNormal
                };
            }else
            {
                surfaceInfo.ValueRW = new GroundSurfaceInfo
                {
                    GroundEntity = Entity.Null,
                    Point = float3.zero,
                    Normal = math.up()
                };
            }

            groundedTags.SetComponentEnabled(entity, isGrounded);
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
