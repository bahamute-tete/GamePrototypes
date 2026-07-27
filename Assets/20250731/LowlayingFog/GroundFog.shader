Shader "Custom/GroundFog"
{
    Properties
    {
        [Header(Color and Density)]
        _FogColor       ("Fog Color",          Color)        = (0.85, 0.92, 1.0, 1.0)
        _FogDensity     ("Fog Density",        Range(0,1))   = 0.75

        [Header(Noise)]
        _NoiseTex       ("Noise Texture",      2D)           = "white" {}
        _NoiseScale     ("Noise Scale",        Float)        = 0.08
        _NoisePower     ("Noise Contrast",     Float)        = 1.4
        _Speed          ("Flow Speed",         Float)        = 0.04

        [Header(Depth Blending)]
        _SoftEdgeRange  ("Soft Edge Range",    Float)        = 1.2

        [Header(Shape)]
        _EdgeFadePower  ("Edge Fade Power",    Float)        = 1.2

        [Header(Footprint Mask)]
        _FogMaskTex     ("Fog Mask RT",        2D)           = "white" {}
        _FogMaskCenter  ("Mask World Center",  Vector)       = (0,0,0,0)
        _FogMaskSize    ("Mask World Size",    Float)        = 40.0
        _MaskInfluence  ("Mask Influence",     Range(0,1))   = 1.0
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

        Pass
        {
            Name "GroundFog"

            ZWrite Off
            ZTest  LEqual
            Cull   Off
            Blend  SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _FogColor;
                half   _FogDensity;
                float  _NoiseScale;
                half   _NoisePower;
                float  _Speed;
                float  _SoftEdgeRange;
                half   _EdgeFadePower;
                float4 _NoiseTex_ST;

                float4 _FogMaskCenter;
                float  _FogMaskSize;
                half   _MaskInfluence;
            CBUFFER_END

            TEXTURE2D(_NoiseTex);    SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_FogMaskTex);  SAMPLER(sampler_FogMaskTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 worldXZ     : TEXCOORD0;
                float2 objectXZ    : TEXCOORD1;
                float4 screenPos   : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = posInputs.positionCS;
                output.worldXZ     = posInputs.positionWS.xz;
                output.objectXZ    = input.positionOS.xz;
                output.screenPos   = ComputeScreenPos(posInputs.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // ── 1. 噪声云雾形状 ─────────────────────────────────────
                float2 baseUV = input.worldXZ * _NoiseScale;
                float2 uv1 = baseUV        + _Time.y * _Speed * float2( 1.00,  0.50);
                float2 uv2 = baseUV * 1.73 + _Time.y * _Speed * float2(-0.35,  0.90);
                float n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv1).r;
                float n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv2).r;
                float fogShape = pow(saturate(n1 * 0.55 + n2 * 0.45), _NoisePower);

                // ── 2. 软粒子深度混合 ───────────────────────────────────
                float2 screenUV      = input.screenPos.xy / input.screenPos.w;
                float  sceneRawDepth = SampleSceneDepth(screenUV);
                float  sceneLinear   = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                float  fogLinear     = input.screenPos.w;
                float  softEdge      = saturate((sceneLinear - fogLinear) / _SoftEdgeRange);

                // ── 3. 边缘渐隐 ─────────────────────────────────────────
                float edgeFade = pow(1.0 - saturate(length(input.objectXZ) / 5.0), _EdgeFadePower);

                // ── 4. 脚步遮罩采样 ─────────────────────────────────────
                float halfSize = _FogMaskSize * 0.5;
                float2 maskUV  = float2(
                    (input.worldXZ.x - _FogMaskCenter.x + halfSize) / _FogMaskSize,
                    (input.worldXZ.y - _FogMaskCenter.y + halfSize) / _FogMaskSize
                );

                // 范围外默认遮罩为 1（雾气不受影响）
                float maskValue = 1.0;
                if (maskUV.x >= 0.0 && maskUV.x <= 1.0 && maskUV.y >= 0.0 && maskUV.y <= 1.0)
                {
                    float sampled = SAMPLE_TEXTURE2D(_FogMaskTex, sampler_FogMaskTex, maskUV).r;
                    maskValue = lerp(1.0, sampled, _MaskInfluence);
                }

                // ── 5. 最终合并 ─────────────────────────────────────────
                half finalAlpha = (half)(fogShape * softEdge * edgeFade * maskValue)
                                  * _FogDensity * _FogColor.a;

                return half4(_FogColor.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
