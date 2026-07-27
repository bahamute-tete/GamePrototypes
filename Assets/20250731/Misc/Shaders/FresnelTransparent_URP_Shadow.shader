Shader "Custom/LiangZhu/FresnelTransparentShadow"
{
    Properties
    {
        _BaseColor            ("Base Color",      Color)           = (0.3, 0.6, 1.0, 0.15)
        _FresnelColor         ("Fresnel Color",   Color)           = (0.5, 0.8, 1.0, 1.0)
        _FresnelPower         ("Fresnel Power",   Range(0.5, 8.0)) = 3.0
        _FresnelScale         ("Fresnel Scale",   Range(0.0, 2.0)) = 1.0
        _Alpha                ("Base Alpha",      Range(0.0, 1.0)) = 0.1

        [Space(10)]
        [Header(Shadow_Settings)]
        [Toggle(_CAST_SHADOWS_ON)]
        _CastShadowsToggle    ("Cast Shadows",    Float)           = 1
        [Toggle(_RECEIVE_SHADOWS_ON)]
        _ReceiveShadowsToggle ("Receive Shadows", Float)           = 1
        _ShadowStrength       ("Shadow Strength", Range(0.0, 1.0)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent"
        }

       
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

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionHCS : SV_POSITION; };

            Varyings vert_depth(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag_depth(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

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

            #pragma shader_feature_local _RECEIVE_SHADOWS_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _FresnelColor;
                half  _FresnelPower;
                half  _FresnelScale;
                half  _Alpha;
                half  _ShadowStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
            #if defined(_RECEIVE_SHADOWS_ON) && defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD2;
            #endif
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs  = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = normInputs.normalWS;

            #if defined(_RECEIVE_SHADOWS_ON) && defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                OUT.shadowCoord = GetShadowCoord(posInputs);
            #endif
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Fresnel
                half3 N      = normalize(IN.normalWS);
                half3 V      = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half  NdotV  = saturate(dot(N, V));
                half  fresnel = pow(1.0h - NdotV, _FresnelPower) * _FresnelScale;

                half3 color = lerp(_BaseColor.rgb, _FresnelColor.rgb, fresnel);
                half  alpha = saturate(_Alpha + fresnel * _FresnelColor.a);

                // Receive Shadows
            #if defined(_RECEIVE_SHADOWS_ON)
                float4 shadowCoord;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    shadowCoord = IN.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                    shadowCoord = float4(0, 0, 0, 0);
                #endif

                Light mainLight   = GetMainLight(shadowCoord);
                half  shadowAtten = mainLight.shadowAttenuation;
                half  shadowFactor = lerp(1.0h, shadowAtten, _ShadowStrength);

                color *= shadowFactor;
                alpha  = saturate(alpha + (1.0h - shadowFactor) * _ShadowStrength * 0.3h);
            #endif

                return half4(color, alpha);
            }
            ENDHLSL
        }

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

            // URP sets these per-light before invoking the ShadowCaster pass.
            float3 _LightDirection;
            float3 _LightPosition;

            // URP shadow bias: x = depth bias, y = normal bias.
            // Declared by the pipeline automatically; we just read it.
            float4 _ShadowBias;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            // Inline replacement for ApplyShadowBias (avoids Shadows.hlsl include chain).
            float3 ApplyShadowBiasManual(float3 positionWS, float3 normalWS, float3 lightDir)
            {
                float invNdotL = 1.0 - saturate(dot(lightDir, normalWS));
                float normalBias = invNdotL * _ShadowBias.y;
                positionWS += lightDir  * _ShadowBias.x;   // depth bias
                positionWS += normalWS  * normalBias;       // normal bias
                return positionWS;
            }

            float4 GetShadowCasterPositionCS(float3 positionOS, float3 normalOS)
            {
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS   = TransformObjectToWorldNormal(normalOS);

            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif

                float4 posCS = TransformWorldToHClip(
                    ApplyShadowBiasManual(positionWS, normalWS, lightDir));

                // Clamp to near clip plane to prevent shadow pancaking.
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
                OUT.positionHCS = GetShadowCasterPositionCS(IN.positionOS.xyz, IN.normalOS);
                return OUT;
            }

            half4 frag_shadow(Varyings IN) : SV_Target
            {
            #if !defined(_CAST_SHADOWS_ON)
                clip(-1);
            #endif
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
