using Unity.Entities;
using Unity.Rendering;
using Unity.Mathematics;


[MaterialProperty("_gameTimeAtFirstFrame")]
public struct VATCharactorComponent : IComponentData
{
    public float GameTimeStartFrame;
}

public struct LandingReactionConfig : IComponentData
{
    //public float BounceSpeed;
    public float BounceImpulse;
}

public struct ConstantForce : IComponentData
{
    public float3 Force;
}

public struct GroundProbeConfig:IComponentData
{
    public float StartOffset;//射线起点位置
    public float Distance;//射线长度
}

public struct GroundSurfaceInfo: IComponentData
{
    public Entity GroundEntity;
    public float3 Normal;
    public float3 Point;
}

public  struct GroundedTag : IComponentData, IEnableableComponent { }


public struct NeedRandomOffsetTag : IComponentData, IEnableableComponent
{
    // This struct is intentionally left empty as a tag component.
}

public struct  LanchOnceTag:IComponentData,IEnableableComponent{}

public struct HasLandedTag : IComponentData, IEnableableComponent{}

public struct LandingReactionTag : IComponentData, IEnableableComponent{}


