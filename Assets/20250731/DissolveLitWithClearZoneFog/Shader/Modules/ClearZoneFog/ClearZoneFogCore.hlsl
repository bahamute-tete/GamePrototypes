#ifndef LZ_CLEAR_ZONE_FOG_CORE_INCLUDED
#define LZ_CLEAR_ZONE_FOG_CORE_INCLUDED
// Pure math. All distances, smoothing widths and radii are world-space units.
float ClearZoneFog_SmoothMin(float a, float b, float k)
{
    if (k <= 1e-5) return min(a, b);
    float h = max(k - abs(a - b), 0.0) / k;
    return min(a, b) - h * h * k * 0.25;
}
float ClearZoneFog_SphereDistance(float3 delta, float radius)
{
    return length(delta) - radius;
}
// Axis xyz must be orthonormal world-space directions; w stores each half extent.
float ClearZoneFog_BoxDistance(float3 delta, float4 axisX, float4 axisY, float4 axisZ)
{
    float3 local = float3(dot(delta, axisX.xyz), dot(delta, axisY.xyz), dot(delta, axisZ.xyz));
    float3 q = abs(local) - float3(axisX.w, axisY.w, axisZ.w);
    return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
}
float ClearZoneFog_DistanceToFactor(float distance, float smoothness, float density)
{
    float halfWidth = max(smoothness * 0.5, 1e-4);
    return saturate(smoothstep(-halfWidth, halfWidth, distance) * density);
}
#endif
