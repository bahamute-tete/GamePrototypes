#ifndef RAYMARCHING_MATHTOOLS_INCLUDED
#define RAYMARCHING_MATHTOOLS_INCLUDED

#include "../HLSL/RandomTools.hlsl"

float2x2 Rot(float angel)
{
    float s = sin(angel);
    float c = cos(angel);
    return float2x2(c, -s, s, c);
}
//==============================================================================
float smin(float a, float b, float k)
{
    float h = clamp(0.5+0.5*(b-a)/k, 0, 1);
    return lerp(b, a, h) - k*h*(1-h);
}

float opUnion( float d1, float d2 )
{
    return min(d1,d2);
}
float opSubtraction( float d1, float d2 )
{
    return max(-d1,d2);
}
float opIntersection( float d1, float d2 )
{
    return max(d1,d2);
}
float opXor(float d1, float d2 )
{
    return max(min(d1,d2),-max(d1,d2));
}

float opSmoothUnion( float d1, float d2, float k )
{
    float h = clamp( 0.5 + 0.5*(d2-d1)/k, 0.0, 1.0 );
    return lerp( d2, d1, h ) - k*h*(1.0-h);
}

float opSmoothSubtraction( float d1, float d2, float k )
{
    float h = clamp( 0.5 - 0.5*(d2+d1)/k, 0.0, 1.0 );
    return lerp( d2, -d1, h ) + k*h*(1.0-h);
}

float opSmoothIntersection( float d1, float d2, float k )
{
    float h = clamp( 0.5 - 0.5*(d2-d1)/k, 0.0, 1.0 );
    return lerp( d2, d1, h ) + k*h*(1.0-h);
}

//==============================================================================

float2 revolutionXZ(float3 p,float r )
{
    //r = distance to center
    float2 q = float2(length(p.xz)-r, p.y);
    return q;
}

float extrusionZ(float3 p,float d_sdf2d,float h)
{
    //d_sdf2d= SDF2D distance 
    //h= extrusion height
    float2 w =float2(d_sdf2d,abs(p.z)-h);
    return min(max(w.x,w.y),0.0) + length(max(w,0.0));
}

float3 elongate(  float3 p, float3 h )
{
    float3 q = p - clamp( p, -h, h );
    return q;
}

float3 elongate2(  float3 p, float3 h )
{
    float3 q = abs(p)-h;
    //use in SDF3D function
    //primitive( max(q,0.0) ) + min(max(q.x,max(q.y,q.z)),0.0);
    return q;
}
//==========================Infinite and limited Repetition===========================================
float3 opRepetition(  float3 p,  float3 s )
{
    float3 id = round(p/s);
    float3 o = sign(p-s*id);//neighbor offset direction

    //Space range [(-s,-s,-s)(s,s,s)]
    //s is the spacing between the instances
    float3 q = p -s*id;

    

    float3 r=0;
    for( int z=0; z<2; z++ )
    {
         for( int y=0; y<2; y++ )
         {
            for( int x=0; x<2; x++ )
            {
                float3 rid = id + float3(x,y,z)*o;
                r = p-rid*s;
            }
         }
    }
        
    return q ;
}

float3 opLimitedRepetition(  float3 p,  float3 s,float3 l )
{
    //Space range [(-l,-l,-l)(l,l,l)]
    //s is the spacing between the instances
    float3 q = p - s*clamp(round(p/s),-l,l);
    return q;
}

//==================================Twist=====================================
float3 opTwistXZ( in float3 p,float k )
{
    float c = cos(k*p.y);
    float s = sin(k*p.y);
    float2x2  m = float2x2(c,-s,s,c);
    float3  q = float3(mul(m,p.xz),p.y);
    return q;
}

//==================================Bend=====================================
float3 opCheapBendXZ( in float3 p,float k )
{
    float c = cos(k*p.x);
    float s = sin(k*p.x);
    float2x2  m = float2x2(c,-s,s,c);
    float3  q = float3(mul(m,p.xy),p.z);
    return q;
}

//==============================================================================
float remap(float value, float inputMin, float inputMax, float outputMin, float outputMax)
{
    return (value - inputMin) * (outputMax - outputMin) / (inputMax - inputMin) + outputMin;
}

//==============================================================================

float checkersGradBox( float2 p, float2 ddx, float2 ddy )
{
    // filter kernel
    float2 w = abs(ddx)+abs(ddy) + 0.001;
    // analytical integral (box filter)
    float2 i = 2.0*(abs(frac((p-0.5*w)*0.5)-0.5)-abs(frac((p+0.5*w)*0.5)-0.5))/w;
    // xor pattern
    return 0.5 - 0.5*i.x*i.y;                  
}

 float3 ACESToneMapping(float3 x)
{
    float a = 2.51f;
    float b = 0.03f;
    float c = 2.43f;
    float d = 0.59f;
    float e = 0.14f;
    return saturate((x*(a*x+b))/(x*(c*x+d)+e));
}

//==============================================================================

float2 RayBoxDst(float3 boxMin, float3 boxMax, float3 pos, float3 rayDir)
{
    float3 t0 = (boxMin - pos) / rayDir;
    float3 t1 = (boxMax - pos) / rayDir;
    
    float3 tmin = min(t0, t1);
    float3 tmax = max(t0, t1);
    
    //dstA =near distance 
    //dstB = far distance
    float dstA = max(max(tmin.x, tmin.y), tmin.z);
    float dstB = min(min(tmax.x, tmax.y), tmax.z);
    
    float dstToBox = max(0, dstA);
    float dstInBox = max(0, dstB - dstToBox);
    
    //x=the distance that ray enter box
    //y=the distance that ray after enter box and exit box
    return float2(dstToBox, dstInBox);
}

#endif
