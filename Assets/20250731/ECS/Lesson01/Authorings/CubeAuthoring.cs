using Unity.Entities;
using UnityEngine;
using Unity.Transforms;
using Unity.Mathematics;

class CubeAuthoring : MonoBehaviour
{
    
}

class CubeAuthoringBaker : Baker<CubeAuthoring>
{
    public override void Bake(CubeAuthoring authoring)
    {
        Entity e = GetEntity(TransformUsageFlags.Dynamic);

        AddComponent(e, new WaveDataComponent
        {
            distance = 0f
        });

        AddComponent(e, new CubeColor
        {
            Value = new float4(1f, 0f, 0f, 1f)
        });

    }
}
