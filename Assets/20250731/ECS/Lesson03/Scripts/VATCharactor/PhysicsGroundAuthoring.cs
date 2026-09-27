using Unity.Entities;
using UnityEngine;


public struct PhysicsGroundTag : IComponentData
{

}


class PhysicsGroundAuthoring : MonoBehaviour
{
    
}

class PhysicsGroundAuthoringBaker : Baker<PhysicsGroundAuthoring>
{
    public override void Bake(PhysicsGroundAuthoring authoring)
    {
        var groundEntity = GetEntity(TransformUsageFlags.None);
        AddComponent(groundEntity, new PhysicsGroundTag());
    }
}
