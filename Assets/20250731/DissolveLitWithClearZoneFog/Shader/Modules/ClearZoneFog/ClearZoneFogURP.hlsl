#ifndef LZ_CLEAR_ZONE_FOG_URP_INCLUDED
#define LZ_CLEAR_ZONE_FOG_URP_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "ClearZoneFogGlobals.hlsl"

// Called from fragment shaders with the interpolated world position.
float ClearZoneFog_UnityFactor(float3 worldPos)
{
    #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
        float viewZ = -TransformWorldToView(worldPos).z;
        float factor = ComputeFogFactorZ0ToFar(max(viewZ - _ProjectionParams.y, 0.0));
        return saturate(1.0 - ComputeFogIntensity(factor));
    #else
        return 0.0;
    #endif
}

// Equivalent to blending two fogged versions of the same original surface color.
// Weight the fog color by its contribution, not just by the handover weight.
void ClearZoneFog_Resolve(float3 worldPos, bool allowShape, out float factor, out float3 fogColor)
{
    float weight = allowShape && _SF_Active > 0.5 ? saturate(_SF_Weight) : 0.0;
    if (weight >= 1.0)
    {
        factor = ClearZoneFog_GetFactor(worldPos);
        fogColor = _SF_FogColor.rgb;
        return;
    }
    float unityFactor = ClearZoneFog_UnityFactor(worldPos);
    if (weight <= 0.0)
    {
        factor = unityFactor;
        fogColor = unity_FogColor.rgb;
        return;
    }
    float shapeContribution = weight * ClearZoneFog_GetFactor(worldPos);
    float unityContribution = (1.0 - weight) * unityFactor;
    factor = shapeContribution + unityContribution;
    fogColor = factor > 0.0
        ? (shapeContribution * _SF_FogColor.rgb + unityContribution * unity_FogColor.rgb) / factor
        : unity_FogColor.rgb;
}
#endif
