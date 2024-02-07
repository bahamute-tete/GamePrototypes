#ifndef RAYMARCHING_RANDOMTOOL_INCLUDED
#define RAYMARCHING_RANDOMTOOL_INCLUDED

#define _ANIM

#define _PERLIN_NOISE
//#define _VALUE_NOISE
//#define _SIMPLEX_NOISE
//==============================================================================

TEXTURE2D(_NoiseTexture);
SAMPLER(sampler_NoiseTexture);

//==============================================================================
float hash11(float p) {
    return frac(sin(p * 727.1)*435.545);
}

float hash21(float2 p)
{
    p = frac(p*float2(123.34, 345.45));
    p +=dot(p,p+34.5);
    return frac(p.x * p.y);
}

float2 grad( float2 z )  // replace this anything that returns a random vector
{
    // 2D to 1D  (feel free to replace by some other)
    int n = z.x+z.y*11111;

    // Hugo Elias hash (feel free to replace by another one)
    n = (n<<13)^n;
    n = (n*(n*n*15731+789221)+1376312589)>>16;

    #if 0
        // simple random vectors
        return vec2(cos(float(n)),sin(float(n)));
        
    #else
        // Perlin style vectors
        n &= 7;
        float2 gr = float2(n&1,n>>1)*2.0-1.0;
        return ( n>=6 ) ? float2(0.0,gr.x) : 
            ( n>=4 ) ? float2(gr.x,0.0) :
                                gr;
    #endif                              
}

float2 hash22(float2 p)
{
    float3 MOD3=float3(.1031,.11369,.13787);
    float3 a = frac(p.xyy * MOD3);
    a += dot (a,a.yxz+34.45);
    //[-1,1]
    //PerlinNosie需要这个范围的梯度向量
    return -1+2*frac(float2 (a.x *a.y ,a.y *a.z));

    //p = float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3)));
    //return -1.0 + 2.0*frac(sin(p)*43758.5453123);
}

float hash31(float3 p3)
{
    float3 a =float3(.1031,.11369,.13787);
	p3  = frac(p3 * a);
    p3 += dot(p3, p3.yzx + 19.19);
    return -1.0 + 2.0 * frac((p3.x + p3.y) * p3.z);
}

float3 hash13(float p) {
	float3 h = float3(127.231,491.7,718.423) * p;	
    return frac(sin(h)*435.543);
}

float3 hash33(float3 p3)
{
    float3 a =float3(.1031,.11369,.13787);
	p3 = frac(p3 * a);
    p3 += dot(p3, p3.yxz+23.19);
    return -1.0 + 2.0 * frac(float3((p3.x + p3.y)*p3.z, (p3.x+p3.z)*p3.y, (p3.y+p3.z)*p3.x));
}
//==================================2D Noise============================================
float ValueNoise2D(float2 p)
{
    float2 id = floor(p);
    float2 f = frac(p);
    f=f*f*(3.0-2.0*f);


    float a0 = hash21(id);
    float a1 = hash21(id+float2(1.0,0.0));
    float a2 = hash21(id+float2(0.0,1.0));
    float a3 = hash21(id+float2(1.0,1.0));

    float v0 = lerp(a0,a1,f.x);
    float v1 = lerp(a2,a3,f.x);

    return lerp(v0,v1,f.y);

}


float PerlinNoise2D(float2 p )
{   
    
    float2 id = floor(p);
    float2 f = frac(p);
    f=f*f*(3.0-2.0*f);


    float2 a0 = grad(id);
    float2 a1 = grad(id+float2(1.0,0.0));
    float2 a2 = grad(id+float2(0.0,1.0));
    float2 a3 = grad(id+float2(1.0,1.0));

    //dot(ab)=cos(ab)|a||b|
    float g0 = dot(a0,f);
    float g1 = dot(a1,f-float2(1.0,0.0));
    float g2 = dot(a2,f-float2(0.0,1.0));
    float g3 = dot(a3,f-float2(1.0,1.0));

    float v0= lerp (g0,g1,f.x);
    float v1= lerp (g2,g3,f.x);

    return lerp(v0,v1,f.y);

}

float SimplexNoise(float2 p)
{
    //F =(sqrt(n+1)-1)/n
    const float K1 = 0.366025404; // (sqrt(3)-1)/2;
    //G =(1-1/sqrt(n))/n
    const float K2 = 0.211324865; // (3-sqrt(3))/6;

    float2 i = floor(p + (p.x + p.y) * K1);
    float2 a = p - (i - (i.x + i.y) * K2);

    float2 o = (a.x < a.y) ? float2(0.0, 1.0) : float2(1.0, 0.0);
    float2 b= a-(o-K2);////变形前输入点到(1, 0)点或(0, 1)点的距离向量
    float2 c = a - (1.0 - 2.0 * K2);////变形前输入点到(1, 1)点的距离向量

    float3 h = max(0.5 - float3(dot(a, a), dot(b, b), dot(c, c)), 0.0);
    float3 n = h * h * h * h * float3(  dot(a, hash22(i)), 
                                    dot(b, hash22(i + o)), 
                                    dot(c, hash22(i + 1.0)));
    return dot(float3(70.0, 70.0, 70.0), n);//归一化

}

//=================================3D Noise=============================================

float ValueNoise3D(float3 p)
{
    float3 pi = floor(p);
    float3 pf = p - pi;
    
    float3 w = pf * pf * (3.0 - 2.0 * pf);
    
    return 	lerp(
        		lerp(
        			lerp(hash31(pi + float3(0, 0, 0)), hash31(pi + float3(1, 0, 0)), w.x),
        			lerp(hash31(pi + float3(0, 0, 1)), hash31(pi + float3(1, 0, 1)), w.x), 
                    w.z),
        		lerp(
                    lerp(hash31(pi + float3(0, 1, 0)), hash31(pi + float3(1, 1, 0)), w.x),
        			lerp(hash31(pi + float3(0, 1, 1)), hash31(pi + float3(1, 1, 1)), w.x), 
                    w.z),
        		w.y);
}


float PerlinNoise3D(float3 p)
{
    float3 pi = floor(p);
    float3 pf = p - pi;
    
    float3 w = pf * pf * (3.0 - 2.0 * pf);
    
    return 	lerp(
        		lerp(
                	lerp(dot(pf - float3(0, 0, 0), hash33(pi + float3(0, 0, 0))), 
                        dot(pf - float3(1, 0, 0), hash33(pi + float3(1, 0, 0))),
                       	w.x),
                	lerp(dot(pf - float3(0, 0, 1), hash33(pi + float3(0, 0, 1))), 
                        dot(pf - float3(1, 0, 1), hash33(pi + float3(1, 0, 1))),
                       	w.x),
                	w.z),
        		lerp(
                    lerp(dot(pf - float3(0, 1, 0), hash33(pi + float3(0, 1, 0))), 
                        dot(pf - float3(1, 1, 0), hash33(pi + float3(1, 1, 0))),
                       	w.x),
                   	lerp(dot(pf - float3(0, 1, 1), hash33(pi + float3(0, 1, 1))), 
                        dot(pf - float3(1, 1, 1), hash33(pi + float3(1, 1, 1))),
                       	w.x),
                	w.z),
    			w.y);
}


float SimplexNoise3D(float3 p)
{
    const float K1 = 0.333333333;
    const float K2 = 0.166666667;
    
    float3 i = floor(p + (p.x + p.y + p.z) * K1);
    float3 d0 = p - (i - (i.x + i.y + i.z) * K2);
    
    // thx nikita: https://www.shadertoy.com/view/XsX3zB
    float3 e = step(float3(0.0,0.,0.), d0 - d0.yzx);
	float3 i1 = e * (1.0 - e.zxy);
	float3 i2 = 1.0 - e.zxy * (1.0 - e);
    
    float3 d1 = d0 - (i1 - 1.0 * K2);
    float3 d2 = d0 - (i2 - 2.0 * K2);
    float3 d3 = d0 - (1.0 - 3.0 * K2);
    
    float4 h = max(0.6 - float4(dot(d0, d0), dot(d1, d1), dot(d2, d2), dot(d3, d3)), 0.0);
    float4 n = h * h * h * h * float4(dot(d0, hash33(i)), dot(d1, hash33(i + i1)), dot(d2, hash33(i + i2)), dot(d3, hash33(i + 1.0)));
    
    return dot(float4(31.316,31.316,31.316,31.316), n);
}
//==============================Noise===========================================

float Noise2DFromTexture( float2 p )
{
    //p+=_Time.y;
    float2 id = floor(p);
    float2 f = frac(p);
	float2 uv = id.xy + f.xy*f.xy*(3.0-2.0*f.xy);
	return  SAMPLE_TEXTURE2D(_NoiseTexture,sampler_NoiseTexture,(uv+0.5)/256.0).r;
}

float Noise3DFromTexture(float3 p)
{
    float3 id = floor(p);
    float3 a =frac(p);
    float3 f = a*a*(3.0-2.0*a);
#ifndef HIGH_QUALITY_NOISE
    float2 uv = (id.xy+float2(37.0,239.0)*id.z)+f.xy;
    float2 rg =  SAMPLE_TEXTURE2D(_NoiseTexture,sampler_NoiseTexture,(uv+0.5)/256.0).rg;
#else
    float2 uv  = (id.xy+float2(37.0,17.0)*id.z);
    float2 rg1 = SAMPLE_TEXTURE2D( _NoiseTexture,sampler_NoiseTexture, (uv+ float2(0.5,0.5))/256.0, 0.0 ).yx;
    float2 rg2 = SAMPLE_TEXTURE2D( _NoiseTexture,sampler_NoiseTexture, (uv+ float2(1.5,0.5))/256.0, 0.0 ).yx;
    float2 rg3 = SAMPLE_TEXTURE2D( _NoiseTexture,sampler_NoiseTexture,(uv+ float2(0.5,1.5))/256.0, 0.0 ).yx;
    float2 rg4 = SAMPLE_TEXTURE2D( _NoiseTexture,sampler_NoiseTexture, (uv+ float2(1.5,1.5))/256.0, 0.0 ).yx;
    float2 rg  = lerp( lerp(rg1,rg2,f.x), lerp(rg3,rg4,f.x), f.y );
#endif	
    return lerp( rg.x, rg.y, f.z )*2.0-1.0;
}



float  Noise2D(float2 p)
{
#if defined(_ANIM)
    p+=_Time.y*0.3;
#endif
    
#if defined(_VALUE_NOISE)
    return ValueNoise2D(p);
#elif defined(_PERLIN_NOISE)
    return PerlinNoise2D(p);
#elif defined(_SIMPLEX_NOISE)
    return SimplexNoise(p);
#else
    return 0;
#endif

}


float Noise3D(float3 p) 
{
#if defined(_ANIM)
    p+=_Time.x;
#endif
#if defined (_PERLIN_NOISE)
    return PerlinNoise3D(p * 2.0);
#elif defined (_VALUE_NOISE)
    return ValueNoise3D(p * 2.0);
#elif defined (_SIMPLEX_NOISE)
    return SimplexNoise3D(p);
#endif
    return 0.0;
}

//==============================================================================
float FBMNoise2DFromTexture(float2 p,float scale)
{
    p *= scale;
   
    float f =0;

    //float2x2(c,s,-s,c)
    float2x2  m2 = float2x2( 1.6,  1.2, 
                            -1.2,  1.6 );

    f  = 1.0*Noise2DFromTexture( p );    p = mul(m2,p);
    f  = 0.5*Noise2DFromTexture( p );    p = mul(m2,p);
    f += 0.25*Noise2DFromTexture( p );   p = mul(m2,p);
    f += 0.125*Noise2DFromTexture( p );  p = mul(m2,p);
    f += 0.0625*Noise2DFromTexture( p  ); p = mul(m2,p);

    return f;

}


float FBMNoise2D(float2 p,float scale)
{
    p *= scale;
   
    float f =0;

    //float2x2(c,s,-s,c)
    float2x2  m2 = float2x2( 1.6,  1.2, 
                            -1.2,  1.6 );

    f  = 1.0*Noise2D( p );    p = mul(m2,p);
    f  = 0.5*Noise2D( p );    p = mul(m2,p);
    f += 0.25*Noise2D( p );   p = mul(m2,p);
    f += 0.125*Noise2D( p );  p = mul(m2,p);
    f += 0.0625*Noise2D( p  ); p = mul(m2,p);

    return f;

}

float FBMNoise2D_ABS(float2 p,float scale)
{
    float f = 0.0;
    p = p *scale;
    f += 1.0000 * abs(Noise2D(p)); p = 2.0 * p;
    f += 0.5000 * abs(Noise2D(p)); p = 2.0 * p;
	f += 0.2500 * abs(Noise2D(p)); p = 2.0 * p;
	f += 0.1250 * abs(Noise2D(p)); p = 2.0 * p;
	f += 0.0625 * abs(Noise2D(p)); p = 2.0 * p;
    
    return f;
}

float FBMNoise2D_ABS_SIN(float2 p,float scale)
{
    float f = FBMNoise2D(p,scale);
    f = sin(f * 2.5 + p.x * 5.0 - 1.5);   
    return f ;
}

float NoiseSelf(float3 p)
{
    return Noise3D(p * 8.0);
}

float FBMNoise3DFromTexture(float3 p,float scale)
{
    p *= scale;
   
    float f =0;

    //float2x2(c,s,-s,c)
 
    float3x3  m3 = float3x3( 0.00,  0.80,  0.60,
                            -0.80,  0.36, -0.48,
                            -0.60, -0.48,  0.64 );

    //f  = 1.0*Noise3DFromTexture( p );    p = mul(m3,p)*2.04;
    f += 0.5*Noise3DFromTexture( p );    p = mul(m3,p);
    f += 0.25*Noise3DFromTexture( p );   p = mul(m3,p);
    f += 0.125*Noise3DFromTexture( p );  p = mul(m3,p);
    f += 0.0625*Noise3DFromTexture( p  ); p = mul(m3,p);

    return f;

}

float FBMNoise3D(float3 p,float scale)
{
    p *= scale;
    float f =0;

    float3x3  m3 = float3x3( 0.00,  0.80,  0.60,
                            -0.80,  0.36, -0.48,
                            -0.60, -0.48,  0.64 );

    f  = 1.0    *  Noise3D( p );   p = mul(m3,p)*2.04;
    f += 0.5    *  Noise3D( p );   p = mul(m3,p)*2.02;
    f += 0.25   *  Noise3D( p );   p = mul(m3,p)*2.01;
    f += 0.125  *  Noise3D( p );   p = mul(m3,p)*2.03;
    f += 0.0625 *  Noise3D( p );  

    // f += 1.0000 * Noise3D(p); p = 2.0 * p;
    // f += 0.5000 * Noise3D(p); p = 2.0 * p;
	// f += 0.2500 * Noise3D(p); p = 2.0 * p;
	// f += 0.1250 * Noise3D(p); p = 2.0 * p;
	// f += 0.0625 * Noise3D(p); p = 2.0 * p;

    return f;

}



float FBMNoise3D_ABS(float3 p,float scale)
{
    float f = 0.0;

    float3x3  m3 = float3x3( 0.00,  0.80,  0.60,
                        -0.80,  0.36, -0.48,
                        -0.60, -0.48,  0.64 );
    p = p * scale;
    f += 1.0000 * abs(Noise3D(p)); p = mul(m3,p)*2.04;;
    f += 0.5000 * abs(Noise3D(p)); p = mul(m3,p)*2.01;
	f += 0.2500 * abs(Noise3D(p)); p = mul(m3,p)*2.03;
	f += 0.1250 * abs(Noise3D(p)); p = mul(m3,p)*2.05;
	f += 0.0625 * abs(Noise3D(p)); 
    
    return f;
}

float FBMNoise3D_ABS_SIN(float3 p,float scale)
{
    float f = FBMNoise3D(p,scale);
    f = sin(f * 2.5 + p.x * 5.0 - 1.5);   
    return f ;
}


// //=============================SomeEffect====================================
//f = FBM(p)
float3 Draw_simple(float f)
{
    f = f * 0.5 + 0.5;
    return f * float3(25.0/255.0, 161.0/255.0, 245.0/255.0);
}

float3 Draw_cloud(float f)
{
    f = f * 0.5 + 0.5;
    return lerp(float3(8.0/255.0, 65.0/255.0, 82.0/255.0),
              	float3(178.0/255.0, 161.0/255.0, 205.0/255.0),
               	f*f);
}

float3 Draw_fire(float f)
{
    f = f * 0.5 + 0.5;
    return lerp(float3(131.0/255.0, 8.0/255.0, 0.0/255.0),
              	float3(204.0/255.0, 194.0/255.0, 56.0/255.0),
               	pow(f, 3.));
}

float3 Draw_marble(float f)
{
    f = f * 0.5 + 0.5;
    return lerp(float3(31.0/255.0, 14.0/255.0, 4.0/255.0),
              	float3(172.0/255.0, 153.0/255.0, 138.0/255.0),
               	1.0 - pow(f, 3.));
}

//==============================================================================


#endif
