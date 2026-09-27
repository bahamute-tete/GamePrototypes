#ifndef LZ_DISSOLVE_MATERIAL_INCLUDED
#define LZ_DISSOLVE_MATERIAL_INCLUDED
#include "DissolveCore.hlsl"
#include "DissolveNoise.hlsl"
// Material adapter. Host declares fields in its ONE UnityPerMaterial CBUFFER.
// See README.md for the complete contract. This include owns no render state.
DissolveSettings DissolveGetMaterialSettings()
{
    DissolveSettings s;
    s.mode = _DissolveMode; s.axis = _DissolveAxis; s.axisCenter = _DissolveAxisCenter;
    s.radial = _DissolveRadial; s.radialReverse = _DissolveRadialReverse;
    s.edgeNoiseStrength = _DissolveEdgeNoiseStrength;
    s.coverageWidth = _DissolveCoverageWidth; s.edgeWidth = _DissolveEdgeWidth;
    return s;
}
float DissolveSampleNoiseTex(float3 pos)
{
    return DissolveSampleNoise(pos, TEXTURE2D_ARGS(_DissolveNoiseTex, sampler_DissolveNoiseTex));
}
float DissolveField_Noise(float3 pos, float scale)
{
    float3 p = pos * scale;
    float result;
    UNITY_BRANCH
    if (_DissolveUseNoiseTex > 0.5)
    {
        result = DissolveSampleNoiseTex(p);
    }
    else
    {
        result = DissolveValueNoise(p);
    }
    return result;
}
float DissolveField_Auto(float3 positionWS, float3 positionOS)
{
    DissolveSettings s = DissolveGetMaterialSettings();
    float3 pos = (_DissolveSpace > 0.5) ? positionWS : positionOS;
    int mode = (int)s.mode;
    float noise = 0.0;
    UNITY_BRANCH
    if ((mode != 1 && mode != 2) || s.edgeNoiseStrength > 0.0)
        noise = DissolveField_Noise(pos, _DissolveNoiseScale);
    return DissolveEvaluateField(pos, s, noise);
}
// Spatial mode: signed distance in world metres. Amount and renderer bounds do not participate.
bool DissolveUsesWorldGeometry()
{
    return _DissolveSpatialMode > 0.5 && _DissolveSpace > 0.5 && (_DissolveMode == 1.0 || _DissolveMode == 2.0);
}
bool DissolveShouldEvaluate(float amount)
{
    return DissolveUsesWorldGeometry() || amount > 0.0001;
}
float DissolveWorldDistance(float3 positionWS)
{
    float distance;
    if (_DissolveMode == 1.0)
        distance = dot(positionWS, _DissolveAxis.xyz) - _DissolveAxisCenter;
    else
    {
        distance = length(positionWS - _DissolveRadial.xyz) - max(_DissolveRadial.w, 0.0);
        if (_DissolveRadialReverse > 0.5) distance = -distance;
    }
    UNITY_BRANCH
    if (_DissolveEdgeNoiseStrength > 0.0)
        distance += (DissolveField_Noise(positionWS, _DissolveNoiseScale) * 2.0 - 1.0) * _DissolveEdgeNoiseStrength;
    return distance;
}
float DissolveGlow(float distance, float pixelWidth)
{
    if (_DissolveEdgeWidth <= 0.0) return 0.0;
    return 1.0 - smoothstep(0.0, max(_DissolveEdgeWidth, pixelWidth), max(distance, 0.0));
}
float DissolveDistance(float field, float amount)
{
    return DissolveDistance(field, amount, _DissolveCoverageWidth);
}
float DissolveCoverageFromDistance(float distance, float pixelWidth)
{
    return DissolveCoverageFromDistance(distance, pixelWidth, _DissolveCoverageWidth);
}
// Fragment-only entry points: use derivatives for filtered coverage.
float ComputeDissolveCoverage(float3 positionWS, float3 positionOS, float amount)
{
    if (DissolveUsesWorldGeometry())
    {
        if (_DissolveSpatialMode > 1.5) return 1.0;
        float distance = DissolveWorldDistance(positionWS);
        return DissolveCoverageFromDistance(distance, fwidth(distance) * max(_DissolveAAPixels, 0.0));
    }
    if (amount <= 0.0) return 1.0;
    if (amount >= 1.0) return 0.0;
    float field = DissolveField_Auto(positionWS, positionOS);
    float pixelWidth = fwidth(field) * max(_DissolveAAPixels, 0.0);
    return DissolveCoverageFromDistance(DissolveDistance(field, amount), pixelWidth);
}
DissolveResult DissolveEvaluateMaterial(float3 positionWS, float3 positionOS, float amount)
{
    DissolveResult result;
    if (DissolveUsesWorldGeometry())
    {
        result.coverage = 1.0; result.edge = 0.0;
        if (_DissolveSpatialMode > 1.5) return result;
        float distance = DissolveWorldDistance(positionWS);
        float pixelWidth = fwidth(distance) * max(_DissolveAAPixels, 0.0);
        result.coverage = DissolveCoverageFromDistance(distance, pixelWidth);
        result.edge = DissolveGlow(distance, pixelWidth);
        return result;
    }
    result.coverage = amount <= 0.0 ? 1.0 : 0.0; result.edge = 0.0;
    if (amount <= 0.0 || amount >= 1.0) return result;
    float field = DissolveField_Auto(positionWS, positionOS);
    float pixelWidth = fwidth(field) * max(_DissolveAAPixels, 0.0);
    result = DissolveEvaluate(field, amount, pixelWidth, DissolveGetMaterialSettings());
    // New controller widths use local length for Local Direction/Radial, threshold units for Noise.
    if (_DissolveEdgeUnits > 0.5)
    {
        float scale = _DissolveMode == 1.0 ? max(_DissolveAxis.w * 2.0, 1e-4)
                    : _DissolveMode == 2.0 ? max(_DissolveRadial.w, 1e-4) : 1.0;
        if (_DissolveMode == 1.0 || _DissolveMode == 2.0) scale *= 1.0 + 2.0 * saturate(_DissolveEdgeNoiseStrength);
        result.edge = DissolveGlow(DissolveDistance(field, amount) * scale, pixelWidth * scale);
    }
    return result;
}
float2 ComputeDissolveCoverageAndEdge(float3 positionWS, float3 positionOS, float amount)
{
    DissolveResult result = DissolveEvaluateMaterial(positionWS, positionOS, amount);
    return float2(result.coverage, result.edge);
}
half AlphaToCutoutCoverage(half alpha, half cutoff)
{
    half aa = max(fwidth(alpha), 1e-5);
    return saturate((alpha - cutoff) / aa + 0.5);
}
// Optional legacy dither helpers; transparent surfaces use continuous coverage instead.
float ComputeDissolveAlphaOnly(float3 positionWS, float3 positionOS, float2 screenPos, float amount)
{
    float coverage = ComputeDissolveCoverage(positionWS, positionOS, amount);
    if (coverage <= 0.0) return 0.0;
    return step(DissolveIGN(screenPos), coverage);
}
float2 ComputeDissolveAlphaAndEdge(float3 positionWS, float3 positionOS, float2 screenPos, float amount)
{
    float2 result = ComputeDissolveCoverageAndEdge(positionWS, positionOS, amount);
    float alpha = result.x <= 0.0 ? 0.0 : step(DissolveIGN(screenPos), result.x);
    return float2(alpha, result.y * alpha);
}
float ComputeDissolveShadowCoverage(float3 positionWS, float3 positionOS, float amount)
{
    if (DissolveUsesWorldGeometry())
    {
        if (_DissolveSpatialMode > 1.5) return 1.0;
        return step(0.0, DissolveWorldDistance(positionWS));
    }
    if (amount <= 0.0) return 1.0;
    if (amount >= 1.0) return 0.0;
    return DissolveShadowCoverage(DissolveField_Auto(positionWS, positionOS), amount, _DissolveCoverageWidth);
}
half ComputeDissolveBrightness(float amount)
{
    if (DissolveUsesWorldGeometry()) return 1.0;
    return DissolveBrightness(amount, _DissolveBrightnessFade, _DissolveBrightnessPower);
}
#endif
