
#include "RayMarchingSDF.hlsl"

float3 GetNormalOld(float3 p) 
{
    float d = GetDist(p);
    float2 e = float2(0.001, 0);
    float3 n = d - float3(
        GetDist(p - e.xyy),
        GetDist(p - e.yxy),
        GetDist(p - e.yyx)
    );
    return normalize(n);
}

float3 GetNormal(float3 p) 
{
    float h = 0.0001; 
    float2 k = float2(1,-1);
    return normalize( k.xyy*GetDist( p + k.xyy*h)+ 
                      k.yyx*GetDist( p + k.yyx*h)+ 
                      k.yxy*GetDist( p + k.yxy*h)+ 
                      k.xxx*GetDist( p + k.xxx*h));
}

float3 GetNormalAdaptive(float3 p, float t)
{
    float h; 
    #if defined(_USE_3D_TEXTURE)
        h = 0.0005;
    #else
    //用这个采样3D纹理会导致采样点跨度过大,值不连续
    // 根据到相机距离调整
         h = 0.0001*t;
    #endif
   

    float2 k = float2(1,-1);
    
    return normalize( k.xyy*GetDist( p + k.xyy*h ) + 
                      k.yyx*GetDist( p + k.yyx*h ) + 
                      k.yxy*GetDist( p + k.yxy*h ) + 
                      k.xxx*GetDist( p + k.xxx*h ) );
}

float3 GetNormalFromTexture3D(float3 p, float scale)
{
    float s = scale;
    float3 uvw = (p / s) * 0.5 + 0.5;
    uvw = saturate(uvw);
    
    // 在纹理空间中计算梯度,使用纹理像素大小作为步长
    float texelSize = 1.0 / 64.0;
    
    float3 gradient;
    gradient.x = SAMPLE_TEXTURE3D_LOD(_SDFTexture, sampler_SDFTexture, uvw + float3(texelSize, 0, 0), 0).r
               - SAMPLE_TEXTURE3D_LOD(_SDFTexture, sampler_SDFTexture, uvw - float3(texelSize, 0, 0), 0).r;
    gradient.y = SAMPLE_TEXTURE3D_LOD(_SDFTexture, sampler_SDFTexture, uvw + float3(0, texelSize, 0), 0).r
               - SAMPLE_TEXTURE3D_LOD(_SDFTexture, sampler_SDFTexture, uvw - float3(0, texelSize, 0), 0).r;
    gradient.z = SAMPLE_TEXTURE3D_LOD(_SDFTexture, sampler_SDFTexture, uvw + float3(0, 0, texelSize), 0).r
               - SAMPLE_TEXTURE3D_LOD(_SDFTexture, sampler_SDFTexture, uvw - float3(0, 0, texelSize), 0).r;
    
    return normalize(-gradient); // 负号因为梯度指向距离增加方向
}

float RayMarchV1(float3 ro, float3 rd) 
{
    float dO = _CamParams.z; 
    float maxDist = RAYMARCHING_MAX_DISTANCE; 

    [loop]
    for(int i=0; i<RAYMARCHING_MAX_STEPS; i++) 
    {
        float3 p = ro + rd * dO;
        float dS = GetDist(p); 
        dO += dS;
        if(dO > maxDist || abs(dS) < SURFANCE_DISTANCE) break;
    }
    
    return dO;
}

float RayMarch(float3 ro, float3 rd) 
{
    float dO = _CamParams.z; 
    float maxDist = RAYMARCHING_MAX_DISTANCE; 

    float omega = 1.2; // 过度放松因子，可在 1.0-1.5 之间调整
    float candidate_error = SURFANCE_DISTANCE;
    float candidate_t = dO;
    float previousRadius = 0.0;
    float stepLength = 0.0;
    float functionSign = GetDist(ro) < 0.0 ? -1.0 : 1.0;

    [loop]
    for(int i=0; i<RAYMARCHING_MAX_STEPS; i++) 
    {
        float3 p = ro + rd * dO;
        float signedRadius = functionSign * GetDist(p);
        float radius = abs(signedRadius);
        
        bool sorFail = omega > 1.0 && (radius + previousRadius) < stepLength;
        
        if (sorFail) 
        {
            stepLength -= omega * stepLength;
            omega = 1.0;
        } 
        else 
        {
            stepLength = signedRadius * omega;
        }
        
        previousRadius = radius;
        float error = radius / dO;
        
        if (!sorFail && error < candidate_error) 
        {
            candidate_t = dO;
            candidate_error = error;
        }
        
        if (!sorFail && error < SURFANCE_DISTANCE || dO > maxDist) 
            break;
            
        dO += stepLength;
    }
    
    if ((dO > maxDist || candidate_error > SURFANCE_DISTANCE)) 
        return maxDist;
        
    return candidate_t;
}



float RayMarchV2(float3 ro, float3 rd) 
{
    float dO = 0.0;
    float lastD = 0.0;
    
    [loop]
    // 第一阶段：常规球体追踪
    for(int i=0; i<RAYMARCHING_MAX_STEPS; i++) 
    {
        float3 p = ro + rd * dO;
        float dS = GetDist(p);
        
        // 检测到穿过表面
        if(dS < 0.0 && i > 0) 
        {
            // 第二阶段：二分搜索
            float tMin = lastD;
            float tMax = dO;
            
            for(int j=0; j<8; j++) // 8次二分迭代
            {
                float tMid = (tMin + tMax) * 0.5;
                float3 pMid = ro + rd * tMid;
                float dMid = GetDist(pMid);
                
                if(dMid < 0.0)
                    tMax = tMid;
                else
                    tMin = tMid;
            }
            
            return (tMin + tMax) * 0.5;
        }
        
        lastD = dO;
        dO += abs(dS);
        
        if(dO > RAYMARCHING_MAX_DISTANCE || abs(dS) < SURFANCE_DISTANCE) 
            break;
    }
    
    return dO;
}


float2 RayMarchWithMat(float3 ro, float3 rd) 
{
    float dO = _CamParams.z; 
    float maxDist = RAYMARCHING_MAX_DISTANCE;

    float omega = 1.2; // 过度放松因子，可在 1.0-1.5 之间调整
    float candidate_error = SURFANCE_DISTANCE;
    float candidate_t = dO;
    float candidate_matID = -1.0; 

    float previousRadius = 0.0;
    float stepLength = 0.0;
    float functionSign = GetDist(ro) < 0.0 ? -1.0 : 1.0;

    [loop]
    for(int i=0; i<RAYMARCHING_MAX_STEPS; i++) 
    {
        float3 p = ro + rd * dO;
        float2 dAndM = functionSign * GetDistAndMat(p);
        float radius = abs(dAndM.x);
        
        bool sorFail = omega > 1.0 && (radius + previousRadius) < stepLength;
        
        if (sorFail) 
        {
            stepLength -= omega * stepLength;
            omega = 1.0;
        } 
        else 
        {
            stepLength = dAndM.x * omega;
        }
        
        previousRadius = radius;
        float error = radius / dO;
        
        if (!sorFail && error < candidate_error) 
        {
            candidate_t = dO;
            candidate_error = error;
            candidate_matID = dAndM.y;
        }
        
        if (!sorFail && error < SURFANCE_DISTANCE || dO > maxDist) 
            break;
            
        dO += stepLength;
       
    }
    
    if ((dO > maxDist || candidate_error > SURFANCE_DISTANCE)) 
        return float2(maxDist, 0.0);
        
    return float2(candidate_t, candidate_matID);
}



