using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class CubeEntityAuthoring : MonoBehaviour
{

}

class CubeEntityAuthoringBaker : Baker<CubeEntityAuthoring>
{
    public override void Bake(CubeEntityAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);

        AddComponent(entity, new CubeComponentL2
        {
            basePosition = authoring.transform.position,
            normalDir = authoring.transform.up,
            radius = 0.5f
        });


        AddComponent(entity,new CubeColor
        {
            Value = new float4(1f, 0f, 0f, 1f)
        });
    }
}
