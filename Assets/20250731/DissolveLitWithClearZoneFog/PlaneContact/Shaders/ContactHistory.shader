// 将当前接触轮廓写入历史，旧轮廓按半衰期消退。两个 RT 交替读写。
Shader "Hidden/LiangZhu/ContactHistory"
{
    Properties { _MainTex("Previous history", 2D) = "black" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _CurrentMask;
            float _Decay, _Cutoff;
            float4 frag(v2f_img i) : SV_Target
            {
                // R 是细线，G 是柔光；始终保存未着色、未乘噪声的原始轮廓。
                float2 oldMask = tex2D(_MainTex,i.uv).rg * _Decay;
                // 半浮点历史可以平滑衰减；低于阈值明确归零，避免永远留下尾数。
                oldMask *= step(_Cutoff,oldMask);
                float2 current = tex2D(_CurrentMask,i.uv).rg;
                // max 保证停在同一处不会逐帧累加变亮，当前接触始终立即写入。
                return float4(max(current,oldMask),0,1);
            }
            ENDHLSL
        }
    }
}
