#ifndef RAYMARCHING_RmixAYMARCHINGSHAPE_INCLUDED
#define RAYMARCHING_RAYMARCHINGSHAPE_INCLUDED


#include"../HLSL/MathTools.hlsl"
//==============================================================================

//==============================================================================
float SDFSphere(float3 p,float radius)
{
    return length(p) - radius;
}

float SDFBox(float3 p, float3 boxSize, float radius)
{
    float3 d = abs(p) -boxSize;
    return length(max(d,0)) - radius + min(max(d.x,max(d.y,d.z)),0.0);
}

float SDFPlane(float3 p, float3 n)
{
    return dot(p,normalize(n));
}

float SDHexPrism( float3 p, float2 h )
{
  const float3 k = float3(-0.8660254, 0.5, 0.57735);
  p = abs(p);
  p.xy -= 2.0*min(dot(k.xy, p.xy), 0.0)*k.xy;
  float2 d = float2(
       length(p.xy-float2(clamp(p.x,-k.z*h.x,k.z*h.x), h.x))*sign(p.y-h.x),
       p.z-h.y );
  return min(max(d.x,d.y),0.0) + length(max(d,0.0));
}

float SDFTorus( float3 p, float2 t )
{
  float2 q = float2(length(p.xz)-t.x,p.y);
  return length(q)-t.y;
}

float SDFCapsule( float3 p, float3 a, float3 b, float r )
{
  float3 pa = p - a, ba = b - a;
  float h = clamp( dot(pa,ba)/dot(ba,ba), 0.0, 1.0 );
  return length( pa - ba*h ) - r;
}

float SDFCappedCylinder( float3 p, float h, float r )
{
  float2 d = abs(float2(length(p.xz),p.y)) - float2(r,h);
  return min(max(d.x,d.y),0.0) + length(max(d,0.0));
}

float SDFCutSphere( float3 p, float r, float h )
{
  // sampling independent computations (only depend on shape)
  float w = sqrt(r*r-h*h);

  // sampling dependant computations
  float2 q = float2( length(p.xz), p.y );
  float s = max( (h-r)*q.x*q.x+w*w*(h+r-2.0*q.y), h*q.x-w*q.y );
  return (s<0.0) ? length(q)-r :
         (q.x<w) ? h - q.y     :
                   length(q-float2(w,h));
}

float SDFCutHollowSphere( float3 p, float r, float h, float t )
{
  // sampling independent computations (only depend on shape)
  float w = sqrt(r*r-h*h);
  
  // sampling dependant computations
  float2 q = float2( length(p.xz), p.y );
  return ((h*q.x<w*q.y) ? length(q-float2(w,h)) : 
                          abs(length(q)-r) ) - t;
}

float SDFEllipsoid( float3 p, float3 r )
{
  float k0 = length(p/r);
  float k1 = length(p/(r*r));
  return k0*(k0-1.0)/k1;
}
//==============================================================================

//==============================================================================
float SDFBallGyroid(float3 p)
{

    p.xz = mul(Rot(_Time.y),p.xz);
    //空间缩小
     p*=8;
    float g =0.7* dot(sin(p),cos(p.yzx));
    //距离也要跟着缩小
    //abs反转内部的面，添加一个厚度
    float gyroid = abs(g/8.0)-0.035;

     float sphere =SDFSphere(p/8.0-float3(0,0.0,0),1.0);
     sphere = abs(sphere)-0.035;//反转内部 并添加一个厚度，球壳

    float ball= smin(sphere,gyroid,-0.02);//boolean intersection

    return ball ;
}
//==============================================================================
//t=-dot(N,ro)/dot(N,rd)
float3 RayPlane(float3 ro,float3 rd,float3 p ,float3 n)
{
    // make a plane cross the sphere
    float t =max(0,-dot(p,n)/dot(rd,n));
    return ro+rd*t;
}

#endif
