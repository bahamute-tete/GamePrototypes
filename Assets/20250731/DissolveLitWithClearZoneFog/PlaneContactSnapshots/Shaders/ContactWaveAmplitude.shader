// 每份 SDF 只输出正负振幅，累加到浮点 RT；不能在这里 abs 或 saturate 振幅。
Shader "Hidden/LiangZhu/ContactWaveAmplitude"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend One One
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_SnapshotSdf); SAMPLER(sampler_SnapshotSdf);
            float _Radius,_Wavelength,_PacketWidth,_Amplitude,_Fade,_Phase;
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            V vert(A i)
            {
                V o;o.positionCS=float4(i.positionOS.xy*2,0.5,1);
                // 与普通纹理采样保持方向一致；输出直接覆盖 RT，不依赖场景相机。
                #if UNITY_UV_STARTS_AT_TOP
                o.positionCS.y=-o.positionCS.y;
                #endif
                o.uv=i.uv;return o;
            }
            float4 frag(V i):SV_Target
            {
                float d=SAMPLE_TEXTURE2D(_SnapshotSdf,sampler_SnapshotSdf,i.uv).r;
                float q=d-_Radius;
                float sigma=max(_PacketWidth,0.001);
                float envelope=exp(-q*q/(2*sigma*sigma));
                float height=_Amplitude*cos(6.283185*q/max(_Wavelength,0.001)+_Phase)*envelope*_Fade;
                // 波包只位于截面外侧；负振幅代表波谷，并不是轮廓内部。
                height*=smoothstep(-max(fwidth(d),0.0001),max(fwidth(d),0.0001),d);
                return float4(height,0,0,0);
            }
            ENDHLSL
        }
    }
}
