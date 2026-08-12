using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;

[BurstCompile]
public partial struct SpawnCubeL3Job : IJobEntity
{

    public EntityCommandBuffer.ParallelWriter ecb;

    public void Execute([EntityIndexInQuery] int entityIndex, ref SpawnL3Component spawnComponent,Entity spawner)
    {

        for (int i = 0; i < spawnComponent.count; i++) 
        {
            var e = ecb.Instantiate(entityIndex, spawnComponent.prefab);

            var centerOffsetWithGap = new float3(spawnComponent.column - 1, 0f, spawnComponent.count / spawnComponent.column - 1) * spawnComponent.gap * 0.5f;
            var position = new float3(i % spawnComponent.column, 0f, i / spawnComponent.column) * spawnComponent.gap - centerOffsetWithGap;

            ecb.SetComponent(entityIndex, e, new LocalTransform
            {
                Position = position,
                Rotation = quaternion.identity,
                Scale = 1f
            });

        }
        

       ecb.SetComponentEnabled<SpawnedL3Tag>(entityIndex, spawner, false);

    }
}
