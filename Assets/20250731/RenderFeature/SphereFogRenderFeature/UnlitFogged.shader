Shader "Custom/LiangZhu/UnlitFogged"
{
    Properties
    {
        [MainTexture] _MainTex ("Texture", 2D) = "white" {}
        [HDR][MainColor] _Color ("Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 1

        [Header(Blend Mode)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1  // One
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1  // One  → Additive

        [Header(Soft Particles)]
        [Toggle(_SOFT_PARTICLES_ON)] _SoftEnable ("Enable Soft Particles", Float) = 0
        _SoftFadeNear ("Near Fade Distance", Float) = 0
        _SoftFadeFar  ("Far Fade Distance",  Float) = 1

        [Header(Sphere Fog)]
        [Toggle(_FOG_AFFECTS_PARTICLE)] _FogEnable ("Affected By Sphere Fog", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType"     = "Plane"
        }

        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _SOFT_PARTICLES_ON
            #pragma shader_feature_local _FOG_AFFECTS_PARTICLE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

           
            #include "SphereFogInclude.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _Intensity;
                float  _SoftFadeNear;
                float  _SoftFadeFar;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
                float3 positionWS  : TEXCOORD1;
                float4 screenPos   : TEXCOORD2;
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
                output.positionCS = vpi.positionCS;
                output.positionWS = vpi.positionWS;
                output.screenPos  = vpi.positionNDC;
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                output.color      = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 基础颜色
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 col = tex * input.color * _Color;
                col.rgb *= _Intensity;

                // Soft Particles：避开和地面/墙的硬边
                #if defined(_SOFT_PARTICLES_ON)
                    float2 screenUV = input.screenPos.xy / input.screenPos.w;
                    float  sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    float  fragEye  = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                    float  range    = max(_SoftFadeFar - _SoftFadeNear, 1e-4);
                    float  softFade = saturate((sceneEye - fragEye - _SoftFadeNear) / range);
                    col.a *= softFade;
                #endif

                // 球形雾衰减：visibility = 1 在体积内 (清晰), = 0 在雾外 (不可见)
                #if defined(_FOG_AFFECTS_PARTICLE)
                    float fogFactor  = SphereFog_GetFactor(input.positionWS);
                    float visibility = 1.0 - fogFactor;
                    // rgb 兼顾 Additive 模式，alpha 兼顾 AlphaBlend 模式
                    col.rgb *= visibility;
                    col.a   *= visibility;
                #endif

                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}