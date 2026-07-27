Shader "Custom/LiangZhu/MagicWaterWithDissolveWithMask"
{
    Properties
    {
        [Header(Base)]
        [HDR] _BaseColor ("Shallow Water Color", Color) = (0.05, 0.15, 0.25, 1)

        [Header(Underwater Fog)]
        [HDR] _FogColor   ("Deep Water Color", Color) = (0.0, 0.04, 0.08, 1)
        _FogDensity       ("Fog Density (Beer-Lambert)", Range(0, 2)) = 0.25
        _FogMaxDepth      ("Fog Max Depth (m)", Float) = 30

        [Header(Edge Light (Object Intersection))]
        [Toggle(_EDGE_LIGHT_ON)] _UseEdgeLight ("Use Edge Light", Float) = 0
        [NoScaleOffset] _EdgeTex ("Edge Filament Texture", 2D) = "white" {}
        [HDR] _EdgeColor ("Edge Color", Color) = (1.8, 1.4, 0.7, 1)
        _EdgeDistance     ("Edge Range (m)",          Float)       = 1.5
        _EdgeFalloff      ("Filament Falloff",        Range(0.5, 8))  = 3.0
        _EdgeCoreSharp    ("Core Line Sharpness",     Range(1, 32))   = 10
        _EdgeCoreStrength ("Core Line Strength",      Range(0, 2))    = 0.4
        _EdgeTiling       ("Filament Tiling (world)", Float)          = 0.4
        _EdgeScroll       ("Filament Scroll (XY)",    Vector)         = (0.03, 0.06, 0, 0)

        [Header(Reflection)]
        [Toggle(_PLANAR_REFLECTION_ON)] _UsePlanarReflection ("Use Planar Reflection (Runtime)", Float) = 0
        [NoScaleOffset] _ReflectionCubeA ("Reflection Cubemap A (blend = 0)", Cube) = "_Skybox" {}
        [NoScaleOffset] _ReflectionCubeB ("Reflection Cubemap B (blend = 1)", Cube) = "_Skybox" {}
        _ReflectionStrength ("Reflection Strength", Range(0, 2)) = 1.0
        _PlanarBlur   ("Planar Reflection Blur",  Range(0, 6)) = 0
        _CubemapBlur  ("Cubemap Reflection Blur", Range(0, 6)) = 0   // ← 新增
        _PlanarDistortion ("Planar Distortion (by normal)", Range(0, 0.2)) = 0.03
        _FresnelPower ("Fresnel Power", Range(0.5, 10)) = 5.0
        _FresnelBias  ("Fresnel Bias",  Range(0, 1))   = 0.02

        [Header(Normal Distortion)]
        [NoScaleOffset] _NormalMap1 ("Normal Map 1", 2D) = "bump" {}
        [NoScaleOffset] _NormalMap2 ("Normal Map 2", 2D) = "bump" {}
        _NormalScale   ("Normal Strength", Range(0, 2)) = 0.4
        _NormalTiling  ("Normal Tiling (world)", Float) = 0.1
        _NormalScroll1 ("Normal 1 Scroll (XY)", Vector) = (0.03, 0.02, 0, 0)
        _NormalScroll2 ("Normal 2 Scroll (XY)", Vector) = (-0.02, 0.04, 0, 0)

        [Header(Light Spot Layer 1)]
        [NoScaleOffset] _LightSpot1 ("Light Spot 1 (RGB)", 2D) = "black" {}
        [HDR] _LightSpot1Color ("Color 1", Color) = (1.0, 0.7, 0.3, 1)
        _LightSpot1Tiling ("Tiling 1", Float) = 0.05
        _LightSpot1Scroll ("Scroll 1 (XY)", Vector) = (0.01, 0.01, 0, 0)

        [Header(Light Spot Layer 2)]
        [NoScaleOffset] _LightSpot2 ("Light Spot 2 (RGB)", 2D) = "black" {}
        [HDR] _LightSpot2Color ("Color 2", Color) = (0.3, 0.6, 1.0, 1)
        _LightSpot2Tiling ("Tiling 2", Float) = 0.08
        _LightSpot2Scroll ("Scroll 2 (XY)", Vector) = (-0.01, 0.015, 0, 0)

        [Header(Depth Fade)]
        _DepthFadeStart ("Fade Start (m)", Float) = 50
        _DepthFadeEnd   ("Fade End (m)",   Float) = 400

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

        [Header(Procedural Circle Mask)]
        [Toggle(_CIRCLE_MASK_ON)] _UseCircleMask ("启用圆形遮罩", Float) = 0
        [Enum(Local,0,World,1)]   _CircleSpace   ("遮罩空间", Float) = 1
        _CircleCenter   ("圆心 (xyz)",          Vector) = (0,0,0,0)
        _CircleRadius   ("半径 (到此完全消失)",   Float)  = 5
        _CircleSoftness ("羽化宽度 (越大越软)",    Float)  = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "MagicWaterForward"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            ZTest LEqual
            Cull Back
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _EDGE_LIGHT_ON
            // 反射方案切换：on 用 planar RT；off 回落 cubemap
            #pragma shader_feature_local _PLANAR_REFLECTION_ON
            #pragma shader_feature_local _CIRCLE_MASK_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "HorizonGlow.hlsl"
           

            TEXTURECUBE(_ReflectionCubeA); SAMPLER(sampler_ReflectionCubeA);
            TEXTURECUBE(_ReflectionCubeB); SAMPLER(sampler_ReflectionCubeB);
            half _SkyBlend;

            #ifdef _PLANAR_REFLECTION_ON
                TEXTURE2D(_PlanarReflectionTex); SAMPLER(sampler_PlanarReflectionTex);
            #endif

            #ifdef _EDGE_LIGHT_ON
                TEXTURE2D(_EdgeTex); SAMPLER(sampler_EdgeTex);
            #endif

            TEXTURE2D(_NormalMap1);  SAMPLER(sampler_NormalMap1);
            TEXTURE2D(_NormalMap2);  SAMPLER(sampler_NormalMap2);
            TEXTURE2D(_LightSpot1);  SAMPLER(sampler_LightSpot1);
            TEXTURE2D(_LightSpot2);  SAMPLER(sampler_LightSpot2);

            TEXTURE2D(_DissolveNoiseTex); SAMPLER(sampler_DissolveNoiseTex);

            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                half4  _FogColor;
                half   _FogDensity;
                float  _FogMaxDepth;

                half   _ReflectionStrength;
                half   _PlanarBlur;
                half   _CubemapBlur;       // ← 新增
                half   _PlanarDistortion;
                half   _FresnelPower;
                half   _FresnelBias;

                half   _NormalScale;
                float  _NormalTiling;
                float4 _NormalScroll1;
                float4 _NormalScroll2;

                half4  _LightSpot1Color;
                float  _LightSpot1Tiling;
                float4 _LightSpot1Scroll;
                half4  _LightSpot2Color;
                float  _LightSpot2Tiling;
                float4 _LightSpot2Scroll;

                float  _DepthFadeStart;
                float  _DepthFadeEnd;

                half4  _EdgeColor;
                float  _EdgeDistance;
                half   _EdgeFalloff;
                half   _EdgeCoreSharp;
                half   _EdgeCoreStrength;
                float  _EdgeTiling;
                float4 _EdgeScroll;

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

                float  _CircleSpace;
                float4 _CircleCenter;
                float  _CircleRadius;
                float  _CircleSoftness;
            CBUFFER_END

             #include "DissolveCore.hlsl"

            // 屏幕空间抖动（Interleaved Gradient Noise），把连续遮罩软化成 stipple
            // 内联实现，避免依赖不同 URP 版本里 InterleavedGradientNoise 的位置
            float IGN(float2 pixCoord)
            {
                const float3 magic = float3(0.06711056, 0.00583715, 52.9829189);
                return frac(magic.z * frac(dot(pixCoord, magic.xy)));
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;

                float3 positionOS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionOS = IN.positionOS.xyz;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.screenPos  = ComputeScreenPos(OUT.positionCS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 wXZ = IN.positionWS.xz;
                float  t   = _Time.y;

                // ====== Depth ======
                float2 screenUV     = IN.screenPos.xy / IN.screenPos.w;
                float  sceneRawZ    = SampleSceneDepth(screenUV);
                float  sceneEyeDepth = LinearEyeDepth(sceneRawZ, _ZBufferParams);
                float  waterEyeDepth = -TransformWorldToView(IN.positionWS).z;
                float  waterColumn   = max(sceneEyeDepth - waterEyeDepth, 0);

                // ====== Normal ======
                float2 nUV1 = wXZ * _NormalTiling          + _NormalScroll1.xy * t;
                float2 nUV2 = wXZ * _NormalTiling * 1.73   + _NormalScroll2.xy * t;

                half3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap1, sampler_NormalMap1, nUV1), _NormalScale);
                half3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap2, sampler_NormalMap2, nUV2), _NormalScale);
                half3 nTS = normalize(half3(n1.xy + n2.xy, n1.z * n2.z));
                half3 normalWS = normalize(half3(nTS.x, nTS.z, nTS.y));

                // ====== View / Reflection direction ======
                float3 viewDirWS  = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float3 reflectDir = reflect(-viewDirWS, normalWS);

                // ====== Reflection sampling (开关在这里) ======
                half3 reflectionCol;
                #ifdef _PLANAR_REFLECTION_ON
                    float2 reflUV = screenUV + nTS.xy * _PlanarDistortion;

                    // #if UNITY_UV_STARTS_AT_TOP
                    //     reflUV.y = 1.0 - reflUV.y;
                    // #endif
                    reflUV = saturate(reflUV);
                    // _PlanarBlur 即 mip level：0 = 原始锐利度，越大越模糊
                    // 三线性过滤会在两个相邻 mip 之间平滑插值，所以小数值也连续生效
                    reflectionCol = SAMPLE_TEXTURE2D_LOD(_PlanarReflectionTex, sampler_PlanarReflectionTex, reflUV, _PlanarBlur).rgb;
                #else
                    // _CubemapBlur 同理：0 = 原始 cubemap mip 0，越大采样越低 mip 越模糊
                    // 注意：cubemap 必须在导入设置里开启 Mipmaps，否则只有 mip 0 可用
                    half3 cubeA = SAMPLE_TEXTURECUBE_LOD(_ReflectionCubeA, sampler_ReflectionCubeA, reflectDir, _CubemapBlur).rgb;
                    half3 cubeB = SAMPLE_TEXTURECUBE_LOD(_ReflectionCubeB, sampler_ReflectionCubeB, reflectDir, _CubemapBlur).rgb;
                    reflectionCol = lerp(cubeA, cubeB, _SkyBlend);
                #endif

                // 两种路径都叠加程序化天际线发光（planar RT 里包含真天空，glow 仍然额外叠一次让它跟 cubemap 路径视觉对齐）
                reflectionCol += ComputeHorizonGlow(reflectDir);
                reflectionCol *= _ReflectionStrength;

                // ====== Fresnel ======
                half NdotV   = saturate(dot(normalWS, viewDirWS));
                half fresnel = _FresnelBias + (1.0 - _FresnelBias) * pow(1.0 - NdotV, _FresnelPower);

                // ====== Underwater fog ======
                float fogDepth     = min(waterColumn, _FogMaxDepth);
                half  transmittance = exp(-fogDepth * _FogDensity);
                half3 waterBody    = lerp(_FogColor.rgb, _BaseColor.rgb, transmittance);

                // ====== Composite ======
                half3 color = lerp(waterBody, reflectionCol, fresnel);

                // ====== Light spots ======
                float2 sUV1 = wXZ * _LightSpot1Tiling + _LightSpot1Scroll.xy * t;
                float2 sUV2 = wXZ * _LightSpot2Tiling + _LightSpot2Scroll.xy * t;
                half3 spot1 = SAMPLE_TEXTURE2D(_LightSpot1, sampler_LightSpot1, sUV1).rgb * _LightSpot1Color.rgb;
                half3 spot2 = SAMPLE_TEXTURE2D(_LightSpot2, sampler_LightSpot2, sUV2).rgb * _LightSpot2Color.rgb;

                // ====== Depth-based distance fade ======
                half farFade = saturate((waterEyeDepth - _DepthFadeStart) / max(_DepthFadeEnd - _DepthFadeStart, 0.0001));
                color += (spot1 + spot2) * (1.0 - farFade);
                color  = lerp(color, reflectionCol, farFade);

                //====== DissolveCore ======
                float2 de = ComputeDissolveAlphaAndEdge(IN.positionWS, IN.positionOS,
                                    IN.positionCS.xy, _DissolveAmount);
                float dissolveAlpha = de.x; // dither 二值 mask:0=已溶解 1=保留
                float edge          = de.y; // 溶解边缘带 [0,1]


                #ifdef _EDGE_LIGHT_ON
                {
                    // 水柱越浅，越靠近交界 —— edge01 在交界处 = 1，距离外 = 0
                    half edge01 = 1.0 - saturate(waterColumn / _EdgeDistance);

                    // 1) 丝状部分：宽一些的衰减，被噪声/丝纹贴图打散成「丝」
                    half edgeBody = pow(edge01, _EdgeFalloff);

                    float2 edgeUV = wXZ * _EdgeTiling + _EdgeScroll.xy * t;
                    half  noise   = SAMPLE_TEXTURE2D(_EdgeTex, sampler_EdgeTex, edgeUV).r;
                    half  filament = edgeBody * noise;

                    // 2) 核心亮线：极窄、紧贴交界的一条「白线」，让边界结构更清晰
                    //    把 _EdgeCoreStrength 设为 0 就只剩丝、不要硬线
                    half coreLine = pow(edge01, _EdgeCoreSharp) * _EdgeCoreStrength;

                    color += (filament + coreLine) * _EdgeColor.rgb;
                }
                #endif



float3 rgb = color.rgb;
float  e   = pow(saturate(edge), max(_DissolveBrightnessPower, 1e-3));
rgb += _DissolveEdgeColor.rgb * _DissolveEdgeIntensity * e;

// ====== 程序化圆形遮罩（软边）======
// circleMask: 1=完全保留, 0=完全消失, 中间为羽化过渡带
float circleMask = 1.0;
#ifdef _CIRCLE_MASK_ON
    float3 cPos  = (_CircleSpace < 0.5) ? IN.positionOS : IN.positionWS;
    float  cDist = distance(cPos.xz, _CircleCenter.xz);
    // d <= R-soft → 1 ; d >= R → 0 ; 中间 smoothstep 过渡
    // _CircleSoftness 越大，过渡带越宽，边缘越软
    float soft = max(_CircleSoftness, 1e-4);
    circleMask = 1.0 - smoothstep(_CircleRadius - soft, _CircleRadius, cDist);
#endif

// 把溶解的二值 mask 与圆形遮罩合并，再用 IGN 抖动 → clip 下得到柔和（stipple）边缘
// 完全保留区(circleMask=1)恒为 1，不会被打散；只有羽化带才出现由密到疏的点
float keep = dissolveAlpha * step(IGN(IN.positionCS.xy), circleMask);
clip(keep - 0.5);

                return half4(rgb, 1);
            }
            ENDHLSL
        }
    }
}
