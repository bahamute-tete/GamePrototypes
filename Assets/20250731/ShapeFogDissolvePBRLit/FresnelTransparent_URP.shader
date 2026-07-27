// =============================================================================
//  FresnelTransparent_URP.shader  +  Unified Dissolve  +  Normal Map
//
//  Normal Map 用途: 给低模丰富 Fresnel 细节. Fresnel = pow(1 - N·V, power),
//  N 在低模上是顶点法线插值, 边缘是平滑梯度. Normal Map 把每像素法线扰动,
//  Fresnel 强度跟着像素粒度变化, 在轮廓上产生细碎高频图案
//  (金属拉丝 / 布料起伏 / 龟壳裂纹这种感觉).
//
//  三个 Pass 都要正确处理 dissolve:
//    DepthOcclusion : clip(dissolve.x - 0.5)
//    ForwardLit     : alpha *= dissolve.x + brightness fade + edge glow
//    ShadowCaster   : clip(dissolve.x - 0.5)
//
//  Normal Map 只有 ForwardLit Pass 用到. 但 _NormalMap_ST / _NormalScale 必须
//  在 3 个 Pass 的 CBUFFER(UnityPerMaterial) 里都声明, 否则 SRP Batcher 失效.
// =============================================================================

Shader "Custom/LiangZhu/FresnelTransparent"
{
    Properties
    {
        _BaseColor            ("Base Color",      Color)           = (0.3, 0.6, 1.0, 0.15)
        _FresnelColor         ("Fresnel Color",   Color)           = (0.5, 0.8, 1.0, 1.0)
        _FresnelPower         ("Fresnel Power",   Range(0.5, 8.0)) = 3.0
        _FresnelScale         ("Fresnel Scale",   Range(0.0, 2.0)) = 1.0
        _Alpha                ("Base Alpha",      Range(0.0, 1.0)) = 0.1

        [Space(10)]
        [Header(Alpha_Map)]
        // Default "white" = full alpha so material works without a texture assigned.
        // Sampled as .a by default - change to .r if you use a grayscale mask.
        _AlphaMap             ("Alpha Map",       2D)              = "white" {}
        // Clip threshold for depth occlusion & shadow caster passes.
        // Pixels with alpha below this won't write depth or cast shadows.
        _AlphaCutoff          ("Alpha Cutoff",    Range(0.0, 1.0)) = 0.1

        [Space(10)]
        [Header(Normal Map (enrich Fresnel detail on low poly meshes))]
        [Toggle(_USE_NORMALMAP)] _UseNormalMap ("Use Normal Map", Float) = 0
        [Normal] _NormalMap   ("Normal Map",       2D)              = "bump" {}
        _NormalScale          ("Normal Scale",     Range(0.0, 2.0)) = 1.0

        [Space(10)]
        [Header(Shadow_Settings)]
        [Toggle(_CAST_SHADOWS_ON)]
        _CastShadowsToggle    ("Cast Shadows",    Float)           = 1
        [Toggle(_RECEIVE_SHADOWS_ON)]
        _ReceiveShadowsToggle ("Receive Shadows", Float)           = 1
        _ShadowStrength       ("Shadow Strength", Range(0.0, 1.0)) = 0.6


        [Space(10)]
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
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent"
        }

        // ---------------------------------------------------------
        // Pass 1 - Depth Occlusion (Cull Front + ColorMask 0)
        // 写背面深度让物体内部自遮挡, 溶解区域必须 clip.
        // 不需要 Normal Map, 但 CBUFFER 字段要与其他 Pass 完全一致 (SRP Batcher).
        // ---------------------------------------------------------
        Pass
        {
            Name "DepthOcclusion"

            Cull      Front
            ZWrite    On
            ZTest     LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   vert_depth
            #pragma fragment frag_depth

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_DissolveNoiseTex); SAMPLER(sampler_DissolveNoiseTex);
            TEXTURE2D(_AlphaMap);
            SAMPLER(sampler_AlphaMap);


            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                half4  _FresnelColor;
                half   _FresnelPower;
                half   _FresnelScale;
                half   _Alpha;
                float4 _NormalMap_ST;   // 3 Pass 一致
                half   _NormalScale;    // 3 Pass 一致
                half   _ShadowStrength;
                float4 _AlphaMap_ST;
                half   _AlphaCutoff;

                // ===== Dissolve =====
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
                float2 uv         : TEXCOORD0; 
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 positionOS  : TEXCOORD2;
            };

            Varyings vert_depth(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vpi.positionCS;
                OUT.positionWS  = vpi.positionWS;
                OUT.positionOS  = IN.positionOS.xyz;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _NormalMap);  // ← 修复 GLSL link error
                return OUT;
            }

            half4 frag_depth(Varyings IN) : SV_Target
            {
                // v3.2: 透明物体 DepthOcclusion 写背面深度, 需要和 ForwardLit
                //   看到的"被消融的像素"一致 → 必须保留 dither
                //   但不需要 edge 计算 → 用 AlphaOnly 版 (省 3 个 step + saturate + abs)
                UNITY_BRANCH
                if (_DissolveAmount > 0.0001)
                {
                    float alpha = ComputeDissolveAlphaOnly(IN.positionWS, IN.positionOS, IN.positionHCS.xy, _DissolveAmount);
                    clip(alpha - 0.5);
                }

                half a = SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, IN.uv).r;
                clip(a - _AlphaCutoff);

                return 0;
                
            }
            ENDHLSL
        }

        // ---------------------------------------------------------
        // Pass 2 - Fresnel Transparent + Receive Shadows + Normal Map
        // ---------------------------------------------------------
        Pass
        {
            Name "FresnelTransparent"
            Tags { "LightMode" = "UniversalForward" }

            Cull   Back
            ZWrite Off
            ZTest  LEqual
            Blend  SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #pragma shader_feature_local _USE_NORMALMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_DissolveNoiseTex); SAMPLER(sampler_DissolveNoiseTex);
            TEXTURE2D(_NormalMap);        SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                half4  _FresnelColor;
                half   _FresnelPower;
                half   _FresnelScale;
                half   _Alpha;
                float4 _NormalMap_ST;
                half   _NormalScale;
                half   _ShadowStrength;
                float4 _AlphaMap_ST;
                half   _AlphaCutoff;

                // ===== Dissolve =====
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
            TEXTURE2D(_AlphaMap);
            SAMPLER(sampler_AlphaMap);

            #include "DissolveCore.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;     // Normal Map TBN 需要
                float2 uv         : TEXCOORD0;   // Normal Map 采样需要
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionOS  : TEXCOORD2;   // Dissolve Local 空间需要
                half4  tangentWS   : TEXCOORD3;   // Normal Map TBN, w = bitangent sign
                float2 uv          : TEXCOORD4;
            #if defined(_RECEIVE_SHADOWS_ON) && defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD5;
            #endif
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs  = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = normInputs.normalWS;
                OUT.positionOS  = IN.positionOS.xyz;
                OUT.tangentWS   = half4(normInputs.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
                OUT.uv          = TRANSFORM_TEX(IN.uv, _NormalMap);

            #if defined(_RECEIVE_SHADOWS_ON) && defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                OUT.shadowCoord = GetShadowCoord(posInputs);
            #endif
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half maskA = SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, IN.uv).r;
                // ---- Normal ----
                half3 N;
            #if defined(_USE_NORMALMAP)
                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv),
                    _NormalScale);
                float3 bitangentWS = IN.tangentWS.w * cross(IN.normalWS, IN.tangentWS.xyz);
                float3x3 tbn = float3x3(IN.tangentWS.xyz, bitangentWS, IN.normalWS);
                N = normalize(mul(normalTS, tbn));
            #else
                N = normalize(IN.normalWS);
            #endif

                // ---- Fresnel ----
                half3 V       = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half  NdotV   = saturate(dot(N, V));
                half  fresnel = pow(1.0h - NdotV, _FresnelPower) * _FresnelScale;

                half3 color = lerp(_BaseColor.rgb, _FresnelColor.rgb, fresnel);
                half  alpha = saturate(_Alpha + fresnel * _FresnelColor.a);

                // ---- Receive Shadows ----
            #if defined(_RECEIVE_SHADOWS_ON)
                float4 shadowCoord;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    shadowCoord = IN.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                    shadowCoord = float4(0, 0, 0, 0);
                #endif

                Light mainLight    = GetMainLight(shadowCoord);
                half  shadowAtten  = mainLight.shadowAttenuation;
                half  shadowFactor = lerp(1.0h, shadowAtten, _ShadowStrength);

                color *= shadowFactor;
                alpha  = saturate(alpha + (1.0h - shadowFactor) * _ShadowStrength * 0.3h);
            #endif
                alpha *= maskA;

                // ---- Dissolve ----
                UNITY_BRANCH
                if (_DissolveAmount > 0.0001)
                {
                    float2 dissolve = ComputeDissolveAlphaAndEdge(IN.positionWS, IN.positionOS, IN.positionHCS.xy, _DissolveAmount);

                    half brightnessFade = pow(saturate(1.0 - _DissolveAmount), max(_DissolveBrightnessPower, 0.01));
                    color *= brightnessFade;

                    color += _DissolveEdgeColor.rgb * dissolve.y * _DissolveEdgeIntensity;
                    alpha *= dissolve.x;
                }

                return half4(color, alpha);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------
        // Pass 3 - ShadowCaster
        // ---------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull     Off
            ZWrite   On
            ZTest    LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   vert_shadow
            #pragma fragment frag_shadow

            #pragma shader_feature_local _CAST_SHADOWS_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;
            float4 _ShadowBias;

            TEXTURE2D(_DissolveNoiseTex); SAMPLER(sampler_DissolveNoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                half4  _FresnelColor;
                half   _FresnelPower;
                half   _FresnelScale;
                half   _Alpha;
                float4 _NormalMap_ST;
                half   _NormalScale;
                half   _ShadowStrength;
                float4 _AlphaMap_ST;
                half   _AlphaCutoff;

                // ===== Dissolve =====
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

             TEXTURE2D(_AlphaMap);
            SAMPLER(sampler_AlphaMap);

            #include "DissolveCore.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 positionOS  : TEXCOORD2;
            };

            float3 ApplyShadowBiasManual(float3 positionWS, float3 normalWS, float3 lightDir)
            {
                float invNdotL = 1.0 - saturate(dot(lightDir, normalWS));
                float normalBias = invNdotL * _ShadowBias.y;
                positionWS += lightDir  * _ShadowBias.x;
                positionWS += normalWS  * normalBias;
                return positionWS;
            }

            float4 GetShadowCasterPositionCS(float3 positionOS, float3 normalOS, out float3 positionWSOut)
            {
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS   = TransformObjectToWorldNormal(normalOS);
                positionWSOut = positionWS;

            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif

                float4 posCS = TransformWorldToHClip(
                    ApplyShadowBiasManual(positionWS, normalWS, lightDir));

            #if UNITY_REVERSED_Z
                posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return posCS;
            }

            Varyings vert_shadow(Attributes IN)
            {
                Varyings OUT;
                float3 wsOut;
                OUT.positionHCS = GetShadowCasterPositionCS(IN.positionOS.xyz, IN.normalOS, wsOut);
                OUT.positionWS  = wsOut;
                OUT.positionOS  = IN.positionOS.xyz;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _NormalMap);  // ← 修复 GLSL link error
                return OUT;
            }

            half4 frag_shadow(Varyings IN) : SV_Target
            {
            #if !defined(_CAST_SHADOWS_ON)
                clip(-1);
            #endif

             half a = SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, IN.uv).r;
            clip(a - _AlphaCutoff);

                // v3.2: shadow map 不需 dither, 走极简 field clip
                UNITY_BRANCH
                if (_DissolveAmount > 0.0001)
                {
                    float field = ComputeDissolveFieldClip(IN.positionWS, IN.positionOS);
                    clip(field - _DissolveAmount);
                }
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
