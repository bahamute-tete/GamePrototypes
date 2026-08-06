using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

[System.Serializable]
public struct  waveParameters
{
    public float waveAmplitude;
    public float waveSpeed;
    public float waveFrequency;
    public Vector2 dir;
}

class SpawnerAuthoring : MonoBehaviour
{
    public GameObject prefab;
    public int spawnCount;
    public int columns;
    public float spacing;

    //[Space(10)]
    //public float waveAmplitude_A;
    //public float waveSpeed_A;
    //public float waveFrequency_A;
    //public Vector2 dir_A;

    //[Space(10)]
    //public float waveAmplitude_B;
    //public float waveSpeed_B;
    //public float waveFrequency_B;
    //public Vector2 dir_B;

    public waveParameters[] waveProperties;
}

class SpawnerAuthoringBaker : Baker<SpawnerAuthoring>
{
    public override void Bake(SpawnerAuthoring authoring)
    {
        Entity spawner = GetEntity(TransformUsageFlags.None);

        AddComponent(spawner, new SpawnerComponet
        {
            prefab =GetEntity(authoring.prefab, TransformUsageFlags.None),
            spawnCount = authoring.spawnCount,
            columns =math.max(1, authoring.columns),  
            spacing = authoring.spacing
        }

        
        );

        //AddComponent(spawner, new WaveSystemComponet
        //{
        //    Amplitude_A = authoring.waveAmplitude_A,
        //    Frequency_A = authoring.waveFrequency_A,
        //    Speed_A = authoring.waveSpeed_A,
        //    dir_A = authoring.dir_A,


        //    Amplitude_B = authoring.waveAmplitude_B,
        //    Frequency_B = authoring.waveFrequency_B,
        //    Speed_B = authoring.waveSpeed_B,
        //    dir_B = authoring.dir_B
        //});

        DynamicBuffer<WaveDynamicBufferComponent> waveBuffer = AddBuffer<WaveDynamicBufferComponent>(spawner);

        int waveCount = authoring.waveProperties.Length;

        for (int i = 0; i < waveCount; i++)
        { 
            waveBuffer.Add(new WaveDynamicBufferComponent
            {
                Amplitude = authoring.waveProperties[i].waveAmplitude,
                Frequency = authoring.waveProperties[i].waveFrequency,
                Speed = authoring.waveProperties[i].waveSpeed,
                dir = authoring.waveProperties[i].dir
            });
        }
      
    }
}