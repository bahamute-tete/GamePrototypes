// 能量门显示版本：复用基础 ContactMask 的 R 亮线 / G 柔光，不改变求交算法。
Shader "Custom/LiangZhu/ContactEnergyGate"
{
    Properties
    {
        _ContactMask("Contact mask", 2D) = "black" {}
        _HistoryMask("History mask", 2D) = "black" {}
        [HideInInspector] _TrailEnabled("Trail enabled", Float) = 0
        [HDR] _TrailLineColor("Trail line", Color) = (0.04,0.5,1,1)
        [HDR] _TrailGlowColor("Trail glow", Color) = (0.015,0.25,0.7,1)
        [HDR] _LineColor("Line", Color) = (0.35,1.8,2.4,1)
        [HDR] _GlowColor("Glow", Color) = (0.02,0.55,0.9,1)
        _SurfaceTint("Surface tint", Color) = (0.02,0.12,0.18,0.035)
        [HDR] _EnergyColor("Energy color", Color) = (0.05,1.2,2.5,1)
        _EnergyIntensity("Energy intensity", Range(0,5)) = 1.2
        // 读取贴图 R 通道；贴图应使用 Repeat、关闭 sRGB，并开启 Mipmap。
        [NoScaleOffset] _NoiseTex("Noise texture (R)", 2D) = "gray" {}
        // 第二张独立辉光遮罩：白色不改变，黑色挖空，只参与辉光而不改变能量图案。
        [NoScaleOffset] _GlowNoiseTex("Extra glow noise (R)", 2D) = "white" {}
        _GlowNoiseScale("Extra glow noise repeats / metre", Float) = 2
        [HideInInspector] _GlowBaseNoiseEnabled("Base glow noise enabled", Float) = 1
        _NoiseScale("Texture repeats / metre", Float) = 9
        _GlowDistortion("Glow noise strength", Range(0,1)) = 1
        _GlowDistortionScale("Glow distortion repeats / metre", Float) = 2
        _GlowEdgeBreakup("Glow edge breakup", Range(0,1)) = 0.75
        _GlowEdgeSoftness("Glow breakup softness", Range(0.001,0.2)) = 0.025
        _FlowSpeed("Flow speed / metres per second", Vector) = (0.12,0.55,0,0)
        _PulseAmount("Pulse amount", Range(0,1)) = 0.2
        _SparkIntensity("Contour fleck intensity", Range(0,5)) = 1.5
        _EffectTime("Effect time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            // 保留双面、深度遮挡与预乘底色 + 加法能量的混合方式。
            Cull Off ZWrite Off ZTest LEqual
            Blend One OneMinusSrcAlpha
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_ContactMask); SAMPLER(sampler_ContactMask);
            TEXTURE2D(_HistoryMask); SAMPLER(sampler_HistoryMask);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_GlowNoiseTex); SAMPLER(sampler_GlowNoiseTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _LineColor, _GlowColor, _SurfaceTint;
            float4 _PlaneOrigin, _PlaneRight, _PlaneUp, _PlaneSize;
            float4 _EnergyColor, _FlowSpeed;
            float4 _TrailLineColor, _TrailGlowColor;
            float _TrailEnabled;
            float _EnergyIntensity, _NoiseScale, _PulseAmount, _SparkIntensity, _EffectTime;
            float _GlowDistortion, _GlowDistortionScale;
            float _GlowNoiseScale, _GlowBaseNoiseEnabled;
            float _GlowEdgeBreakup, _GlowEdgeSoftness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 plane : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                float3 delta = world - _PlaneOrigin.xyz;
                // 米制平面坐标使纹理密度不随平面宽高变化；映射基底与基础组件完全一致。
                o.plane = float2(dot(delta, _PlaneRight.xyz), dot(delta, _PlaneUp.xyz));
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.plane / max(_PlaneSize.xy, float2(0.01,0.01)) + 0.5;
                half2 mask = SAMPLE_TEXTURE2D(_ContactMask, sampler_ContactMask, uv).rg;
                // 历史包含当前轮廓，先减去实时值，避免接触处同时叠加两套完整亮度。
                // 历史保存的是原始形状：下方仍使用当前时间的噪声，残影上的纹理会继续流动。
                half2 history = SAMPLE_TEXTURE2D(_HistoryMask,sampler_HistoryMask,uv).rg;
                half2 trail = max(history-mask,0.0)*saturate(_TrailEnabled);
                // 直接将原方向的噪声灰度乘到 G 通道，不旋转贴图、不移动辉光 UV。
                float2 glowNoiseUV = (i.plane - _FlowSpeed.xy * _EffectTime) * max(_GlowDistortionScale,0.1);
                float glowNoise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, glowNoiseUV).r;
                // 原贴图缺失时，辉光这一支使用乘法单位元 1，不受能量分支中灰兜底影响。
                glowNoise = lerp(1.0,glowNoise,_GlowBaseNoiseEnabled);
                // 新图按原方向独立缩放，继续乘在第一层结果上，不旋转或偏移接触轮廓。
                float2 extraUV = (i.plane-_FlowSpeed.xy*_EffectTime)*max(_GlowNoiseScale,0.1);
                glowNoise *= SAMPLE_TEXTURE2D(_GlowNoiseTex,sampler_GlowNoiseTex,extraUV).r;
                // 下方亮度相乘和边缘破碎共用叠加结果，使新图的黑色区域确实被挖掉。
                // 用未经噪声调制的 G 判断内外：强辉光靠近接触线，弱辉光位于外围。
                // 外围提高噪声保留门槛，形成真正为零的缺口；贴图方向和坐标不变。
                float outer = 1.0-smoothstep(0.10,0.70,mask.g);
                float threshold = saturate(_GlowEdgeBreakup)*outer;
                // fwidth 提供屏幕抗锯齿，Softness 控制缺口边缘过渡；不 clip 整个像素。
                float edgeAA = max(max(_GlowEdgeSoftness,0.001),fwidth(glowNoise)*0.5);
                float keepGlow = smoothstep(threshold-edgeAA,threshold+edgeAA,glowNoise);
                // 破碎强度为 0 时严格回到原来的灰度乘法；内圈不受新增侵蚀影响。
                keepGlow = lerp(1.0,keepGlow,saturate(_GlowEdgeBreakup*100.0)*smoothstep(0.0,0.1,outer));
                // 0 保留原辉光；1 完整乘噪声。白色保留，黑色挖空，灰色减弱。
                // 只影响辉光 G，接触亮线 R 保持原位和原强度。
                mask.g *= lerp(1.0, glowNoise, saturate(_GlowDistortion));
                mask.g *= keepGlow;
                // 对残影应用同样的两层噪声乘法与外围破碎规则；颜色、消退由历史组件独立控制。
                float trailOuter = 1.0-smoothstep(0.10,0.70,history.g);
                float trailKeep = smoothstep(_GlowEdgeBreakup*trailOuter-edgeAA,_GlowEdgeBreakup*trailOuter+edgeAA,glowNoise);
                trailKeep = lerp(1.0,trailKeep,saturate(_GlowEdgeBreakup*100.0)*smoothstep(0.0,0.1,trailOuter));
                trail.g *= lerp(1.0,glowNoise,saturate(_GlowDistortion))*trailKeep;
                // 能量纹理继续独立流动。
                float2 flow = (i.plane - _FlowSpeed.xy * _EffectTime) * max(_NoiseScale,0.1);
                // 用贴图本身的明暗决定能量形状，不再生成程序噪声或正弦条纹。
                // 不对 UV 做 frac，让采样器 Repeat 并保留连续导数以正确选择 Mipmap。
                float n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, flow).r;
                // 同一贴图以另一尺度采样，提供闪光细节，减少两层图案完全重合。
                float detail = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, flow * 2.1 + float2(0.37,0.19)).r;
                float aa = max(fwidth(n),0.02);
                float filaments = smoothstep(0.45-aa,0.75+aa,n);
                float pulse = 1.0 + _PulseAmount * sin(_EffectTime * 3.0);
                float energy = mask.g * (0.12 + filaments * (0.3 + detail)) * _EnergyIntensity * pulse;
                // 稀疏高亮噪声与当前轮廓相乘，得到平面内闪动光点；这些不是空间粒子。
                float flecks = smoothstep(0.68,0.9,detail) * mask.r * _SparkIntensity;
                float3 rgb = _SurfaceTint.rgb * _SurfaceTint.a;
                // 基础亮线始终存在，噪声低谷也不会使交界轮廓断裂。
                rgb += mask.r * _LineColor.rgb + mask.g * _GlowColor.rgb * 0.45;
                rgb += energy * _EnergyColor.rgb + flecks * _LineColor.rgb;
                // 残影强度随历史 RT 衰减；不改变平面底色的透明度，也不影响角色溶解。
                float trailEnergy = trail.g*(0.12+filaments*(0.3+detail))*_EnergyIntensity*pulse;
                rgb += trail.r*_TrailLineColor.rgb + (trail.g*0.45+trailEnergy)*_TrailGlowColor.rgb;
                return half4(rgb, _SurfaceTint.a);
            }
            ENDHLSL
        }
    }
}


