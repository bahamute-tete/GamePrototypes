// =============================================================================
//  SceneLitFoggedDissolve.shader
//  URP 14.x / Unity 2022.3 / XR Single Pass Instanced / Mobile VR (PICO 4U)
//
//  在 SceneLitFogged 的基础上集成 Dither Dissolve 消失效果 + Detail Maps:
//    - PBR (GGX + Schlick + Energy Conserve)
//    - Metallic / AO / Smoothness MaskMap + Normal Map (开关式)
//    - Detail Maps (BaseMap + NormalMap + Mask, 开关式, 独立 tiling/offset)
//    - Sphere Fog (per-object) + Unity 内置雾
//    - Lightmap + SH + Shadowmask + Reflection Probe (Forward/Forward+ 都支持)
//    - Meta Pass (烘焙正确, 包含 detail albedo)
//    - Dissolve: 三模式 (Noise/Axis/Radial), 进度由外部 (Timeline+Controller) 驱动
//    - A2C 替代 clip() 保 early-Z, 运行时分支替代 keyword (per-renderer 独立)
//    - 溶解时同步 fade lighting/反射/自发光,避免 A2C 残留点的亮度问题
//
//  Detail Map 工作流:
//    - _DetailBaseMap (sRGB grey 128 ≠ 中性, 用 linearGrey 默认 = 中性 x2 mul)
//      混合公式: albedo *= lerp(1, detail*2, mask)
//      detail<0.5 压暗, =0.5 中性, >0.5 提亮
//    - _DetailNormalMap (有自己的 tiling/offset 让细节可以不同尺度平铺)
//      混合公式: BlendNormal(mainNormalTS, detailNormalTS) — Whiteout, 移动端最便宜
//    - _DetailMask (R 通道, 共用主 UV) 同时门控 detail 强度
//      mask=0 完全不显示 detail, mask=1 显示完整 detail
//
//  需要在 Material 同目录或可访问路径放 DissolveCore.hlsl,
//  并修改下面 SphereFogInclude.hlsl 的路径到你项目的实际位置。
// =============================================================================

Shader "Custom/LiangZhu/SceneLitFoggedDissolve"
{
    Properties
    {
        // ===== Base =====
        [MainTexture] _BaseMap   ("Albedo", 2D)         = "white" {}
        [MainColor]   _BaseColor ("Color",  Color)      = (1, 1, 1, 1)

        _Metallic   ("Metallic",   Range(0, 1)) = 0
        _Smoothness ("Smoothness (multiplier when PBR Maps on)", Range(0, 1)) = 0.5

        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        [NoScaleOffset] _EmissionMap ("Emission Map", 2D) = "white" {}

        // ===== Alpha Test (Cutout) =====
        [Header(Alpha Test)]
        // Alpha 来源 = _BaseMap.a * _BaseColor.a
        // ForwardLit 走 A2C coverage(保 early-Z, MSAA 自动抗锯齿);
        // Shadow / Depth / Meta 走 clip(depth-only / 烘焙 Pass 必须如此)。
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Enable Alpha Test (Cutout)", Float) = 0
        // 阈值: alpha < _Cutoff 的像素被裁掉; 滑杆调整 cutout 强度
        _Cutoff ("Alpha Cutoff (threshold)", Range(0, 1)) = 0.5

        // ===== PBR Maps =====
        [Header(PBRMaps)]
        [Toggle(_USE_PBR_MAPS)] _UseMaps ("Use Mask Map (Metallic+AO+Smoothness)", Float) = 0
        [NoScaleOffset] _MaskMap ("Mask (R:Metallic  G:AO  A:Smoothness)", 2D) = "white" {}

        // ===== Normal Map (独立开关,跟 Mask 解耦) =====
        [Header(NormalMap)]
        [Toggle(_USE_NORMAL_MAP)] _UseNormal ("Use Normal Map", Float) = 0
        [NoScaleOffset][Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 1.0

        // ===== Detail Maps (独立开关, 三贴图同步开关) =====
        [Header(Detail Maps)]
        [Toggle(_USE_DETAIL_MAP)] _UseDetailMap ("Use Detail Maps", Float) = 0
        // detail base: 用 linearGrey 默认 = 线性 0.5, x2 multiply 后 = 1.0 中性
        // 用 "gray" (sRGB 128) 默认会出现"开关一勾整体压暗"的奇怪现象
        _DetailBaseMap     ("Detail Albedo (RGB, x2 multiply)", 2D) = "linearGrey" {}
        [Normal] _DetailNormalMap ("Detail Normal Map", 2D) = "bump" {}
        [NoScaleOffset] _DetailMask ("Detail Mask (R, shares main UV)", 2D) = "white" {}
        _DetailNormalScale ("Detail Normal Scale", Range(0, 2)) = 1.0

        // ===== Sphere Fog =====
        [Header(SphereFog)]
        [Toggle(_FOG_AFFECTS)] _FogEnable ("Affected By Sphere Fog", Float) = 1

        // ===== Dissolve =====
        [Header(Dissolve)]
        // 进度 0..1,由 DissolveController 通过 MPB 覆盖 (不要在材质上手调)
        _DissolveAmount      ("Amount (driven by Controller)", Range(0, 1)) = 0

        _DissolveMode        ("Mode (0=Noise 1=Axis 2=Radial)", Float) = 0
        // 空间:0 = 物体本地空间(各自的 OS), 1 = 世界空间(整组共享)
        _DissolveSpace       ("Space (0=Local 1=World)", Float) = 1

        _DissolveEdgeWidth   ("Edge Soft Width",        Range(0.001, 0.5)) = 0.1
        _DissolveNoiseScale  ("Noise Scale (Noise mode)", Range(0.1, 10))   = 2.0

        // Noise 贴图:置空时走过程化 ValueNoise,赋值后走 triplanar 采样
        [NoScaleOffset] _DissolveNoiseTex ("Noise Texture (R)", 2D) = "white" {}
        _DissolveUseNoiseTex ("Use Noise Tex (0/1)", Float) = 0

        _DissolveAxis        ("Axis (xyz=dir, w=halfExtent)", Vector) = (0, 1, 0, 1)
        _DissolveAxisCenter  ("Axis Center Projection",       Float)  = 0

        _DissolveRadial      ("Radial (xyz=center, w=maxDist)", Vector) = (0, 0, 0, 1)
        _DissolveRadialReverse ("Radial Reverse (Outside-In)",        Float)  = 0

        [HDR] _DissolveEdgeColor ("Edge Color (HDR)",     Color)        = (1, 0.5, 0.1, 1)
        _DissolveEdgeIntensity   ("Edge Glow Intensity",  Range(0, 10)) = 3.0

        // 溶解时同步 fade lighting/反射/自发光,避免 A2C 残留点过亮
        // 0 = 不 fade (老行为), 1 = 线性, 2 = 二次 (推荐), 更大 = 更陡的末尾衰减
        _DissolveBrightnessPower ("Brightness Fade Power", Range(0, 8)) = 2.0

        // ===== Fresnel =====
        [Header(Fresnel)]
        [HDR] _FresnelColor    ("Fresnel Color (HDR)",          Color)         = (0.5, 0.7, 1.0, 1.0)
        _FresnelPower          ("Fresnel Power (falloff)",      Range(0.1, 16))= 3.0
        _FresnelIntensity      ("Fresnel Intensity (0=off)",    Range(0, 10))  = 0.0
        _FresnelBias           ("Fresnel Bias (offset)",        Range(-1, 1))  = 0.0

        // ===== Render State =====
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
		[Space(10)][Toggle(_BLINNPHONE_LIGHT)] _BlinnPhongLight("BlinnPhong光照", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        // =====================================================================
        // ForwardLit Pass : PBR + Detail + Dissolve (A2C path)
        // =====================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            // AlphaToMask 保证 dissolve 不触发 discard,保 early-Z
            // 要求 URP Asset 开启 MSAA (推荐 4x for VR)
            AlphaToMask On

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            // ---- URP keywords ----
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _ENVIRONMENTREFLECTIONS_OFF
            #pragma multi_compile_fog

            // ---- 本 Shader keywords ----
            #pragma shader_feature_local _USE_PBR_MAPS
            #pragma shader_feature_local _USE_NORMAL_MAP
            #pragma shader_feature_local _USE_DETAIL_MAP
            #pragma shader_feature_local _FOG_AFFECTS
            #pragma shader_feature_local _ALPHATEST_ON
			#pragma shader_feature_local_fragment _BLINNPHONE_LIGHT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #include "ShapeFogInclude.hlsl"

            TEXTURE2D(_BaseMap);          SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap);      SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_MaskMap);          SAMPLER(sampler_MaskMap);
            TEXTURE2D(_NormalMap);        SAMPLER(sampler_NormalMap);
            TEXTURE2D(_DetailBaseMap);    SAMPLER(sampler_DetailBaseMap);
            TEXTURE2D(_DetailNormalMap);  SAMPLER(sampler_DetailNormalMap);
            TEXTURE2D(_DetailMask);       SAMPLER(sampler_DetailMask);
            TEXTURE2D(_DissolveNoiseTex); SAMPLER(sampler_DissolveNoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _Metallic;
                float  _Smoothness;
                float4 _EmissionColor;
                float  _NormalScale;
                float4 _DetailBaseMap_ST;
                float4 _DetailNormalMap_ST;
                float  _DetailNormalScale;
                float  _DissolveAmount;

                // Dissolve(per-material 共享 + per-renderer 通过 MPB 覆盖)
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
                float4 _FresnelColor;
                float  _FresnelPower;
                float  _FresnelIntensity;
                float  _FresnelBias;
                float  _Cutoff;
            CBUFFER_END

            // Dissolve 数学(放在同目录),必须在 CBUFFER 之后 include
            #include "DissolveCore.hlsl"

            // Alpha -> Coverage (A2C cutout): 在 cutoff 处用屏幕导数做 ~1px 抗锯齿过渡,
            // 不使用 clip/discard => 保留 early-Z(对齐本 shader 的 dissolve A2C 策略)。
            // MSAA 关闭时退化为 cutoff 处的硬边裁切。
            half AlphaToCutoutCoverage(half alpha, half cutoff)
            {
                half aa = max(fwidth(alpha), 1e-5);
                return saturate((alpha - cutoff) / aa + 0.5);
            }

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
                float3 positionOS  : TEXCOORD6;   // Dissolve 的 Axis/Radial 需要
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
                output.positionOS = input.positionOS.xyz;
                output.normalWS   = vni.normalWS;
                output.tangentWS  = half4(vni.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor  = ComputeFogFactor(output.positionCS.z);

                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float3 V = normalize(GetCameraPositionWS() - input.positionWS);

                // ============ Albedo + PBR 参数 ============
                half4 albedoSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half metallic   = _Metallic;
                half smoothness = _Smoothness;
                half occlusion  = 1.0;

                #if defined(_USE_PBR_MAPS)
                    half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv);
                    metallic   = mask.r;
                    occlusion  = mask.g;
                    // mask.a 提供 "哪里反光" 的空间模式, _Smoothness 滑杆做整体乘子调节
                    //   _Smoothness=1: 用 mask 原值
                    //   _Smoothness=0: 完全哑光, 无视 mask
                    smoothness = mask.a * _Smoothness;
                #endif

                // ============ Detail Mask (Albedo + Normal 共用) ============
                #if defined(_USE_DETAIL_MAP)
                    half detailMask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, input.uv).r;

                    // Detail Albedo: 标准 x2 multiply (=Unity Lit shader 的 detailAlbedo 公式)
                    //   detail=0.5 → ×1.0 中性, <0.5 压暗, >0.5 提亮
                    //   mask 控制哪里应用 detail
                    float2 detailUV = TRANSFORM_TEX(input.uv, _DetailBaseMap);
                    half3 detailAlbedo = SAMPLE_TEXTURE2D(_DetailBaseMap, sampler_DetailBaseMap, detailUV).rgb;
                    albedoSample.rgb *= lerp(half3(1, 1, 1), detailAlbedo * 2.0h, detailMask);
                #endif

                // ============ Normal (主 + Detail 统一构 TBN) ============
                // 任一开关 ON 都要构 TBN, 在 tangent space 做混合后一次性 mul 到 world
                float3 N;
                #if defined(_USE_NORMAL_MAP) || defined(_USE_DETAIL_MAP)
                    // 起点: 平 tangent space normal
                    half3 normalTS = half3(0, 0, 1);

                    #if defined(_USE_NORMAL_MAP)
                        normalTS = UnpackNormalScale(
                            SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv),
                            _NormalScale);
                    #endif

                    #if defined(_USE_DETAIL_MAP)
                        float2 detailNormalUV = TRANSFORM_TEX(input.uv, _DetailNormalMap);
                        // Mask 直接乘进 scale, 实现 per-pixel 强度调制
                        half3 detailNormalTS = UnpackNormalScale(
                            SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, detailNormalUV),
                            _DetailNormalScale * detailMask);
                        // Whiteout blend (URP CommonMaterial.hlsl 自带), 移动端最便宜
                        normalTS = BlendNormal(normalTS, detailNormalTS);
                    #endif

                    float3 bitangentWS = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                    float3x3 tbn = float3x3(input.tangentWS.xyz, bitangentWS, input.normalWS);
                    N = normalize(mul(normalTS, tbn));
                #else
                    N = normalize(input.normalWS);
                #endif

                // ============ Indirect data ============
                half3 bakedGI    = SAMPLE_GI(input.lightmapUV, input.vertexSH, N);
                half4 shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);

                // ============ Emission ============
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb
                                 * _EmissionColor.rgb;

                half3 color = half3(0,0,0);
                #ifdef _BLINNPHONE_LIGHT
                    InputData inputData = (InputData)0;;
				    inputData.positionWS = input.positionWS;
				    inputData.viewDirectionWS = V;
				    inputData.shadowCoord = float4( 0, 0, 0, 0 );

				    inputData.normalWS = N;
				    inputData.fogCoord = 0;//IN.fogFactorAndVertexLight.x;
				    inputData.vertexLighting = half3(0,0,0);//IN.fogFactorAndVertexLight.yzw;
				    inputData.bakedGI = bakedGI;

					//half4 color = UniversalFragmentBlinnPhong(inputData, albedoSample, half4(Specular,1), Smoothness, emission, Alpha, input.normalWS);//inputData.normalWS
					color = UniversalFragmentBlinnPhong(inputData, albedoSample, half4(metallic,0,0,1), smoothness, emission, 1, input.normalWS).xyz;//inputData.normalWS
				#else
                    // ============ BRDF ============
                    BRDFData brdfData;
                    InitializeBRDFData(albedoSample.rgb, metallic, half3(0,0,0), smoothness, albedoSample.a, brdfData);

                    // ============ Main light ============
                    Light mainLight;
                    #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                        float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                        mainLight = GetMainLight(shadowCoord, input.positionWS, shadowMask);
                    #else
                        mainLight = GetMainLight(float4(0,0,0,0), input.positionWS, shadowMask);
                    #endif

                    // ============ Indirect (GlobalIllumination 展开版,5 参数 GlossyEnvRefl) ============
                    half3 reflectVector = reflect(-V, N);
                    half  NoV           = saturate(dot(N, V));
                    half  fresnelTerm   = Pow4(1.0 - NoV);

                    half3 indirectDiffuse  = bakedGI * occlusion;
                    half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, input.positionWS,
                                                                          brdfData.perceptualRoughness, occlusion, screenUV);

                    half3 indirectColor = EnvironmentBRDF(brdfData, indirectDiffuse, indirectSpecular, fresnelTerm);

                    // ============ Main light direct ============
                    half3 mainDirect = LightingPhysicallyBased(brdfData, mainLight, N, V);

                    // ============ Additional lights ============
                    half3 additionalLighting = 0;
                    #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                        InputData inputData = (InputData)0;
                        inputData.positionWS              = input.positionWS;
                        inputData.normalizedScreenSpaceUV = screenUV;

                        uint pixelLightCount = GetAdditionalLightsCount();
                        LIGHT_LOOP_BEGIN(pixelLightCount)
                            Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                            additionalLighting += LightingPhysicallyBased(brdfData, light, N, V);
                        LIGHT_LOOP_END
                    #endif

                    // ============ Composite ============
                    color = indirectColor + mainDirect + additionalLighting + emission;
				#endif

                // ============ Fresnel (rim) ============
                // 加在 PBR 之后、dissolve 之前:
                //   - 会随 dissolve 的 brightnessFade 一起淡出 (合理:rim 是表面光照的一部分)
                //   - 会被 SphereFog 影响 (合理:雾里的物体边缘也该变暗)
                // _FresnelIntensity = 0 时 UNITY_BRANCH 跳过整段,不付出代价
                UNITY_BRANCH
                if (_FresnelIntensity > 1e-4)
                {
                    half NoV = saturate(dot(N, V));
                    half f   = saturate(1.0 - NoV + _FresnelBias);
                    half fresnel = pow(f, max(_FresnelPower, 0.01));
                    color += _FresnelColor.rgb * fresnel * _FresnelIntensity;
                }

                // ============ Dissolve (运行时分支,非 keyword) ============
                // amount=0: 完全没 dissolve 开销 (分支跳过,coherent across draw call)
                // amount>0: 应用 A2C alpha + fade lighting/reflection 避免亮点
                // ============ Alpha Test (cutout, A2C path) ============
                // 走 coverage 不走 clip => 保 early-Z; 与 dissolve coverage 相乘合成,
                // 任一为 0 该像素即被裁掉。
                half outputAlpha = 1.0;
                #if defined(_ALPHATEST_ON)
                    #if UNITY_ANDROID
                        outputAlpha = AlphaToCutoutCoverage(albedoSample.a, _Cutoff);
					#else
                        clip(albedoSample.a - _Cutoff);
					#endif
                #endif

                UNITY_BRANCH
                if (_DissolveAmount > 0.0001)
                {
                    float2 dissolve = ComputeDissolveAlphaAndEdge(input.positionWS, input.positionOS, input.positionCS.xy, _DissolveAmount);
                    outputAlpha *= dissolve.x;

                    // 关键:把表面 lighting/反射/自发光按消融进度同步压暗,
                    // 这样 A2C 残留的少量像素不会保留亮的 specular/reflection,
                    // amount=1 时 surface 趋近 0,只剩下面那行 edge 发光 (它自身在 amount→1 时也由 edge 因子自然衰减)。
                    half brightnessFade = pow(saturate(1.0 - _DissolveAmount), max(_DissolveBrightnessPower, 0.01));
                    color *= brightnessFade;

                    // Edge glow:本身就在 amount∈(0,1) 之间最强, 端点为 0
                    color += _DissolveEdgeColor.rgb * dissolve.y * _DissolveEdgeIntensity;
                }

                // ============ Fog (Sphere + Unity) ============
                #if defined(_FOG_AFFECTS)
                    color = SphereFog_Apply(color, input.positionWS);
                #endif
                color = MixFog(color, input.fogFactor);

                return half4(color, outputAlpha);
            }
            ENDHLSL
        }

        // =====================================================================
        // ShadowCaster Pass : 阴影投射(无 A2C,用 clip)
        // =====================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On  ZTest LEqual  ColorMask 0  Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Unity 自动注入,不通过 include 拉 Shadows.hlsl(URP 14 include 链有坑)
            float3 _LightDirection;
            float4 _ShadowBias;        // x: depth bias, y: normal bias

            TEXTURE2D(_DissolveNoiseTex); SAMPLER(sampler_DissolveNoiseTex);
            #if defined(_ALPHATEST_ON)
                TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
            #endif

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _Metallic;
                float  _Smoothness;
                float4 _EmissionColor;
                float  _NormalScale;
                float4 _DetailBaseMap_ST;
                float4 _DetailNormalMap_ST;
                float  _DetailNormalScale;
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
                float4 _FresnelColor;
                float  _FresnelPower;
                float  _FresnelIntensity;
                float  _FresnelBias;
                float  _Cutoff;
            CBUFFER_END

            // Dissolve 数学,必须在 CBUFFER 之后 include
            #include "DissolveCore.hlsl"

            float3 ApplyShadowBiasInlined(float3 positionWS, float3 normalWS, float3 lightDir)
            {
                float invNdotL = 1.0 - saturate(dot(lightDir, normalWS));
                float scale    = invNdotL * _ShadowBias.y;
                positionWS = lightDir * _ShadowBias.xxx + positionWS;
                positionWS = normalWS * scale.xxx       + positionWS;
                return positionWS;
            }

            struct ShadowA
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                #if defined(_ALPHATEST_ON)
                    float2 uv     : TEXCOORD0;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowV
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                #if defined(_ALPHATEST_ON)
                    float2 uv     : TEXCOORD2;
                #endif
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
                o.positionWS = posWS;
                o.positionOS = i.positionOS.xyz;
                #if defined(_ALPHATEST_ON)
                    o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                #endif
                return o;
            }

            half4 ShadowFrag(ShadowV i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a;
                    clip(a - _Cutoff);
                #endif

                // v3.2: shadow map 是光源空间二值 mask, 不需要屏幕空间 dither
                //   原版: ComputeDissolveAlphaAndEdge (field + IGN + edge + 3 step) ~100 ALU
                //   现在: ComputeDissolveFieldClip (只有 field 计算)           ~30 ALU
                //   在 cascade 模式下一帧跑 4 次, 收益线性放大
                UNITY_BRANCH
                if (_DissolveAmount > 0.0001)
                {
                    float field = ComputeDissolveFieldClip(i.positionWS, i.positionOS);
                    clip(field - _DissolveAmount);
                }
                return 0;
            }
            ENDHLSL
        }

        // =====================================================================
        // DepthOnly Pass : 深度预 Pass(无 A2C,同 ShadowCaster 用 clip)
        // =====================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On  ColorMask 0  Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_DissolveNoiseTex); SAMPLER(sampler_DissolveNoiseTex);
            #if defined(_ALPHATEST_ON)
                TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
            #endif

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _Metallic;
                float  _Smoothness;
                float4 _EmissionColor;
                float  _NormalScale;
                float4 _DetailBaseMap_ST;
                float4 _DetailNormalMap_ST;
                float  _DetailNormalScale;
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
                float4 _FresnelColor;
                float  _FresnelPower;
                float  _FresnelIntensity;
                float  _FresnelBias;
                float  _Cutoff;
            CBUFFER_END

            // Dissolve 数学,必须在 CBUFFER 之后 include
            #include "DissolveCore.hlsl"

            struct DepthA
            {
                float4 positionOS : POSITION;
                #if defined(_ALPHATEST_ON)
                    float2 uv     : TEXCOORD0;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthV
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                #if defined(_ALPHATEST_ON)
                    float2 uv     : TEXCOORD2;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthV DepthVert(DepthA i)
            {
                DepthV o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionOS = i.positionOS.xyz;
                #if defined(_ALPHATEST_ON)
                    o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                #endif
                return o;
            }

            half4 DepthFrag(DepthV i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a;
                    clip(a - _Cutoff);
                #endif

                // v3.2: DepthOnly 同 ShadowCaster, 是单 channel 深度值, 不需 dither
                UNITY_BRANCH
                if (_DissolveAmount > 0.0001)
                {
                    float field = ComputeDissolveFieldClip(i.positionWS, i.positionOS);
                    clip(field - _DissolveAmount);
                }
                return 0;
            }
            ENDHLSL
        }

        // =====================================================================
        // Meta Pass : Lightmap 烘焙(不应用 Dissolve,baker 永远看到完整 mesh)
        //             Detail Albedo 影响 bake (法线不影响 bake)
        // =====================================================================
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex   MetaVert
            #pragma fragment MetaFrag
            #pragma shader_feature_local _USE_PBR_MAPS
            #pragma shader_feature_local _USE_DETAIL_MAP
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature EDITOR_VISUALIZATION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap);    SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_MaskMap);        SAMPLER(sampler_MaskMap);
            TEXTURE2D(_DetailBaseMap);  SAMPLER(sampler_DetailBaseMap);
            TEXTURE2D(_DetailMask);     SAMPLER(sampler_DetailMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _Metallic;
                float  _Smoothness;
                float4 _EmissionColor;
                float  _NormalScale;
                float4 _DetailBaseMap_ST;
                float4 _DetailNormalMap_ST;
                float  _DetailNormalScale;
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
                float4 _FresnelColor;
                float  _FresnelPower;
                float  _FresnelIntensity;
                float  _FresnelBias;
                float  _Cutoff;
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
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);

                #if defined(_ALPHATEST_ON)
                    clip(baseSample.a * _BaseColor.a - _Cutoff);
                #endif

                half3 albedo = baseSample.rgb * _BaseColor.rgb;

                #if defined(_USE_DETAIL_MAP)
                    half  detailMask   = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, i.uv).r;
                    float2 detailUV    = TRANSFORM_TEX(i.uv, _DetailBaseMap);
                    half3  detailAlbedo = SAMPLE_TEXTURE2D(_DetailBaseMap, sampler_DetailBaseMap, detailUV).rgb;
                    albedo *= lerp(half3(1, 1, 1), detailAlbedo * 2.0h, detailMask);
                #endif

                half metallic = _Metallic;
                #if defined(_USE_PBR_MAPS)
                    metallic = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, i.uv).r;
                #endif

                MetaInput m;
                m.Albedo   = albedo * (1.0 - metallic);   // 金属吸收 diffuse
                m.Emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb
                             * _EmissionColor.rgb;
                return MetaFragment(m);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
