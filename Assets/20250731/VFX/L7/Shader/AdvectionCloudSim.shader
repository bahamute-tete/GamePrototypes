Shader "URP/AdvectionCloudSim"
{
    Properties
    {
        _MainTex ("Cloud Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "gray" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        _Alpha ("Alpha", Range(0,1)) = 1
        _DistortionStrength ("Distortion Strength", Range(0, 0.5)) = 0.1
        _Speed ("Animation Speed", Vector) = (0.1, 0.1, 0.05, 0.05)
        _NoiseScale ("Noise Scale", Vector) = (1, 1, 2, 2)
        _WindDirection ("Wind Direction", Vector) = (1, 0.3, 0, 0)
        _WindStrength ("Wind Strength", Range(0, 2)) = 0.5
        _TurbulenceStrength ("Turbulence Strength", Range(0, 1)) = 0.3
        _VortexStrength ("Vortex Strength", Range(0, 1)) = 0.2
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionHCS : SV_POSITION;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                float4 _Color;
                float _Alpha;
                float _DistortionStrength;
                float4 _Speed;
                float4 _NoiseScale;
                float4 _WindDirection;
                float _WindStrength;
                float _TurbulenceStrength;
                float _VortexStrength;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                float time = _Time.y;
                float2 baseUV = input.uv;
                
                // 噪声采样 - 不受风影响的基础噪声
                float2 noiseUV1 = baseUV * _NoiseScale.xy + _Speed.xy * time;
                float2 noiseUV2 = baseUV * _NoiseScale.zw + _Speed.zw * time * 1.3;
                float2 noiseUV3 = baseUV * _NoiseScale.xy * 0.5 + _Speed.xy * time * 0.7;

                half4 noise1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV1);
                half4 noise2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV2);
                half4 noise3 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV3);
                
                // 涡流效果
                float2 center = float2(0.5, 0.5);
                float2 toCenter = baseUV - center;
                float dist = length(toCenter);
                float angle = atan2(toCenter.y, toCenter.x) + time * _VortexStrength * (1.0 - dist);
                float2 vortexOffset = float2(cos(angle), sin(angle)) * dist * _VortexStrength * 0.1;
                
                // 基础湍流扰动
                float2 baseTurbulence = (noise1.rg - 0.5) * 2.0 + (noise2.rg - 0.5) * 1.5 + (noise3.rg - 0.5) * 0.8;
                
                // 风方向影响扰动 - 将扰动朝风向偏移
                float2 windInfluence = normalize(_WindDirection.xy) * _WindStrength;
                float2 turbulence = baseTurbulence + windInfluence * (noise1.r + noise2.g) * 0.3;
                turbulence *= _TurbulenceStrength;
                
                // 组合所有扰动
                float2 totalDistortion = (turbulence + vortexOffset) * _DistortionStrength;
                
                // 应用扰动到主纹理UV - 不进行整体平移
                float2 finalUV = baseUV + totalDistortion;
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, finalUV);
                
                // 添加密度变化模拟云层厚度变化
                float densityNoise = (noise1.r + noise2.g) * 0.5;
                texColor.a *= (0.7 + densityNoise * 0.6);
                
                half4 finalColor = texColor * _Color;
                finalColor.a *= _Alpha;
                return finalColor;
            }
            ENDHLSL
        }
    }
}
