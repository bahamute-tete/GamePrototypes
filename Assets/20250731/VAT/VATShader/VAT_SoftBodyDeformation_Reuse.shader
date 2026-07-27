Shader "SideFX/VAT_SoftBodyDeformation_Reuse"
{
    Properties
    {
        [Header(Playback)]
        [ToggleUI] _B_autoPlayback("Auto Playback", Float) = 1
        _gameTimeAtFirstFrame("Game Time at First Frame", Float) = 0
        _PlaybackStartFrame("Playback Start Frame", Float) = 1
        _displayFrame("Display Frame", Float) = 1
        _playbackSpeed("Playback Speed", Float) = 1
        _houdiniFPS("Houdini FPS", Float) = 60
        [ToggleUI] _B_interpolate("Interframe Interpolation", Float) = 0
        [ToggleUI] _B_interpolateCol("Interpolate Color", Float) = 0

        [Header(Surface)]
        [ToggleUI] _B_surfaceNormals("Support Surface Normal Maps", Float) = 1
        [ToggleUI] _B_twoSidedNorms("Two Sided Normals", Float) = 0
        [Normal][NoScaleOffset] _SurfaceNormalMap("Surface Normal Map", 2D) = "bump" {}

        [Header(VAT Textures)]
        [NoScaleOffset] _posTexture("Position Texture", 2D) = "white" {}
        [NoScaleOffset] _posTexture2("Position Texture 2", 2D) = "white" {}
        [NoScaleOffset] _rotTexture("Rotation Texture", 2D) = "white" {}
        [NoScaleOffset] _colTexture("Color Texture", 2D) = "white" {}
        [Toggle(_B_LOAD_COL_TEX)] _B_LOAD_COL_TEX("Load Color Texture", Float) = 1
        [Toggle(_B_UNLOAD_ROT_TEX)] _B_UNLOAD_ROT_TEX("Use Compressed Normals", Float) = 0
        [Toggle(_B_LOAD_NORM_TEX)] _B_LOAD_NORM_TEX("Load Surface Normal Map", Float) = 0
        [Toggle(_B_LOAD_POS_TWO_TEX)] _B_LOAD_POS_TWO_TEX("Positions Require Two Textures", Float) = 0

        [Header(VAT Data)]
        _frameCount("Frame Count", Float) = 1
        _boundMaxX("Bound Max X", Float) = 0
        _boundMaxY("Bound Max Y", Float) = 0
        _boundMaxZ("Bound Max Z", Float) = 0
        _boundMinX("Bound Min X", Float) = 0
        _boundMinY("Bound Min Y", Float) = 0
        _boundMinZ("Bound Min Z", Float) = 0

        // Kept so existing materials and scripts retain their serialized values.
        [HideInInspector][ToggleUI] _B_interpolateSpareCol("Interpolate Spare Color", Float) = 0
        [HideInInspector][NoScaleOffset] _spareColTexture("Spare Color Texture", 2D) = "white" {}
        [HideInInspector] _QueueOffset("Queue Offset", Float) = 0
        [HideInInspector] _QueueControl("Queue Control", Float) = -1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "UniversalMaterialType" = "Lit"
            "Queue" = "Geometry"
        }

        Cull Back
        ZWrite On
        ZTest LEqual

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _B_autoPlayback;
            float _gameTimeAtFirstFrame;
            float _PlaybackStartFrame;
            float _displayFrame;
            float _playbackSpeed;

            float _houdiniFPS;
            float _B_interpolate;
            float _B_interpolateCol;
            float _B_surfaceNormals;

            float _B_twoSidedNorms;
            float _frameCount;
            float _boundMaxX;
            float _boundMaxY;

            float _boundMaxZ;
            float _boundMinX;
            float _boundMinY;
            float _boundMinZ;
        CBUFFER_END

        StructuredBuffer<float> _gameTimeAtFirstFrameBuffer;

        TEXTURE2D(_posTexture);
        SAMPLER(sampler_posTexture);
        TEXTURE2D(_posTexture2);
        SAMPLER(sampler_posTexture2);
        TEXTURE2D(_rotTexture);
        SAMPLER(sampler_rotTexture);
        TEXTURE2D(_colTexture);
        SAMPLER(sampler_colTexture);
        TEXTURE2D(_SurfaceNormalMap);
        SAMPLER(sampler_SurfaceNormalMap);

        #include "VAT_SoftBodyDeformation_Shared.hlsl"

        #if defined(_B_LOAD_POS_TWO_TEX)
            #define VAT_LOAD_POSITION_TEXTURE_2 1.0
        #else
            #define VAT_LOAD_POSITION_TEXTURE_2 0.0
        #endif

        #if defined(_B_UNLOAD_ROT_TEX)
            #define VAT_USE_COMPRESSED_NORMALS 1.0
        #else
            #define VAT_USE_COMPRESSED_NORMALS 0.0
        #endif

        #if defined(_B_LOAD_COL_TEX)
            #define VAT_LOAD_COLOR_TEXTURE 1.0
        #else
            #define VAT_LOAD_COLOR_TEXTURE 0.0
        #endif

        float3 GetVATBoundMax()
        {
            return float3(_boundMaxX, _boundMaxY, _boundMaxZ);
        }

        float3 GetVATBoundMin()
        {
            return float3(_boundMinX, _boundMinY, _boundMinZ);
        }

        VATFrameData GetVATFrameData(float4 vatUV)
        {
            return VAT_GetFrameData(
                vatUV,
                _TimeParameters.x,
                _B_autoPlayback,
                _gameTimeAtFirstFrame,
                _PlaybackStartFrame,
                _displayFrame,
                _playbackSpeed,
                _houdiniFPS,
                _frameCount,
                GetVATBoundMax(),
                GetVATBoundMin()
            );
        }

        VATGeometry EvaluateVATGeometry(
            float3 sourcePositionOS,
            float4 vatUV,
            VATFrameData frameData)
        {
            return VAT_EvaluateGeometry(
                sourcePositionOS,
                vatUV,
                frameData,
                VAT_BuildTexture2D(TEXTURE2D_ARGS(_posTexture, sampler_posTexture)),
                VAT_BuildTexture2D(TEXTURE2D_ARGS(_posTexture2, sampler_posTexture2)),
                VAT_BuildTexture2D(TEXTURE2D_ARGS(_rotTexture, sampler_rotTexture)),
                _B_interpolate,
                _B_surfaceNormals,
                VAT_USE_COMPRESSED_NORMALS,
                VAT_LOAD_POSITION_TEXTURE_2,
                GetVATBoundMax(),
                GetVATBoundMin()
            );
        }

        float3 EvaluateVATPosition(float3 sourcePositionOS, float4 vatUV)
        {
            VATFrameData frameData = GetVATFrameData(vatUV);
            return VAT_EvaluatePosition(
                sourcePositionOS,
                vatUV,
                frameData,
                VAT_BuildTexture2D(TEXTURE2D_ARGS(_posTexture, sampler_posTexture)),
                VAT_BuildTexture2D(TEXTURE2D_ARGS(_posTexture2, sampler_posTexture2)),
                _B_interpolate,
                VAT_LOAD_POSITION_TEXTURE_2,
                GetVATBoundMax(),
                GetVATBoundMin()
            );
        }

        half3 SampleVATColor(VATFrameData frameData)
        {
            return (half3)VAT_SampleColor(
                frameData,
                VAT_BuildTexture2D(TEXTURE2D_ARGS(_colTexture, sampler_colTexture)),
                VAT_LOAD_COLOR_TEXTURE,
                _B_interpolate,
                _B_interpolateCol
            );
        }

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv0 : TEXCOORD0;
            float4 vatUV : TEXCOORD1;
            float2 dynamicLightmapUV : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct LitVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half3 tangentWS : TEXCOORD2;
            float2 uv0 : TEXCOORD3;
            half3 color : TEXCOORD4;
            DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 5);
            #ifdef DYNAMICLIGHTMAP_ON
                float2 dynamicLightmapUV : TEXCOORD6;
            #endif
            half4 fogAndVertexLight : TEXCOORD7;
            float4 shadowCoord : TEXCOORD8;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        LitVaryings LitPassVertex(Attributes input)
        {
            LitVaryings output = (LitVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            VATFrameData frameData = GetVATFrameData(input.vatUV);
            VATGeometry geometry = EvaluateVATGeometry(input.positionOS.xyz, input.vatUV, frameData);
            VertexPositionInputs positionInputs = GetVertexPositionInputs(geometry.positionOS);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(
                geometry.normalOS,
                float4(geometry.tangentOS, input.tangentOS.w)
            );

            output.positionCS = positionInputs.positionCS;
            output.positionWS = positionInputs.positionWS;
            output.normalWS = normalInputs.normalWS;
            output.tangentWS = normalInputs.tangentWS;
            output.uv0 = input.uv0;
            output.color = SampleVATColor(frameData);
            output.shadowCoord = GetShadowCoord(positionInputs);

            OUTPUT_LIGHTMAP_UV(input.vatUV.xy, unity_LightmapST, output.staticLightmapUV);
            #ifdef DYNAMICLIGHTMAP_ON
                output.dynamicLightmapUV = input.dynamicLightmapUV * unity_DynamicLightmapST.xy
                    + unity_DynamicLightmapST.zw;
            #endif
            OUTPUT_SH(output.normalWS, output.vertexSH);

            half fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
            half3 vertexLight = VertexLighting(positionInputs.positionWS, normalInputs.normalWS);
            output.fogAndVertexLight = half4(fogFactor, vertexLight);
            return output;
        }

        half3 GetSurfaceNormalWS(LitVaryings input, bool isFrontFace)
        {
            half3 normalWS = NormalizeNormalPerPixel(input.normalWS);

            #if !defined(_B_UNLOAD_ROT_TEX) && defined(_B_LOAD_NORM_TEX)
                if (_B_surfaceNormals > 0.5)
                {
                    half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(
                        _SurfaceNormalMap,
                        sampler_SurfaceNormalMap,
                        input.uv0
                    ));
                    half3 tangentWS = SafeNormalize(input.tangentWS);
                    half3 bitangentWS = SafeNormalize(cross(tangentWS, normalWS));
                    normalWS = NormalizeNormalPerPixel(
                        tangentWS * normalTS.x + bitangentWS * normalTS.y + normalWS * normalTS.z
                    );
                }
            #endif

            if (_B_twoSidedNorms > 0.5 && !isFrontFace)
            {
                normalWS = -normalWS;
            }
            return normalWS;
        }

        void InitializeVATInputData(LitVaryings input, half3 normalWS, out InputData inputData)
        {
            inputData = (InputData)0;
            inputData.positionWS = input.positionWS;
            inputData.positionCS = input.positionCS;
            inputData.normalWS = normalWS;
            inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            inputData.shadowCoord = input.shadowCoord;
            inputData.fogCoord = input.fogAndVertexLight.x;
            inputData.vertexLighting = input.fogAndVertexLight.yzw;

            #if defined(DYNAMICLIGHTMAP_ON)
                inputData.bakedGI = SAMPLE_GI(
                    input.staticLightmapUV,
                    input.dynamicLightmapUV,
                    input.vertexSH,
                    normalWS
                );
            #else
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, normalWS);
            #endif

            inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
        }

        SurfaceData GetVATSurfaceData(half3 color)
        {
            SurfaceData surfaceData = (SurfaceData)0;
            surfaceData.albedo = color;
            surfaceData.metallic = 0.0;
            surfaceData.specular = 0.0;
            surfaceData.smoothness = 0.0;
            surfaceData.normalTS = half3(0.0, 0.0, 1.0);
            surfaceData.emission = 0.0;
            surfaceData.occlusion = 1.0;
            surfaceData.alpha = 1.0;
            surfaceData.clearCoatMask = 0.0;
            surfaceData.clearCoatSmoothness = 0.0;
            return surfaceData;
        }
        ENDHLSL

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One Zero

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment ForwardFragment

            #pragma shader_feature_local_vertex _B_LOAD_POS_TWO_TEX
            #pragma shader_feature_local _B_UNLOAD_ROT_TEX
            #pragma shader_feature_local_vertex _B_LOAD_COL_TEX
            #pragma shader_feature_local_fragment _B_LOAD_NORM_TEX

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS

            half4 ForwardFragment(
                LitVaryings input,
                FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC
            ) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = GetSurfaceNormalWS(input, IS_FRONT_VFACE(face, true, false));
                InputData inputData;
                InitializeVATInputData(input, normalWS, inputData);
                SurfaceData surfaceData = GetVATSurfaceData(input.color);

                #ifdef _DBUFFER
                    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "GBuffer"
            Tags { "LightMode" = "UniversalGBuffer" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles3 glcore
            #pragma vertex LitPassVertex
            #pragma fragment GBufferFragment

            #pragma shader_feature_local_vertex _B_LOAD_POS_TWO_TEX
            #pragma shader_feature_local _B_UNLOAD_ROT_TEX
            #pragma shader_feature_local_vertex _B_LOAD_COL_TEX
            #pragma shader_feature_local_fragment _B_LOAD_NORM_TEX

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"

            FragmentOutput GBufferFragment(
                LitVaryings input,
                FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = GetSurfaceNormalWS(input, IS_FRONT_VFACE(face, true, false));
                InputData inputData;
                InitializeVATInputData(input, normalWS, inputData);
                SurfaceData surfaceData = GetVATSurfaceData(input.color);

                #ifdef _DBUFFER
                    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif

                BRDFData brdfData;
                InitializeBRDFData(surfaceData, brdfData);

                Light mainLight = GetMainLight(
                    inputData.shadowCoord,
                    inputData.positionWS,
                    inputData.shadowMask
                );
                MixRealtimeAndBakedGI(
                    mainLight,
                    inputData.normalWS,
                    inputData.bakedGI,
                    inputData.shadowMask
                );
                half3 bakedColor = GlobalIllumination(
                    brdfData,
                    inputData.bakedGI,
                    surfaceData.occlusion,
                    inputData.positionWS,
                    inputData.normalWS,
                    inputData.viewDirectionWS
                );

                return BRDFDataToGbuffer(
                    brdfData,
                    inputData,
                    surfaceData.smoothness,
                    surfaceData.emission + bakedColor,
                    surfaceData.occlusion
                );
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma shader_feature_local_vertex _B_LOAD_POS_TWO_TEX
            #pragma shader_feature_local_vertex _B_UNLOAD_ROT_TEX
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ShadowVaryings ShadowVertex(Attributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VATFrameData frameData = GetVATFrameData(input.vatUV);
                VATGeometry geometry = EvaluateVATGeometry(input.positionOS.xyz, input.vatUV, frameData);
                float3 positionWS = TransformObjectToWorld(geometry.positionOS);
                float3 normalWS = TransformObjectToWorldNormal(geometry.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                output.positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS)
                );
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            half4 ShadowFragment(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0.0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma shader_feature_local_vertex _B_LOAD_POS_TWO_TEX
            #pragma multi_compile_instancing

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthVertex(Attributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionOS = EvaluateVATPosition(input.positionOS.xyz, input.vatUV);
                output.positionCS = TransformObjectToHClip(positionOS);
                return output;
            }

            half4 DepthFragment(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0.0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local_vertex _B_LOAD_POS_TWO_TEX
            #pragma shader_feature_local _B_UNLOAD_ROT_TEX
            #pragma shader_feature_local_fragment _B_LOAD_NORM_TEX
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                half3 tangentWS : TEXCOORD1;
                float2 uv0 : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthNormalsVaryings DepthNormalsVertex(Attributes input)
            {
                DepthNormalsVaryings output = (DepthNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VATFrameData frameData = GetVATFrameData(input.vatUV);
                VATGeometry geometry = EvaluateVATGeometry(input.positionOS.xyz, input.vatUV, frameData);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(
                    geometry.normalOS,
                    float4(geometry.tangentOS, input.tangentOS.w)
                );

                output.positionCS = TransformObjectToHClip(geometry.positionOS);
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = normalInputs.tangentWS;
                output.uv0 = input.uv0;
                return output;
            }

            half3 GetDepthNormalWS(DepthNormalsVaryings input, bool isFrontFace)
            {
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                #if !defined(_B_UNLOAD_ROT_TEX) && defined(_B_LOAD_NORM_TEX)
                    if (_B_surfaceNormals > 0.5)
                    {
                        half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(
                            _SurfaceNormalMap,
                            sampler_SurfaceNormalMap,
                            input.uv0
                        ));
                        half3 tangentWS = SafeNormalize(input.tangentWS);
                        half3 bitangentWS = SafeNormalize(cross(tangentWS, normalWS));
                        normalWS = NormalizeNormalPerPixel(
                            tangentWS * normalTS.x + bitangentWS * normalTS.y + normalWS * normalTS.z
                        );
                    }
                #endif

                if (_B_twoSidedNorms > 0.5 && !isFrontFace)
                {
                    normalWS = -normalWS;
                }
                return normalWS;
            }

            half4 DepthNormalsFragment(
                DepthNormalsVaryings input,
                FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC
            ) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normalWS = GetDepthNormalWS(input, IS_FRONT_VFACE(face, true, false));

                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormal = PackNormalOctQuadEncode(normalWS);
                    float2 remapped = saturate(octNormal * 0.5 + 0.5);
                    return half4(PackFloat2To888(remapped), 0.0);
                #else
                    return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
