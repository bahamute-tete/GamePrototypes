// 独立快照显示：R/G 淡出、截面填充、米制 SDF 色带及固定等距线检查。
Shader "Custom/LiangZhu/ContactSnapshotDisplay"
{
    Properties
    {
        _SnapshotMask("Snapshot RG",2D) = "black" {}
        _SnapshotSdf("Signed distance in metres",2D) = "black" {}
        _HasSdf("Has SDF",Float) = 0
        _ViewMode("View mode",Float) = 0
        _PreviewDistance("Preview distance / metres",Float) = 0.1
        _PreviewLineWidth("Preview line width / metres",Float) = 0.015
        _WaveRadius("Wave radius / metres",Float) = 0
        _WaveWidth("Wave width / metres",Float) = 0.025
        _WaveSoftness("Wave softness / metres",Float) = 0.02
        [HDR] _WaveColor("Wave color",Color) = (0.08,0.8,1.4,1)
        [HDR] _LineColor("Snapshot line",Color) = (0.03,0.25,0.55,1)
        [HDR] _GlowColor("Snapshot glow",Color) = (0.01,0.08,0.22,1)
        _Fade("Lifetime fade",Range(0,1)) = 1
        _InterferenceHeight("Signed summed height",2D) = "black" {}
        _MergedDistance("Merged distance and fade",2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+1" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            // 加法显示历史，支持双面；仍被前方不透明物体遮挡，不写深度。
            Cull Off ZWrite Off ZTest LEqual
            Blend One One
            Offset -1,-1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_SnapshotMask); SAMPLER(sampler_SnapshotMask);
            TEXTURE2D(_SnapshotSdf); SAMPLER(sampler_SnapshotSdf);
            TEXTURE2D(_InterferenceHeight); SAMPLER(sampler_InterferenceHeight);
            TEXTURE2D(_MergedDistance); SAMPLER(sampler_MergedDistance);
            CBUFFER_START(UnityPerMaterial)
            float4 _LineColor,_GlowColor;
            float _Fade;
            float _HasSdf,_ViewMode,_PreviewDistance,_PreviewLineWidth;
            float _WaveRadius,_WaveWidth,_WaveSoftness;
            float4 _WaveColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes i)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                if(_ViewMode>5.5)
                {
                    float2 merged=SAMPLE_TEXTURE2D(_MergedDistance,sampler_MergedDistance,i.uv).rg;
                    // 只画并集的零边界，既不逐圈画线，也不把各圈颜色叠加。
                    float aa=max(fwidth(merged.r),0.0001);
                    float edge=1.0-smoothstep(_WaveWidth*.5,_WaveWidth*.5+max(_WaveSoftness,aa),abs(merged.r));
                    return half4(edge*saturate(merged.g)*_WaveColor.rgb,0);
                }
                if(_ViewMode>4.5)
                {
                    // 所有正负波形已先相加，只在最终统一显示时转成亮度。
                    // 波峰+波谷会抵消，相同相位会增强；不能先对每圈取绝对值。
                    float h=SAMPLE_TEXTURE2D(_InterferenceHeight,sampler_InterferenceHeight,i.uv).r;
                    return half4(abs(h)*_WaveColor.rgb,0);
                }
                half2 mask=SAMPLE_TEXTURE2D(_SnapshotMask,sampler_SnapshotMask,i.uv).rg;
                // 无可靠 SDF 时不伪造外扩，检查模式仍可回退到普通快照。
                if(_ViewMode>3.5 && _HasSdf<0.5) return half4(0,0,0,0);
                if(_HasSdf>0.5 && _ViewMode>0.5)
                {
                    // RFloat 直接存有符号米制距离，无须 0..1 解码；负值为内部。
                    float d=SAMPLE_TEXTURE2D(_SnapshotSdf,sampler_SnapshotSdf,i.uv).r;
                    float aa=max(fwidth(d),0.0001);
                    float boundary=1.0-smoothstep(0.0,aa*1.5,abs(d));
                    float3 color;
                    if(_ViewMode<1.5)
                        color=float3(0.02,0.4,0.6)*(1.0-smoothstep(-aa,aa,d))+boundary;
                    else if(_ViewMode<2.5)
                    {
                        // 内蓝外橙，每 0.1 米一圈距离色带；白线标记 d=0。
                        float bands=0.6+0.4*cos(d*62.831853);
                        color=(d<0?float3(0.02,0.25,0.7):float3(0.5,0.13,0.015))*bands+boundary;
                    }
                    else if(_ViewMode<3.5)
                    {
                        // 固定正距离只在轮廓外侧形成等值线，不使用重心缩放。
                        float wave=1.0-smoothstep(_PreviewLineWidth*.5,_PreviewLineWidth*.5+aa,abs(d-_PreviewDistance));
                        color=wave*float3(0.1,0.8,1.0)+boundary*float3(0.2,0.2,0.2);
                    }
                    else
                    {
                        // 半径由该份快照的年龄驱动。只取正距离波前，不使用重心或缩放。
                        float offset=abs(d-max(0.0,_WaveRadius));
                        float wave=1.0-smoothstep(_WaveWidth*.5,_WaveWidth*.5+max(_WaveSoftness,aa),offset);
                        // 出生时限制亮带进入负距离区，避免额外产生向内收缩的一圈。
                        wave*=smoothstep(-aa,aa,d);
                        color=wave*_WaveColor.rgb;
                    }
                    return half4(color*saturate(_Fade),0);
                }
                return half4((mask.r*_LineColor.rgb+mask.g*_GlowColor.rgb)*saturate(_Fade),0);
            }
            ENDHLSL
        }
    }
}

