using Unity.Entities;
using UnityEngine;

class CubeL3Authoring : MonoBehaviour
{
    
}

class CubeL3AuthoringBaker : Baker<CubeL3Authoring>
{
    public override void Bake(CubeL3Authoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);

    }

    
}
