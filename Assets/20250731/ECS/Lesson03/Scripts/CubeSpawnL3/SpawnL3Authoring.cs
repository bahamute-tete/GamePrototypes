using Unity.Entities;
using UnityEngine;

class SpawnL3Authoring : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject prefab;
    public int count=25;
    public int column =5;
    public float gap = 1.0f;



    private void OnDrawGizmos()
    {
        if (prefab == null) return;
       
        Gizmos.color = Color.green;
        Vector3 centerOffsetWithGap = new Vector3(column - 1, 0f, count / column - 1) * 0.5f * gap;
        Vector3 size = (prefab.transform.localScale + new Vector3(gap, 0, gap));
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x * column , size.y , size.z * (count / column)));
    }
}

class SpawnL3AuthoringBaker : Baker<SpawnL3Authoring>
{
    public override void Bake(SpawnL3Authoring authoring)
    {
        var  spawnEntity = GetEntity(TransformUsageFlags.None);
        AddComponent(spawnEntity, new SpawnL3Component
        {
            prefab = GetEntity(authoring.prefab, TransformUsageFlags.None),
            count = authoring.count,
            column = authoring.column,
            gap = authoring.gap,
        });

        AddComponent<SpawnedL3Tag>(spawnEntity);
       

    }
}
