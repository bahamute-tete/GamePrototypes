Shader "VATLearning/BoneMatrixUnlit"
{
    Properties
    {
        [NoScaleOffset] _BoneTexture("Bone matrices (3 texels per bone)", 2D) = "white" {}
        _FrameCount("Frame count", Float) = 120
        _Frame("Frame (fractional)", Float) = 0
        [Toggle] _Interpolate("Interpolate frames", Float) = 1
        [Toggle] _ApplySkinning("Apply bone matrices", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "BoneMatrixUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BoneTexture);
            CBUFFER_START(UnityPerMaterial)
                float _FrameCount;
                float _Frame;
                float _Interpolate;
                float _ApplySkinning;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 boneIndices : TEXCOORD1; // Mesh.SetUVs(1, ...)
                float2 boneWeights : TEXCOORD2; // Mesh.SetUVs(2, ...)
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float weight1 : TEXCOORD1;
            };

            // 纹理 x = boneIndex * 3 + 矩阵行，y = 帧编号。
            // Load 直接读取整数像素：无需 UV、过滤器或半像素偏移。
            float3 TransformByBone(float3 position, int bone, int frame)
            {
                int x = bone * 3;
                float4 row0 = LOAD_TEXTURE2D(_BoneTexture, int2(x + 0, frame));
                float4 row1 = LOAD_TEXTURE2D(_BoneTexture, int2(x + 1, frame));
                float4 row2 = LOAD_TEXTURE2D(_BoneTexture, int2(x + 2, frame));
                float4 p = float4(position, 1.0); // w=1，平移才会生效。
                return float3(dot(row0, p), dot(row1, p), dot(row2, p));
            }

            float3 SkinAtFrame(Attributes input, int frame)
            {
                float3 p0 = TransformByBone(input.positionOS, (int)input.boneIndices.x, frame);
                float3 p1 = TransformByBone(input.positionOS, (int)input.boneIndices.y, frame);
                return p0 * input.boneWeights.x + p1 * input.boneWeights.y;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS;
                if (_ApplySkinning > 0.5)
                {
                    float frame = frac(_Frame / max(_FrameCount, 1.0)) * max(_FrameCount, 1.0);
                    int f0 = (int)floor(frame);
                    int f1 = (f0 + 1) % max((int)_FrameCount, 1);
                    positionOS = SkinAtFrame(input, f0);
                    if (_Interpolate > 0.5)
                        positionOS = lerp(positionOS, SkinAtFrame(input, f1), frac(frame));
                }
                // 先在网格局部空间蒙皮，再由每个物体的 Transform 放到世界中。
                output.positionCS = TransformObjectToHClip(positionOS);
                output.uv = input.uv;
                output.weight1 = input.boneWeights.y;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 color = lerp(half3(0.12, 0.45, 0.95), half3(1.0, 0.36, 0.08), input.weight1);
                // 条纹随原始 UV 运动，帮助观察网格的弯曲。
                float edge = abs(frac(input.uv.y * 20.0 + 0.5) - 0.5);
                float stripeMask = 1.0 - smoothstep(0.015, 0.015 + fwidth(input.uv.y * 20.0), edge);
                return half4(lerp(color, color * 0.3, stripeMask * 0.65), 1.0);
            }
            ENDHLSL
        }
    }
}
