using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

partial struct WaveSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        //RequireForUpdate 的含义："查询不满足时，本系统整帧不跑"
        state.RequireForUpdate<WaveDynamicBufferComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float time = (float)SystemAPI.Time.ElapsedTime;

        //var waveSystemQuery = SystemAPI.GetSingleton<WaveSystemComponet>();

        var waveSystemQuerys = SystemAPI.GetSingletonBuffer<WaveDynamicBufferComponent>();

        foreach (var(transform, waveData, cubeColor, entity) in SystemAPI.Query<RefRW<LocalTransform>,RefRO<WaveDataComponent>,RefRW<CubeColor>>().WithEntityAccess())
        {
            float3 position = transform.ValueRW.Position;

            float2 xzPosition = position.xz;

            float waveHeight = 0f;
            float maxHeigh = 0f;

            foreach (var wave in waveSystemQuerys)
            {
                waveHeight += math.sin(math.dot(xzPosition, wave.dir) * wave.Frequency + time * wave.Speed) * wave.Amplitude;
                maxHeigh += wave.Amplitude;
            }

            //float waveA =math.sin(math.dot(xzPosition, waveSystemQuerys[0].dir)* waveSystemQuerys[0].Frequency+ time * waveSystemQuerys[0].Speed);
            //float waveB = math.sin(math.dot(xzPosition, waveSystemQuerys[1].dir) * waveSystemQuerys[1].Frequency*1.5f + time * waveSystemQuerys[1].Speed*0.7f);
            //float combinedWave = waveA* waveSystemQuerys[0].Amplitude + waveB * waveSystemQuerys[1].Amplitude;

            position.y = waveHeight;
           
            transform.ValueRW.Position = position;

            float maxHeight = math.max(maxHeigh,0.0001f);

            float normalizedY = math.clamp((position.y + maxHeight) / (2.0f*maxHeight), 0f, 1f);

            float3 deepWater = new float3(0.01f, 0.06f, 0.22f);
            float3 midWater = new float3(0.00f, 0.35f, 0.65f);
            float3 foam = new float3(0.65f, 0.95f, 1.00f);

            float3 waterColor = math.lerp(deepWater, midWater, normalizedY);

            float foamAmount = math.smoothstep(0.72f, 1.0f, normalizedY);
            waterColor = math.lerp(waterColor, foam, foamAmount);

            cubeColor.ValueRW.Value =new float4( waterColor, 1f);
        }
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
