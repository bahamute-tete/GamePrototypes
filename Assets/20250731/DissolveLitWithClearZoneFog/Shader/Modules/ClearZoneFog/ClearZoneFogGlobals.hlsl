#ifndef LZ_CLEAR_ZONE_FOG_GLOBALS_INCLUDED
#define LZ_CLEAR_ZONE_FOG_GLOBALS_INCLUDED
#include "ClearZoneFogCore.hlsl"
// Controller-owned global ABI. Do not place these in UnityPerMaterial.
#define SF_MAX_VOLUMES 8
float _SF_Active;
float _SF_Weight;
int _SF_VolumeCount;
float4 _SF_CenterRadius[SF_MAX_VOLUMES];
float4 _SF_AxisX[SF_MAX_VOLUMES];
float4 _SF_AxisY[SF_MAX_VOLUMES];
float4 _SF_AxisZ[SF_MAX_VOLUMES];
float _SF_Smoothness;
float _SF_SmoothUnionK;
float _SF_Density;
float4 _SF_FogColor;


float ClearZoneFog_Distance(float3 worldPos)
{
    float distance = 1e20;
    [loop] for (int i = 0; i < min(_SF_VolumeCount, SF_MAX_VOLUMES); i++)
    {
        float4 center = _SF_CenterRadius[i];
        float3 delta = worldPos - center.xyz;
        float d;
        if (center.w >= 0.0)
            d = ClearZoneFog_SphereDistance(delta, center.w);
        else
            d = ClearZoneFog_BoxDistance(delta, _SF_AxisX[i], _SF_AxisY[i], _SF_AxisZ[i]);
        distance = ClearZoneFog_SmoothMin(distance, d, _SF_SmoothUnionK);
    }
    return distance;
}
float ClearZoneFog_GetFactor(float3 worldPos)
{
    if (_SF_Active < 0.5 || _SF_VolumeCount <= 0) return 0.0;
    return ClearZoneFog_DistanceToFactor(ClearZoneFog_Distance(worldPos), _SF_Smoothness, _SF_Density);
}
// Shape-only application for sky: intentionally no Unity distance fog fallback.
float3 ClearZoneFog_Apply(float3 color, float3 worldPos)
{
    return lerp(color, _SF_FogColor.rgb, ClearZoneFog_GetFactor(worldPos) * saturate(_SF_Weight));
}
#endif
