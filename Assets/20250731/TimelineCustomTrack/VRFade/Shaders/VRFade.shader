Shader "Hidden/VRFade/SolidColor"
{
    // 4 种过渡效果合一的 shader：
    //   默认（无 keyword） : SolidColor / Flash —— 整屏 lerp 到 _FadeColor
    //   _FADE_IRIS         : 圆形虹膜遮罩（head-locked，VR 最舒适）
    //   _FADE_DESAT        : 色彩降饱和 + 压暗（最温和）
    //   _FADE_DEPTH        : 深度感应淡入（远先黑 / 近先黑）
    //
    // 后续做径向遮罩 / 十字溶解等只需新建 shader 并保留以下属性接口：
    //   _FadeColor / _FadeAlpha 是必备
    //   各类型的专属 uniform 见 Properties 块

    Properties
    {
        [Header(Common)]
        _FadeColor              ("Fade Color",            Color) = (0, 0, 0, 1)
        _FadeAlpha              ("Fade Alpha",            Range(0, 1)) = 0

        [Header(Iris)]
        _IrisCenter             ("Iris Center (UV)",      Vector) = (0.5, 0.5, 0, 0)
        _IrisSoftness           ("Iris Softness",         Range(0.001, 0.5)) = 0.05
        _IrisAspectCorrect      ("Iris Aspect Correct",   Float) = 1

        [Header(Desaturate)]
        _DesaturationAmount     ("Desaturation Amount",   Range(0, 1)) = 1
        _BrightnessMultiplier   ("Brightness Multiplier", Range(0, 1)) = 0.4

        [Header(DepthFade)]
        _DepthNear              ("Depth Near",            Float) = 5
        _DepthFar               ("Depth Far",             Float) = 50
        _DepthInvert            ("Depth Invert",          Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        ZTest Always
        ZWrite Off
        Cull   Off

        Pass
        {
            Name "VRFade Combined"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            // 默认（无 keyword）= SolidColor 路径
            #pragma multi_compile_local_fragment _ _FADE_IRIS _FADE_DESAT _FADE_DEPTH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // DepthFade 路径才会真正用到深度纹理；其他路径里这个 include 不会引入开销
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // ================== Uniforms ==================
            float4 _FadeColor;
            float  _FadeAlpha;

            float4 _IrisCenter;
            float  _IrisSoftness;
            float  _IrisAspectCorrect;

            float  _DesaturationAmount;
            float  _BrightnessMultiplier;

            float  _DepthNear;
            float  _DepthFar;
            float  _DepthInvert;

            // ================== Helpers ==================
            half3 ApplySolid(half3 src)
            {
                return lerp(src, _FadeColor.rgb, _FadeAlpha);
            }

            half3 ApplyIris(half3 src, float2 uv)
            {
                float2 d = uv - _IrisCenter.xy;
                // 宽高比校正：让虹膜在物理屏幕上是正圆而不是椭圆
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                d.x *= lerp(1.0, aspect, _IrisAspectCorrect);

                float dist = length(d);

                // _FadeAlpha 0 -> 1 控制虹膜从全开 (radius=0.9, 看不见遮罩) 收缩到全闭 (radius=0)
                float radius = lerp(0.9, 0.0, _FadeAlpha);

                // mask: 0 = 在虹膜内部（透明，看到原画），1 = 虹膜外（被 _FadeColor 覆盖）
                float mask = smoothstep(radius - _IrisSoftness, radius + _IrisSoftness, dist);

                return lerp(src, _FadeColor.rgb, mask);
            }

            half3 ApplyDesaturate(half3 src)
            {
                // Rec.709 luminance
                float lum = dot(src, half3(0.2126, 0.7152, 0.0722));
                half3 grey = lum.xxx;

                // 第一步：往灰阶混
                half3 desat = lerp(src, grey, _DesaturationAmount * _FadeAlpha);

                // 第二步：随 alpha 压暗（_FadeAlpha=1 时亮度 = _BrightnessMultiplier）
                float brightness = lerp(1.0, _BrightnessMultiplier, _FadeAlpha);
                return desat * brightness;
            }

            half3 ApplyDepth(half3 src, float2 uv)
            {
                float rawDepth = SampleSceneDepth(uv);
                float linearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);

                float depthMask = smoothstep(_DepthNear, _DepthFar, linearDepth);
                depthMask = lerp(depthMask, 1.0 - depthMask, _DepthInvert);

                float a = depthMask * _FadeAlpha;
                return lerp(src, _FadeColor.rgb, a);
            }

            // ================== Fragment ==================
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);

                half3 result;

                #if defined(_FADE_IRIS)
                    result = ApplyIris(src.rgb, input.texcoord);
                #elif defined(_FADE_DESAT)
                    result = ApplyDesaturate(src.rgb);
                #elif defined(_FADE_DEPTH)
                    result = ApplyDepth(src.rgb, input.texcoord);
                #else
                    // SolidColor / Flash
                    result = ApplySolid(src.rgb);
                #endif

                return half4(result, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
