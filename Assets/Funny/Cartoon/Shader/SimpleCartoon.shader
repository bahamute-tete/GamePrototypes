Shader "RayMarchingShader/SimpleCartoon"
{
    Properties
    {
        _MainTex("MainTex",2D)="white"{}
        
        [Header(Main_Gradient)]
        _Color1("C1",Color)=(1,1,1,1)
        _Color2("C2",Color)=(1,1,1,1)
        _Color3("C3",Color)=(1,1,1,1)
        _ColorPos("ColorPos",Range(0,1))=0


        [Header(Addition_Diffuse_Gradient)]
        _Color1_AD("C1_AD",Color)=(1,1,1,1)
        _Color2_AD("C2_AD",Color)=(1,1,1,1)
        _Color3_AD("C3_AD",Color)=(1,1,1,1)

        [Header(Addition_Specular_Gradient)]
        _Color1_AS("C1_AS",Color)=(1,1,1,1)
        _Color2_AS("C2_AS",Color)=(1,1,1,1)
        _Color3_AS("C3_AS",Color)=(1,1,1,1)


        _Glossiness("Glossiness",Range(0,10))=2
        _SpecColor("SpecColor",Color)=(1,1,1,1)
        _Thickness("Thickness",Range(0,0.05))=0.01
        _EdgeColor("EdgeColor",Color)=(1,1,1,1)
        

    }
    SubShader
    {

        Tags { "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            LOD 100
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            //#include "../HLSL/LightModels.hlsl"
            // #include "../HLSL/MathTools.hlsl"
            // #include "../HLSL/RayMarchingShape.hlsl"


            

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

       

            CBUFFER_START(UnityPerMaterial)
            float _K;
            float3 _Pos;
            float4 _Color1,_Color2,_Color3;
            float4 _Color1_AD,_Color2_AD,_Color3_AD;
            float4 _Color1_AS,_Color2_AS,_Color3_AS;
            float _ColorPos;
            float _Glossiness;
            float3 _SpecColor;
            

            CBUFFER_END




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


            float3 ColorspaceRGB2Linear(float3 RGBColor)
            {
                float3 linearRGBLo = RGBColor / 12.92;;
                float3 linearRGBHi = pow(max(abs((RGBColor + 0.055) / 1.055), 1.192092896e-07), float3(2.4, 2.4, 2.4));
                return float3(RGBColor <= 0.04045) ? linearRGBLo : linearRGBHi;
            }

            float3 ColorspaceRGB2HSV(float3 RGBColor)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 P = lerp(float4(RGBColor.bg, K.wz), float4(RGBColor.gb, K.xy), step(RGBColor.b, RGBColor.g));
                float4 Q = lerp(float4(P.xyw, RGBColor.r), float4(RGBColor.r, P.yzx), step(P.x, RGBColor.r));
                float D = Q.x - min(Q.w, Q.y);
                float  E = 1e-10;
                return float3(abs(Q.z + (Q.w - Q.y)/(6.0 * D + E)), D / (Q.x + E), Q.x);
            }


            float3 ColorspaceHSV2RGB(float3 HSVColor)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 P = abs(frac(HSVColor.xxx + K.xyz) * 6.0 - K.www);
                return HSVColor.z * lerp(K.xxx, saturate(P - K.xxx), HSVColor.y);
            }



            fragmentData vert (vertexData input)
            {
                fragmentData o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

          

            half4 frag (fragmentData input) : SV_Target
            {
                // sample the texture
                float4 color = 0;

                float3 diffuseColor = 0;
                float3 specularColor = 0;

                GradientColor _Gradient_Main = CreateGradient(_Color1,_Color2,_Color3);
                GradientColor _Gradient_AD = CreateGradient(_Color1_AD,_Color2_AD,_Color3_AD);
                GradientColor _Gradient_AS = CreateGradient(_Color1_AS,_Color2_AS,_Color3_AS);
                
                Light light = GetMainLight();
                float3 lightDir =light.direction;
                float3 lightColor = light.color;  
                float  lightShadowAtten =light.shadowAttenuation;

                float3 nor = normalize(input.normalWS);
                float3 wPos = input.positionWS;

                float3 WorldView =SafeNormalize(_WorldSpaceCameraPos.xyz-wPos);
                float3 h = SafeNormalize(lightDir+WorldView);

                int pixelLightCount = GetAdditionalLightsCount();
                float addtionDiffuse=0;
                for (int i = 0; i < pixelLightCount; ++i)
                {
                    Light light = GetAdditionalLight(i, wPos);
                    half3 attenuatedLightColor = light.color * (light.distanceAttenuation * light.shadowAttenuation);
                    diffuseColor += LightingLambert(attenuatedLightColor, light.direction, nor);
                    specularColor += LightingSpecular(attenuatedLightColor, light.direction, nor, WorldView, float4(_SpecColor, 0), exp2(_Glossiness));
                    addtionDiffuse =diffuseColor*rcp(attenuatedLightColor).x;
                }

                diffuseColor = ColorspaceRGB2HSV(diffuseColor);
                diffuseColor.z =addtionDiffuse;
                diffuseColor =ColorspaceHSV2RGB(diffuseColor);


              

               

                float diffuse = max(0,dot(nor,lightDir))*lightShadowAtten;
                float specular = pow(max(0,dot(nor,h)),exp2(_Glossiness))*lightShadowAtten;
               float3 shadow_Main = SampleGradient(_Gradient_Main,diffuse)*lightColor;


               float3 shadow_Addition_AD = SampleGradient(_Gradient_AD,addtionDiffuse);
               float3 shadow_Addition_AS = SampleGradient(_Gradient_AS,addtionDiffuse);
               color.rgb = diffuseColor*shadow_Addition_AD+specularColor*shadow_Addition_AS;

               float3 texColor = _MainTex.Sample(sampler_MainTex,input.uv);

               color.rgb+= (texColor)*shadow_Main;

                return color;
            }
            ENDHLSL
        }


        Pass
        {
            
            Tags{"LightMode" = "SRPDefaultUnlit"}
            Cull Front
            LOD 100
            HLSLPROGRAM
            #pragma vertex vertOutLine
            #pragma fragment fragOutLine

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            struct vertexData
            {
                float4 positionOS : POSITION;
                float3 normalOS:NORMAL;

            };

            struct fragmentData
            {
                float4 positionCS : SV_POSITION; 
            };



            float _Thickness;
            float4 _EdgeColor;



            fragmentData vertOutLine (vertexData input)
            {
                fragmentData o;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS);

                float4 scaledScreenParams = GetScaledScreenParams();
                float aspect = (scaledScreenParams.y/scaledScreenParams.x);


                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 positionWS =TransformObjectToWorld(input.positionOS.xyz);
                float3 postionVS =TransformWorldToView(positionWS);

                

                // float3 normalWS =TransformObjectToWorldNormal(input.normalOS);
                // float3 normalCS =TransformWorldToHClipDir(normalWS);

                // normalCS = normalize(normalCS)*o.positionCS.w;
                // normalCS.x*=aspect;

                // o.positionCS.xy+=normalCS.xy* _Thickness ;



                float3 normalVS =mul((float3x3)UNITY_MATRIX_IT_MV,input.normalOS);
                normalVS.z =-0.5; 
                postionVS+=normalVS*_Thickness;
                 o.positionCS =TransformWViewToHClip(postionVS);
                return o;
            }

          

            half4 fragOutLine (fragmentData input) : SV_Target
            {
                // sample the texture
                float4 color = _EdgeColor;
                return color;
            }
            ENDHLSL
        }


      


        Pass
        {
            Tags {"LightMode"="ShadowCaster"}

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma shader_feature _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 positionOS : POSITION;
                float4 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : NORMAL;
                float3 positionWS: TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 _LightDirection;
            float4 _ShadowBias; // x: depth bias, y: normal bias
            half4 _MainLightShadowParams;
            sampler2D _MainTex;

            float3 ApplyShadowBias(float3 positionWS, float3 normalWS, float3 lightDirection)
            {
                float invNdotL = 1.0 - saturate(dot(lightDirection, normalWS));
                float scale = invNdotL * _ShadowBias.y;

                // normal bias is negative since we want to apply an inset normal offset
                positionWS = lightDirection * _ShadowBias.xxx + positionWS;
                positionWS = normalWS * scale.xxx + positionWS;
                return positionWS;
            }

            v2f vert (appdata v)
            {
                v2f o = (v2f)0;
                UNITY_SETUP_INSTANCE_ID(v);

                UNITY_TRANSFER_INSTANCE_ID(v, o);

                

                float3 worldPos = TransformObjectToWorld(v.positionOS.xyz);
                half3 normalWS = TransformObjectToWorldNormal(v.normalOS.xyz);
                worldPos = ApplyShadowBias(worldPos, normalWS, _LightDirection);

                o.positionCS = TransformWorldToHClip(worldPos);

                #if UNITY_REVERSED_Z
                o.positionCS.z = min(o.positionCS.z, o.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                o.positionCS.z = max(o.positionCS.z, o.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                o.uv= v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                #if defined(_ALPHATEST_ON)
                half4 col = tex2D(_MainTex, i.uv);
                clip(col.a - 0.01);
                #endif

                return 0;
            }
            ENDHLSL
        }
    }
}
