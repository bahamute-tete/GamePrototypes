#ifndef LIGHT_INPUT_INCLUDED
#define LIGHT_INPUT_INCLUDED



#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"


CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _NormalMap_ST;
half4 _BaseColor;
half4 _SpecColor;
half _Cutoff;
half _Smoothness;
half _Metallic;
half _NormalScale;
half _OcclusionStrength;
float4 _GroundMap_ST;
float4 _GroundColor;
CBUFFER_END


TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
TEXTURE2D(_GroundMap);          SAMPLER(sampler_GroundMap);
TEXTURE2D(_GroundNormalMap);    SAMPLER(sampler_GroundNormalMap);
TEXTURE2D(_NormalMap);          SAMPLER(sampler_NormalMap);





 ///////////////////////////////////////////////////////////////////////////////

 half Alpha(half albedoAlpha, half4 color, half cutoff)
 {
 #if !defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A) && !defined(_GLOSSINESS_FROM_BASE_ALPHA)
     half alpha = albedoAlpha * color.a;
 #else
     half alpha = color.a;
 #endif
 
 #if defined(_ALPHATEST_ON)
     clip(alpha - cutoff);
 #endif
 
     return alpha;
 }
 
 half4 SampleAlbedoAlpha(float2 uv, TEXTURE2D_PARAM(albedoAlphaMap, sampler_albedoAlphaMap))
 {
     return half4(SAMPLE_TEXTURE2D(albedoAlphaMap, sampler_albedoAlphaMap, uv));
 }
 
 half3 SampleNormal(float2 uv, TEXTURE2D_PARAM(bumpMap, sampler_bumpMap), half scale = half(1.0))
 {
 #ifdef _NORMALMAP
     half4 n = SAMPLE_TEXTURE2D(bumpMap, sampler_bumpMap, uv);
     #if BUMP_SCALE_NOT_SUPPORTED
         return UnpackNormal(n);
     #else
         return UnpackNormalScale(n, scale);
     #endif
 #else
     return half3(0.0h, 0.0h, 1.0h);
 #endif
 }
 
 //////////////////////////////////////////////////////////////////////////////
 
 half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
 {
     half4 specGloss;
 
 #ifdef _METALLICSPECGLOSSMAP
     specGloss = half4(SAMPLE_METALLICSPECULAR(uv));
     #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
         specGloss.a = albedoAlpha * _Smoothness;
     #else
         specGloss.a *= _Smoothness;
     #endif
 #else // _METALLICSPECGLOSSMAP
     #if _SPECULAR_SETUP
         specGloss.rgb = _SpecColor.rgb;
     #else
         specGloss.rgb = _Metallic.rrr;
     #endif
 
     #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
         specGloss.a = albedoAlpha * _Smoothness;
     #else
         specGloss.a = _Smoothness;
     #endif
 #endif
 
     return specGloss;
 }
 
 
 half SampleOcclusion(float2 uv)
 {
     #ifdef _OCCLUSIONMAP
         // TODO: Controls things like these by exposing SHADER_QUALITY levels (low, medium, high)
         #if defined(SHADER_API_GLES)
             return SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
         #else
             half occ = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
             return LerpWhiteTo(occ, _OcclusionStrength);
         #endif
     #else
         return half(1.0);
     #endif
 }
 





 
inline void InitializeStandardLitSurfaceData(float2 uv ,out SurfaceData outSurfaceData)
 {

    outSurfaceData = (SurfaceData) 0;
     half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
     outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);
 
     half4 specGloss = SampleMetallicSpecGloss(uv, albedoAlpha.a);
     outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;
 
 #if _SPECULAR_SETUP
     outSurfaceData.metallic = half(1.0);
     outSurfaceData.specular = specGloss.rgb;
 #else
     outSurfaceData.metallic = specGloss.r;
     outSurfaceData.specular = half3(0.0, 0.0, 0.0);
 #endif
 
     outSurfaceData.smoothness = specGloss.a;
     outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_NormalMap, sampler_NormalMap), _NormalScale);
     outSurfaceData.occlusion = SampleOcclusion(uv);
     
 #if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
     half2 clearCoat = SampleClearCoat(uv);
     outSurfaceData.clearCoatMask       = clearCoat.r;
     outSurfaceData.clearCoatSmoothness = clearCoat.g;
 #else
     outSurfaceData.clearCoatMask       = half(0.0);
     outSurfaceData.clearCoatSmoothness = half(0.0);
 #endif
 
 #if defined(_DETAIL)
     half detailMask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, uv).a;
     float2 detailUv = uv * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
     outSurfaceData.albedo = ApplyDetailAlbedo(detailUv, outSurfaceData.albedo, detailMask);
     outSurfaceData.normalTS = ApplyDetailNormal(detailUv, outSurfaceData.normalTS, detailMask);
 #endif
 } 


#endif