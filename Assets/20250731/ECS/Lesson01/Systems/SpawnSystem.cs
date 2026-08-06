using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms; 

partial struct SpawnSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpawnerComponet>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        //manually create an entity command buffer to store the commands we want to execute later
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
       

        foreach (var (spawner,spawnerEntity) in SystemAPI.Query<RefRO<SpawnerComponet>>().WithEntityAccess().WithNone<SpawnedTag>())
        { 
            int columns = math.max(1,spawner.ValueRO.columns);
            for(int i = 0; i < spawner.ValueRO.spawnCount; i++)
            {
                int row = i / columns;
                int column = i % columns;

                float3 center = new float3((columns - 1) * spawner.ValueRO.spacing * 0.5f, 0, (spawner.ValueRO.spawnCount / columns - 1) * spawner.ValueRO.spacing * 0.5f);
                float3 position = new float3(column * spawner.ValueRO.spacing , 0, row * spawner.ValueRO.spacing) - center;

                float distance2Center = math.distance(float3.zero, position);
               
                var  e = ecb.Instantiate(spawner.ValueRO.prefab);
                ecb.SetComponent(e,LocalTransform.FromPosition(position));
                ecb.SetComponent(e, new WaveDataComponent
                {
                    distance = distance2Center
                });
                ecb.SetComponent(e, new CubeColor
                {
                    Value = new float4(1f, 0f, 0f, 1f)
                });

            }

            ecb.AddComponent<SpawnedTag>(spawnerEntity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
      
           
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
