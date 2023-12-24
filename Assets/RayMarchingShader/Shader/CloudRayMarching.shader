Shader "RayMarchingShader/Cloud"
{
    Properties
    {

        _BackGround("Tex",Cube)="white"{}
        _NoiseTexture("NoiseTexture",2D)="white"{}


    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

               Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"

            // #include "../HLSL/LightModels.hlsl"
            // #include "../HLSL/MathTools.hlsl"
            // #include "../HLSL/RayMarchingShape.hlsl"


            #define SURFANCE_DIS 1e-4
            #define MAX_STEPS 68
            #define MAX_DISTACNE 30
            #define ZERO 0
            #define PLANEHEIGHT 0.5
            #define IOR 1.45


            #define MAT_DEFAULT 0
            #define MAT_GROUND 1
            #define MAT_BALL 2
            #define MAT_Box 3

            #define USE_LOD  1

            struct vertexData
            {
                float4 positionOS : POSITION;
                float3 normalOS:NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct fragmentData
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
                float3 normalWS:TEXCOORD2;
                
            };



            TEXTURECUBE(_BackGround);
            SAMPLER(sampler_BackGround);

            TEXTURE2D(_NoiseTexture);
            SAMPLER(sampler_NoiseTexture);


            fragmentData vert (vertexData input)
            {
                fragmentData o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv;
                return o;
            }

            float3x3 Camera_matrix(float3 origin, float3 target)
            {
                float3 forward = normalize(target - origin);
                float3 right = cross(forward,float3(0,1,0));
                float3 up = cross(right,forward);

                return float3x3(right,up,forward);
            }

            float noise( in float3 x )
            {
                float3 p = floor(x);
                float3 f = frac(x);
                f = f*f*(3.0-2.0*f);


                float2 uv = (p.xy+float2(37.0,239.0)*p.z)+f.xy;
                float2 rg = _NoiseTexture.Sample(sampler_NoiseTexture,(uv+0.5)/256.0).yx;
                return lerp( rg.x, rg.y, f.z )*2.0-1.0;
              
            }

            float map( in float3 p, int oct )
            {
                float3 q = p - float3(0.0,0.1,1.0)*_Time.y;
                float g = 0.5+0.5*noise( q*0.3 );

                float f;
                f  = 0.50000*noise( q ); q = q*2.02;
                #if USE_LOD==1
                if( oct>=2 ) 
                #endif
                f += 0.25000*noise( q ); q = q*2.23;
                #if USE_LOD==1
                if( oct>=3 )
                #endif
                f += 0.12500*noise( q ); q = q*2.41;
                #if USE_LOD==1
                if( oct>=4 )
                #endif
                f += 0.06250*noise( q ); q = q*2.62;
                #if USE_LOD==1
                if( oct>=5 )
                #endif
                f += 0.03125*noise( q ); 

                f = lerp( f*0.1-0.5, f, g*g );

                return 1*f-0.5  - p.y;
            }

            const float3 sundir = normalize( float3(1.0,0.0,1.0) );


            float4 raymarch( in float3 ro, in float3 rd, in float3 bgcol, in int2 px )
            {
                // bounding planes	
                const float yb = -3.0;
                const float yt =  0.6;
                float tb = (yb-ro.y)/rd.y;
                float tt = (yt-ro.y)/rd.y;

                // find tigthest possible raymarching segment
                float tmin, tmax;
                if( ro.y>yt )
                {
                    // above top plane
                    if( tt<0.0 ) return 0; // early exit
                    tmin = tt;
                    tmax = tb;
                }
                else
                {
                    // inside clouds slabs
                    tmin = 0.0;
                    tmax = 60.0;
                    if( tt>0.0 ) tmax = min( tmax, tt );
                    if( tb>0.0 ) tmax = min( tmax, tb );
                }
                
                // dithered near distance
                float t = tmin+0.001;
                
                // raymarch loop
                float4 sum = 0;
                for( int i=0; i<64; i++ )
                {
                // step size
                float dt =0.05;


                int oct = 4;

                
                // sample cloud
                float3 pos = ro + t*rd;
                float den = map( pos,oct );
                if( den>0.01 ) // if inside
                {
                    // do lighting
                    float dif = clamp((den - map(pos+0.3*sundir,oct))/0.25, 0.0, 1.0 );
                    float3  lin = float3(0.65,0.65,0.75)*1.1 + 0.8*float3(1.0,0.6,0.3)*dif;
                    float4  col = float4( lerp( float3(1.0,0.93,0.84), float3(0.25,0.3,0.4), den ), den );
                    col.xyz *= lin;
                    // fog
                    col.xyz = lerp(col.xyz,bgcol, 1.0-exp2(-0.1*t));
                    // composite front to back
                    col.w    = min(col.w*8.0*dt,1.0);
                    col.rgb *= col.a;
                    sum += col*(1.0-sum.a);
                }
                // advance ray
                t += dt;
                // until far clip or full opacity
                if( t>tmax || sum.a>0.99 ) break;
                }

                return clamp( sum, 0.0, 1.0 );
            }


            float4 render( in float3 ro, in float3 rd, in int2 px )
            {
                float sun = clamp( dot(sundir,rd), 0.0, 1.0 );
                //float3 col=0;

                // // background sky
                float3 col = float3(0.76,0.75,0.95);
                col -= 0.6*float3(0.90,0.75,0.95)*rd.y;
                col += 0.2*float3(1.00,0.60,0.10)*pow( sun, 8.0 );

                // clouds    
                float4 res = raymarch( ro, rd, col, px );
                col = col*(1.0-1) + res.xyz;

                // sun glare    
                col += 0.2*float3(1.0,0.4,0.2)*pow( sun, 3.0 );

                // tonemap
                col = smoothstep(0.15,1.1,col);

                return float4( col, 1.0 );
            }

       

            half4 frag (fragmentData input) : SV_Target
            {
                // sample the texture
                half4 col = 0;
                float2 uv = (2*input.uv-1.0)*_ScreenParams.xy/_ScreenParams.y;
               
                float3 ro =_WorldSpaceCameraPos;
                float3 target =0;
                float3 rd = mul(float3(uv.x,uv.y,1.0),Camera_matrix(ro,target));
                Light light = GetMainLight();

                
                 col = render( ro, rd, int2(0,0));
                
                return col;
            }
            ENDHLSL
        }
    }
}
