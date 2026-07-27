Shader "Custom/LiangZhu/SceneLitFogged"
{
    Properties
    {
        [MainTexture] _BaseMap   ("Albedo", 2D) = "white" {}
        [MainColor]   _BaseColor ("Color",  Color) = (1, 1, 1, 1)

        [Header(Surface)]
        _Metallic   ("Metallic",   Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5

        [Header(PBR Maps)]
        [Toggle(_USE_PBR_MAPS)] _UseMaps ("Use Mask + Normal Maps", Float) = 0

        [NoScaleOffset] _MaskMap ("Mask (R:Metallic  G:AO  A:Smoothness)", 2D) = "white" {}

        [NoScaleOffset][Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 1.0

        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        [NoScaleOffset] _EmissionMap ("Emission Map", 2D) = "white" {}

        [Header(Reflection)]
        _ReflectionIntensity ("Reflection Intensity", Range(0, 1)) = 0.2

        [Toggle(_FOG_AFFECTS)] _FogEnable ("Affected By Sphere Fog", Float) = 1

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Instancing
            #pragma multi_compile_instancing

            // Lightmap
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON

            // 主光阴影
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            // 附加光（点光/聚光）
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            // 阴影品质
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            // 混合光照
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK

            // Forward+（URP Asset 选了 Forward+ 时需要）
            #pragma multi_compile _ _FORWARD_PLUS

            #pragma multi_compile_fog

            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _ENVIRONMENTREFLECTIONS_OFF

            #pragma shader_feature_local _FOG_AFFECTS

            #pragma shader_feature_local _USE_PBR_MAPS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"


            #include "SphereFogInclude.hlsl"

            TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_MaskMap);     SAMPLER(sampler_MaskMap);
            TEXTURE2D(_NormalMap);   SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _Metallic;
                float  _Smoothness;
                float4 _EmissionColor;
                float  _NormalScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT; 
                float2 uv         : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 3);
                half   fogFactor   : TEXCOORD4;
                half4  tangentWS   : TEXCOORD5;
                // float4 shadowCoord : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vpi = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   vni = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vpi.positionCS;
                output.positionWS = vpi.positionWS;
                output.normalWS   = vni.normalWS;
                output.tangentWS  = half4(vni.tangentWS, input.tangentOS.w * GetOddNegativeScale()); 
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                // output.shadowCoord = GetShadowCoord(vpi);

                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float3 V = normalize(GetCameraPositionWS() - input.positionWS);

                // ============ Albedo ============
                half4 albedoSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // ============ PBR 参数：贴图 or 常量 ============
                half metallic   = _Metallic;
                half smoothness = _Smoothness;
                half occlusion  = 1.0;

                #if defined(_USE_PBR_MAPS)
                half4 mask  = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv);
                metallic    = mask.r;
                occlusion   = mask.g;
                smoothness  = mask.a;
                #endif

                // ============ 法线：贴图 or 顶点法线 ============
                float3 N;
                #if defined(_USE_PBR_MAPS)
                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv),
                    _NormalScale);

                // TBN：把切线空间法线转到世界空间
                float3 bitangentWS = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                float3x3 tbn = float3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);
                N = normalize(mul(normalTS, tbn));
                #else
                N = normalize(input.normalWS);
                #endif

                // ============ 间接光数据 ============
                half3 bakedGI    = SAMPLE_GI(input.lightmapUV, input.vertexSH, N);
                half4 shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);

                // ============ BRDF ============
                BRDFData brdfData;
                InitializeBRDFData(albedoSample.rgb, metallic, half3(0,0,0), smoothness, albedoSample.a, brdfData);

                // ============ 主光 ============
                Light mainLight;
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                mainLight = GetMainLight(shadowCoord, input.positionWS, shadowMask);
                #else
                mainLight = GetMainLight(float4(0,0,0,0), input.positionWS, shadowMask);
                #endif

                // ============ 间接光（手动展开 GlobalIllumination，应用 AO） ============
                half3 reflectVector = reflect(-V, N);
                half  NoV           = saturate(dot(N, V));
                half  fresnelTerm   = Pow4(1.0 - NoV);

                half3 indirectDiffuse  = bakedGI * occlusion;
                half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, input.positionWS,
                                                                    brdfData.perceptualRoughness, occlusion, screenUV);

                half3 indirectColor = EnvironmentBRDF(brdfData, indirectDiffuse, indirectSpecular, fresnelTerm);

                // ============ 主光直接光 ============
                half3 mainDirect = LightingPhysicallyBased(brdfData, mainLight, N, V);

                // ============ 附加光 ============
                half3 additionalLighting = 0;
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = screenUV;

                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                    additionalLighting += LightingPhysicallyBased(brdfData, light, N, V);
                LIGHT_LOOP_END
                #endif

                // ============ Emission ============
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb
                                * _EmissionColor.rgb;

                // ============ 合成 ============
                half3 color = indirectColor + mainDirect + additionalLighting + emission;

                #if defined(_FOG_AFFECTS)
                color = SphereFog_Apply(color, input.positionWS);
                #endif

                color = MixFog(color, input.fogFactor);

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // 阴影投射 —— 不 include Shadows.hlsl，手写 ApplyShadowBias Shadows.hlsl 有问题
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // 这两个全局是 URP 在投影时自动设置的
            float3 _LightDirection;
            float4 _ShadowBias;   // x = depth bias, y = normal bias

            float3 ApplyShadowBiasInlined(float3 positionWS, float3 normalWS, float3 lightDir)
            {
                float invNdotL = 1.0 - saturate(dot(lightDir, normalWS));
                float scale    = invNdotL * _ShadowBias.y;
                positionWS = lightDir   * _ShadowBias.xxx + positionWS;
                positionWS = normalWS   * scale.xxx       + positionWS;
                return positionWS;
            }

            struct ShadowA
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct ShadowV
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowV ShadowVert(ShadowA i)
            {
                ShadowV o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 posWS = TransformObjectToWorld(i.positionOS.xyz);
                float3 nWS   = TransformObjectToWorldNormal(i.normalOS);
                float4 posCS = TransformWorldToHClip(ApplyShadowBiasInlined(posWS, nWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                o.positionCS = posCS;
                return o;
            }

            half4 ShadowFrag(ShadowV i) : SV_Target { return 0; }
            ENDHLSL
        }

        // 深度预 Pass —— 替换 UsePass，避免又拉进 Shadows.hlsl
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask 0 Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthA
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct DepthV
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthV DepthVert(DepthA i)
            {
                DepthV o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                return o;
            }

            half4 DepthFrag(DepthV i) : SV_Target { return 0; }
            ENDHLSL
        }

       
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex   MetaVert
            #pragma fragment MetaFrag
            #pragma shader_feature_local _USE_PBR_MAPS    // 新增：和主 Pass 同步
            #pragma shader_feature EDITOR_VISUALIZATION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_MaskMap);     SAMPLER(sampler_MaskMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _Metallic;
                float  _Smoothness;
                float4 _EmissionColor;
                float  _NormalScale;
            CBUFFER_END

            struct MetaA
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 uvLM       : TEXCOORD1;
                float2 uvDLM      : TEXCOORD2;
            };

            struct MetaV
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            MetaV MetaVert(MetaA i)
            {
                MetaV o;
                o.positionCS = MetaVertexPosition(i.positionOS, i.uvLM, i.uvDLM,
                                                  unity_LightmapST, unity_DynamicLightmapST);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 MetaFrag(MetaV i) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;

                // 用 MaskMap 时取贴图金属度，否则用常量
                half metallic = _Metallic;
                #if defined(_USE_PBR_MAPS)
                    metallic = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, i.uv).r;
                #endif

                MetaInput m;
                // 金属吸收 diffuse → 让 lightmap 不会把金属面反弹得过亮
                m.Albedo   = albedo * (1.0 - metallic);
                m.Emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb
                             * _EmissionColor.rgb;
                return MetaFragment(m);
            }
            ENDHLSL
        }
    }
}