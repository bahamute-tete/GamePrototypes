Shader "Custom/PlaneDeformation"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _DeformationType("Deformation Type", Int) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "2DSDFPass"

            
            HLSLPROGRAM
            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING
            #pragma vertex vert
            #pragma fragment frag

            #define DEFORMED_ONE 1
            #define DEFORMED_TWO 2
            #define DEFORMED_THREE 3
            #define DEFORMED_FOUR 4
            #define DEFORMED_FIVE 5
            #define DEFORMED_SIX 6
            #define DEFORMED_SEVEN 7
            #define DEFORMED_EIGHT 8
            #define DEFORMED_NINE 9
            #define DEFORMED_TEN 10
            #define DEFORMED_ELEVEN 11



            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #define pi 3.14159265359

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            int _DeformationType;
            
            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }
            

            half4 frag (Varyings i) : SV_Target
            {
                
                float2 uv = 2*i.uv-1;
                uv*=_ScreenParams.xy/_ScreenParams.x;
                float a = atan2(uv.y, uv.x);
                float r = sqrt( dot(uv,uv) );
             

                float u;
                float v;
                float h;
                float s;

               
                if (_DeformationType == DEFORMED_ONE)
                {
                    u = uv.x*cos(2*r) - uv.y*sin(2*r);
                    v = uv.y*cos(2*r) + uv.x*sin(2*r);
                }
                else if (_DeformationType == DEFORMED_TWO)
                {
                    u = 0.5/(r+0.5*uv.x);
                    v = 3*a/pi;
                }
                else if (_DeformationType == DEFORMED_THREE)
                {
                    u = 0.02*uv.x+0.03*cos(a*3)/r;
                    v = 0.02*uv.y+0.03*sin(a*3)/r;
                }
                else if (_DeformationType == DEFORMED_FOUR)
                {
                    u = 0.1*uv.x/(0.11+r*0.5);
                    v = 0.1*uv.y/(0.11+r*0.5);
                }
                else if (_DeformationType == DEFORMED_FIVE)
                {
                    u = 0.5*a/pi;
                    v = sin(7*r);
                }
                else if (_DeformationType == DEFORMED_SIX)
                {
                    u = r*cos(a+r);
                    v = r*sin(a+r);
                }
                else if (_DeformationType == DEFORMED_SEVEN)
                {
                    u = 1/(r+0.5+0.5*sin(5*a));
                    v = a*3/pi;
                }
                else if (_DeformationType == DEFORMED_EIGHT)
                {
                    u = uv.x/abs(uv.y);
                    v = 1/abs(uv.x);
                }
                else if (_DeformationType == DEFORMED_NINE)
                {
                    u = cos( a )/r;
                    v = sin( a )/r;
                }
                else if (_DeformationType == DEFORMED_TEN)
                {
                    a += sin(0.5*r-0.5*_Time.y);
                    h = 0.5 + 0.5*cos(9.0*a);
                    s = smoothstep(0.4,0.5,h);
                    a =3*a / 3.1415926;
                    r = _Time.y + 1.0/(r+0.1*s);
                    u=r;v=a;
                }
                else if (_DeformationType == DEFORMED_ELEVEN)
                {
                    r = pow( pow(uv.x*uv.x,16.0) + pow(uv.y*uv.y,16.0), 1.0/32.0 );
                    a = atan2(uv.y,uv.x);
                    r =0.5 * _Time.y + 0.5/r;
                    a = a / 3.1415927;
                    h = sin(32.0*a);
                    r += .85*smoothstep( -0.1,0.1,h);
                    u=r;v=a;
                }
                else
                {
                   
                    u = uv.x;
                    v = uv.y;
                }

                uv = float2(u,v);

                float3 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;

                return float4(col,1.0);
            }
            ENDHLSL
        }

      
    }
}

