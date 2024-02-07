#ifndef RAYMARCHING_TOOLS_INCLUDED
#define RAYMARCHING_TOOLS_INCLUDED

#include "../HLSL/SDFDistance.hlsl"

#define SURFANCE_DIS 1e-3
#define MAX_STEPS 68
#define MAX_DISTACNE 30
#define ZERO 0
#define PLANEHEIGHT 0.5



float3x3 Camera_matrix(float3 origin, float3 target)
{
    float3 forward = normalize(target - origin);
    float3 right = cross(forward,float3(0,1,0));
    float3 up = cross(right,forward);

    return float3x3(right,up,forward);
}




float2 RayMarching(float3 ro, float3 rd)
{
    float  d=0.0;
    float mat=0.0;

    for (int i = 0; i < MAX_STEPS; i++)
    {
        float3 p = ro + rd * d;

        float currentDistance =GetDistance_float(p).x;
        mat =GetDistance_float(p).y;

        if (abs(currentDistance) < SURFANCE_DIS || d > MAX_DISTACNE)
            break;

        d += currentDistance;
    }

    return  float2(d,mat);
}


float3 GetNormal(float3 p) // for function f(p)
{
    float h = 0.0001; // replace by an appropriate value
    float2 k = float2(1,-1);
    return normalize( k.xyy*GetDistance_float( p + k.xyy*h).x + 
                      k.yyx*GetDistance_float( p + k.yyx*h).x + 
                      k.yxy*GetDistance_float( p + k.yxy*h).x + 
                      k.xxx*GetDistance_float( p + k.xxx*h).x);
}

float Softshadow( float3 ro, float3 rd, float mint, float maxt, float w)
{
//w = solid angle
    float res = 1.0;
    float t = mint;
    for( int i=0; i<256 && t<maxt; i++ )
    {
        float h = GetDistance_float(ro + rd*t).x;
        res = min( res, h/(w*t) );
        t += clamp(h, 0.005, 0.50);
        if( res<-1.0 || t>maxt ) break;
    }
    res = max(res,-1.0);
    return 0.25*(1.0+res)*(1.0+res)*(2.0-res);
}

float CalcAO( float3 pos, float3 nor )
{
	float occ = 0.0;
    float sca = 1.0;
    for( int i=ZERO; i<5; i++ )
    {
        //沿着法线方向给定5个步长
        float h = 0.01 + 0.12*float(i)/4.0;
        //计算 这5个采样点的距离
        float d = GetDistance_float( pos + h*nor).x;
        occ += (h-d)*sca;
        sca *= 0.95;
        if( occ>0.35 ) break;
    }
    return clamp( 1.0 - 3.0*occ, 0.0, 1.0 ) * (0.5+0.5*nor.y);
}



#endif
