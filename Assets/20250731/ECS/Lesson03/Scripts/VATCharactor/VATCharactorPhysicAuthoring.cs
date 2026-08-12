using Unity.Entities;
using UnityEngine;

class VATCharactorPhysicAuthoring : MonoBehaviour
{
    
}

class VATCharactorPhysicAuthoringBaker : Baker<VATCharactorPhysicAuthoring>
{
    public override void Bake(VATCharactorPhysicAuthoring authoring)
    {
        var physicEntity = GetEntity(TransformUsageFlags.Dynamic);

        AddComponent<HasLandedTag>(physicEntity);
        SetComponentEnabled<HasLandedTag>(physicEntity, false);


        AddComponent<LandingReactionTag>(physicEntity);
        SetComponentEnabled<LandingReactionTag>(physicEntity, false);
    }
}
