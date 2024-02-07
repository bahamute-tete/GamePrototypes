Shader "Unlit/TessellationShader"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)

        _CausticsMap("Caustics", 2D)="white" {}
        [NoScaleOffset] _FlowMap ("Flow (RG)", 2D) = "black" {}



        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}

        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMap("Specular", 2D) = "white" {}

        _NormalMap ("NormalMap", 2D) = "bump" {}
        _NormalScale("NormalScale", Float) = 1.0

        _OcclusionMap("Occlusion", 2D) = "white" {}
        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0


        _TessellationUniform("_TessellationUniform",Range(1,64))=1

        _NoiseScale("NoiseScale",float)=1
        _NoiseFrequency("NoiseFrequency",float)=1
        _NoiseOffset("NoiseOffset",Vector)=(0,0,0,0)


        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque"  "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "IgnoreProjector" = "True" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags{"LightMode" = "UniversalForward"}
            HLSLPROGRAM

            #pragma target 5.0

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT




            #pragma vertex MyTessellationVertexProgram
            #pragma hull MyHullProgram
            #pragma domain MyDomainProgram
            #pragma fragment frag



            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "../_Shaders/noiseSimplex.cginc"


            #define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
            #define _NORMALMAP
            #define  _OCCLUSIONMAP
            #define  _SPECULARHIGHLIGHTS_OFF
            //#define  _METALLICSPECGLOSSMAP
            //#define  _SPECULAR_SETUP

            #ifdef _SPECULAR_SETUP
                #define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, uv)
            #else
                #define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv)
            #endif

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NormalMap_ST;
                float4 _CausticsMap_ST;
                half4 _BaseColor;
                half4 _SpecColor;
                half _Cutoff;
                half _Smoothness;
                half _Metallic;
                half _NormalScale;
                half _OcclusionStrength;
                float _NoiseScale,_NoiseFrequency;
                float4 _NoiseOffset;
                float _TessellationUniform;
            CBUFFER_END

            TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_SpecGlossMap);       SAMPLER(sampler_SpecGlossMap);
            TEXTURE2D(_NormalMap);          SAMPLER(sampler_NormalMap);
            TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_CausticsMap);        SAMPLER(sampler_CausticsMap);
            TEXTURE2D(_FlowMap);            SAMPLER(sampler_FlowMap);


            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS:NORMAL;
                float4 tangentOS:TANGENT;
                float2 uv:TEXCOORD0;
            };
            

            struct TessellationControlPoint
            {
                float3 positionOS:INTERNALTESSPOS;
                //float3 normalWS: TEXCOORD0;
                float3 normalOS:NORMAL;
                float4 tangentOS:TANGENT;
                float2 uv:TEXCOORD1;
                float3 normalWS:TEXCOORD2;
                half3 tangentWS:TEXCOORD3;
                float3 viewDirWS:TEXCOORD4;
                float4 shadowCoord : TEXCOORD5; 

            };

            struct Interpolators
            {

                float4 positionCS : SV_POSITION;
                float3 positionWS: TEXCOORD0;
                float3 normalWS: TEXCOORD1;
                float2 uv:TEXCOORD2;
                float3x3 tangentToWorld : TEXCOORD3;
                half4 tangentWS:TEXCOORD6;
                float4 shadowCoord : TEXCOORD7;
            };

            
            struct TessellationFactors {
                float edges[3] : SV_TessFactor;
                float inside : SV_InsideTessFactor;
            };
            
            
            TessellationControlPoint MyTessellationVertexProgram (Attributes input)
            {
                TessellationControlPoint output = (TessellationControlPoint)0;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);   
                VertexNormalInputs  normalInputs = GetVertexNormalInputs(input.normalOS);
                output.uv =TRANSFORM_TEX(input.uv,_BaseMap);

                output.normalWS = normalInputs.normalWS;
                output.tangentWS = normalInputs.tangentWS;

                output.normalOS= input.normalOS;

                output.positionOS = input.positionOS.xyz;

                output.tangentOS= input.tangentOS;

                


                return output;
            }
            
       
            TessellationFactors MyPatchConstantFunction (InputPatch<TessellationControlPoint,3> patch)
            {
                TessellationFactors f;
                f.edges[0] = _TessellationUniform;
                f.edges[1] = _TessellationUniform;
                f.edges[2] = _TessellationUniform;
                f.inside = _TessellationUniform;
                return f;
            }


      

            [domain("tri")]//deal with triangle
            [outputcontrolpoints(3)] //fingerout ever control point control ever angle in triangle
            [outputtopology("triangle_cw")]//fingerout triangle direction
            [partitioning("fractional_even")]//fingerout how to partition the domain
            [patchconstantfunc("MyPatchConstantFunction")]//define function to partition triangle //erver patch call onece not ever control point
            TessellationControlPoint MyHullProgram(InputPatch<TessellationControlPoint,3> patch,
                                                    uint id :SV_OutputControlPointID)
            {
                return patch[id];
            }


            [domain("tri")]
            Interpolators MyDomainProgram(    TessellationFactors factors,
                                    OutputPatch<TessellationControlPoint,3> patch,
                                    float3 barycentricCoord :SV_DomainLocation)
            {

                Interpolators output = (Interpolators)0;
                float3 positionOS =     patch[0].positionOS.xyz * barycentricCoord.x +
                                        patch[1].positionOS.xyz * barycentricCoord.y +
                                        patch[2].positionOS.xyz * barycentricCoord.z;

                // float3 positionWS =     patch[0].positionWS * barycentricCoord.x+
                //                         patch[1].positionWS * barycentricCoord.y+
                //                         patch[2].positionWS * barycentricCoord.z;
                
                // float3 normalWS =       patch[0].normalWS * barycentricCoord.x+
                //                         patch[1].normalWS * barycentricCoord.y+
                //                         patch[2].normalWS * barycentricCoord.z;


                float3 normalOS =       patch[0].normalOS * barycentricCoord.x+
                                        patch[1].normalOS * barycentricCoord.y+
                                        patch[2].normalOS * barycentricCoord.z;

                float4 tangentOS =    patch[0].tangentOS * barycentricCoord.x+
                                      patch[1].tangentOS * barycentricCoord.y+
                                      patch[2].tangentOS * barycentricCoord.z;

                float3 tangentWS =    patch[0].tangentWS * barycentricCoord.x+
                                    patch[1].tangentWS * barycentricCoord.y+
                                    patch[2].tangentWS * barycentricCoord.z;

                float2 uv =     patch[0].uv * barycentricCoord.x+
                                patch[1].uv * barycentricCoord.y+
                                patch[2].uv * barycentricCoord.z;




                //output.positionCS = TransformWorldToHClip(positionWS);

                // float3 noisePos = float3(   positionOS.x+_NoiseOffset.x,
                //                             positionOS.y+_NoiseOffset.y,
                //                             positionOS.z+_NoiseOffset.z);

                // float noise = snoise(noisePos*_NoiseFrequency);
                // positionOS.y += noise*_NoiseScale;

                float3 v0 =positionOS.xyz;
                float3 bitangentOS =normalize( cross(normalOS,tangentOS.xyz))*tangentOS.w;
                float3 v1 =v0 + tangentOS.xyz*0.01;
                float3 v2 =v0 + bitangentOS*0.01;

                


                float n0 =_NoiseScale*snoise((float3(v0.x+_NoiseOffset.x,v0.x+_NoiseOffset.y,v0.z+_NoiseOffset.z))*_NoiseFrequency);
                v0.xyz +=((n0+1)/2)*normalOS;

                float n1 =_NoiseScale*snoise((float3(v1.x+_NoiseOffset.x,v1.x+_NoiseOffset.y,v1.z+_NoiseOffset.z))*_NoiseFrequency);
                v1.xyz +=((n1+1)/2)*normalOS;

                float n2 =_NoiseScale*snoise((float3(v2.x+_NoiseOffset.x,v2.x+_NoiseOffset.y,v2.z+_NoiseOffset.z))*_NoiseFrequency);
                v2.xyz +=((n2+1)/2)*normalOS;

                float3 constructNormal =normalize(cross(v2-v0,v1-v0));

                float3x3 tangentToWorld = CreateTangentToWorld(constructNormal,tangentOS.xyz,tangentOS.w);

                

                output.normalWS =TransformObjectToWorldNormal(constructNormal);

                output.positionWS= TransformObjectToWorld(v0);

                output.positionCS = TransformObjectToHClip(v0);

                output.uv = uv;

                output.tangentToWorld =tangentToWorld;


                
                real sign = tangentOS.w * GetOddNegativeScale();
                float4 tangentWSOut = half4(tangentWS.xyz, sign);
                output.tangentWS = tangentWSOut;

 
                output.shadowCoord = TransformWorldToShadowCoord(output.positionWS);


                return output;
            }

            half Alpha(half albedoAlpha, half4 color, half cutoff)
            {
            #if !defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A) && !defined(_GLOSSINESS_FROM_BASE_ALPHA)
                half alpha = albedoAlpha * color.a;
            #else
                half alpha = color.a;
            #endif

            #if defined(_ALPHATEST_ON)
                clip(alpha - cutoff);
            #endif

                return alpha;
            }

            half4 SampleAlbedoAlpha(float2 uv, TEXTURE2D_PARAM(albedoAlphaMap, sampler_albedoAlphaMap))
            {
                return half4(SAMPLE_TEXTURE2D(albedoAlphaMap, sampler_albedoAlphaMap, uv));
            }

            half3 SampleNormal(float2 uv, TEXTURE2D_PARAM(bumpMap, sampler_bumpMap), half scale = half(1.0))
            {
            #ifdef _NORMALMAP
                half4 n = SAMPLE_TEXTURE2D(bumpMap, sampler_bumpMap, uv);
                #if BUMP_SCALE_NOT_SUPPORTED
                    return UnpackNormal(n);
                #else
                    return UnpackNormalScale(n, scale);
                #endif
            #else
                return half3(0.0h, 0.0h, 1.0h);
            #endif
            }

            half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
            {
                half4 specGloss;

            #ifdef _METALLICSPECGLOSSMAP
                specGloss = half4(SAMPLE_METALLICSPECULAR(uv));
                #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
                    specGloss.a = albedoAlpha * _Smoothness;
                #else
                    specGloss.a *= _Smoothness;
                #endif
            #else // _METALLICSPECGLOSSMAP
                #if _SPECULAR_SETUP
                    specGloss.rgb = _SpecColor.rgb;
                #else
                    specGloss.rgb = _Metallic.rrr;
                #endif

                #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
                    specGloss.a = albedoAlpha * _Smoothness;
                #else
                    specGloss.a = _Smoothness;
                #endif
            #endif

                return specGloss;
            }


            half SampleOcclusion(float2 uv)
            {
                #ifdef _OCCLUSIONMAP
                    // TODO: Controls things like these by exposing SHADER_QUALITY levels (low, medium, high)
                    #if defined(SHADER_API_GLES)
                        return SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
                    #else
                        half occ = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
                        return LerpWhiteTo(occ, _OcclusionStrength);
                    #endif
                #else
                    return half(1.0);
                #endif
            }

            


            inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
            {
                half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
                outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);
            
                half4 specGloss = SampleMetallicSpecGloss(uv, albedoAlpha.a);
                outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;
            
            #if _SPECULAR_SETUP
                outSurfaceData.metallic = half(1.0);
                outSurfaceData.specular = specGloss.rgb;
            #else
                outSurfaceData.metallic = specGloss.r;
                outSurfaceData.specular = half3(0.0, 0.0, 0.0);
            #endif
            
                outSurfaceData.smoothness = specGloss.a;
                outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_NormalMap, sampler_NormalMap), _NormalScale);
                outSurfaceData.occlusion = SampleOcclusion(uv);
                outSurfaceData.emission = 0;
            
            #if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
                half2 clearCoat = SampleClearCoat(uv);
                outSurfaceData.clearCoatMask       = clearCoat.r;
                outSurfaceData.clearCoatSmoothness = clearCoat.g;
            #else
                outSurfaceData.clearCoatMask       = half(0.0);
                outSurfaceData.clearCoatSmoothness = half(0.0);
            #endif
            
            #if defined(_DETAIL)
                half detailMask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, uv).a;
                float2 detailUv = uv * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
                outSurfaceData.albedo = ApplyDetailAlbedo(detailUv, outSurfaceData.albedo, detailMask);
                outSurfaceData.normalTS = ApplyDetailNormal(detailUv, outSurfaceData.normalTS, detailMask);
            #endif
            }


            void InitializeInputData(Interpolators input, half3 normalTS, out InputData inputData)
            {
                inputData = (InputData)0;

            #if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
                inputData.positionWS = input.positionWS;
            #endif

                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

            #if defined(_NORMALMAP)
                float sgn = input.tangentWS.w;      // should be either +1 or -1
                float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);

                #if defined(_NORMALMAP)
                inputData.tangentToWorld = tangentToWorld;
                #endif

                inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
            #else
                inputData.normalWS = input.normalWS;
            #endif

                inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
                inputData.viewDirectionWS = viewDirWS;

            }

            float3 FlowUVW (float2 uv, float2 flowVector, float time) {
                float progress = frac(time);
                float3 uvw;
                uvw.xy = uv - flowVector * progress;
                uvw.z = 1;
                return uvw;
            }

            half4 frag (Interpolators input) : SV_Target
            {

                SurfaceData surfaceData;
                InitializeStandardLitSurfaceData(input.uv, surfaceData);

                InputData inputData;
                InitializeInputData(input, surfaceData.normalTS, inputData);

                half shadow = MainLightRealtimeShadow(input.shadowCoord);

                float2 causticsUV= input.uv*6+_Time.xx;
                float4 caustics = SAMPLE_TEXTURE2D(_CausticsMap, sampler_CausticsMap, causticsUV);
			    



                half4 color = UniversalFragmentPBR(inputData, surfaceData);

                float4 finalColor = lerp(color*shadow,caustics*0.15,caustics.r);
                return finalColor;

            }
            ENDHLSL
        }


        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            // -------------------------------------
            // Universal Pipeline keywords

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
}
