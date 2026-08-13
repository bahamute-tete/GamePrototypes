
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;


[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
partial struct VATCharactorCollisionSystem : ISystem
{
    //private NativeQueue<Entity> m_VatCollisionEntities;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SimulationSingleton>();

        //m_VatCollisionEntities =
        //    new NativeQueue<Entity>(Allocator.Persistent);
    }


    public void OnUpdate(ref SystemState state)
    {
        var collisionJob = new VATCharactorCollisionJob
        {
            //CollisionQueue = m_VatCollisionEntities.AsParallelWriter(),
            dynamicBodies = SystemAPI.GetComponentLookup<PhysicsVelocity>(true),//用物理组件识别角色,动态角色有 PhysicsVelocity，静态地面没有
            landTags = SystemAPI.GetComponentLookup<HasLandedTag>(),
            landingReactionTags = SystemAPI.GetComponentLookup<LandingReactionTag>(),
        };
       state.Dependency = collisionJob.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);



        //while(m_VatCollisionEntities.TryDequeue(out var entity))
        //{
        //    // Process the dequeued entity
        //    UnityEngine.Debug.Log($"VATCharactor collided: {entity}");
        //}
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        //state.Dependency.Complete();
        //if (m_VatCollisionEntities.IsCreated)
        //   m_VatCollisionEntities.Dispose();
    }
}


[BurstCompile]
public struct VATCharactorCollisionJob : ICollisionEventsJob
{
    //[ReadOnly]
    //public ComponentLookup<VATCharactorComponent> vatCharactors;

    [ReadOnly]
    public ComponentLookup<PhysicsVelocity> dynamicBodies;
    //public NativeQueue<Entity>.ParallelWriter CollisionQueue;


    //landTags 不能是 ReadOnly，因为需要在碰撞时修改它的状态
    public ComponentLookup<HasLandedTag> landTags;
    public ComponentLookup<LandingReactionTag> landingReactionTags;

    public void Execute(CollisionEvent collisionEvent)
    {
        var entityA = collisionEvent.EntityA;
        var entityB = collisionEvent.EntityB;

        //// Check if either entity has the PhysicsVelocity
        //if (dynamicBodies.HasComponent(entityA))
        //{
        //    CollisionQueue.Enqueue(entityA);
        //}
        //else if (dynamicBodies.HasComponent(entityB))
        //{
        //    CollisionQueue.Enqueue(entityB);
        //}

        TryMarkLanded(entityA);
        TryMarkLanded(entityB);


    }

    private void TryMarkLanded(Entity entity)
    {
        if (!dynamicBodies.HasComponent(entity) || 
            !landTags.HasComponent(entity) || 
            !landingReactionTags.HasComponent(entity)||
            landTags.IsComponentEnabled(entity) || 
            landingReactionTags.IsComponentEnabled(entity))
        { 
            return ;
        }

        landTags.SetComponentEnabled(entity, true);
        landingReactionTags.SetComponentEnabled(entity, true);
        //CollisionQueue.Enqueue(entity);
    }
   
}
