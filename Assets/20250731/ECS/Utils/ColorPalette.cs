using Unity.Mathematics;

public static class PaletteUtil
{
    public static float3 CosinePalette(float t, float3 baseColor, float3 amplitude, float3 frequency, float3 phase)
    {
        float3 a = baseColor;   // base
        float3 b = amplitude;   // amplitude
        float3 c = frequency;   // frequency
        float3 d = phase; // phase
        return a + b * math.cos(math.TAU * (c * t + d));
    }
}