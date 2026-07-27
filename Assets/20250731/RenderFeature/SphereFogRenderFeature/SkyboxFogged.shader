Shader "Custom/LiangZhu/SkyboxFogged"
{
    Properties
    {
        [Toggle(_USE_CUBEMAP)] _UseCubemap ("Use Cubemap (off = Gradient)", Float) = 1

        [Header(Cubemap)]
        [NoScaleOffset] _Tex ("Cubemap", Cube) = "grey" {}
        [HDR] _Tint    ("Tint",     Color)         = (1, 1, 1, 1)
        _Exposure      ("Exposure", Range(0, 8))   = 1.0
        _Rotation      ("Rotation Y (degrees)", Range(0, 360)) = 0

        [Header(Gradient (used when Cubemap is off))]
        [HDR] _ColorTop     ("Top Color",     Color) = (0.05, 0.07, 0.10, 1)
        [HDR] _ColorHorizon ("Horizon Color", Color) = (0.15, 0.18, 0.22, 1)
        [HDR] _ColorBottom  ("Bottom Color",  Color) = (0.02, 0.03, 0.04, 1)
        _Exponent ("Gradient Exponent", Range(0.1, 8)) = 1.5

        [Header(Sphere Fog)]
        [Toggle(_FOG_AFFECTS_SKY)] _FogEnable ("Affected By Sphere Fog", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Background"
            "RenderType"     = "Background"
            "PreviewType"    = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _USE_CUBEMAP
            #pragma shader_feature_local _FOG_AFFECTS_SKY

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #include "SphereFogInclude.hlsl"

            // Cubemap & Unity 自动写入的 HDR 解码参数
            TEXTURECUBE(_Tex);
            SAMPLER(sampler_Tex);
            half4 _Tex_HDR;

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float  _Exposure;
                float  _Rotation;
                float4 _ColorTop;
                float4 _ColorHorizon;
                float4 _ColorBottom;
                float  _Exponent;
            CBUFFER_END

            // 等价于 URP 的 DecodeHDREnvironment，inline 避免 include 链问题
            half3 DecodeHDRSky(half4 data, half4 inst)
            {
                half a = max(inst.w * (data.a - 1.0) + 1.0, 0.0);
                return (inst.x * pow(a, inst.y)) * data.rgb;
            }

            // 绕世界 Y 轴旋转视线方向
            float3 RotateAroundY(float3 v, float degrees)
            {
                float rad = radians(degrees);
                float c = cos(rad);
                float s = sin(rad);
                return float3(v.x * c - v.z * s, v.y, v.x * s + v.z * c);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirOS  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.viewDirOS  = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 dir = normalize(input.viewDirOS);
                half3 color;

                #if defined(_USE_CUBEMAP)
                    float3 rotDir   = RotateAroundY(dir, _Rotation);
                    half4 envSample = SAMPLE_TEXTURECUBE(_Tex, sampler_Tex, rotDir);
                    half3 envColor  = DecodeHDRSky(envSample, _Tex_HDR);
                    color = envColor * _Tint.rgb * _Exposure;
                #else
                    float y = dir.y;
                    if (y >= 0)
                    {
                        float t = pow(saturate(y), _Exponent);
                        color = lerp(_ColorHorizon.rgb, _ColorTop.rgb, t);
                    }
                    else
                    {
                        float t = pow(saturate(-y), _Exponent);
                        color = lerp(_ColorHorizon.rgb, _ColorBottom.rgb, t);
                    }
                #endif

                #if defined(_FOG_AFFECTS_SKY)
                    float3 farPoint = _WorldSpaceCameraPos + dir * 10000.0;
                    color = SphereFog_Apply(color, farPoint);
                #endif

                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}