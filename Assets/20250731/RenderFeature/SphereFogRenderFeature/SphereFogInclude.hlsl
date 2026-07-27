#ifndef SPHERE_FOG_INCLUDE
#define SPHERE_FOG_INCLUDE

// 这些是 RenderPass 通过 SetGlobal 写入的全局 uniform
// 前缀 _SF_ 避免和后处理 Shader 的 local uniform 冲突
float _SF_FogShape;
float3 _SF_FogCenter;
float _SF_SphereRadius;
float4 _SF_BoxAxisX;
float4 _SF_BoxAxisY;
float4 _SF_BoxAxisZ;
float _SF_Smoothness;
float _SF_Density;
float4 _SF_FogColor;
float _SF_NoiseScale;
float _SF_NoiseStrength;
float3 _SF_NoiseSpeed;

float SphereFog_SDF(float3 worldPos)
{
    if (_SF_FogShape < 0.5)
    {
        return length(worldPos - _SF_FogCenter) - _SF_SphereRadius;
    }
    else
    {
        float3 d = worldPos - _SF_FogCenter;
        float3 local = float3(
            dot(d, _SF_BoxAxisX.xyz),
            dot(d, _SF_BoxAxisY.xyz),
            dot(d, _SF_BoxAxisZ.xyz));
        float3 halfExt = float3(_SF_BoxAxisX.w, _SF_BoxAxisY.w, _SF_BoxAxisZ.w);
        float3 q = abs(local) - halfExt;
        return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
    }
}

/// 输入世界坐标，返回雾因子 (0 = 清晰, 1 = 全雾)
float SphereFog_GetFactor(float3 worldPos)
{
    float sdf = SphereFog_SDF(worldPos);
    float halfS = max(_SF_Smoothness * 0.5, 1e-4);
    float fog = smoothstep(-halfS, halfS, sdf) * _SF_Density;
    return saturate(fog);
}

/// 便捷函数：直接返回混合后的颜色
float3 SphereFog_Apply(float3 sceneColor, float3 worldPos)
{
    float fog = SphereFog_GetFactor(worldPos);
    return lerp(sceneColor, _SF_FogColor.rgb, fog);
}

#endif