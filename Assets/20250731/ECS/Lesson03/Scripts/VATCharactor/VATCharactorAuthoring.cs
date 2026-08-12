using Unity.Entities;
using UnityEngine;

class VATCharactorAuthoring : MonoBehaviour
{
    public float GameTimeStartFrame;
}

class VATCharactorAuthoringBaker : Baker<VATCharactorAuthoring>
{
    public override void Bake(VATCharactorAuthoring authoring)
    {
        var renderEntity = GetEntity(TransformUsageFlags.Dynamic);

        AddComponent(renderEntity, new VATCharactorComponent()
        {
            GameTimeStartFrame = authoring.GameTimeStartFrame,
        });

        AddComponent(renderEntity, new NeedRandomOffsetTag());

        AddComponent(renderEntity, new LanchOnceTag());


    }
}
