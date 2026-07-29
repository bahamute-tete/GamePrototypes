// =============================================================================
// 3D Gaussian Splatting · URP 渲染 Shader（仅 DC 颜色）
// =============================================================================
// 配合 GsplatURPCompute.compute + CSGuassianSplatRender.cs 使用：
//   Graphics.DrawProceduralIndirect 每 instance 画 6 顶点（两个三角形拼成 quad），
//   顶点阶段按 compute 算出的 NDC 椭圆轴展开 quad，
//   片元阶段用 exp(-0.5·r²) 做高斯衰减，alpha 预乘不透明度。
//
// 数据协议（与 compute 输出对应）：
//   _SplatClipPos[idx] = float4 clip 中心
//   _SplatAxis[idx]    = float4(axis1.xy, axis2.xy)，σ 单位，corner(±1) × 3σ 展开
//   _SplatColor[idx]   = float4(rgb = DC 颜色, a = 不透明度)
//   _SplatOrder[i]     = back-to-front 排序后的 splat 索引
// =============================================================================
Shader "GaussianSplat/URP_Splat"
{
    Properties
    {
        _AlphaCutoff("Alpha Cutoff", Range(0.0, 0.5)) = 0.004   // ~1/255，低于即 discard
    }

    SubShader
    {
        Tags
        {
            "Queue"      = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GaussianSplat"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma require compute

            #include "UnityCG.cginc"

            // 与 compute 输出对应
            StructuredBuffer<float4> _SplatClipPos;
            StructuredBuffer<float4> _SplatAxis;
            StructuredBuffer<float4> _SplatColor;
            StructuredBuffer<uint>   _SplatOrder;

            float _AlphaCutoff;

            // quad 两个三角形的角点（±1），实际偏移 = corner × 3σ（与 compute 的 SIZE_CUTOFF_SIGMAS 对应）
            static const float2 CORNERS[6] =
            {
                float2(-1, -1), float2(1, -1), float2(1, 1),
                float2(-1, -1), float2(1,  1), float2(-1, 1)
            };
            static const float CUTOFF_SIGMAS = 3.0;

            struct Varyings
            {
                float4 clipPos : SV_POSITION;
                float2 r       : TEXCOORD0;   // 相对中心的 σ 距离坐标（r² 用于高斯衰减）
                float4 color   : TEXCOORD1;   // rgb = DC 颜色, a = 不透明度
            };

            Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                Varyings o;

                uint idx = _SplatOrder[instanceID];

                float4 center = _SplatClipPos[idx];   // clip 空间
                float4 axis   = _SplatAxis[idx];      // NDC 空间两主轴（σ 单位）
                float2 corner = CORNERS[vertexID];

                // NDC 偏移 = (corner.x·axis1 + corner.y·axis2) × 3σ，乘 w 转到 clip 空间
                float2 offsetNDC = (corner.x * axis.xy + corner.y * axis.zw) * CUTOFF_SIGMAS;
                o.clipPos = float4(center.xy + offsetNDC * center.w, center.z, center.w);

                o.r     = corner * CUTOFF_SIGMAS;     // σ 坐标，片元端算 r²

                // DC 颜色是 sRGB 值；Linear 色彩空间工程下需先转线性，
                // 否则帧缓冲再做一次线性→sRGB 编码会让画面发白、偏亮
                float4 c = _SplatColor[idx];
#ifdef UNITY_COLORSPACE_GAMMA
                o.color = c;
#else
                o.color = float4(GammaToLinearSpace(c.rgb), c.a);
#endif
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float alpha = i.color.a * exp(-0.5 * dot(i.r, i.r));
                if (alpha < _AlphaCutoff) discard;
                return float4(i.color.rgb, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
