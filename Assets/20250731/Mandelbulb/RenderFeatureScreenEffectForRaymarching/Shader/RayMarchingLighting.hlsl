
#include "RayMarchingSDFRender.hlsl"



float CalcAO( float3 pos, float3 nor )
{
    float occ = 0.0;
    float sca = 1.0;
    for( int i=0; i<5; i++ )
    {
    //沿着法线方向给定5个步长
    float h = 0.01 + 0.12*float(i)/4.0;
    //计算 这5个采样点的距离
    float d = GetDist( pos + h*nor);
    //如果 d很大（采样点周围很空旷），则 (h - d)会是一个负值或很小的正值，遮蔽贡献小。
    //如果 d很小（采样点紧挨着其他几何体），甚至为负（采样点位于几何体内部），则 (h - d)会是一个较大的正值，表示该处被严重遮蔽
    occ += (h-d)*sca;
    sca *= 0.95;
    if( occ>0.35 ) break;
    }
    return clamp( 1.0 - _AOIntensity*occ, 0.0, 1.0 ) * (0.5+0.5*nor.y);
}

float Softshadow2( float3 ro, float3 rd, float mint, float maxt, float w)
{
    //w = solid angle 
    float res = 1.0;
    float t = mint;
    for( int i=0; i<SHADOW_MAX_STEPS && t<maxt; i++ )
    {
        float h = GetDist(ro + rd*t);
        //shadow & proportional; closest_miss / distance_to_closest_miss
        //h is the closest_miss
        //sphere radius = h when viewed from distance t
        //so angular size = h / t
        res = min( res, w*(h/t) );
        t += clamp(h, 0.01, 0.20);//clamp确保了步进在安全范围内
        if( res<-1.0 || t>maxt ) break;
    }
    res = max(res,-1.0);
    return 0.25*(1.0+res)*(1.0+res)*(2.0-res);//smoothstep
}


float Softshadow(float3 ro, float3 rd, float mint, float maxt, float k,int maxSteps=SHADOW_MAX_STEPS) {
    float res = 1.0; 
    float t = mint;  
    float ph = 1e20; // 前一次步进的SDF值，初始设为一个很大的数


    for(int i = 0; i < maxSteps; i++) {
        if(t >= maxt) break;

        float3 p = ro + rd * t;
        float h = GetDist(p);

        if(h < SURFANCE_DISTANCE) {
            return 0.0;
        }
        // 利用前一次和当前的SDF值估算射线到表面的最近距离
        float y = h * h / (2.0 * ph);
        float d = sqrt(h * h - y * y);
        // 计算阴影系数，k控制光源大小（柔和度）
        res = min(res, k * d / max(0.0, t - y));
      
        ph = h;
        t += h;

     
        if(res < 0.001) break;
    }
   
    return clamp(res, 0.0, 1.0);
}


float3 TraceReflection(float3 ro, float3 rd, int maxBounces)
{
    float3 color = float3(0, 0, 0);
    float3 reflectivity = float3(1, 1, 1); // 累积的反射系数
    
    for(int bounce = 0; bounce < maxBounces; bounce++)
    {
        float d = RayMarch(ro, rd);
        
        if(d >= RAYMARCHING_MAX_DISTANCE)
        {
            color += reflectivity * float3(0.5, 0.7, 1.0); 
            break;
        }
        
        float3 p = ro + rd * d;
        float3 n = GetNormalAdaptive(p, d);
        
        // 计算该点的光照（简化版，完整版在fragment shader中）
        float3 lightDir = normalize(_LightDirection.xyz);
        float diff = max(0, dot(n, lightDir));
        float3 localColor = diff * _LightColor.rgb;
        
        // // 应用AO和阴影
        // float ao = CalcAO(p, n);
        // float shadow = Softshadow(p + n * 0.001, lightDir, 0.02, 10.0, 0.1);
        // localColor *= ao * shadow;
        
        // 累积颜色
        color += reflectivity * localColor;
        
        // 材质反射率
        float materialReflectivity = 0.3; // 30%反射率
        reflectivity *= materialReflectivity;
        
        // 能量衰减判断
        if(length(reflectivity) < 0.01)
            break;
        
        // 计算反射方向
        rd = Reflect(rd, n);
        ro = p + n * 0.001; // 偏移避免自相交
    }
    
    return color;
}

float3 SimpleReflection(float3 ro, float3 rd, float3 p, float3 n, float reflectivity)
{
    // 计算反射方向
    float3 reflectDir = Reflect(rd, n);
    
    // 追踪反射光线
    float d = RayMarch(p + n * 0.001, reflectDir);
    
    if(d >= RAYMARCHING_MAX_DISTANCE)
    {
        // 返回天空颜色
        return float3(0.5, 0.7, 1.0) * reflectivity;
    }
    
    // 计算反射击中点的颜色
    float3 reflectHitPos = p + reflectDir * d;
    float3 reflectNormal = GetNormalAdaptive(reflectHitPos, d);
    
    // 简单光照计算
    float3 lightDir = normalize(_LightDirection.xyz);
    float diff = max(0, dot(reflectNormal, lightDir));
    float3 reflectColor = diff * _LightColor.rgb;
    
    // 应用AO
    // float ao = CalcAO(reflectHitPos, reflectNormal);
    // reflectColor *= ao;
    
    return reflectColor * reflectivity;
}

float Fresnel(float3 viewDir, float3 normal, float F0)
{
    float cosTheta = saturate(dot(-viewDir, normal));
    return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
}

float3 ReflectionWithFresnel(float3 ro, float3 rd, float3 hitPos, float3 normal)
{
    // 计算菲涅尔反射率（基础反射率取0.04，适合非金属材质）
    float fresnel = Fresnel(rd, normal, 0.04);
    return SimpleReflection(ro, rd, hitPos, normal, fresnel);
}


