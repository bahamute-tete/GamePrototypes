Shader "Custom/LiangZhu/FresnelTransparentClip"
{
    Properties
    {
        // 基础颜色（透明体颜色）
        _BaseColor      ("Base Color",         Color)  = (0.3, 0.6, 1.0, 0.15)

        // 菲涅尔颜色（边缘光颜色）
        _FresnelColor   ("Fresnel Color",       Color)  = (0.5, 0.8, 1.0, 1.0)

        // 菲涅尔强度（越大边缘越亮）
        _FresnelPower   ("Fresnel Power",       Range(0.5, 8.0)) = 3.0

        // 菲涅尔整体亮度倍数
        _FresnelScale   ("Fresnel Scale",       Range(0.0, 2.0)) = 1.0

        // 整体透明度
        _Alpha          ("Base Alpha",          Range(0.0, 1.0)) = 0.1

        // 裁切平面法线（世界空间，正方向保留，反方向裁切）
        _ClipPlaneNormal ("Clip Plane Normal (WS)", Vector) = (0, 1, 0, 0)

        // 裁切平面位置（世界空间，平面经过该点）
        _ClipPlanePoint  ("Clip Plane Point (WS)",  Vector) = (0, 0, 0, 0)

        // 反方向透明衰减范围（0=硬裁切）
        _ClipFadeRange   ("Clip Fade Range",       Range(0.0, 100.0)) = 0.25

        // 裁切边缘噪波图与参数
        _ClipNoiseTex    ("Clip Noise",            2D) = "gray" {}
        _ClipNoiseScale  ("Clip Noise Scale",      Float) = 1.0
        _ClipNoiseStrength("Clip Noise Strength",   Range(0.0, 1.0)) = 0.08
    }

    SubShader
    {
        // URP 渲染队列必须设在 Transparent 范围内
        Tags
        {
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "Queue"           = "Transparent"
        }

        // =========================================================
        // Pass 1 — 深度遮挡 Pass
        // 目的：把背面写入深度缓冲，使后续正面渲染时
        //       被背面深度遮挡，从而看不见内部结构。
        // 关键点：
        //   Cull Front    → 只渲染背面
        //   ZWrite On     → 向深度缓冲写深度
        //   ColorMask 0   → 不向颜色缓冲写任何内容
        // =========================================================
        Pass
        {
            Name "DepthOcclusion"

            Cull      Front
            ZWrite    On
            ZTest     LEqual
            ColorMask 0       // 不输出颜色，只占深度

            HLSLPROGRAM
            #pragma vertex   vert_depth
            #pragma fragment frag_depth

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _ClipPlaneNormal;
                half4  _ClipPlanePoint;
                half   _ClipFadeRange;
                half   _ClipNoiseScale;
                half   _ClipNoiseStrength;
            CBUFFER_END

            TEXTURE2D(_ClipNoiseTex);
            SAMPLER(sampler_ClipNoiseTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
            };

            Varyings vert_depth(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                return OUT;
            }

            // 什么都不需要输出，ColorMask 0 会丢弃颜色
            half4 frag_depth(Varyings IN) : SV_Target
            {
                half3 clipN = normalize(_ClipPlaneNormal.xyz);
                half signedDist = dot(IN.positionWS - _ClipPlanePoint.xyz, clipN);

                float2 noiseUV = IN.positionWS.xz * max(_ClipNoiseScale, 1e-4h);
                half noise = SAMPLE_TEXTURE2D(_ClipNoiseTex, sampler_ClipNoiseTex, noiseUV).r;
                half noiseOffset = (noise * 2.0h - 1.0h) * _ClipNoiseStrength;
                signedDist += noiseOffset;

                half fadeRange = max(_ClipFadeRange, 1e-4h);
                half clipFade = saturate((signedDist + fadeRange) / fadeRange);

                // 在完全不可见区域不写深度，避免错误遮挡。
                clip(clipFade - 0.001h);
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }

        // =========================================================
        // Pass 2 — 透明菲涅尔渲染 Pass
        // 目的：渲染正面，叠加菲涅尔边缘效果。
        // 关键点：
        //   Cull Back     → 只渲染正面
        //   ZWrite Off    → 不覆盖深度缓冲（保持透明层次）
        //   ZTest LEqual  → 正面通过深度测试才渲染（被背面遮挡的像素会被Pass1的深度剔除）
        //   Blend 标准透明混合
        // =========================================================
        Pass
        {
            Name "FresnelTransparent"
            Tags { "LightMode" = "UniversalForward" }

            Cull     Back
            ZWrite   Off
            ZTest    LEqual
            Blend    SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                half4  _FresnelColor;
                half   _FresnelPower;
                half   _FresnelScale;
                half   _Alpha;
                half4  _ClipPlaneNormal;
                half4  _ClipPlanePoint;
                half   _ClipFadeRange;
                half   _ClipNoiseScale;
                half   _ClipNoiseStrength;
            CBUFFER_END

            TEXTURE2D(_ClipNoiseTex);
            SAMPLER(sampler_ClipNoiseTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // 顶点坐标变换
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;

                // 法线变换到世界空间
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS);
                OUT.normalWS = normInputs.normalWS;

                // 视线方向（世界空间，从顶点指向摄像机）
                OUT.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // --------------------------------------------------
                // 裁切平面（法线正方向保留，反方向按 Range 平滑衰减）
                // --------------------------------------------------
                half3 clipN = normalize(_ClipPlaneNormal.xyz);
                half signedDist = dot(IN.positionWS - _ClipPlanePoint.xyz, clipN);

                float2 noiseUV = IN.positionWS.xz * max(_ClipNoiseScale, 1e-4h);
                half noise = SAMPLE_TEXTURE2D(_ClipNoiseTex, sampler_ClipNoiseTex, noiseUV).r;
                half noiseOffset = (noise * 2.0h - 1.0h) * _ClipNoiseStrength;
                signedDist += noiseOffset;

                half fadeRange = max(_ClipFadeRange, 1e-4h);
                half clipFade = saturate((signedDist + fadeRange) / fadeRange);

                // 归一化向量
                half3 N = normalize(IN.normalWS);
                half3 V = normalize(IN.viewDirWS);

                // --------------------------------------------------
                // 菲涅尔计算
                // NdotV 在边缘处趋近 0，正对时趋近 1
                // 1 - NdotV 在边缘处趋近 1（边缘更亮）
                // --------------------------------------------------
                half NdotV    = saturate(dot(N, V));
                half fresnel  = pow(1.0h - NdotV, _FresnelPower) * _FresnelScale;

                // 菲涅尔颜色叠加到基础颜色上
                half3 color = lerp(_BaseColor.rgb, _FresnelColor.rgb, fresnel);

                // 透明度：基础透明度 + 菲涅尔让边缘更不透明
                half alpha = saturate(_Alpha + fresnel * _FresnelColor.a);
                alpha *= clipFade;

                // 透明度接近 0 时丢弃，避免无意义开销。
                clip(alpha - 0.001h);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    // 在不支持 URP 的情况下回退
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
