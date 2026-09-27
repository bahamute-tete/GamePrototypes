#ifndef LZ_DISSOLVE_CORE_INCLUDED
#define LZ_DISSOLVE_CORE_INCLUDED
// Pure math: no material fields, textures, pipeline includes or screen derivatives.
// Positions must already be in the coordinate space used by axis/radial.
struct DissolveSettings
{
    float mode;
    float4 axis;
    float axisCenter;
    float4 radial;
    float radialReverse;
    float edgeNoiseStrength;
    float coverageWidth;
    float edgeWidth;
};
struct DissolveResult { float coverage; float edge; };
float DissolveField_Axis(float3 pos, float3 axisDir, float halfExtent, float axisCenter)
{
    float proj  = dot(pos, axisDir);
    float minP  = axisCenter - halfExtent;
    float range = max(halfExtent * 2.0, 1e-4);
    return saturate((proj - minP) / range);
}
float DissolveField_Radial(float3 pos, float3 center, float maxDist, float reverse)
{
    float d = length(pos - center);
    float n = saturate(d / max(maxDist, 1e-4));
    return reverse > 0.5 ? 1.0 - n : n;
}
float DissolveEvaluateField(float3 pos, DissolveSettings settings, float noise)
{
    int mode = (int)settings.mode;
    float result;
    if (mode == 1)
        result = DissolveField_Axis(pos, settings.axis.xyz, settings.axis.w, settings.axisCenter);
    else if (mode == 2)
        result = DissolveField_Radial(pos, settings.radial.xyz, settings.radial.w, settings.radialReverse);
    else
        result = noise;
    if ((mode == 1 || mode == 2) && settings.edgeNoiseStrength > 0.0)
    {
        float strength = saturate(settings.edgeNoiseStrength);
        noise = saturate(noise);
        result = (result + (noise * 2.0 - 1.0) * strength + strength) / (1.0 + 2.0 * strength);
    }
    return result;
}
float DissolveDistance(float field, float amount, float coverageWidth)
{
    float softWidth = max(coverageWidth, 0.0);
    float threshold = lerp(-softWidth * 0.5, 1.0 + softWidth * 0.5, saturate(amount));
    return field - threshold;
}
float DissolveCoverageFromDistance(float distance, float pixelWidth, float coverageWidth)
{
    float width = max(max(coverageWidth, pixelWidth), 1e-5);
    return saturate(distance / width + 0.5);
}
// pixelWidth is supplied by the caller; fragment callers can use fwidth(field) * aaPixels.
DissolveResult DissolveEvaluate(float field, float amount, float pixelWidth, DissolveSettings settings)
{
    DissolveResult result;
    result.coverage = amount <= 0.0 ? 1.0 : 0.0;
    result.edge = 0.0;
    if (amount <= 0.0 || amount >= 1.0) return result;
    float distance = DissolveDistance(field, amount, settings.coverageWidth);
    result.coverage = DissolveCoverageFromDistance(distance, pixelWidth, settings.coverageWidth);
    float glowWidth = max(max(settings.edgeWidth, pixelWidth), 1e-5);
    result.edge = 1.0 - smoothstep(0.0, glowWidth, max(distance, 0.0));
    result.edge *= step(0.001, amount) * step(amount, 0.999);
    return result;
}
float DissolveShadowCoverage(float field, float amount, float coverageWidth)
{
    if (amount <= 0.0) return 1.0;
    if (amount >= 1.0) return 0.0;
    return step(0.0, DissolveDistance(field, amount, coverageWidth));
}
half DissolveBrightness(float amount, float enabled, float power)
{
    if (enabled < 0.5 || power <= 0.0) return 1.0;
    return pow(saturate(1.0 - amount), power);
}
float DissolveIGN(float2 screenPos)
{
    return frac(52.9829189 * frac(dot(screenPos, float2(0.06711056, 0.00583715))));
}
#endif
