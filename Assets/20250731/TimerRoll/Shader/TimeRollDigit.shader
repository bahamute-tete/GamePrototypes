// LiangZhu - 时间回溯日历 / 数字轮 shader (URP 14, Single Pass Instanced)
// 单张竖排 0-9 长条(Wrap=Repeat,关 Mipmap)。每个轮 quad 用 0..1 的 UV。
//   显示窗口 = 一格(1/10)高度,V = (s + uv.y)/10,Repeat 自动给出 9<->0 回卷与跨格滚动。
//   运动模糊 = 沿 V 轴做 TAP_COUNT 次定长(展开)采样取平均,铺开距离 = |速度|*快门,限幅。
//   透明度   = 覆盖度 * 该轮上限 * 速度淡出。
//   溶解     = 复用 DissolveCore.hlsl(完整版),字形覆盖 × 溶解 mask,边缘自发光。
// 不做 PostEffect:模糊只发生在这几个小 quad 的像素上。

Shader "Custom/LiangZhu/TimeRollDigit"
{
    Properties
    {
        _MainTex ("数字长条 0-9 (竖排, Wrap=Repeat, 关Mip)", 2D) = "white" {}
        _Color   ("颜色", Color) = (1,1,1,1)

        [Header(Per Wheel (driven by MPB))]
        _Scroll    ("滚动位置 s (格)", Float) = 0
        _Speed     ("速度 v (格每秒)", Float) = 0
        _AlphaCeil ("透明度上限", Range(0,1)) = 1

        [Header(Global)]
        _FlipStrip  ("长条0在顶端=1, 0在底端=0", Float) = 1
        _Shutter    ("快门(秒) 模糊长度系数", Float) = 0.01
        _MaxBlur    ("最大模糊(格)", Float) = 2.5
        _SpeedLo    ("开始变透的速度", Float) = 5
        _SpeedHi    ("最透的速度", Float) = 60
        _FloorAlpha ("最透到", Range(0,1)) = 0
        _EdgeFeather("边缘羽化(V, 仅模糊时生效)", Range(0,0.5)) = 0.15

        [Header(Dissolve (driven by MPB, DissolveController))]
        _DissolveAmount    ("溶解量 0=完整 1=消失", Range(0,1)) = 0
        [Enum(Noise,0,Axis,1,Radial,2)] _DissolveMode  ("溶解模式", Float) = 0
        [Enum(Local,0,World,1)]         _DissolveSpace ("溶解空间", Float) = 0
        _DissolveEdgeWidth ("边缘宽度", Range(0.001,0.5)) = 0.05
        _DissolveNoiseScale("噪声缩放", Float) = 5
        _DissolveAxis      ("轴 xyz=方向 w=半长", Vector) = (0,1,0,0.5)
        _DissolveAxisCenter("轴中心投影", Float) = 0
        _DissolveRadial    ("径向 xyz=中心 w=最大距离", Vector) = (0,0,0,1)
        [Enum(Normal,0,Reverse,1)] _DissolveRadialReverse ("径向反向", Float) = 0
        [HDR] _DissolveEdgeColor ("边缘颜色", Color) = (1,0.5,0.1,1)
        _DissolveEdgeIntensity   ("边缘强度", Float) = 2
        _DissolveBrightnessPower ("边缘锐度幂", Float) = 1
        [Enum(Off,0,On,1)] _DissolveUseNoiseTex ("用噪声贴图", Float) = 0
        _DissolveNoiseTex  ("溶解噪声(可选)", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"   = "UniversalPipeline"
            "Queue"            = "Transparent"
            "RenderType"       = "Transparent"
            "IgnoreProjector"  = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define TAP_COUNT 8   // Adreno 上用固定展开次数,避免动态循环落 scratch

            // CBUFFER 外:贴图 + 采样器(Unity 约定)
            TEXTURE2D(_MainTex);          SAMPLER(sampler_MainTex);
            TEXTURE2D(_DissolveNoiseTex); SAMPLER(sampler_DissolveNoiseTex);

            // 所有标量/向量属性进同一个 UnityPerMaterial(SRP Batcher 友好);MPB 覆盖时各 Renderer 取各自值
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _Scroll;
                float  _Speed;
                float  _AlphaCeil;
                float  _FlipStrip;
                float  _Shutter;
                float  _MaxBlur;
                float  _SpeedLo;
                float  _SpeedHi;
                float  _FloorAlpha;
                float  _EdgeFeather;

                // ---- 溶解 uniform(DissolveCore.hlsl 要求的全套)----
                float  _DissolveAmount;
                float  _DissolveMode;
                float  _DissolveSpace;
                float  _DissolveEdgeWidth;
                float  _DissolveNoiseScale;
                float4 _DissolveAxis;
                float  _DissolveAxisCenter;
                float4 _DissolveRadial;
                float  _DissolveRadialReverse;
                float4 _DissolveEdgeColor;
                float  _DissolveEdgeIntensity;
                float  _DissolveBrightnessPower;
                float  _DissolveUseNoiseTex;
            CBUFFER_END

            // 必须在 uniform/贴图声明之后再 include:DissolveCore 直接引用这些全局量
            // 路径按你工程实际位置调整(与其它已接入溶解的 shader 保持一致)
            #include "DissolveCore.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1; // 溶解 field(世界空间)
                float3 positionOS  : TEXCOORD2; // 溶解 field(物体空间)
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS  = IN.positionOS.xyz;
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // 长条排布修正:你的图是“0 在顶端、自上而下 0..9”,V=0 在底部,
                // 故格子方向相反,需把位置取反 pos = 9 - s。_FlipStrip=0 则为“0 在底”。
                float pos   = (_FlipStrip > 0.5) ? (9.0 - _Scroll) : _Scroll;
                float baseV = (pos + IN.uv.y) * 0.1;

                // 模糊铺开距离(V 单位):|速度|*快门,限幅到 _MaxBlur 格
                float L = clamp(abs(_Speed) * _Shutter, 0.0, _MaxBlur) * 0.1;

                float cov = 0.0;
                [unroll]
                for (int k = 0; k < TAP_COUNT; k++)
                {
                    float t  = (float)k / (float)(TAP_COUNT - 1) - 0.5; // -0.5 .. 0.5
                    float vv = baseV + t * L;
                    // 用 LOD0 采样:无需屏幕导数,且贴图本就关 Mip;Wrap=Repeat 负责 9<->0 回卷
                    cov += SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, float2(IN.uv.x, vv), 0).a;
                }
                cov *= (1.0 / TAP_COUNT);

                // ===== 溶解(完整版:alpha + edge)=====
                // screenPos 用片元的屏幕像素坐标(SV_POSITION.xy),供 IGN dither
                float2 de = ComputeDissolveAlphaAndEdge(IN.positionWS, IN.positionOS,
                                                        IN.positionHCS.xy, _DissolveAmount);
                float dissolveAlpha = de.x; // dither 二值 mask:0=已溶解 1=保留
                float edge          = de.y; // 溶解边缘带 [0,1]

                // 速度淡出:越快越透
                float fade  = lerp(1.0, _FloorAlpha, smoothstep(_SpeedLo, _SpeedHi, abs(_Speed)));

                // 边缘羽化:只在高速模糊时,把 quad 上下硬边渐隐,
                // 避免糊成均匀带后露出矩形边框。静止/清晰数字 blurNorm≈0 → 不羽化。
                float blurNorm = saturate(abs(_Speed) * _Shutter / max(_MaxBlur, 1e-4)); // 0..1
                float fw       = _EdgeFeather * blurNorm;
                float feather  = (fw > 1e-4)
                    ? smoothstep(0.0, fw, IN.uv.y) * smoothstep(0.0, fw, 1.0 - IN.uv.y)
                    : 1.0;

                // 字形覆盖 × 溶解 mask × 各级透明 × 边缘羽化
                float alpha = cov * dissolveAlpha * _AlphaCeil * fade * feather * _Color.a;

                // 边缘自发光(加性);_DissolveBrightnessPower 控制锐度
                float3 rgb = _Color.rgb;
                float  e   = pow(saturate(edge), max(_DissolveBrightnessPower, 1e-3));
                rgb += _DissolveEdgeColor.rgb * _DissolveEdgeIntensity * e;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
