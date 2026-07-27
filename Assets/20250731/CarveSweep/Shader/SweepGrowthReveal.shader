Shader "Custom/LiangZhu/SweepGrowthReveal"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        [HDR]_EmissionColor("Emission", Color) = (0,0,0,0)

        _Fade("Fade", Range(0,1)) = 1

        [Header(Growth)]
        _GrowT("Grow T (driven by MPB / Timeline)", Range(0,1)) = 0
        _GrowFeather("Body Fade Width", Float) = 0.05
        [HDR]_GlowColor("Edge Glow Color", Color) = (1, 0.8, 0.3, 1)
        _GlowIntensity("Edge Glow Intensity", Float) = 3
        _GrowGlowWidth("Edge Glow Width", Float) = 0.08
        [Toggle(_GROW_DISTANCE)] _GrowDistance("Grow By World Distance (uv2.y)", Float) = 0

        [Header(Per Path Stagger)]
        _PathCount("Path Count (auto via VineGrowthController)", Float) = 1
        _GrowSpan("Grow Span (1 = all together)", Range(0.01,1)) = 1
        _GrowMode("Stagger (0 sequential, 1 scatter)", Float) = 0

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull (Tube=Back, Ribbon=Off)", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GrowthForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _GROW_DISTANCE
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // SRP Batcher：静态属性全部进 UnityPerMaterial，逐字节一致
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _EmissionColor;
                float  _GrowFeather;
                half4  _GlowColor;
                float  _GlowIntensity;
                float  _GrowGlowWidth;
                float  _PathCount;   // 默认 1；VineGrowthController 会按实际路径数 MPB 覆盖
                float  _GrowSpan;    // 默认 1 → 与原逐条齐长行为完全一致
                float  _GrowMode;    // 0 sequential / 1 scatter
                float  _Fade;
            CBUFFER_END

            // 每-renderer 由 MPB 驱动，置于 CBUFFER 外（避免污染 UnityPerMaterial）
            float _GrowT;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float2 growUV     : TEXCOORD2;   // UV2 = (u, distance)
                float2 growMaskUV : TEXCOORD3;   // UV3 = (pathId, pathLength)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float2 growUV      : TEXCOORD1;
                float2 growMask    : TEXCOORD2;  // (pathId, pathLength)
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // 把全局进度 _GrowT 按 path id 拆成每条自己的 [0,1] 进度
            float PathStart(float pathId)
            {
                float maxStart = max(1.0 - _GrowSpan, 0.0);
                if (_GrowMode < 0.5)                       // Sequential：按 id 均匀铺开
                    return (pathId / max(_PathCount - 1.0, 1.0)) * maxStart;
                float h = frac(sin(pathId * 12.9898) * 43758.5453);
                return h * maxStart;                        // Scatter：hash 随机相位
            }

            float PathGrowT(float pathId)
            {
                if (_PathCount <= 1.5) return saturate(_GrowT); // 单条：忽略错峰，等于全局
                float start = PathStart(pathId);
                return saturate((_GrowT - start) / max(_GrowSpan, 1e-4));
            }

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionHCS = p.positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.growUV = v.growUV;
                o.growMask = v.growMaskUV;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float localT = PathGrowT(i.growMask.x);     // 本条路径的进度

            #if defined(_GROW_DISTANCE)
                float gc = i.growUV.y;                       // 世界距离(米)
                float threshold = localT * i.growMask.y;     // 把 [0,1] 进度换算回本条弧长
            #else
                float gc = i.growUV.x;                       // 归一化 u
                float threshold = localT;
            #endif

                float behind = threshold - gc;                       // >=0 已生长, <0 尚未到达
                float core = saturate(behind / max(_GrowFeather, 1e-4));
                float glow = saturate(1.0 - behind / max(_GrowGlowWidth, 1e-4)) * step(0.0, behind);

                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;

                // 前沿处 core≈0，由 glow 顶起 alpha → 最亮发光软边；后方 core=1 → 实体本体
                half a = baseTex.a * saturate(core + glow * _GlowColor.a);
                clip(a - 0.003);                                     // 未生长段直接丢弃，省 blend 带宽

                half3 col = baseTex.rgb + _EmissionColor.rgb;
                col += _GlowColor.rgb * (_GlowIntensity * glow);     // HDR 自发光喂 Bloom

                return half4(col, a*_Fade);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
