Shader "RayMarchingShader/Cartoon"
{
    Properties
    {
        _MainTex("MainTex",2D)="white"{}
        _Pos("_Pos",vector)=(0,0,0,0)
        _K("_K",Range(-1,1))=0


        _Color1("C1",Color)=(1,1,1,1)
        _Color2("C2",Color)=(1,1,1,1)
        _Color3("C3",Color)=(1,1,1,1)
        _ColorPos("ColorPos",Range(0,1))=0
        

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

            //#include "../HLSL/LightModels.hlsl"
            #include "../HLSL/MathTools.hlsl"
            #include "../HLSL/RayMarchingShape.hlsl"


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


            struct GradientColor
            {
                float4 colors[3]; 
                float type;
                float colorsLength;
            };

            TEXTURECUBE(_MainTex);
            SAMPLER(sampler_MainTex);

            float _K;
            float3 _Pos;
            float4 _Color1,_Color2,_Color3;
            float _ColorPos;




            GradientColor CreateGradient(float4 color1, float4 color2, float4 color3)
            {
                GradientColor g;
                g.colorsLength =3;
                g.type = 1;
               
                g.colors[0] = color1;
                g.colors[1] = color2;
                g.colors[2] = color3;
                return g;
            }

            float3 SampleGradient(GradientColor Gradient, float t)
            {
                float3 color = Gradient.colors[0].rgb;
                
                for (int c = 1; c < 3; c++)
                {
                    float colorPos = saturate((t - Gradient.colors[c-1].w) / (Gradient.colors[c].w - Gradient.colors[c-1].w)) * step(c, Gradient.colorsLength-1);
                    float interpolateFactor =lerp(colorPos, step(0.01, colorPos), Gradient.type);
                    color = lerp(color, Gradient.colors[c].rgb, interpolateFactor);
                }
                #ifndef UNITY_COLORSPACE_GAMMA
                color = SRGBToLinear(color);
                #endif
                // float alpha = Gradient.alphas[0].x; 
                // [unroll]
                // for (int a = 1; a < 8; a++)
                // {
                // float alphaPos = saturate((Time - Gradient.alphas[a-1].y) / (Gradient.alphas[a].y - Gradient.alphas[a-1].y)) * step(a, Gradient.alphasLength-1);
                // alpha = lerp(alpha, Gradient.alphas[a].x, lerp(alphaPos, step(0.01, alphaPos), Gradient.type));
                // }
               return color;
            }



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


            float map(float3 p)
            {   
                float d= 0 ;
                float sphere1= SDFSphere(p, 1.0);
                float sphere2= SDFSphere(p-float3(0.5,1,0.6), 0.7);
                float sphere3= SDFSphere(p-float3(-0.5,-1,-0.3), 0.8);
                d=smin(sphere1,sphere2,0.3);
                d=smin(sphere3,d,0.5);
                return d;

            }

            float raymarch(float3 ro, float3 rd)
            {
                float d=0;
                for(int i=0; i<32; i++)
                {
                    float3 pos = ro + d * rd;
                    float t = map(pos);
                    if(d>30||t<0.01) break;
                    d += t;
                }
                return d;
            }

            float3 getNormals(float3 p)
            {
                float h = 0.0001; // replace by an appropriate value
                float2 k = float2(1,-1);
                return normalize( k.xyy*map( p + k.xyy*h)+ 
                    k.yyx*map( p + k.yyx*h)+ 
                    k.yxy*map( p + k.yxy*h)+ 
                    k.xxx*map( p + k.xxx*h));

            }


            half4 frag (fragmentData input) : SV_Target
            {
                // sample the texture
                float4 color = 0;
                float2 uv = (2*input.uv-1.0)*_ScreenParams.xy/_ScreenParams.y;

              
                float3 ro =_WorldSpaceCameraPos;
                float3 target =0;
                float3 rd = mul(float3(uv.x,uv.y,1.),Camera_matrix(ro,target));
                Light light = GetMainLight();
                float3 lightDir =light.direction;
                float3 lightColor = light.color;  
                float  lightShadowAtten =light.shadowAttenuation;


                float d = raymarch(ro,rd);
                if (d<30)
                {
                    float3 pos = ro + rd*d;
                    float3 nor = getNormals(pos);

                    float3 lightDir = light.direction;
                    float3 refDir = reflect(rd,nor);

                    float diffuse = max(0,dot(nor,lightDir))*lightShadowAtten;

                    GradientColor _Gradient = CreateGradient(_Color1,_Color2,_Color3);

                    color.rgb = SampleGradient(_Gradient,diffuse)*lightColor;
                    
                }
                
                return color;
            }
            ENDHLSL
        }
    }
}
