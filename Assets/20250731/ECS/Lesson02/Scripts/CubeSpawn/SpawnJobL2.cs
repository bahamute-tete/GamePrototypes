using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct SpawnJobL2 : IJobEntity
{

    public EntityCommandBuffer.ParallelWriter ecb;


    public float deltaTime;
    public void Execute([EntityIndexInQuery] int entityIndex,Entity spawner,ref CubeSpawnComponet cubeSpawn)
    {
        cubeSpawn.timer -= deltaTime;

        if (cubeSpawn.timer > 0f)
            return;


        for (int i = 0; i < cubeSpawn.spawnCount; i++)
        {
            var cube = ecb.Instantiate(entityIndex, cubeSpawn.prefab);
           
            //ecb.AddComponent(entityIndex, cube, new CubeLifeTimeComponent { lifeTime = cubeSpawn.cubeLifeTime });

            Random random = new Random((uint)(i + 1) * 123456789);
            float3 dir = random.NextFloat3Direction();

            //sphere体积 正比于 r^3 
            float radius =math.pow( random.NextFloat(),1f/3f) * cubeSpawn.radius;
            float3 pos = dir * radius;

            quaternion rot = quaternion.LookRotationSafe(-dir, math.up());


            float dis2Center = math.length(pos);
            float dis2XZ = math.length(pos.xz);

            float t = math.min(dis2Center,dis2XZ) / cubeSpawn.radius;
            t = math.clamp(t, 0f, 1f);

            ecb.AddComponent(entityIndex, cube, new OrbitTrapColorFactor
            {
                factor = t
            });

            ecb.SetComponent(entityIndex, cube, new LocalTransform
            {       
                Position = pos,
                Rotation = rot,
                Scale = 1f
            });

            ecb.SetComponent(entityIndex, cube, new CubeComponentL2 
            { 
                basePosition = pos,
                normalDir = dir , 
                radius = radius 
            });



        }


        cubeSpawn.timer = cubeSpawn.spawnInterval;
        cubeSpawn.waveCount++;

        //if (cubeSpawn.waveCount >= cubeSpawn.maxWaveCount && cubeSpawn.maxWaveCount>0)
        //{
        //    ecb.SetComponentEnabled<SpawnerActiveTagL2>(entityIndex, spawner, false);
        //}

        ecb.SetComponentEnabled<SpawnerActiveTagL2>(entityIndex, spawner, false);
    }

}


