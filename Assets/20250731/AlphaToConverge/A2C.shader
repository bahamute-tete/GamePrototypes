Shader "Learning/A2C"
{
    Properties
    {
        //_MainTex("Main Texture", 2D) = "white" {}
        _Alpha("Alpha", Range(0, 1)) = 1

        [Enum(Alpha Clip, 0, A2C, 1)]
        _Mode("Mode", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }


            AlphaToMask [_Mode]
            Blend Off
            ZWrite On

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Alpha;
                float _Mode;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;

                // 模型空间 → 裁剪空间。
                output.positionCS =
                    TransformObjectToHClip(input.positionOS.xyz);

                output.uv = input.uv;

                return output;
            }

             half4 Frag(Varyings input) : SV_Target
            {
                 float distanceToCenter = length(input.uv - float2(0.5, 0.5));

                // 圆的半径，稍小于 Quad 的一半。
                float radius = 0.35;

                // 估计这个距离值跨越一个屏幕像素时的变化量。
                float edgeWidth = max(fwidth(distanceToCenter), 0.0001);

                // 圆内为 1，圆外为 0，边缘约一个像素宽的渐变。
                float alpha = saturate(
                    (radius - distanceToCenter) / edgeWidth + 0.5
                );

                alpha *= _Alpha;

                 if (_Mode < 0.5)
                {
                    // Alpha Clip：小于 0.5 就丢弃。
                    clip(alpha - 0.5);
                }

                return half4(0.1, 0.8, 0.2, alpha);
            }

            ENDHLSL
        }
    }
}