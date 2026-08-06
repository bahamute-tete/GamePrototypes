using Unity.Entities;
using Unity.Mathematics;

public struct WaveSystemComponet : IComponentData
{
    public float Amplitude_A;
    public float Frequency_A;
    public float Speed_A;
    public float2 dir_A;


    public float Amplitude_B;
    public float Frequency_B;
    public float Speed_B;
    public float2 dir_B;

}
