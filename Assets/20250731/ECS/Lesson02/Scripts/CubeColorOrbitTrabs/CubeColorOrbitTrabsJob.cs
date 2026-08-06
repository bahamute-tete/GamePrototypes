using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct CubeColorOrbitTrabsJob : IJobEntity
{

    public float3 baseColor;
    public float3 amplitudeColor;
    public float3 frequencyColor;
    public float3 phaseColor;
    public void Execute(ref CubeColor cubeColor, in OrbitTrapColorFactor orbitTrapColorFactor)
    {
        float3 rgb = PaletteUtil.CosinePalette(orbitTrapColorFactor.factor, baseColor, amplitudeColor, frequencyColor, phaseColor);
        cubeColor.Value = new float4(rgb, 1f);
    }
}
