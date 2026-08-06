using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

[MaterialProperty("_TargetColor")]
public struct CubeColor :IComponentData
{
    public float4 Value;
}


