// =============================================================================
//  VFX_Shuiwen.shader  —  常规手写版 (URP / 移动 VR / PICO 4U·Adreno 740)
//
//  由 Shader Graph 生成版重构而来。功能与原版逐像素等价:
//    · raodong 扰动图(随时间滚动) -> 扰动主图与 Alpha 图的 UV
//    · 主图 * _Float * _Color(HDR)  = 自发光颜色
//    · Alpha_tex(受扰动+滚动) * Alpha2_tex(静态) = 透明度
//    · Blend SrcAlpha One (叠加), ZWrite Off
//
//  相比 Graph 生成版的改动:
//    1. 6 个 Pass -> 1 个 Forward Pass。透明叠加特效不写深度/不投影/不进
//       延迟管线, 那几个 Pass 是 Graph 模板硬塞的, 删掉即省编译期与变体数。
//    2. GUID 命名的中间变量 -> 语义化命名, 去掉冗余 float4 运算
//       (alpha 原本算完整 float4 再取 .x, 这里直接标量 r*r)。
//    3. 颜色/Alpha 用 half (mediump), 仅 位置/UV/时间 用 float ——
//       Adreno 740 上 half ALU 吞吐翻倍。
//    4. 新增 Dissolve(溶解) 与 SphereFog(球形雾) 支持, 用 shader_feature
//       关键字门控 —— 不开启时该材质变体里零开销。
//
// =============================================================================
Shader "Custom/LiangZhu/VFX_Shuiwen"
{
    Properties
    {
        [Header(Main)]
        [NoScaleOffset] _Main      ("Main", 2D)            = "white" {}
        _main_Tiling               ("Main Tiling", Vector) = (1, 1, 0, 0)
        _main_offset               ("Main Scroll", Vector) = (0, 0, 0, 0)
        _Float                     ("Main Intensity", Float) = 1
        [HDR] _Color               ("Color (HDR)", Color)  = (1, 1, 1, 1)

        [Header(Distortion)]
        [NoScaleOffset] _raodong   ("Distortion", 2D)            = "white" {}
        _raodong_Tiling            ("Distortion Tiling", Vector) = (0, 0, 0, 0)
        _raodong_offset            ("Distortion Scroll", Vector)= (1, 1, 0, 0)
        _raodong_int               ("Distortion Strength", Float) = 1

        [Header(Alpha)]
        [NoScaleOffset] _Alpha_tex ("Alpha Tex", 2D)        = "white" {}
        _alpha_tiling              ("Alpha Tiling", Vector) = (1, 1, 0, 0)
        _alpha_offset              ("Alpha Scroll", Vector) = (0, 0, 0, 0)
        [NoScaleOffset] _Alpha2_tex("Alpha2 Tex (static)", 2D) = "white" {}

        // ---------------- Dissolve ----------------
        [Header(Dissolve)]
        [Toggle(_DISSOLVE_ON)] _DissolveEnabled ("Enable Dissolve", Float) = 0
        _DissolveAmount          ("Amount", Range(0,1))              = 0
        [Enum(Noise,0,Axis,1,Radial,2)] _DissolveMode  ("Mode", Float)  = 0
        [Enum(Local,0,World,1)]         _DissolveSpace ("Space", Float) = 0
        _DissolveEdgeWidth       ("Edge Width", Range(0.001, 0.5))   = 0.05
        _DissolveNoiseScale      ("Noise Scale", Float)              = 5
        [Toggle] _DissolveUseNoiseTex ("Use Noise Texture", Float)   = 0
        [NoScaleOffset] _DissolveNoiseTex ("Noise Texture", 2D)      = "gray" {}
        _DissolveAxis            ("Axis (xyz dir, w halfExtent)", Vector) = (0,1,0,1)
        _DissolveAxisCenter      ("Axis Center", Float)              = 0
        _DissolveRadial          ("Radial (xyz center, w maxDist)", Vector) = (0,0,0,1)
        [Toggle] _DissolveRadialReverse ("Radial Reverse", Float)    = 0
        [HDR] _DissolveEdgeColor ("Edge Color (HDR)", Color)         = (1, 0.5, 0.1, 1)
        _DissolveEdgeIntensity   ("Edge Intensity", Float)           = 2
        _DissolveBrightnessPower ("Brightness Power (reserved)", Float) = 1

        // ---------------- Sphere Fog ----------------
        [Header(Sphere Fog)]
        // 雾的形状/中心/密度等是全局参数(在 ShapeFogInclude 的 SphereFogGlobals
        // 里, 由脚本 Shader.SetGlobalXXX 设置), 这里只给一个材质级开关。
        [Toggle(_SPHEREFOG_ON)] _SphereFogEnabled ("Enable Sphere Fog", Float) = 0

        [HideInInspector] _QueueOffset  ("_QueueOffset", Float)  = 0
        [HideInInspector] _QueueControl ("_QueueControl", Float) = -1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"        = "UniversalPipeline"
            "RenderType"            = "Transparent"
            "UniversalMaterialType" = "Unlit"
            "Queue"                 = "Transparent"
        }

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode" = "UniversalForward" }

            // 透明叠加特效的标准渲染状态
            Cull   Off
            Blend  SrcAlpha One
            ZTest  LEqual
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            // VR / 实例化 (PICO 单 Pass 立体渲染必需)
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            // 效果开关 —— 关闭时对应代码完全不编进该变体, 零开销
            #pragma shader_feature_local_fragment _DISSOLVE_ON
            #pragma shader_feature_local_fragment _SPHEREFOG_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // -----------------------------------------------------------------
            //  材质常量 (放在 UnityPerMaterial 内, 保证 SRP Batcher 兼容)
            // -----------------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4 _Main_TexelSize;
                float4 _raodong_TexelSize;
                float4 _Alpha_tex_TexelSize;
                float4 _Alpha2_tex_TexelSize;

                float2 _main_Tiling;
                float2 _main_offset;
                float  _Float;
                half4  _Color;

                float2 _raodong_Tiling;
                float2 _raodong_offset;
                float  _raodong_int;

                float2 _alpha_tiling;
                float2 _alpha_offset;

                // Dissolve (DissolveCore.hlsl 要求这些都声明在 UnityPerMaterial 内)
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

            // 纹理 (CBUFFER 外)
            TEXTURE2D(_Main);        SAMPLER(sampler_Main);
            TEXTURE2D(_raodong);     SAMPLER(sampler_raodong);
            TEXTURE2D(_Alpha_tex);   SAMPLER(sampler_Alpha_tex);
            TEXTURE2D(_Alpha2_tex);  SAMPLER(sampler_Alpha2_tex);
            // DissolveCore.hlsl 要求的噪声图 (在 include 之前声明)
            TEXTURE2D(_DissolveNoiseTex); SAMPLER(sampler_DissolveNoiseTex);

            // 效果模块 (必须在上面的属性/纹理声明之后)
            #if defined(_DISSOLVE_ON)
                #include "DissolveCore.hlsl"
            #endif
            #if defined(_SPHEREFOG_ON)
                #include "ShapeFogInclude.hlsl"
            #endif

            // -----------------------------------------------------------------
            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1; // SphereFog / 世界空间 Dissolve 用
                float3 positionOS : TEXCOORD2; // 局部空间 Dissolve 用
                half   fogFactor  : TEXCOORD3; // Unity 内置距离雾
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.positionOS = IN.positionOS;
                OUT.uv         = IN.uv;
                OUT.fogFactor  = half(ComputeFogFactor(pos.positionCS.z));
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float t = _TimeParameters.x; // = Shader Graph 的 Time 节点

                // 1) 扰动向量: 采样滚动的 raodong 图, rg 通道 * 强度
                float2 raodongUV = IN.uv * _raodong_Tiling + _raodong_offset * t;
                float2 distortion = SAMPLE_TEXTURE2D(_raodong, sampler_raodong, raodongUV).rg
                                    * _raodong_int;

                // 2) 主图: UV 受扰动后再滚动, 采样 * 强度 * HDR 颜色
                float2 mainUV = (distortion + IN.uv) * _main_Tiling + _main_offset * t;
                half3 mainCol = SAMPLE_TEXTURE2D(_Main, sampler_Main, mainUV).rgb;
                half3 emissive = mainCol * _Float * _Color.rgb;

                // 3) Alpha: (扰动+滚动的 Alpha_tex).r * (静态 Alpha2_tex).r
                float2 alphaUV  = IN.uv * _alpha_tiling + _alpha_offset * t;
                float2 alpha1UV = distortion + alphaUV;
                half a1 = SAMPLE_TEXTURE2D(_Alpha_tex,  sampler_Alpha_tex,  alpha1UV).r;
                half a2 = SAMPLE_TEXTURE2D(_Alpha2_tex, sampler_Alpha2_tex, IN.uv).r;
                half alpha = a1 * a2;

                // 4) Dissolve(溶解): 裁掉 alpha + 边缘辉光
                #if defined(_DISSOLVE_ON)
                    // screenPos 用片元的像素坐标 (positionCS.xy) 做 IGN 抖动
                    float2 de = ComputeDissolveAlphaAndEdge(
                        IN.positionWS, IN.positionOS, IN.positionCS.xy, _DissolveAmount);
                    alpha   *= de.x;                                  // 溶解掩码
                    emissive += _DissolveEdgeColor.rgb * _DissolveEdgeIntensity * de.y;
                    alpha     = saturate(alpha + de.y);               // 边缘处提亮 alpha 让辉光在叠加下可见
                #endif

                // 5) SphereFog(球形雾): 叠加特效里以"衰减"方式融入雾
                //    (叠加混合下加雾色会让背景变亮, 不合理; 故按 (1-fog) 削弱辉光)
                #if defined(_SPHEREFOG_ON)
                    half fog = half(SphereFog_GetFactor(IN.positionWS));
                    alpha *= (1.0h - fog);
                    // 若想让特效在雾中"染上雾色"而非淡出, 改用下面这行:
                    // emissive = SphereFog_Apply(emissive, IN.positionWS);
                #endif

                // 6) Unity 内置距离雾: 叠加特效按强度衰减(而非混向雾色)
                alpha *= half(ComputeFogIntensity(IN.fogFactor));

                return half4(emissive, alpha);
            }
            ENDHLSL
        }
    }

    // 用默认材质面板(属性上的 [Toggle]/[Enum]/[HDR] 已能正确显示)
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
