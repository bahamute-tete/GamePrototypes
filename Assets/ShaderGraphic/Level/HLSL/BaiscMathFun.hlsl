#ifndef BASICMATHFUN_INCLUDED
#define BASICMATHFUN_INCLUDED

#define fract frac
#define fmod mod
#define pi 3.14159265358979323846264338
#define mat2 float2x2
#define mat3 float3x3
#define mat4 float4x4
#define vec2 float2
#define vec3 float3
#define vec4 float4
// __ Matrix functions __ _____________________________________

    // Return 2x2 rotation matrix
    // With vector swizzle/mask can use as a 3x3 xform
    // For y, you need to invert 
    // angle in radians
    // ========================================
    mat2 Rot2(float a ) {
        float c = cos( a );
        float s = sin( a );
        return mat2( c, -s, s, c );
    }

    // http://www.songho.ca/opengl/gl_anglestoaxes.html

    // Return 4x4 rotation X matrix
    // angle in radians
    // ========================================
    mat4 Rot4X(float a ) {
        float c = cos( a );
        float s = sin( a );
        return mat4( 1, 0, 0, 0,
                     0, c,-s, 0,
                     0, s, c, 0,
                     0, 0, 0, 1 );
    }

    // Return 4x4 rotation Y matrix
    // angle in radians
    // ========================================
    mat4 Rot4Y(float a ) {
        float c = cos( a );
        float s = sin( a );
        return mat4( c, 0, s, 0,
                     0, 1, 0, 0,
                    -s, 0, c, 0,
                     0, 0, 0, 1 );
    }

    // Return 4x4 rotation Z matrix
    // angle in radians
    // ========================================
    mat4 Rot4Z(float a ) {
        float c = cos( a );
        float s = sin( a );
        return mat4(
            c,-s, 0, 0,
            s, c, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1
         );
    }

    // Translate is simply: p - d
    // opTx will do transpose(m)
    // p' = m*p
    //    = [m0 m1 m2 m3 ][ p.x ]
    //      [m4 m5 m6 m7 ][ p.y ]
    //      [m8 m9 mA mB ][ p.z ]
    //      [mC mD mE mF ][ 1.0 ]
    // ========================================
    mat4 Loc4( vec3 p ) {
        p *= -1.;
        return mat4(
            1,  0,  0,  p.x,
            0,  1,  0,  p.y,
            0,  0,  1,  p.z,
            0,  0,  0,  1
        );
    }


    // if no support for GLSL 1.2+
    //     #version 120
    // ========================================
    mat4 transposeM4(in mat4 m ) {
        vec4 r0 = m[0];
        vec4 r1 = m[1];
        vec4 r2 = m[2];
        vec4 r3 = m[3];

        mat4 t = mat4(
             float4( r0.x, r1.x, r2.x, r3.x ),
             float4( r0.y, r1.y, r2.y, r3.y ),
             float4( r0.z, r1.z, r2.z, r3.z ),
             float4( r0.w, r1.w, r2.w, r3.w )
        );
        return t;
    }

// __ Smoothing functions _____________________________________

    // Smooth Min
    // https://iquilezles.org/articles/smin

    // Min Polynomial
    // ========================================
    float sMinP( float a, float b, float k ) {
        float h = clamp( 0.5+0.5*(b-a)/k, 0.0, 1.0 );
        return lerp( b, a, h ) - k*h*(1.0-h);
    }

    // Min Exponential
    // ========================================
    float sMinE( float a, float b, float k) {
        float res = exp( -k*a ) + exp( -k*b );
        return -log( res )/k;
    }

    // Min Power
    // ========================================
    float sMin( float a, float b, float k ) {
        a = pow( a, k );
        b = pow( b, k );
        return pow( (a*b) / (a+b), 1.0/k );
    }


// __ Surface Primitives ____________________________

    // Return max component x, y, or z
    // ========================================
    float maxcomp(in vec3 p ) {
        return max(p.x,max(p.y,p.z));
    }

// Basic

    // Op Repetition
    // ========================================
    // vec3 opRep( vec3 p, vec3 spacing ) {
    //     return fmod(p,spacing) - 0.5*spacing;
    // }

// Deformations

    // Op Twist X
    // ========================================
    // vec3 opTwistX( vec3 p, float angle ) {
    //     mat2 m = Rot2( angle * p.x );
    //     return   vec3( m*p.yz, p.x );
    // }

    // Op Twist Y
    // ========================================
    vec3 opTwistY( vec3 p, float angle ) {
#if 0 // original
        float c = cos( angle * p.y );
        float s = sin( angle * p.y );
        mat2  m = mat2( c, -s, s, c );
        vec3  q = vec3( m*p.xz, p.y );
        // return primitive(q); // BUG in iq's docs, should be: return q
        return q;
#else // cleaned up
        mat2 m = Rot2( angle * p.y );
        return   vec3( m*p.xz, p.y );
#endif
    }

    // Op Twist Z
    // ========================================
    vec3 opTwistZ( vec3 p, float angle ) {
        mat2 m = Rot2( angle * p.z );
        return   vec3( m*p.xy, p.z );
    }

    // iq's bend X
    // ========================================
    vec3 opCheapBend( vec3 p, float angle ) {
#if 0 // original // broken :-(
        float c = cos( angle * p.y );
        float s = sin( angle * p.y );
        mat2  m = mat2( c, -s, s, c );
        vec3  q = vec3( m*p.xy, p.z ); // BUG in iq's docs, should be: p.yx
#else
        mat2  m = Rot2( angle * p.y );
        vec3  q = vec3( m*p.yx, p.z );
#endif
        return q;
    }

    // Op Cheap Bend X
    // ========================================
    vec3 opBendX( vec3 p, float angle ) {
        mat2 m = Rot2( angle * p.y );
        return   vec3( m*p.yx, p.z );
    }

    // Op Cheap Bend Y
    // ========================================
    vec3 opBendY( vec3 p, float angle ) {
        mat2 m = Rot2( angle * p.z );
        return   vec3( m*p.zy, p.x );
    }

    // Op Cheap Bend Z
    // ========================================
    vec3 opBendZ( vec3 p, float angle ) {
        mat2 m = Rot2( angle * p.x );
        return   vec3( m*p.xz, p.y );
    }

    // d = distance to move
    // ========================================
    vec3 opTrans( vec3 p, vec3 d ) {
        return p - d;
    }

    // Note: m must already be inverted!
    // TODO: invert(m) transpose(m)
    // Op Rotation / Translation
    // ========================================
    vec3 opTx( vec3 p, mat4 m ) {   // BUG in iq's docs, should be q
        return (transposeM4(m)*vec4(p,1.0)).xyz;
    }

    // Op Scale
    // ========================================
    float opScale( vec3 p, float s ) {
        return sdBox( p/s, vec3(1.2,0.2,1.0), 0.01 ) * s; // TODO: FIXME: NOTE: replace with primative sd*()
    }


#endif