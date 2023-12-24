#ifndef PRIMITIVESDF_INCLUDED
#define PRIMITIVESDF_INCLUDED

#define vec3 float4
#define vec3 float3
#define vec2 float2
#define mix lerp


#include "BasicMathFun.hlsl"

// Signed

    // b.x = Width
    // b.y = Height
    // b.z = Depth
    // Leave r=0 if radius not needed
    // ========================================
    float sdBox(vec3 p, vec3 b, float r) {
        vec3 d = abs(p) - b;
        return min(maxcomp(d),0.0) - r + length(max(d,0.0));
        // Inlined maxcomp
        //return min(max(d.x,max(d.y,d.z)),0.0) - r + length(max(d,0.0));
    }

    // ========================================
    float sdCappedCylinder( vec3 p, vec2 h ) {
        vec2 d = abs(vec2(length(p.xz),p.y)) - h;
        return min(max(d.x,d.y),0.0) + length(max(d,0.0));
    }

    // ========================================
    float sdCapsule( vec3 p, vec3 a, vec3 b, float r ) {
        vec3 pa = p - a, ba = b - a;
        float h = clamp( dot(pa,ba) / dot(ba,ba), 0.0, 1.0 );
        return length( pa - ba*h ) - r;
    }

    // c.x Width
    // c.y Base Radius
    // c.z Depth
    // Note: c must be normalized
    // ========================================
    float sdCone( vec3 p, vec3 c) // TODO: do we need to use 'in' for all primitives?
    {
        // c.x = length
        // c.y = base radius
        //float q = length( p.xy );
        //return dot( c, vec2( q, p.z ) ); // BUG in iq's docs -- laying on side

        float q = length( p.xz );
        return dot( c.xy, vec2( q, p.y ) );

        // Alt. cone formula given in: ???
        //vec2 q = vec2( length( p.xz ), p.y );
        //float d1 = -p.y - c.z;
        //float d2 = max( dot(q,c.xy), p.y );
        //return length(max(vec2(d1,d2),0.0)) + min(max(d1,d2), 0.0);
    }

    // ========================================
    float sdCylinder( vec3 p, vec3 c ) {
        return length(p.xz - c.xy) - c.z;
    }

    // n.xyz = point on plane
    // n.w   = distance to plane
    // Note: N must be normalized!
    // ========================================
    float sdPlane( vec3 p, vec4 n ) {
        return dot( p, n.xyz ) + n.w;
    }

    // 4 sided pyramid
    // h.x = base X
    // h.y = height
    // h.z = base Z (usually same as h.x)
    // ========================================
    float sdPyramid4( vec3 p, vec3 h ) {
        p.xz = abs(p.xz);                   // Symmetrical about XY and ZY
        vec3 n = normalize(h);
        return sdPlane(p, vec4( n, 0.0 ) ); // cut off bottom
    }

    // ========================================
    float sdSphere( vec3 p, float r ) {
        return length(p) - r;
    }

    // ========================================
    float sdSphere2( vec3 p, float r ) {
        return abs(length(p) - r);
    }

    // ========================================
    float sdTorus( vec3 p, vec2 t ) {
        vec2 q = vec2(length(p.xy) - t.x, p.z);
        return length(q) - t.y;
    }

    // TODO: document/derive magic number 0.866025
    // ========================================
    float sdTriPrism( vec3 p, vec2 h ) {
        vec3 q = abs(p);
        return max(q.z-h.y,max(q.x*0.866025+p.y*0.5,-p.y)-h.x*0.5);
    }

// Unsigned

    // Box
    // ========================================
    float udBox( vec3 p, vec3 b ) {
        return length( max( abs(p) - b, 0.0 ) );
    }

    // Round Box
    // ========================================
    float udRoundBox(vec3 p, vec3 b, float r)
    {
        return length(max(abs(p) - b, 0.0))- r;
    }


// __ Distance Operations _____________________________________

// Basic
    // Op Union
    // ========================================
    float opU( float d1, float d2 ) {
        return min( d1, d2 );
    }

    // Op Union
    // ========================================
    vec4 opU2( vec4 d1, vec4 d2 ) {
        return min( d1, d2 );
    }

    // Op Union
    // ========================================
    vec4 opU( vec4 a, vec4 b ) {
        return mix(a, b, step(b.x, a.x));
    }

    // Op Subtraction
    // ========================================
    float opS( float a, float b ) {
        return max( -b, a ); // BUG in iq's docs: -a, b
    }
    // Op Subtraction
    // ========================================
    vec4 opS( vec4 a, vec4 b ) {
        return max( -b, a );
    }

    // Op Intersection
    // ========================================
    float opI( float a, float b ) {
        return max( a, b );
    }

    // Op Intersection
    // ========================================
    vec4 opI( vec4 a, vec4 b ) {
        return max( a, b );
    }

// Advanced
    // ========================================
    float opBlend( float a, float b, float k ) {
        return sMin( a, b, k );
    }

    // a angle
    // ========================================
    float displacement( vec3 p, float a ) {
        return sin(a*p.x)*sin(a*p.y)*sin(a*p.z); // NOTE: Replace with your own!
    }

    // ========================================
    float opDisplace( vec3 p, float d1, float d2 ) {
        return d1 + d2;
    }

    // Op Union Translated
    // ========================================
    vec4 opUt( vec4 a, vec4 b, float fts ){
        vec4 vScaled = vec4(b.x * (fts * 2.0 - 1.0), b.yzw);
        return mix(a, vScaled, step(vScaled.x, a.x) * step(0.0, fts));
    }



#define kNt  -1.0 //no trans
#define kTt   1.0 //yes trans
#define kIt   0.0 //inverse trans

const float MATERIAL_1 = 1.0;
const float MATERIAL_2 = 2.0;
/* */ float gMaterial  = MATERIAL_1;

// TODO: Document these structure member fields!
// rd Ray Direction
// rl Ray Length
struct sRay   { vec3 ro ; vec3  rd ; float sd; float rl; };
struct sHit   { vec3 hp ; float hd ; vec3 oid; };
struct sSurf  { vec3 nor; vec3  ref; vec3 tra; };
struct sMat   { vec3 ctc; float frs; float smt; vec2 par; float trs; float fri; };
struct sShade { vec3 dfs; vec3  spc; };
struct sLight { vec3 rd ; vec3  col; };


#endif