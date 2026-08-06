using Unity.Entities;
using Unity.Mathematics;

public struct CubeDragInput : IComponentData
{
    public float3 TrargetPos;
    public float IsDragging;
    public float3 CurrentOffset;
}
