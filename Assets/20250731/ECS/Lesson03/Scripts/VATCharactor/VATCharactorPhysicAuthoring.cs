using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

class VATCharactorPhysicAuthoring : MonoBehaviour
{
    //[Min(0f)]
    //public float bounceSpeed = 5f;
    [Min(0f)]
    public float bounceImpulse = 5f;

    public Vector3 constantForce = Vector3.zero;

    [Min(0f)]
    public float groundStartOffset = 0f;

    [Min(0f)]
    public float groundDistance = 0f;
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

        AddComponent<LandingReactionConfig>(physicEntity);
        SetComponent(physicEntity, new LandingReactionConfig { BounceImpulse = authoring.bounceImpulse });

        AddComponent(physicEntity, new ConstantForce { Force = new float3(
            authoring.constantForce.x, 
            authoring.constantForce.y, 
            authoring.constantForce.z) });

        AddComponent(physicEntity,new GroundProbeConfig 
        { 
            StartOffset = authoring.groundStartOffset,
            Distance = authoring.groundDistance
        });

        AddComponent<GroundedTag>(physicEntity);
        SetComponentEnabled<GroundedTag>(physicEntity, false);


        AddComponent(physicEntity, new GroundSurfaceInfo {
            Normal = new float3(0f, 1f, 0f), 
            Point = float3.zero, 
            GroundEntity = Entity.Null });
    }
}
