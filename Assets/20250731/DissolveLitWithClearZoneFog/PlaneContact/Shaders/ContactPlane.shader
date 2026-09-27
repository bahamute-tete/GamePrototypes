// 场景平面显示：用共享平面坐标读取接触遮罩，再赋予亮线与柔光颜色。
Shader "Custom/LiangZhu/ContactPlane"
{
    Properties
    {
        // 由 ContactMask 生成的线性 RT：R=亮线强度，G=柔光强度。
        _ContactMask("Contact mask", 2D) = "black" {}
        [HDR] _LineColor("Line", Color) = (0.35,1.8,2.4,1)
        [HDR] _GlowColor("Glow", Color) = (0.02,0.55,0.9,1)
        _SurfaceTint("Surface tint", Color) = (0.02,0.12,0.18,0.035)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            // 双面透明平面，不写深度，但仍被前方角色和其他不透明物体遮挡。
            Cull Off ZWrite Off ZTest LEqual
            // 最终 RGB = 输出 RGB + 背景 RGB*(1-输出 Alpha)。
            // 底色做预乘透明混合，亮线和柔光独立加上去。
            Blend One OneMinusSrcAlpha
            // 为共面叠加提供深度偏移，缓解闪烁；不是世界位置偏移。
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ContactMask); SAMPLER(sampler_ContactMask);

            CBUFFER_START(UnityPerMaterial)
            float4 _LineColor, _GlowColor, _SurfaceTint;
            // C# 传入世界中心、两个平面单位方向和世界宽高。
            // 与生成遮罩时的坐标系一致，保证交线映射到正确位置。
            float4 _PlaneOrigin, _PlaneRight, _PlaneUp, _PlaneSize;
            CBUFFER_END

            struct Attributes 
            { 
                float4 positionOS : POSITION; 
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings 
            { 
                float4 positionCS : SV_POSITION; 
                float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO 
            };

            Varyings vert(Attributes v)
            {
                // 初始化实例与双眼数据；几何定位仍采用普通世界到相机变换。
                Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                // 从平面中心指向当前顶点的世界向量。
                float3 delta = world-_PlaneOrigin.xyz;
                // 点积得到平面内的米制 x/y，除以宽高后由 -0.5..0.5 平移为 0..1。
                // 此处是整个平面的 UV，不是 ContactMask 中每段线的局部 UV。
                o.uv = float2(dot(delta,_PlaneRight.xyz),dot(delta,_PlaneUp.xyz))/_PlaneSize.xy+.5;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // R/G 是强度，不是最终颜色；可独立调整两种颜色而不改变几何。
                half2 mask = SAMPLE_TEXTURE2D(_ContactMask,sampler_ContactMask,i.uv).rg;
                // 只将底色乘 Alpha。输出 Alpha 也仅用底色 Alpha，
                // 所以底色 Alpha=0 时仍显示接触光；Line/Glow 的 Alpha 未使用。
                return half4(_SurfaceTint.rgb*_SurfaceTint.a + mask.r*_LineColor.rgb + mask.g*_GlowColor.rgb, _SurfaceTint.a);
            }
            ENDHLSL
        }
    }
}
