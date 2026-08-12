using Unity.Entities;
using Unity.Mathematics;

public struct SpawnL3Component : IComponentData
{
    public Entity prefab;
    public int count;
    public int column;
    public float gap;
}

public struct SpawnedL3Tag : IComponentData, IEnableableComponent
{
    // This struct is intentionally left empty as a tag component.
}
