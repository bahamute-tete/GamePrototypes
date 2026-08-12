using Unity.Entities;
using Unity.Rendering;


[MaterialProperty("_gameTimeAtFirstFrame")]
public struct VATCharactorComponent : IComponentData
{
    public float GameTimeStartFrame;
}


public struct NeedRandomOffsetTag : IComponentData, IEnableableComponent
{
    // This struct is intentionally left empty as a tag component.
}

public struct  LanchOnceTag:IComponentData,IEnableableComponent{}

public struct HasLandedTag : IComponentData, IEnableableComponent{}

public struct LandingReactionTag : IComponentData, IEnableableComponent{}


