// 合并扩散区域，R=并集距离，G=当前边界所属快照的淡出强度。
Shader "Hidden/LiangZhu/ContactWaveUnion"
{
    Properties { _MainTex("Previous union",2D)="white" {} }
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
            sampler2D _MainTex,_SnapshotSdf;
            float _Radius,_Fade;
            float4 frag(v2f_img i):SV_Target
            {
                float2 previous=tex2D(_MainTex,i.uv).rg;
                // d-radius < 0 表示已经被这一圈扩散覆盖。
                float next=tex2D(_SnapshotSdf,i.uv).r-_Radius;
                // 并集取 min；被另一圈覆盖的旧边界会变成负值，不再画内部交叉线。
                if(next<previous.r) return float4(next,_Fade,0,0);
                // 相同轮廓只保留较强的淡出值，不做亮度累加。
                if(next==previous.r) previous.g=max(previous.g,_Fade);
                return float4(previous,0,0);
            }
            ENDHLSL
        }
    }
}
