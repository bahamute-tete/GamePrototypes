Shader "RayMarchingShader/Test"
{
    Properties
    {
        _MainTex("MainTex",2D)="white"{}
        _RampTex("Ramptex",2D)="white"{}
        _MainColor("Main Color", Color) = (1,1,1)
        _ShadowColor ("Shadow Color", Color) = (0.7, 0.7, 0.8)
        _ShadowRange ("Shadow Range", Range(0, 1)) = 0.5
        _ShadowSmooth("Shadow Smooth", Range(0, 1)) = 0.2
        
        [Header(RampMap)]
        _Color1("RampMapC1",Color)=(1,1,1,1)
        _Color2("RampMapC2",Color)=(1,1,1,1)
        _Color3("RampMapC3",Color)=(1,1,1,1)


        _Glossiness("Glossiness",Range(0,10))=2
        _SpecColor("SpecColor",Color)=(1,1,1,1)

         [Space(10)]
        _RimMin("RimMin",float)=0
        _RimMax("RimMax",float)=1
        _RimSmooth("RimSmooth",Range(0,1))=0.5
        _RimColor("RimColor",Color)=(1,1,1,1)
        _RimBloomExp("RimBloomExp",Range(0,10))=1


        // [Header(Addition_Diffuse_Gradient)]
        // _Color1_AD("C1_AD",Color)=(1,1,1,1)
        // _Color2_AD("C2_AD",Color)=(1,1,1,1)
        // _Color3_AD("C3_AD",Color)=(1,1,1,1)

        // [Header(Addition_Specular_Gradient)]
        // _Color1_AS("C1_AS",Color)=(1,1,1,1)
        // _Color2_AS("C2_AS",Color)=(1,1,1,1)
        // _Color3_AS("C3_AS",Color)=(1,1,1,1)


        // _Glossiness("Glossiness",Range(0,10))=2
        // _SpecColor("SpecColor",Color)=(1,1,1,1)

        [Space(10)]
        _Thickness("Thickness",Range(0,0.05))=0.01
        _EdgeColor("EdgeColor",Color)=(1,1,1,1)


        _StretchedNoiseTex("StretchedNoiseTex",2D)="white" {}
         _ShiftTangent("_ShiftTangent",Range(0,1))=0.5
        _AnisotropicPowerScale("_AnisotropicPowerScale",float)=1
        _AnisotropicPowerValue("_AnisotropicPowerValue",Range(0,100))=1




        [KeywordEnum(OFF,ON)]_RAMP_MAP("RampMap",float)=0

        [KeywordEnum(OFF,ON)]_HAIR("HairSpec",float)=0
        

    }

    SubShader
    {   
        
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}

        
        Pass
        {
             Tags{"LightMode" = "UniversalForward"}
             ZWrite On
             Cull Back


            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature _ _RAMP_MAP_ON
            #pragma shader_feature _ _HAIR_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            


            struct vertexData
            {
                float4 positionOS : POSITION;
                float3 normalOS:NORMAL;
                float2 uv:TEXCOORD0;
                float3 tangentOS:TANGENT;

            };

            struct fragmentData
            {
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS:TEXCOORD2;
                float3 tangentWS:TEXCOORD3;
                float4 positionCS : SV_POSITION;
                
            };

            struct GradientColor
            {
                float4 colors[3]; 
                float type;
                float colorsLength;
            };


            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float3 _MainColor;

            TEXTURE2D(_RampTex);
            SAMPLER(sampler_RampTex);

            TEXTURE2D(_StretchedNoiseTex);
             SAMPLER(sampler_StretchedNoiseTex);

           
            float _Glossiness;
            float3 _SpecColor;

            float _ShadowRange;
#if defined (_RAMP_MAP_ON)
            float4 _Color1,_Color2,_Color3;

#else
            float _ShadowSmooth;

#endif
            float3 _ShadowColor;
            float _RimMin,_RimMax;
            float _RimSmooth;
            float4 _RimColor;
            float _RimBloomExp;

            float _AnisotropicPowerValue,_AnisotropicPowerScale;
            float _ShiftTangent;






            fragmentData vert (vertexData input)
            {
                fragmentData o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                o.tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz); 
                //o.tangentWS = input.tangentOS.xyz;              
                return o;
            }

              GradientColor CreateGradient(float4 color1, float4 color2, float4 color3)
            {
                GradientColor g;
                g.colorsLength =3;
                g.type = 0;
               
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


            

          

            half4 frag (fragmentData input) : SV_Target
            {
                // sample the texture
                float4 color = 1;
                float4 texColor = _MainTex.Sample(sampler_MainTex, input.uv);
                float3 viewDir=normalize(GetWorldSpaceViewDir(input.positionWS));
                //float3 viewDir=normalize(_WorldSpaceCameraPos.xyz - input.positionWS.xyz);
                float3 worldNormal =normalize( input.normalWS);

                Light light = GetMainLight();
                float3 lightDir =light.direction;
                float3 lightColor = light.color;  
                float  lightShadowAtten =light.shadowAttenuation;

                float  diffuse =LightingLambert(lightColor, lightDir, worldNormal);
                float  specular =LightingSpecular(lightColor, light.direction, worldNormal, viewDir, float4(_SpecColor, 0), exp2(_Glossiness));


                //_StretchedNoiseTex 拉升的噪音贴图
                float3 bTangent =normalize(cross(input.tangentWS,worldNormal));
                float shift = _StretchedNoiseTex.Sample(sampler_StretchedNoiseTex,input.uv*0.6).r + _ShiftTangent;
                float3 T_Shift = normalize( bTangent+ worldNormal*shift);
                float3 H = normalize(viewDir+lightDir);
                //因为 sin^2+cos^2 =1 所以 sin = sqrt(1-cos^2)
                float dotTH = dot(T_Shift,H);
                float sinTH = sqrt(1- dotTH*dotTH);

                float dirAtten = smoothstep(-1,0,dotTH);
                float HairSpecular= dirAtten * pow(sinTH,_AnisotropicPowerValue)*_AnisotropicPowerScale;
               





                half f =  1.0 - saturate(dot(viewDir, worldNormal));
                float rim =smoothstep(_RimMin,_RimMax,f);
                rim = smoothstep(0,_RimSmooth,rim);
                half NdotL = max(0, dot(worldNormal, lightDir));
                half rimBloom = pow (f, _RimBloomExp) * rim * NdotL;
                float3 rimColor = _RimColor.rgb*rimBloom*_RimColor.a;
                //col.a = rimBloom;

                #if defined (_RAMP_MAP_ON)
                    GradientColor _Gradient_Main = CreateGradient(_Color1,_Color2,_Color3);
                    float3 ramp = SampleGradient(_Gradient_Main,diffuse-_ShadowRange);
                    color.rgb = ramp*texColor.rgb*lightColor+specular*_SpecColor+rimColor;
                    // float ramp =_RampTex.Sample(sampler_RampTex,float2((diffuse),0.5)).r;
                    // float3 albedo =lerp(_ShadowColor, _MainColor, ramp);
                    //color.rgb=(albedo*texColor.rgb+rimColor+specular)*lightColor;

                #else
                    // float3 ramp = diffuse>_ShadowRange?_MainColor:_ShadowColor;
                    // ramp*=texColor.rgb;
                    float ramp=smoothstep(0,_ShadowSmooth,diffuse-_ShadowRange);
                    float3 albedo =lerp(_ShadowColor, _MainColor, ramp);
                    #if defined (_HAIR_ON)
                    specular =HairSpecular;
                    #endif
                    color.rgb=(albedo*texColor.rgb+rimColor+specular)*lightColor;
                #endif


                //color.rgb =input.tangentWS;
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
                float4 tangentOS:TANGENT;

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

                

                float3 normalWS =TransformObjectToWorldNormal(input.normalOS);
                float3 normalCS =TransformWorldToHClipDir(normalWS);

                normalCS = normalize(normalCS)*o.positionCS.w;
                normalCS.x*=aspect;

                o.positionCS.xy+=normalCS.xy* _Thickness ;



                // float3 normalVS =mul((float3x3)UNITY_MATRIX_IT_MV,input.tangentOS);
                // normalVS.z =-0.5; 
                // postionVS+=normalVS*_Thickness;
                //  o.positionCS =TransformWViewToHClip(postionVS);
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


        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _GLOSSINESS_FROM_BASE_ALPHA

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }




    }
}
