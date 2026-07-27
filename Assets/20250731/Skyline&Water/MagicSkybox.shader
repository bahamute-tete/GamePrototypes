Shader "Custom/LiangZhu/MagicSkybox"
{
    Properties
    {
        _YOffset ("YBias", Range(-0.5, 0.5)) = 0.0
      
        // ========== Sky A (blend = 0) ==========
        [NoScaleOffset] _SkyCubeA ("Sky Cubemap A", Cube) = "_Skybox" {}
        _TintA      ("Tint A",            Color)            = (1,1,1,1)
        _ExposureA  ("Exposure A",        Range(0, 8))      = 1.0
        _RotationA  ("Rotation A (deg)",  Range(-360, 360)) = 0

        [Header(Vertical Gradient A)]
        [HDR] _HorizonBaseColorA ("Horizon Base Color A", Color) = (0.6, 0.5, 0.5, 1)
        [HDR] _ZenithColorA      ("Zenith Color A",       Color) = (0.1, 0.15, 0.3, 1)
        _GradientStrengthA ("Gradient Strength A", Range(0,1)) = 0.3

        // ========== Sky B (blend = 1) ==========
        [Space(12)]
        [NoScaleOffset] _SkyCubeB ("Sky Cubemap B", Cube) = "_Skybox" {}
        _TintB      ("Tint B",            Color)            = (1,1,1,1)
        _ExposureB  ("Exposure B",        Range(0, 8))      = 1.0
        _RotationB  ("Rotation B (deg)",  Range(-360, 360)) = 0

        [Header(Vertical Gradient B)]
        [HDR] _HorizonBaseColorB ("Horizon Base Color B", Color) = (0.1, 0.1, 0.2, 1)
        [HDR] _ZenithColorB      ("Zenith Color B",       Color) = (0.0, 0.0, 0.05, 1)
        _GradientStrengthB ("Gradient Strength B", Range(0,1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "HorizonGlow.hlsl"

 
            #include "SphereFogInclude.hlsl"

            TEXTURECUBE(_SkyCubeA); SAMPLER(sampler_SkyCubeA);
            TEXTURECUBE(_SkyCubeB); SAMPLER(sampler_SkyCubeB);

            // 全局变量 —— 由 MagicWaterController 推送，不进 UnityPerMaterial
            half _SkyBlend;

            // 由 SphereFogVolume.cs 推送的开关 (0 = 不影响天空, 1 = 影响)
            // 默认 0,即使没挂 Volume 也不会有意外行为
            half _SF_AffectSky;

            // 由 SphereFogVolume.cs 推送的"天空虚拟距离" (米)。
            // 把天空盒视作距离相机此远的一个点 → 由 SphereFog 采样得到雾因子。
            // 雾体积尺寸 < _SF_SkyDistance 时,虚拟点落在雾区,天空被遮蔽;
            // 雾体积尺寸 > _SF_SkyDistance 时,虚拟点进入清净区,天空显现。
            half _SF_SkyDistance;

            CBUFFER_START(UnityPerMaterial)
                half _YOffset;
                // Sky A
                half4 _TintA;
                half  _ExposureA;
                half  _RotationA;
                half4 _HorizonBaseColorA;
                half4 _ZenithColorA;
                half  _GradientStrengthA;

                // Sky B
                half4 _TintB;
                half  _ExposureB;
                half  _RotationB;
                half4 _HorizonBaseColorB;
                half4 _ZenithColorB;
                half  _GradientStrengthB;
            CBUFFER_END

            // 绕世界 Y 轴旋转方向向量（角度制）
            float3 RotateAroundY(float3 dir, float angleDeg)
            {
                float a = radians(angleDeg);
                float c = cos(a);
                float s = sin(a);
                return float3(c * dir.x - s * dir.z, dir.y, s * dir.x + c * dir.z);
            }

            // 单张 sky 的完整处理（cubemap 旋转用 rotatedDir；垂直渐变用 worldDir）
            half3 EvaluateSky(float3 worldDir, float3 rotatedDir,
                              TEXTURECUBE_PARAM(cube, samp),
                              half4 tint, half exposure,
                              half4 horizonBase, half4 zenith, half gradStrength)
            {
                half3 sky = SAMPLE_TEXTURECUBE(cube, samp, rotatedDir).rgb;
                sky *= tint.rgb * exposure;

                // 渐变用未旋转的世界 dir.y —— 「上下」是世界量，不跟着 cubemap 旋
                half t = saturate(worldDir.y);
                half3 grad = lerp(horizonBase.rgb, zenith.rgb, t);
                sky = lerp(sky, sky * grad, gradStrength);

                return sky;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir        : TEXCOORD0; // skybox mesh: positionOS 就是方向
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dir = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

               

                float3 dir = normalize(IN.dir);
                dir.y += _YOffset;
                dir = normalize(dir);

                // 两张 sky 各自独立旋转后采样
                float3 dirA = RotateAroundY(dir, _RotationA);
                float3 dirB = RotateAroundY(dir, _RotationB);

                half3 skyA = EvaluateSky(dir, dirA, TEXTURECUBE_ARGS(_SkyCubeA, sampler_SkyCubeA),
                                         _TintA, _ExposureA,
                                         _HorizonBaseColorA, _ZenithColorA, _GradientStrengthA);

                half3 skyB = EvaluateSky(dir, dirB, TEXTURECUBE_ARGS(_SkyCubeB, sampler_SkyCubeB),
                                         _TintB, _ExposureB,
                                         _HorizonBaseColorB, _ZenithColorB, _GradientStrengthB);

                // A / B 之间按 _SkyBlend 全局混合
                half3 sky = lerp(skyA, skyB, _SkyBlend);

                // 天际线发光叠加 —— 用未旋转的 dir，天际线由世界水平面定义
                sky += ComputeHorizonGlow(dir);

                // ============ Sphere Fog ============
                // 把天空盒当作"距相机 _SF_SkyDistance 米沿视线方向"的虚拟点,
                // 由 SphereFog 在该点求 fog factor:
                //   - 体积尺寸 < _SF_SkyDistance → 虚拟点在体积外雾区 → 天空被雾遮蔽
                //   - 体积尺寸 > _SF_SkyDistance → 虚拟点进入清净区 → 天空显现
                // 这样把天空盒的"无穷远"问题转成一个可配的有限距离参数。
                UNITY_BRANCH
                if (_SF_AffectSky > 0.5)
                {
                    float3 skyWorldPos = _WorldSpaceCameraPos.xyz + dir * _SF_SkyDistance;
                    sky = SphereFog_Apply(sky, skyWorldPos);
                }

                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
