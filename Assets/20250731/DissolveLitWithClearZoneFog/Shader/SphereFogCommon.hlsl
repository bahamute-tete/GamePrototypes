// Compatibility entry point for existing sky and surface shaders.
#ifndef CLEAR_ZONE_FOG_COMMON_INCLUDED
#define CLEAR_ZONE_FOG_COMMON_INCLUDED
#include "Modules/ClearZoneFog/ClearZoneFogGlobals.hlsl"
float SphereFog_SmoothMin(float a, float b, float k) { return ClearZoneFog_SmoothMin(a, b, k); }
float SphereFog_SDF(float3 worldPos) { return ClearZoneFog_Distance(worldPos); }
float SphereFog_GetFactor(float3 worldPos) { return ClearZoneFog_GetFactor(worldPos); }
float3 SphereFog_Apply(float3 color, float3 worldPos) { return ClearZoneFog_Apply(color, worldPos); }
#endif
