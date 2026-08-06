using Unity.Entities;
using Unity.Mathematics;

public struct CubeAnimationComponent : IComponentData
{
    public float amplitude;    
    public float frequency;    
    public float speed;        
    public float2 direction;   
}
