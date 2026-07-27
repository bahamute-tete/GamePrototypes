Shader "Custom/LiangZhu/GodRay_RadialBlur"
{
    // -------------------------------------------------------------------------
    // God Ray (Radial Blur) — XR-correct, Mobile VR friendly
    //
    // Pass 0: Highlight    — extract bright source pixels (sky / threshold / minus exclusion)
    // Pass 1: RadialBlur   — radial blur from per-eye light screen position
    // Pass 2: Composite    — additive blend of god ray over original color
    // Pass 3: ExclusionMask— solid white silhouette of excluded-layer geometry
    //
    // Architectural notes:
    //   * Passes 0/1/2 use the Blit.hlsl template (Vert + Varyings provided).
    //     This gives us a fullscreen triangle, automatic stereo output, and the
    //     conventional _BlitTexture / _BlitScaleBias plumbing. We never declare
    //     our own fullscreen vert struct for these passes.
    //   * All screen-space sampling uses TEXTURE2D_X / SAMPLE_TEXTURE2D_X. In
    //     Single Pass Instanced these become Texture2DArray sampling and pick
    //     the correct slice via unity_StereoEyeIndex automatically.
    //   * _LightScreenPos is a float4[2] array — one entry per eye. The radial
    //     blur center MUST differ between eyes, otherwise you get parallax
    //     mismatch and the world-space "sun" appears stuck to one eyeball.
    //   * The CBUFFER block is identical across all four passes (placed in
    //     HLSLINCLUDE), which is required for SRP Batcher compatibility.
    //   * Pass 3 uses a normal vertex pipeline (object-space → HClip) because
    //     it's drawn over real geometry via DrawRenderers, not a fullscreen quad.
    // -------------------------------------------------------------------------

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // SRP Batcher requires this CBUFFER to be IDENTICAL across all passes
            // in this SubShader. Keep it here in HLSLINCLUDE so every pass shares it.
            CBUFFER_START(UnityPerMaterial)
                float4 _LightScreenPos[2];      // xy = screen UV per eye; z = behindCamera (0/1); w unused
                float4 _LightColor;             // rgb only (a unused)
                float4 _TintColor;              // composite tint (rgb), a unused
                float4 _GodRayParams;           // x = threshold, y = blurStrength, z = blurFalloff, w = intensity
                float4 _GodRayParams2;          // x = sunDiscIntensity, y = sunDiscSize, zw reserved
                int    _SampleCount;
                float  _AngleAttenuation;       // [0,1] global multiplier from camera-vs-light dot
                float  _UseSkyOnly;             // 0 = threshold OR sky, 1 = sky only
                int    _OcclusionSteps;         // ray-march steps for occlusion mode (Pass 0 only)
                float  _OcclusionMaxRayLength;  // cap on ray length in UV space
            CBUFFER_END

            // External textures injected from C# via SetGlobalTexture.
            // _BlitTexture is provided by Blit.hlsl and rebound by Blitter.* every blit call.
            TEXTURE2D_X(_ExclusionMaskRT);
            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
        ENDHLSL

        // =====================================================================
        // Pass 0 : Highlight (sources for the god rays)
        //
        // Two source modes selectable via shader keyword (set from C# per frame):
        //
        //   _GODRAY_SOURCE_LUMINANCE (default, no keyword set)
        //       Classic mode: bright pixels (sky + threshold pass) become the
        //       source. Cheap, but depends on scene brightness — has nothing
        //       to draw rays from in dim indoor scenes.
        //
        //   _GODRAY_SOURCE_OCCLUSION
        //       For each pixel, ray-march toward the light screen position and
        //       count how many sample steps see "sky" (= unobstructed view of
        //       the sun). The ratio becomes the source brightness. Stable in
        //       any lighting; works indoors as long as something far is visible
        //       through gaps. Costs ~3x the luminance mode but still cheap
        //       (depth sampling is the optimized path on Mobile GPUs).
        //
        // Both modes write to the half-res brightMaskRT and feed Pass 1 unchanged.
        // Excluded-layer pixels are suppressed in either mode.
        // =====================================================================
        Pass
        {
            Name "Highlight"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragHighlight
            #pragma multi_compile_local _ _GODRAY_SOURCE_OCCLUSION

            // ---- Helper: classic luminance threshold mask ---------------------
            float ComputeLuminanceMask(float2 uv, out half3 sourceColor)
            {
                half4 col   = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, uv).r;

                #if UNITY_REVERSED_Z
                    float skyMask = step(depth, 0.0001);
                #else
                    float skyMask = step(0.9999, depth);
                #endif

                float threshold = _GodRayParams.x;
                float luminance = dot(col.rgb, float3(0.299, 0.587, 0.114));
                float softMask  = smoothstep(threshold * 0.85, threshold, luminance);
                float brightMask = lerp(saturate(softMask + skyMask), skyMask, _UseSkyOnly);

                sourceColor = col.rgb;
                return brightMask;
            }

            // ---- Helper: screen-space occlusion ray march ---------------------
            // Returns 0..1 = "fraction of the ray to the sun that is unobstructed".
            // Output is grayscale; Pass 1 will tint by _LightColor.
            float ComputeOcclusionMask(float2 uv)
            {
                float4 lightSP   = _LightScreenPos[unity_StereoEyeIndex];
                float2 lightUV   = lightSP.xy;
                float  behindCam = lightSP.z;

                // Light behind camera: no rays at all.
                if (behindCam > 0.5)
                    return 0.0;

                float2 dir  = lightUV - uv;
                float  dist = length(dir);

                // At light center we'd divide by zero — let the radial blur
                // create the hot-spot, contribute nothing here.
                if (dist < 0.0001)
                    return 0.0;

                dir /= dist;

                // Cap the ray length to: (a) the user limit, (b) actual distance
                // to the light, AND (c) the screen boundary. The third bound is
                // the important one for correctness: out-of-screen samples have
                // no defined meaning (the depth texture only covers what we
                // rendered), so we keep the entire march inside [0,1] UV.
                //
                // tBound{X,Y} is the parametric t along (uv + dir*t) where the
                // ray first crosses 0 or 1 on that axis.  min() gives the first
                // exit, which is our hard upper bound.
                float tBoundX = (dir.x > 0.0) ? (1.0 - uv.x) / dir.x :
                                (dir.x < 0.0) ? (-uv.x)        / dir.x : 1e6;
                float tBoundY = (dir.y > 0.0) ? (1.0 - uv.y) / dir.y :
                                (dir.y < 0.0) ? (-uv.y)        / dir.y : 1e6;
                float screenBound = min(tBoundX, tBoundY);

                float rayLen  = min(min(dist, _OcclusionMaxRayLength), screenBound);
                float stepLen = rayLen / float(_OcclusionSteps);

                // Pixels right on the screen edge with a ray pointing outward
                // produce rayLen ≈ 0. That correctly degenerates to occlusion=0
                // (i.e. "we have no information, so don't emit a ray") — Pass 1
                // will smoothly fade the result in from neighbouring pixels.

                float occlusion = 0.0;

                [loop]
                for (int s = 1; s <= _OcclusionSteps; s++)
                {
                    float2 sampleUV = uv + dir * stepLen * float(s);

                    float d = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, sampleUV).r;

                    #if UNITY_REVERSED_Z
                        float isSky = step(d, 0.0001);
                    #else
                        float isSky = step(0.9999, d);
                    #endif

                    occlusion += isSky;
                }

                return occlusion / float(_OcclusionSteps);
            }

            half4 FragHighlight(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float exclusion = SAMPLE_TEXTURE2D_X(_ExclusionMaskRT, sampler_PointClamp, uv).r;

                #if defined(_GODRAY_SOURCE_OCCLUSION)
                    // Occlusion: grayscale visibility ratio. Pass 1 tints with _LightColor.
                    float occ = ComputeOcclusionMask(uv);
                    occ *= (1.0 - exclusion);
                    return half4(occ, occ, occ, 1.0);
                #else
                    // Luminance: preserve source color so red emissives produce red rays.
                    half3 sourceColor;
                    float brightMask = ComputeLuminanceMask(uv, sourceColor);
                    brightMask *= (1.0 - exclusion);
                    return half4(sourceColor * brightMask, 1.0);
                #endif
            }
            ENDHLSL
        }

        // =====================================================================
        // Pass 1 : RadialBlur
        // Reads the bright-mask RT and blurs each pixel toward the per-eye
        // light screen position. The accumulated falloff produces the streaks.
        // =====================================================================
        Pass
        {
            Name "RadialBlur"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragRadialBlur

            half4 FragRadialBlur(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                // PER-EYE blur center. Without this you'd get a single mono center
                // baked across both eyes => parallax breakage in VR.
                float4 lightSP    = _LightScreenPos[unity_StereoEyeIndex];
                float2 blurCenter = lightSP.xy;
                float  behindCam  = lightSP.z;

                float blurStrength = _GodRayParams.y;
                float blurFalloff  = _GodRayParams.z;
                float intensity    = _GodRayParams.w;

                float2 dir  = uv - blurCenter;
                float  dist = length(dir);

                if (dist < 0.0001)
                    return half4(0, 0, 0, 1);

                dir /= dist;

                float  stepSize  = blurStrength * 0.01;
                float2 currentUV = uv;
                float4 accCol    = 0;
                float  weightSum = 0.0001;

                [loop]
                for (int j = 0; j < _SampleCount; j++)
                {
                    float weight = 1.0 - (float(j) / float(_SampleCount));
                    accCol      += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, currentUV) * weight;
                    weightSum   += weight;
                    currentUV   -= dir * stepSize;
                }

                accCol /= weightSum;

                // Edge fade: god ray streaks naturally taper as we move away
                // from the light. (centerFade has been removed — it was
                // forcing dist→0 pixels to 0, creating a black hole at the
                // light center with no corresponding bright source to fill it.)
                float edgeFade = 1.0 - saturate(dist * blurFalloff * 0.5);

                // God ray streaks from the radial blur.
                half3 streaks = accCol.rgb * _LightColor.rgb * intensity * edgeFade;

                // Sun disc: a gaussian-falloff bright core centered exactly on
                // the light's screen position. Two roles:
                //   1. Fills the center area where the radial blur has no
                //      meaningful samples to take (dist→0).
                //   2. Acts as the "sun itself" — gives the rays a visible
                //      source to converge into.
                //
                // sunDiscIntensity = 0 → no disc (e.g. indoor scenes where the
                //   "sun" is conceptually outside the window, you only want streaks).
                // sunDiscIntensity > 0 → visible bright core (outdoor / dramatic light).
                float sunDiscIntensity = _GodRayParams2.x;
                float sunDiscSize      = max(_GodRayParams2.y, 0.001);
                float disc             = exp(-(dist * dist) / (sunDiscSize * sunDiscSize));
                half3 sunDisc          = _LightColor.rgb * disc * sunDiscIntensity * intensity;

                // Combine streaks + disc, then apply global attenuations
                // (behind-camera hard kill, view-angle soft fade).
                half3 result = (streaks + sunDisc) * (1.0 - behindCam) * _AngleAttenuation;
                return half4(result, 1.0);
            }
            ENDHLSL
        }

        // =====================================================================
        // Pass 2 : Composite (hardware additive blend onto camera color)
        //
        // Old design: read source + _GodRayTex, output sum to a compositeRT,
        //             then copy compositeRT back to source. Two RTs touched.
        //
        // New design: Blend One One does the addition in fixed-function
        //             hardware. Shader only reads the god ray (as _BlitTexture)
        //             and emits its color; GPU adds it onto whatever's already
        //             in the destination — which CAN be `source` itself, since
        //             we never read source in the shader.
        //
        // Mobile-VR win: one fewer full-res RT, one fewer tile resolve, one
        // fewer fullscreen blit. ~0.5–0.8ms saved on PICO 4 Ultra.
        // =====================================================================
        Pass
        {
            Name "Composite"
            Blend One One
            BlendOp Add

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite

            half4 FragComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // _BlitTexture is the god-ray RT (passed as Blitter source).
                // Existing camera color is in the destination — hardware adds.
                half4 godRayCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                return half4(godRayCol.rgb * _TintColor.rgb, 1.0);
            }
            ENDHLSL
        }

        // =====================================================================
        // Pass 3 : ExclusionMask
        // Drawn over real geometry (DrawRenderers with overrideMaterialPassIndex).
        // Outputs solid white wherever excluded-layer geometry is visible.
        // ZTest LEqual + Cull Back ensures only front-facing visible pixels mark.
        // =====================================================================
        Pass
        {
            Name "ExclusionMask"
            ZWrite Off
            ZTest LEqual
            Cull Back
            ColorMask R

            HLSLPROGRAM
            #pragma vertex VertExclusion
            #pragma fragment FragExclusion
            #pragma multi_compile_instancing

            struct AttributesEx
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsEx
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsEx VertExclusion(AttributesEx input)
            {
                VaryingsEx output = (VaryingsEx)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 FragExclusion(VaryingsEx input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(1, 1, 1, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
