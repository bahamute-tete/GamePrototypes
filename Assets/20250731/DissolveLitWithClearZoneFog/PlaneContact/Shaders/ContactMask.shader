// 将 C# 已求出的交线画入 RT：R 保存亮线，G 保存柔光。
// 此 Shader 不读取角色网格，不负责三角形求交。
Shader "Hidden/LiangZhu/ContactMask"
{
    SubShader
    {
        Pass
        {
            // 遮罩绘制不需要场景遮挡，所有方向和深度的线段都参与。
            Cull Off ZWrite Off ZTest Always
            Blend One One

            // 重叠线段逐通道取最大值，连接处不会随重叠数量叠亮。
            BlendOp Max

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            // 两个宽度都是世界米：亮线半宽与柔光半径。
            float _CoreHalfWidth, _GlowRadius;

            // position.xy 已由 C# 归一化为裁剪坐标 -1..1。
            // uv 是每条线段自己的米制坐标：起点 (0,0)，终点 (L,0)。
            // length.x 保存 L；uv 不是普通的 0..1 贴图坐标。
            struct Attributes { float4 position : POSITION; float2 uv : TEXCOORD0; float2 length : TEXCOORD1; };

            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float length : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                // 直接定位到 RT，不再使用模型/相机矩阵；w=1 保持二维比例。
                Varyings o; o.position = float4(v.position.xy, 0.5, 1);

                // 适配图形 API 的纹理纵向约定，使写入与平面采样方向一致。
                #if UNITY_UV_STARTS_AT_TOP
                o.position.y = -o.position.y;
                #endif
                o.uv = v.uv; 
                o.length = v.length.x; 
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                // 将沿线段坐标 x 转成超出端点的距离：
                // x<0 时为 -x；0<=x<=L 时为 0；x>L 时为 x-L。
                float outside = max(max(-i.uv.x, i.uv.x-i.length), 0);//左右边界外的距离，中间为负数，取0

                // 点到有限线段的最近距离。垂足在线段内时 outside=0，故 d=abs(y)。
                // 超出两端时则算到最近端点的斜距。d-r 才是胶囊形 SDF。
                float d = length(float2(outside, i.uv.y));

                // 距离值在像素邻域的变化量，作为亮线边缘抗锯齿过渡宽度。
                float aa = max(fwidth(d), 0.0001);

                // 半宽以内亮、以外暗；smoothstep 将硬阈值改成柔和边界。
                float core = 1-smoothstep(max(0,_CoreHalfWidth-aa),_CoreHalfWidth+aa,d);

                // 指数项随距离衰减；最后 20% 半径平滑归零，避免矩形边缘截断。
                // 直接用距离生成柔光，不需要再对贴图做模糊。
                float glow = exp(-4*d*d/max(_GlowRadius*_GlowRadius,1e-8)) * (1-smoothstep(_GlowRadius*.8,_GlowRadius,d));

                // A 只是已绘制区域标记；ContactPlane 实际只读取 R/G 两个通道。
                return half4(core,glow,0,1);
            }
            ENDHLSL
        }
    }
}
