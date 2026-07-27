Shader "Custom/LiangZhu/SceneGlassFogged"
{
    Properties
    {
        [MainTexture] _MainTex ("Base Texture", 2D) = "white" {}
        [MainColor]   _Color   ("Tint (alpha = base opacity)", Color) = (1, 1, 1, 0.3)

        [Header(Surface)]
        _Smoothness    ("Smoothness",         Range(0, 1)) = 0.95
        _SpecIntensity ("Specular Intensity", Range(0, 8)) = 1.0

        [Header(Fresnel)]
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
            #pragma shader_feature_local _REFRACTION_ON
            #pragma shader_feature_local _FOG_AFFECTS_GLASS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"


            #include "SphereFogInclude.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            #if defined(_REFRACTION_ON)
                TEXTURE2D_X(_CameraOpaqueTexture);
                SAMPLER(sampler_CameraOpaqueTexture);
            #endif

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _Smoothness;
                float  _SpecIntensity;
                float4 _FresnelColor;
                float  _FresnelPower;
                float  _FresnelIntensity;
                float  _ReflectIntensity;
                float  _RefractStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float4 screenPos   : TEXCOORD3;
                half3 vertexSH : TEXCOORD4;
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

                OUTPUT_SH(output.normalWS, output.vertexSH);

                output.positionCS = vpi.positionCS;
                output.positionWS = vpi.positionWS;
                output.normalWS   = vni.normalWS;
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                output.screenPos  = vpi.positionNDC;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 N = normalize(input.normalWS);
                float3 V = normalize(GetCameraPositionWS() - input.positionWS);

                half3 indirectGI = SampleSH(N) + input.vertexSH;

                // 1. 基础颜色
                half4 tex  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 base = tex * _Color ;

                // 2. Fresnel
                float NdotV   = saturate(dot(N, V));
                float fresnel = pow(1.0 - NdotV, _FresnelPower) * _FresnelIntensity;

                // 3. 主光 Blinn-Phong 高光
                Light  mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float3 H = normalize(L + V);
                float  specPow  = exp2(_Smoothness * 11.0) + 2.0;
                float  specMask = pow(saturate(dot(N, H)), specPow);
                float3 spec = mainLight.color * specMask * _SpecIntensity;

                // 4. 环境反射（Reflection Probe / Skybox）
                float3 R = reflect(-V, N);
                float3 reflection = GlossyEnvironmentReflection(R, input.positionWS,
                                        1.0 - _Smoothness, 1.0) * _ReflectIntensity;

                // 5. 折射（可选）
                #if defined(_REFRACTION_ON)
                    float2 screenUV   = input.screenPos.xy / input.screenPos.w;
                    float2 distortion = N.xy * _RefractStrength;
                    half3 backColor = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture,
                                          sampler_CameraOpaqueTexture,
                                          screenUV + distortion).rgb;
                #endif

                // 6. 合成
                half3 rgb;
                half  a;

                

                #if defined(_REFRACTION_ON)
                    rgb = lerp(backColor, base.rgb, base.a)
                          + spec + reflection + _FresnelColor.rgb * fresnel;
                    a   = 1.0;
                #else
                    rgb = base.rgb * indirectGI * 0.3 + spec + reflection + _FresnelColor.rgb * fresnel;
                    a   = saturate(base.a + fresnel * _FresnelColor.a);
                #endif

                // 7. SphereFog：玻璃在雾里逐渐消失
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