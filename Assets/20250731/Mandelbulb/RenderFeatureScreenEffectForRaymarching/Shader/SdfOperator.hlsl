
float opExtrusion( in float3 p, in float sdf, in float h )
{
    float2 w = float2( sdf, abs(p.z) - h );
  	return min(max(w.x,w.y),0.0) + length(max(w,0.0));
}

// float opRevolution( in vec3 p, in sdf2d primitive, in float o )
// {
//     vec2 q = vec2( length(p.xz) - o, p.y );
//     return primitive(q)
// }
float2 opRevolution( in float3 p, float w )
{
    return float2( length(p.xz) - w, p.y );
}

// float opElongate( in sdf3d primitive, in vec3 p, in vec3 h )
// {
//     vec3 q = p - clamp( p, -h, h );
//     return primitive( q );
// }
float opElongate( in float3 p, in float3 h )
{
    
    return float3(p - clamp( p, -h, h ));
}

// float opElongate( in sdf3d primitive, in vec3 p, in vec3 h )
// {
//     vec3 q = abs(p)-h;
//     return primitive( max(q,0.0) ) + min(max(q.x,max(q.y,q.z)),0.0);
// }
float opElongate11( in float3 p, in float3 h )
{
    float3 q = abs(p)-h;
    return max((q,0.0) ) + min(max(q.x,max(q.y,q.z)),0.0);
}

// float opRound( in sdf3d primitive, in float rad )
// {
//     return primitive(p) - rad
// }

float opOnion( in float sdf, in float thickness )
{
    return abs(sdf)-thickness;
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
float opXor( float d1, float d2 )
{
    return max(min(d1,d2),-max(d1,d2));
}

float opSmoothUnion( float d1, float d2, float k )
{
    k *= 4.0;
    float h = max(k-abs(d1-d2),0.0);
    return min(d1, d2) - h*h*0.25/k;
}

float opSmoothSubtraction( float d1, float d2, float k )
{
    return -opSmoothUnion(d1,-d2,k);

    // k *= 4.0;
    // float h = max(k-abs(-d1-d2),0.0);
    // return max(-d1, d2) + h*h*0.25/k;
}

float opSmoothIntersection( float d1, float d2, float k )
{
    return -opSmoothUnion(-d1,-d2,k);

    // k *= 4.0;
    // float h = max(k-abs(d1-d2),0.0);
    // return max(d1, d2) + h*h*0.25/k;
}

// float3 opTx( in float3 p, in transform t, in sdf3d primitive )
// {
//     return primitive( invert(t)*p );
// }

// float opScale( in float3 p, in float s, in sdf3d primitive )
// {
//     return primitive(p/s)*s;
// }

// float opSymX( in float3 p, in sdf3d primitive )
// {
//     p.x = abs(p.x);
//     return primitive(p);
// }

// float opSymXZ( in float3 p, in sdf3d primitive )
// {
//     p.xz = abs(p.xz);
//     return primitive(p);
// }

// float opRepetition( in float3 p, in float3 s, in sdf3d primitive )
// {
//     float3 q = p - s*round(p/s);
//     return primitive( q );
// }

// float3 opLimitedRepetition( in float3 p, in float s, in float3 l, in sdf3d primitive )
// {
//     float3 q = p - s*clamp(round(p/s),-l,l);
//     return primitive( q );
// }

// float opDisplace( in sdf3d primitive, in float3 p )
// {
//     float d1 = primitive(p);
//     float d2 = displacement(p);
//     return d1+d2;
// }

// float opTwist( in sdf3d primitive, in float3 p )
// {
//     const float k = 10.0; // or some other amount
//     float c = cos(k*p.y);
//     float s = sin(k*p.y);
//     mat2  m = mat2(c,-s,s,c);
//     float3  q = float3(m*p.xz,p.y);
//     return primitive(q);
// }

// float opCheapBend( in sdf3d primitive, in float3 p )
// {
//     const float k = 10.0; // or some other amount
//     float c = cos(k*p.x);
//     float s = sin(k*p.x);
//     mat2  m = mat2(c,-s,s,c);
//     float3  q = float3(m*p.xy,p.z);
//     return primitive(q);
// }