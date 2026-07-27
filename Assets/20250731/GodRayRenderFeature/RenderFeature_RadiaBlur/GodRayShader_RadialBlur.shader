Shader "Custom/GodRay_RadialBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
        LOD 100
        ZWrite Off
        ZTest Always
        Cull Off

        // Pass 0: Highlight / Bright Mask
        // Excluded-layer objects are zeroed out here so they don't contribute
        // any bright source pixels to the radial blur.
        Pass
        {
            Name "Highlight"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv          : TEXCOORD0;
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float3 _LightDir;
            float4 _LightColor;
            float4 _TintColor;
            float  _BlurStrength;
            int    _SampleCount;
            float  _BlurFalloff;
            float  _Threshold;
            float  _Intensity;
            CBUFFER_END

            TEXTURE2D(_MainTex);             SAMPLER(sampler_MainTex);
            TEXTURE2D(_CameraDepthTexture);  SAMPLER(sampler_CameraDepthTexture);

            // White = excluded (no god ray source), Black = normal
            TEXTURE2D(_ExclusionMaskRT);     SAMPLER(sampler_ExclusionMaskRT);

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float depth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, i.uv).r;
                half4 col   = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                #if UNITY_REVERSED_Z
                    float skyMask = step(depth, 0.0001);
                #else
                    float skyMask = step(0.9999, depth);
                #endif

                float luminance  = dot(col.rgb, float3(0.299, 0.587, 0.114));
                float brightMask = saturate(skyMask + step(_Threshold, luminance));

                // Zero out excluded-layer pixels before they can become a god-ray source.
                float exclusion = SAMPLE_TEXTURE2D(_ExclusionMaskRT, sampler_ExclusionMaskRT, i.uv).r;
                brightMask *= (1.0 - exclusion);

                return half4(col.rgb * brightMask, 1.0);
            }
            ENDHLSL
        }

        // Pass 1: Radial Blur — unchanged
        Pass
        {
            Name "RadialBlur"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv          : TEXCOORD0;
                float4 positionHCS : SV_POSITION;
            };

            struct BlurParams
            {
                float2 blurCenter;
                float  blurStrength;
                int    sampleCount;
                float  blurFalloff;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float3 _LightDir;
            float4 _LightColor;
            float4 _TintColor;
            float  _BlurStrength;
            int    _SampleCount;
            float  _BlurFalloff;
            float  _Threshold;
            float  _Intensity;
            float4 _LightScreenPos;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            float4 RadialBlur(float2 uv, BlurParams params)
            {
                float4 accCol = 0;
                float2 dir    = uv - params.blurCenter;
                float  dist   = length(dir);

                if (dist < 0.0001)
                    return float4(0, 0, 0, 1);

                dir = dir / dist;

                float  stepSize  = params.blurStrength * 0.01;
                float2 currentUV = uv;
                float  weightSum = 0.0001;

                [loop]
                for (int j = 0; j < params.sampleCount; j++)
                {
                    float weight = 1.0 - (float(j) / float(params.sampleCount));
                    accCol      += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, currentUV) * weight;
                    weightSum   += weight;
                    currentUV   -= dir * stepSize;
                }

                accCol /= weightSum;

                float centerFade = smoothstep(0.0, 0.4, dist);
                float edgeFade   = 1.0 - saturate(dist * params.blurFalloff * 0.5);
                accCol.rgb      *= _LightColor.rgb * _Intensity * centerFade * edgeFade;

                return accCol;
            }

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 lightScreenPos = _LightScreenPos.xy;
                float  behindCamera   = _LightScreenPos.z;

                BlurParams params   = (BlurParams)0;
                params.blurCenter   = lightScreenPos;
                params.blurStrength = _BlurStrength;
                params.sampleCount  = _SampleCount;
                params.blurFalloff  = _BlurFalloff;

                float4 blurredCol = RadialBlur(i.uv, params);
                blurredCol *= (1.0 - behindCamera);
                return blurredCol;
            }
            ENDHLSL
        }

        // Pass 2: Composite — completely unchanged
        Pass
        {
            Name "Composite"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv          : TEXCOORD0;
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _TintColor;
            CBUFFER_END

            TEXTURE2D(_MainTex);   SAMPLER(sampler_MainTex);
            TEXTURE2D(_GodRayTex); SAMPLER(sampler_GodRayTex);

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 originalCol = SAMPLE_TEXTURE2D(_MainTex,   sampler_MainTex,   i.uv);
                half4 godRayCol   = SAMPLE_TEXTURE2D(_GodRayTex, sampler_GodRayTex, i.uv);
                return half4(originalCol.rgb + godRayCol.rgb * _TintColor.rgb, 1.0);
            }
            ENDHLSL
        }

        // Pass 3: ExclusionMask
        // Renders excluded objects as solid white using proper depth testing,
        // so their screen-space silhouettes suppress the bright-mask source.
        Pass
        {
            Name "ExclusionMask"
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(1.0, 1.0, 1.0, 1.0);
            }
            ENDHLSL
        }
    }
}
