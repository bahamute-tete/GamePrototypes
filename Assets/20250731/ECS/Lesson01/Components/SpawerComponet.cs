using Unity.Entities;

public struct SpawnerComponet : IComponentData
{
    public Entity prefab;
    public int spawnCount;
    public int columns;
    public float spacing;
    
}

public struct SpawnedTag : IComponentData { }

