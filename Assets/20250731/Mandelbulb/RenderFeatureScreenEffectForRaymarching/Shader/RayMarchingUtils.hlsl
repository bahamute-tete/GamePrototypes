

#define pi 3.14159265359

float3 SampleSH(float3 normal)
{
    float4 n = float4(normal, 1.0);
    
    // Linear + Constant
    float3 x1;
    x1.r = dot(_SHAr, n);
    x1.g = dot(_SHAg, n);
    x1.b = dot(_SHAb, n);
    
    // Quadratic polynomials
    // x*y, y*z,z*z,z*x
    float4 vB = n.xyzz * n.yzzx;
    float3 x2;
    x2.r = dot(_SHBr, vB);
    x2.g = dot(_SHBg, vB);
    x2.b = dot(_SHBb, vB);
    
    // Final Quadratic term
    float vC = n.x*n.x - n.y*n.y;
    float3 x3 = _SHC.rgb * vC;
    
    return max(0, x1 + x2 + x3); 
}


float2x2 Rot(float radangel)
{
    float s = sin(radangel);
    float c = cos(radangel);
    return float2x2(c, -s, s, c);
}

float2 Rotate2D(float2 uv, float angle)
{
    float s = sin(angle);
    float c = cos(angle);
    return float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);
}

float remap(float v, float minOld, float maxOld, float minNew, float maxNew)
{
    return minNew + (v - minOld) * (maxNew - minNew) / (maxOld - minOld);
}

float3 Reflect(float3 incident, float3 normal)
{
    return incident - 2.0 * dot(incident, normal) * normal;
}

float smin(float a, float b, float k)
{
    // 计算混合因子 h，限制在 [0, 1] 之间
    float h = clamp(0.5+0.5*(b-a)/k, 0, 1);
     // 线性插值减去一个二次修正项
    return lerp(b, a, h) - k*h*(1-h);
}

float ssub(float d1, float d2, float k) 
{
    float h = clamp(0.5 - 0.5 * (d2 + d1) / k, 0.0, 1.0);
    return lerp(d2, -d1, h) + k * h * (1.0 - h);
}


float smax(float a, float b, float k) 
{
    float h = clamp(0.5 - 0.5 * (b - a) / k, 0.0, 1.0);
    return lerp(b, a, h) + k * h * (1.0 - h);
}

float opDilate(float d, float r)
{
    return d - r;
}

float opErosion(float d, float r)
{
    return d + r;
}

float2 opUnion(float2 d1, float2 d2)
{
    return (d1.x < d2.x) ? d1 : d2; 
}



float2 BoxIntersection(float3 ro, float3 rd, float3 boxSize)
{
    float3 m = 1.0 / rd;
    float3 n = m * ro;
    float3 k = abs(m) * boxSize;
    float3 t1 = -n - k;
    float3 t2 = -n + k;
    float tN = max(max(t1.x, t1.y), t1.z);
    float tF = min(min(t2.x, t2.y), t2.z);
    return float2(tN, tF);
}


float hash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}


float noise3D(float3 x)
{
    float3 p = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    
    return lerp(
        lerp(lerp(hash(p + float3(0,0,0)), hash(p + float3(1,0,0)), f.x),
             lerp(hash(p + float3(0,1,0)), hash(p + float3(1,1,0)), f.x), f.y),
        lerp(lerp(hash(p + float3(0,0,1)), hash(p + float3(1,0,1)), f.x),
             lerp(hash(p + float3(0,1,1)), hash(p + float3(1,1,1)), f.x), f.y),
        f.z);
}

// octaves: 3-4 (过多会太嘈杂)
// lacunarity: 2.0-2.5 (频率增长,控制细节分布)
// gain: 0.4-0.6 (振幅衰减,控制细节强度)
float fbm(float3 p, int octaves, float lacunarity = 2.0, float gain = 0.5)
{
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;
    
    for(int i = 0; i < octaves; i++)
    {
        value += amplitude * noise3D(p * frequency);
        frequency *= lacunarity;  
        amplitude *= gain;        
    }
    
    return value;
}









