// =============================================================================
//  SceneGlassFogged.shader
//  URP 14.x / Unity 2022.3 / XR Single Pass Instanced / Mobile VR
//
//  特性:
//    - PBR (GGX) / Blinn-Phong 双模式, 通过 _USE_PBR 切换
//    - Mask Map (R:Metallic G:AO A:Smoothness) — 开关 _USE_PBR_MAPS
//    - Normal Map (支持 tiling/offset) — 独立开关 _USE_NORMAL_MAP
//      Mask 和 Normal 完全解耦, 任意组合
//    - PBR Maps 模式下 _Smoothness 滑杆作为 mask.a 的乘子 (整体强度调节)
//    - 透明玻璃 + 自定义 Fresnel Rim + Refraction + Sphere Fog
//    - 标准 Alpha Blend: 贴图 alpha 通道直接驱动软透明 (无硬边, 无 clip)
//    - Dissolve 字段/调用方式与 SceneLitFoggedDissolve 完全一致
//    - 反射探针在 Forward+ 下正确采样 (GlossyEnvironmentReflection 5-arg)
//
//  ---- Alpha pipeline (纯 alpha blend, 软透明) ----
//    1. base.a = tex.a * _Color.a   贴图 alpha × 材质 alpha
//       (_Color.a 默认 1.0, 这样贴图 alpha 就是主控. 想整体调暗透明度, 把 _Color.a 调低)
//    2. 输出 a = saturate(base.a + fresnel * _FresnelColor.a)
//       blend 阶段 (Blend SrcAlpha OneMinusSrcAlpha) 按这个 alpha 软混合 framebuffer
//    3. 折射模式: backColor 替换 indirect diffuse, 玻璃染色由 brdfData.diffuse 或 base.rgb 完成
//       alpha 处理与非折射完全一致, 由 blend 软混合
//
//  Forward+ 反射探针: GlossyEnvironmentReflection 必须用 5-arg 重载.
// =============================================================================

Shader "Custom/LiangZhu/SceneGlassFoggedDissolve"
{
    Properties
    {
        [MainTexture] _MainTex ("Base Texture (RGB color, A opacity)", 2D) = "white" {}
        [MainColor]   _Color   ("Tint (alpha = global opacity multiplier)", Color) = (1, 1, 1, 1)

        [Header(Lighting Model)]
        [Toggle(_USE_PBR)] _UsePBR ("Use PBR Lighting (off = Blinn Phong)", Float) = 0

        [Header(Surface)]
        _Smoothness    ("Smoothness (multiplier when PBR Maps on)", Range(0, 1)) = 0.95
        _Metallic      ("Metallic (PBR only)",                Range(0, 1)) = 0
        _SpecIntensity ("Specular Intensity (Blinn Phong)",   Range(0, 8)) = 1.0

        [Header(PBR Maps)]
        [Toggle(_USE_PBR_MAPS)] _UseMaps ("Use Mask Map (R:Metallic  G:AO  A:Smoothness)", Float) = 0
        [NoScaleOffset] _MaskMap   ("Mask Map", 2D) = "white" {}

        [Header(Normal Map)]
        [Toggle(_USE_NORMAL_MAP)] _UseNormalMap ("Use Normal Map", Float) = 0
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 1.0

        [Header(Fresnel (custom rim, both modes))]
        [HDR] _FresnelColor ("Fresnel Color",      Color)         = (1, 1, 1, 1)
        _FresnelPower       ("Fresnel Power",      Range(0.1, 10))= 4
        _FresnelIntensity   ("Fresnel Intensity",  Range(0, 4))   = 1

        [Header(Reflection)]
        _ReflectIntensity ("Env Reflection",       Range(0, 1))   = 0.4

        [Header(Refraction (requires Opaque Texture in URP Asset))]
        [Toggle(_REFRACTION_ON)] _RefractionEnable ("Enable Refraction", Float) = 0
        _RefractStrength ("Refraction Distortion", Range(0, 0.1)) = 0.02

        [Header(Sphere Fog)]
        [Toggle(_FOG_AFFECTS_GLASS)] _FogEnable ("Affected By Sphere Fog", Float) = 1

        // =====================================================================
        // Dissolve  (字段名 / 类型 / 默认值 与 SceneLitFoggedDissolve 完全一致)
        // =====================================================================
        [Header(Dissolve)]
        _DissolveAmount      ("Amount (driven by Controller)", Range(0, 1)) = 0
        _DissolveMode        ("Mode (0=Noise 1=Axis 2=Radial)", Float) = 0
        _DissolveSpace       ("Space (0=Local 1=World)",        Float) = 1
        _DissolveEdgeWidth   ("Edge Soft Width",        Range(0.001, 0.5)) = 0.1
        _DissolveNoiseScale  ("Noise Scale (Noise mode)", Range(0.1, 10))   = 2.0
        [NoScaleOffset] _DissolveNoiseTex ("Noise Texture (R)", 2D) = "white" {}
        _DissolveUseNoiseTex ("Use Noise Tex (0/1)", Float) = 0
        _DissolveAxis        ("Axis (xyz=dir, w=halfExtent)", Vector) = (0, 1, 0, 1)
        _DissolveAxisCenter  ("Axis Center Projection",       Float)  = 0
        _DissolveRadial      ("Radial (xyz=center, w=maxDist)", Vector) = (0, 0, 0, 1)
        _DissolveRadialReverse ("Radial Reverse (Outside-In)",  Float) = 0
        [HDR] _DissolveEdgeColor ("Edge Color (HDR)",     Color)        = (1, 0.5, 0.1, 1)
        _DissolveEdgeIntensity   ("Edge Glow Intensity",  Range(0, 10)) = 3.0
        _DissolveBrightnessPower ("Brightness Fade Power", Range(0, 8)) = 2.0

        [Header(Render State)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2  // 2 = Back
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        // 标准 Alpha Blend (软透明, 贴图 alpha 通道驱动)
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull [_Cull]

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            // ---- 本 Shader 自己的 keywords ----
            #pragma shader_feature_local _USE_PBR
            #pragma shader_feature_local _USE_PBR_MAPS
            #pragma shader_feature_local _USE_NORMAL_MAP
            #pragma shader_feature_local _REFRACTION_ON
            #pragma shader_feature_local _FOG_AFFECTS_GLASS

            // ---- URP keywords (Forward+ / 反射探针 / 阴影 / 附加光) ----
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _ENVIRONMENTREFLECTIONS_OFF

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #include "ShapeFogInclude.hlsl"

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskMap);    SAMPLER(sampler_MaskMap);
            TEXTURE2D(_NormalMap);  SAMPLER(sampler_NormalMap);

            #if defined(_REFRACTION_ON)
                TEXTURE2D_X(_CameraOpaqueTexture);
                SAMPLER(sampler_CameraOpaqueTexture);
            #endif

            TEXTURE2D(_DissolveNoiseTex); SAMPLER(sampler_DissolveNoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NormalMap_ST;
                float4 _Color;
                float  _Smoothness;
                float  _Metallic;
                float  _SpecIntensity;
                float  _NormalScale;
                float4 _FresnelColor;
                float  _FresnelPower;
                float  _FresnelIntensity;
                float  _ReflectIntensity;
                float  _RefractStrength;

                // ===== Dissolve (与 SceneLitFoggedDissolve 同名同类型同顺序) =====
                float  _DissolveAmount;
                float  _DissolveMode;
                float  _DissolveEdgeWidth;
                float  _DissolveNoiseScale;
                float4 _DissolveAxis;
                float  _DissolveAxisCenter;
                float4 _DissolveRadial;
                float  _DissolveRadialReverse;
                float4 _DissolveEdgeColor;
                float  _DissolveEdgeIntensity;
                float  _DissolveBrightnessPower;
                float  _DissolveSpace;
                float  _DissolveUseNoiseTex;
            CBUFFER_END

            #include "DissolveCore.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 uv          : TEXCOORD2;   // .xy = _MainTex / _MaskMap UV, .zw = _NormalMap UV
                half3  vertexSH    : TEXCOORD3;
                float3 positionOS  : TEXCOORD4;
                half4  tangentWS   : TEXCOORD5;
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
                VertexNormalInputs   vni = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vpi.positionCS;
                output.positionWS = vpi.positionWS;
                output.normalWS   = vni.normalWS;
                output.tangentWS  = half4(vni.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv.xy      = TRANSFORM_TEX(input.uv, _MainTex);
                output.uv.zw      = TRANSFORM_TEX(input.uv, _NormalMap);
                output.positionOS = input.positionOS.xyz;
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float3 V = normalize(GetCameraPositionWS() - input.positionWS);

                // ============ 1. Base 颜色 + Alpha (软透明的源头) ============
                // tex.a 是贴图 alpha 通道, _Color.a 是材质上的整体透明度滑杆
                // 想让贴图 alpha 完全主导, 把 _Color.a 设为 1
                // 想整体半透明, 把 _Color.a 调低
                half4 tex  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv.xy);
                half4 base = tex * _Color;

                // ============ 2. Normal Map (可选, 独立开关, 支持 tiling) ============
                float3 N;
                #if defined(_USE_NORMAL_MAP)
                    half3 normalTS = UnpackNormalScale(
                        SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv.zw),
                        _NormalScale);
                    float3 bitangentWS = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                    float3x3 tbn = float3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);
                    N = normalize(mul(normalTS, tbn));
                #else
                    N = normalize(input.normalWS);
                #endif

                // ============ 3. 自定义 Fresnel rim ============
                float NdotV   = saturate(dot(N, V));
                float fresnel = pow(1.0 - NdotV, _FresnelPower) * _FresnelIntensity;

                // ============ 4. 折射采样 ============
                #if defined(_REFRACTION_ON)
                    // distortion *= base.a: alpha=0 处不浪费 cache 采远偏移像素 (反正会被 blend 丢弃)
                    float2 distortion = N.xy * _RefractStrength * base.a;
                    half3 backColor = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture,
                                          sampler_CameraOpaqueTexture,
                                          screenUV + distortion).rgb;
                #endif

                // ============ 5. SH 间接光 ============
                half3 indirectGI = SampleSH(N) + input.vertexSH;

                // ============ 6. Lighting ============
                half3 surfaceColor;

                #if defined(_USE_PBR)
                    // ============ PBR (GGX) ============
                    half metallic   = _Metallic;
                    half smoothness = _Smoothness;
                    half occlusion  = 1.0;
                    #if defined(_USE_PBR_MAPS)
                        half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv.xy);
                        metallic   = mask.r;
                        occlusion  = mask.g;
                        // mask.a 提供 "哪里反光" 的空间模式, _Smoothness 滑杆做整体乘子调节
                        //   _Smoothness=1: 用 mask 原值
                        //   _Smoothness=0: 完全哑光, 无视 mask
                        smoothness = mask.a * _Smoothness;
                    #endif

                    BRDFData brdfData;
                    InitializeBRDFData(base.rgb, metallic, half3(0,0,0), smoothness, base.a, brdfData);

                    Light mainLight;
                    #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                        float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                        mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1,1,1,1));
                    #else
                        mainLight = GetMainLight();
                    #endif

                    half3 reflectVector = reflect(-V, N);
                    half  fresnelTerm   = Pow4(1.0 - NdotV);

                    // 折射: 透射光 (backColor) 替换 indirect diffuse
                    // EnvironmentBRDF 会乘 brdfData.diffuse 自动用玻璃颜色给透射染色
                    #if defined(_REFRACTION_ON)
                        half3 indirectDiffuse  = backColor;
                    #else
                        half3 indirectDiffuse  = indirectGI * occlusion;
                    #endif

                    half3 indirectSpecular = GlossyEnvironmentReflection(
                                                reflectVector,
                                                input.positionWS,
                                                brdfData.perceptualRoughness,
                                                occlusion,
                                                screenUV) * _ReflectIntensity;

                    half3 indirectColor = EnvironmentBRDF(brdfData, indirectDiffuse, indirectSpecular, fresnelTerm);

                    half3 mainDirect = LightingPhysicallyBased(brdfData, mainLight, N, V);

                    half3 additionalLighting = 0;
                    #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                        InputData inputData = (InputData)0;
                        inputData.positionWS              = input.positionWS;
                        inputData.normalizedScreenSpaceUV = screenUV;

                        uint pixelLightCount = GetAdditionalLightsCount();
                        LIGHT_LOOP_BEGIN(pixelLightCount)
                            Light light = GetAdditionalLight(lightIndex, input.positionWS);
                            additionalLighting += LightingPhysicallyBased(brdfData, light, N, V);
                        LIGHT_LOOP_END
                    #endif

                    surfaceColor = indirectColor + mainDirect + additionalLighting;

                #else
                    // ============ Blinn-Phong ============
                    Light  mainLight = GetMainLight();
                    float3 L = normalize(mainLight.direction);
                    float3 H = normalize(L + V);
                    float  specPow  = exp2(_Smoothness * 11.0) + 2.0;
                    float  specMask = pow(saturate(dot(N, H)), specPow);
                    float3 spec = mainLight.color * specMask * _SpecIntensity;
                    float3 R = reflect(-V, N);
                    half3 reflection = GlossyEnvironmentReflection(
                                            R,
                                            input.positionWS,
                                            1.0 - _Smoothness,
                                            1.0,
                                            screenUV) * _ReflectIntensity;

                    // 折射: 透射光 (backColor * base.rgb 染色) 作为漫反射基底
                    #if defined(_REFRACTION_ON)
                        surfaceColor = backColor * base.rgb + spec + reflection;
                    #else
                        surfaceColor = base.rgb + indirectGI * 0.0 + spec + reflection;
                    #endif
                #endif

                // ============ 7. Composite (标准 alpha blend 软透明) ============
                // 这里就是关键: rgb 给 surface, a 给 base.a (贴图 alpha × _Color.a)
                // blend 阶段会按 a 做 SrcAlpha OneMinusSrcAlpha 软混合, 没有任何硬切
                half3 fresnelRGB = _FresnelColor.rgb * fresnel;
                half3 rgb = surfaceColor + fresnelRGB;
                half  a   = saturate(base.a + fresnel * _FresnelColor.a);

                // ============ 8. Dissolve ============
                UNITY_BRANCH
                if (_DissolveAmount > 0.0001)
                {
                    float2 dissolve = ComputeDissolveAlphaAndEdge(input.positionWS, input.positionOS, input.positionCS.xy, _DissolveAmount);

                    half brightnessFade = pow(saturate(1.0 - _DissolveAmount), max(_DissolveBrightnessPower, 0.01));
                    rgb *= brightnessFade;

                    rgb += _DissolveEdgeColor.rgb * dissolve.y * _DissolveEdgeIntensity;
                    a   *= dissolve.x;
                }

                // ============ 9. Sphere Fog ============
                #if defined(_FOG_AFFECTS_GLASS)
                    float fogFactor = SphereFog_GetFactor(input.positionWS);
                    rgb = lerp(rgb, _SF_FogColor.rgb, fogFactor);
                    a   = saturate(a * (1.0 - fogFactor));
                #endif

                return half4(rgb, a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
