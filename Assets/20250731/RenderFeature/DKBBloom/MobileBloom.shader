Shader "Hidden/PostProcess/MobileBloom"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    // _BlitTexture 与 sampler_LinearClamp 由 Blit.hlsl 提供
    TEXTURE2D_X(_BloomTexture);
    TEXTURE2D_X(_PreviousMip);

    float4 _Threshold;     // x=threshold, y=threshold-knee, z=2*knee, w=0.25/knee
    float4 _Params;        // xy=halfPixel(source), z=scatter
    float  _Intensity;
    float4 _Tint;

    half3 SafeHDR(half3 c) { return min(c, half3(65000.0, 65000.0, 65000.0)); }

    // 软膝盖阈值
    half3 SoftThreshold(half3 c)
    {
        half br = max(c.r, max(c.g, c.b));
        half rq = clamp(br - _Threshold.y, 0.0, _Threshold.z);
        rq = rq * rq * _Threshold.w;
        return c * max(rq, br - _Threshold.x) / max(br, 1e-4);
    }

    // ====== Pass 0: Prefilter + Box4 Downsample ======
    half4 FragPrefilter(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;
        float2 d  = _Params.xy;

        half3 a = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-d.x, -d.y)).rgb;
        half3 b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( d.x, -d.y)).rgb;
        half3 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-d.x,  d.y)).rgb;
        half3 e = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( d.x,  d.y)).rgb;

        half3 col = (a + b + c + e) * 0.25;
        col = SoftThreshold(SafeHDR(col));
        return half4(col, 1.0);
    }

    // ====== Pass 1: Dual Kawase Downsample (5 tap) ======
    half4 FragDownsample(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;
        float2 d  = _Params.xy;

        half3 sum = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb * 4.0;
        sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-d.x, -d.y)).rgb;
        sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( d.x, -d.y)).rgb;
        sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-d.x,  d.y)).rgb;
        sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( d.x,  d.y)).rgb;
        return half4(sum / 8.0, 1.0);
    }

    // ====== Pass 2: Dual Kawase Upsample (8 tap tent) + 多 band 融合 ======
    half4 FragUpsample(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;
        float2 d  = _Params.xy;
        half scatter = _Params.z;

        half3 sum = 0;
        sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-d.x * 2.0,  0.0     )).rgb;
        sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-d.x,        d.y     )).rgb * 2.0;
        sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( 0.0,        d.y * 2.0)).rgb;
        sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( d.x,        d.y     )).rgb * 2.0;
        sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( d.x * 2.0,  0.0     )).rgb;
        sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( d.x,       -d.y     )).rgb * 2.0;
        sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( 0.0,       -d.y * 2.0)).rgb;
        sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-d.x,       -d.y     )).rgb * 2.0;
        half3 blurred = sum / 12.0;

        // 与同分辨率的 down 级混合,scatter=1 时纯模糊,scatter=0 时只有 band
        half3 band = SAMPLE_TEXTURE2D_X(_PreviousMip, sampler_LinearClamp, uv).rgb;
        return half4(lerp(band, blurred, scatter), 1.0);
    }

    // ====== Pass 3: Composite ======
    half4 FragComposite(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;
        half3 src   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
        half3 bloom = SAMPLE_TEXTURE2D_X(_BloomTexture, sampler_LinearClamp, uv).rgb;
        return half4(src + bloom * _Tint.rgb * _Intensity, 1.0);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off  ZTest Always  Cull Off

        Pass
        {
            Name "Prefilter Downsample"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPrefilter
            ENDHLSL
        }
        Pass
        {
            Name "Dual Kawase Downsample"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDownsample
            ENDHLSL
        }
        Pass
        {
            Name "Dual Kawase Upsample"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUpsample
            ENDHLSL
        }
        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }
    }
    Fallback Off
}
