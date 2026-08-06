using Unity.Entities;
using Unity.Mathematics;

[InternalBufferCapacity(4)]
public struct WaveDynamicBufferComponent : IBufferElementData
{
    public float Amplitude;
    public float Frequency;
    public float Speed;
    public float2 dir;
}
