Shader "Grass 1"
{
    Properties
    {
        _position("position", Vector) = (0, 0, 0, 0)
        _WindShiftStreth("WindShiftStreth", Float) = 1
        _WindSpeed("WindSpeed", Float) = 1
        _WindStrenth("WindStrenth", Float) = 1
        [NonModifiableTextureData][NoScaleOffset]_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1("Texture2D", 2D) = "white" {}
        [HideInInspector]_QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector]_QueueControl("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "UniversalMaterialType" = "Lit"
            "Queue"="Geometry"
            "ShaderGraphShader"="true"
            "ShaderGraphTargetId"="UniversalLitSubTarget"
        }
        Pass
        {
            Name "Universal Forward"
            Tags
            {
                "LightMode" = "UniversalForward"
            }
        
        // Render State
        Cull Off
        Blend One Zero
        ZTest LEqual
        ZWrite On
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 4.5
        #pragma exclude_renderers gles gles3 glcore
        #pragma multi_compile_instancing
        #pragma multi_compile_fog
        #pragma instancing_options renderinglayer
        #pragma multi_compile _ DOTS_INSTANCING_ON
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
        #pragma multi_compile _ LIGHTMAP_ON
        #pragma multi_compile _ DYNAMICLIGHTMAP_ON
        #pragma multi_compile _ DIRLIGHTMAP_COMBINED
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
        #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
        #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
        #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
        #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
        #pragma multi_compile_fragment _ _SHADOWS_SOFT
        #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
        #pragma multi_compile _ SHADOWS_SHADOWMASK
        #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
        #pragma multi_compile_fragment _ _LIGHT_LAYERS
        #pragma multi_compile_fragment _ DEBUG_DISPLAY
        #pragma multi_compile_fragment _ _LIGHT_COOKIES
        #pragma multi_compile _ _CLUSTERED_RENDERING
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_TEXCOORD1
        #define ATTRIBUTES_NEED_TEXCOORD2
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TANGENT_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_VIEWDIRECTION_WS
        #define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        #define VARYINGS_NEED_SHADOW_COORD
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_FORWARD
        #define _FOG_FRAGMENT 1
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 uv1 : TEXCOORD1;
             float4 uv2 : TEXCOORD2;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
             float4 tangentWS;
             float4 texCoord0;
             float3 viewDirectionWS;
            #if defined(LIGHTMAP_ON)
             float2 staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
             float2 dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
             float3 sh;
            #endif
             float4 fogFactorAndVertexLight;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
             float4 shadowCoord;
            #endif
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 TangentSpaceNormal;
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 WorldSpaceTangent;
             float3 ObjectSpaceBiTangent;
             float3 WorldSpaceBiTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float4 uv0;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float3 interp0 : INTERP0;
             float3 interp1 : INTERP1;
             float4 interp2 : INTERP2;
             float4 interp3 : INTERP3;
             float3 interp4 : INTERP4;
             float2 interp5 : INTERP5;
             float2 interp6 : INTERP6;
             float3 interp7 : INTERP7;
             float4 interp8 : INTERP8;
             float4 interp9 : INTERP9;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.interp0.xyz =  input.positionWS;
            output.interp1.xyz =  input.normalWS;
            output.interp2.xyzw =  input.tangentWS;
            output.interp3.xyzw =  input.texCoord0;
            output.interp4.xyz =  input.viewDirectionWS;
            #if defined(LIGHTMAP_ON)
            output.interp5.xy =  input.staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
            output.interp6.xy =  input.dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.interp7.xyz =  input.sh;
            #endif
            output.interp8.xyzw =  input.fogFactorAndVertexLight;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            output.interp9.xyzw =  input.shadowCoord;
            #endif
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.positionWS = input.interp0.xyz;
            output.normalWS = input.interp1.xyz;
            output.tangentWS = input.interp2.xyzw;
            output.texCoord0 = input.interp3.xyzw;
            output.viewDirectionWS = input.interp4.xyz;
            #if defined(LIGHTMAP_ON)
            output.staticLightmapUV = input.interp5.xy;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
            output.dynamicLightmapUV = input.interp6.xy;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.sh = input.interp7.xyz;
            #endif
            output.fogFactorAndVertexLight = input.interp8.xyzw;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            output.shadowCoord = input.interp9.xyzw;
            #endif
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
       // CBUFFER_START(UnityPerMaterial)
        float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1_TexelSize;
        float3 _position;
        float _WindShiftStreth;
        float _WindSpeed;
        float _WindStrenth;
        //CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        SAMPLER(sampler_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        
        inline float Unity_SimpleNoise_RandomValue_float (float2 uv)
        {
            float angle = dot(uv, float2(12.9898, 78.233));
            #if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN))
                // 'sin()' has bad precision on Mali GPUs for inputs > 10000
                angle = fmod(angle, TWO_PI); // Avoid large inputs to sin()
            #endif
            return frac(sin(angle)*43758.5453);
        }
        
        inline float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t)
        {
            return (1.0-t)*a + (t*b);
        }
        
        
        inline float Unity_SimpleNoise_ValueNoise_float (float2 uv)
        {
            float2 i = floor(uv);
            float2 f = frac(uv);
            f = f * f * (3.0 - 2.0 * f);
        
            uv = abs(frac(uv) - 0.5);
            float2 c0 = i + float2(0.0, 0.0);
            float2 c1 = i + float2(1.0, 0.0);
            float2 c2 = i + float2(0.0, 1.0);
            float2 c3 = i + float2(1.0, 1.0);
            float r0 = Unity_SimpleNoise_RandomValue_float(c0);
            float r1 = Unity_SimpleNoise_RandomValue_float(c1);
            float r2 = Unity_SimpleNoise_RandomValue_float(c2);
            float r3 = Unity_SimpleNoise_RandomValue_float(c3);
        
            float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
            float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
            float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
            return t;
        }
        void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
        {
            float t = 0.0;
        
            float freq = pow(2.0, float(0));
            float amp = pow(0.5, float(3-0));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(1));
            amp = pow(0.5, float(3-1));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(2));
            amp = pow(0.5, float(3-2));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            Out = t;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void TriangleWave_float(float In, out float Out)
        {
            Out = 2.0 * abs( 2 * (In - floor(0.5 + In)) ) - 1.0;
        }
        
        void Unity_Lerp_float(float A, float B, float T, out float Out)
        {
            Out = lerp(A, B, T);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Split_4943b3735a0e4ba5819db29ad339111a_R_1 = IN.WorldSpacePosition[0];
            float _Split_4943b3735a0e4ba5819db29ad339111a_G_2 = IN.WorldSpacePosition[1];
            float _Split_4943b3735a0e4ba5819db29ad339111a_B_3 = IN.WorldSpacePosition[2];
            float _Split_4943b3735a0e4ba5819db29ad339111a_A_4 = 0;
            float _Split_1df4c052b12745db8c3ab81a1115c945_R_1 = IN.WorldSpacePosition[0];
            float _Split_1df4c052b12745db8c3ab81a1115c945_G_2 = IN.WorldSpacePosition[1];
            float _Split_1df4c052b12745db8c3ab81a1115c945_B_3 = IN.WorldSpacePosition[2];
            float _Split_1df4c052b12745db8c3ab81a1115c945_A_4 = 0;
            float4 _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4;
            float3 _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5;
            float2 _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6;
            Unity_Combine_float(_Split_1df4c052b12745db8c3ab81a1115c945_R_1, _Split_1df4c052b12745db8c3ab81a1115c945_G_2, 0, 0, _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4, _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5, _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6);
            float _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2;
            Unity_SimpleNoise_float(_Combine_c8a8a8a174fe417389fcb951c422011a_RG_6, 500, _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2);
            float _Property_f5800ee87a11435ba90f964e3635c567_Out_0 = _WindShiftStreth;
            float _Multiply_44b7a7abd320474289777104102625d7_Out_2;
            Unity_Multiply_float_float(_SimpleNoise_e8723a906e95458096db370034b18abe_Out_2, _Property_f5800ee87a11435ba90f964e3635c567_Out_0, _Multiply_44b7a7abd320474289777104102625d7_Out_2);
            float _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0 = _WindSpeed;
            float _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2);
            float _Add_5236d4953a3147c88bc756d35f145c51_Out_2;
            Unity_Add_float(_Multiply_44b7a7abd320474289777104102625d7_Out_2, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2, _Add_5236d4953a3147c88bc756d35f145c51_Out_2);
            float _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1;
            TriangleWave_float(_Add_5236d4953a3147c88bc756d35f145c51_Out_2, _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1);
            float _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0 = _WindStrenth;
            float _Multiply_642789de8d1c4560848f311ee76485d0_Out_2;
            Unity_Multiply_float_float(_TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1, _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2);
            float _Add_d86e51a612d648418462758f30bf5a46_Out_2;
            Unity_Add_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2, _Add_d86e51a612d648418462758f30bf5a46_Out_2);
            float4 _UV_14cd8fc313be473da647d5d3c819522b_Out_0 = IN.uv0;
            float _Split_524956ebfd064436818e66014c0bb66b_R_1 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[0];
            float _Split_524956ebfd064436818e66014c0bb66b_G_2 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[1];
            float _Split_524956ebfd064436818e66014c0bb66b_B_3 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[2];
            float _Split_524956ebfd064436818e66014c0bb66b_A_4 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[3];
            float _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3;
            Unity_Lerp_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Add_d86e51a612d648418462758f30bf5a46_Out_2, _Split_524956ebfd064436818e66014c0bb66b_G_2, _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3);
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_R_1 = IN.WorldSpacePosition[0];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2 = IN.WorldSpacePosition[1];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3 = IN.WorldSpacePosition[2];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_A_4 = 0;
            float4 _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4;
            float3 _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5;
            float2 _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6;
            Unity_Combine_float(_Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3, _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2, _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3, 0, _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4, _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5, _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6);
            float3 _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1 = TransformWorldToObject(_Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5.xyz);
            description.Position = _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float3 NormalTS;
            float3 Emission;
            float Metallic;
            float Smoothness;
            float Occlusion;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).GetTransformedUV(IN.uv0.xy));
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_R_4 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.r;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_G_5 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.g;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_B_6 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.b;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_A_7 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.a;
            surface.BaseColor = (_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.xyz);
            surface.NormalTS = IN.TangentSpaceNormal;
            surface.Emission = float3(0, 0, 0);
            surface.Metallic = 0;
            surface.Smoothness = 0.5;
            surface.Occlusion = 1;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.WorldSpaceTangent =                          TransformObjectToWorldDir(input.tangentOS.xyz);
            output.ObjectSpaceBiTangent =                       normalize(cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w > 0.0f ? 1.0f : -1.0f) * GetOddNegativeScale());
            output.WorldSpaceBiTangent =                        TransformObjectToWorldDir(output.ObjectSpaceBiTangent);
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.uv0 =                                        input.uv0;
            output.TimeParameters =                             _TimeParameters.xyz;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
            // FragInputs from VFX come from two places: Interpolator or CBuffer.
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
            output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);
        
        
            output.uv0 = input.texCoord0;
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBRForwardPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "GBuffer"
            Tags
            {
                "LightMode" = "UniversalGBuffer"
            }
        
        // Render State
        Cull Off
        Blend One Zero
        ZTest LEqual
        ZWrite On
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 4.5
        #pragma exclude_renderers gles gles3 glcore
        #pragma multi_compile_instancing
        #pragma multi_compile_fog
        #pragma instancing_options renderinglayer
        #pragma multi_compile _ DOTS_INSTANCING_ON
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        #pragma multi_compile _ LIGHTMAP_ON
        #pragma multi_compile _ DYNAMICLIGHTMAP_ON
        #pragma multi_compile _ DIRLIGHTMAP_COMBINED
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
        #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
        #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
        #pragma multi_compile_fragment _ _SHADOWS_SOFT
        #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
        #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
        #pragma multi_compile _ SHADOWS_SHADOWMASK
        #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
        #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
        #pragma multi_compile_fragment _ _LIGHT_LAYERS
        #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
        #pragma multi_compile_fragment _ DEBUG_DISPLAY
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_TEXCOORD1
        #define ATTRIBUTES_NEED_TEXCOORD2
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TANGENT_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_VIEWDIRECTION_WS
        #define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        #define VARYINGS_NEED_SHADOW_COORD
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_GBUFFER
        #define _FOG_FRAGMENT 1
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 uv1 : TEXCOORD1;
             float4 uv2 : TEXCOORD2;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
             float4 tangentWS;
             float4 texCoord0;
             float3 viewDirectionWS;
            #if defined(LIGHTMAP_ON)
             float2 staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
             float2 dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
             float3 sh;
            #endif
             float4 fogFactorAndVertexLight;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
             float4 shadowCoord;
            #endif
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 TangentSpaceNormal;
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 WorldSpaceTangent;
             float3 ObjectSpaceBiTangent;
             float3 WorldSpaceBiTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float4 uv0;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float3 interp0 : INTERP0;
             float3 interp1 : INTERP1;
             float4 interp2 : INTERP2;
             float4 interp3 : INTERP3;
             float3 interp4 : INTERP4;
             float2 interp5 : INTERP5;
             float2 interp6 : INTERP6;
             float3 interp7 : INTERP7;
             float4 interp8 : INTERP8;
             float4 interp9 : INTERP9;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.interp0.xyz =  input.positionWS;
            output.interp1.xyz =  input.normalWS;
            output.interp2.xyzw =  input.tangentWS;
            output.interp3.xyzw =  input.texCoord0;
            output.interp4.xyz =  input.viewDirectionWS;
            #if defined(LIGHTMAP_ON)
            output.interp5.xy =  input.staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
            output.interp6.xy =  input.dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.interp7.xyz =  input.sh;
            #endif
            output.interp8.xyzw =  input.fogFactorAndVertexLight;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            output.interp9.xyzw =  input.shadowCoord;
            #endif
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.positionWS = input.interp0.xyz;
            output.normalWS = input.interp1.xyz;
            output.tangentWS = input.interp2.xyzw;
            output.texCoord0 = input.interp3.xyzw;
            output.viewDirectionWS = input.interp4.xyz;
            #if defined(LIGHTMAP_ON)
            output.staticLightmapUV = input.interp5.xy;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
            output.dynamicLightmapUV = input.interp6.xy;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.sh = input.interp7.xyz;
            #endif
            output.fogFactorAndVertexLight = input.interp8.xyzw;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            output.shadowCoord = input.interp9.xyzw;
            #endif
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1_TexelSize;
        float3 _position;
        float _WindShiftStreth;
        float _WindSpeed;
        float _WindStrenth;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        SAMPLER(sampler_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        
        inline float Unity_SimpleNoise_RandomValue_float (float2 uv)
        {
            float angle = dot(uv, float2(12.9898, 78.233));
            #if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN))
                // 'sin()' has bad precision on Mali GPUs for inputs > 10000
                angle = fmod(angle, TWO_PI); // Avoid large inputs to sin()
            #endif
            return frac(sin(angle)*43758.5453);
        }
        
        inline float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t)
        {
            return (1.0-t)*a + (t*b);
        }
        
        
        inline float Unity_SimpleNoise_ValueNoise_float (float2 uv)
        {
            float2 i = floor(uv);
            float2 f = frac(uv);
            f = f * f * (3.0 - 2.0 * f);
        
            uv = abs(frac(uv) - 0.5);
            float2 c0 = i + float2(0.0, 0.0);
            float2 c1 = i + float2(1.0, 0.0);
            float2 c2 = i + float2(0.0, 1.0);
            float2 c3 = i + float2(1.0, 1.0);
            float r0 = Unity_SimpleNoise_RandomValue_float(c0);
            float r1 = Unity_SimpleNoise_RandomValue_float(c1);
            float r2 = Unity_SimpleNoise_RandomValue_float(c2);
            float r3 = Unity_SimpleNoise_RandomValue_float(c3);
        
            float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
            float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
            float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
            return t;
        }
        void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
        {
            float t = 0.0;
        
            float freq = pow(2.0, float(0));
            float amp = pow(0.5, float(3-0));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(1));
            amp = pow(0.5, float(3-1));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(2));
            amp = pow(0.5, float(3-2));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            Out = t;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void TriangleWave_float(float In, out float Out)
        {
            Out = 2.0 * abs( 2 * (In - floor(0.5 + In)) ) - 1.0;
        }
        
        void Unity_Lerp_float(float A, float B, float T, out float Out)
        {
            Out = lerp(A, B, T);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Split_4943b3735a0e4ba5819db29ad339111a_R_1 = IN.WorldSpacePosition[0];
            float _Split_4943b3735a0e4ba5819db29ad339111a_G_2 = IN.WorldSpacePosition[1];
            float _Split_4943b3735a0e4ba5819db29ad339111a_B_3 = IN.WorldSpacePosition[2];
            float _Split_4943b3735a0e4ba5819db29ad339111a_A_4 = 0;
            float _Split_1df4c052b12745db8c3ab81a1115c945_R_1 = IN.WorldSpacePosition[0];
            float _Split_1df4c052b12745db8c3ab81a1115c945_G_2 = IN.WorldSpacePosition[1];
            float _Split_1df4c052b12745db8c3ab81a1115c945_B_3 = IN.WorldSpacePosition[2];
            float _Split_1df4c052b12745db8c3ab81a1115c945_A_4 = 0;
            float4 _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4;
            float3 _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5;
            float2 _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6;
            Unity_Combine_float(_Split_1df4c052b12745db8c3ab81a1115c945_R_1, _Split_1df4c052b12745db8c3ab81a1115c945_G_2, 0, 0, _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4, _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5, _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6);
            float _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2;
            Unity_SimpleNoise_float(_Combine_c8a8a8a174fe417389fcb951c422011a_RG_6, 500, _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2);
            float _Property_f5800ee87a11435ba90f964e3635c567_Out_0 = _WindShiftStreth;
            float _Multiply_44b7a7abd320474289777104102625d7_Out_2;
            Unity_Multiply_float_float(_SimpleNoise_e8723a906e95458096db370034b18abe_Out_2, _Property_f5800ee87a11435ba90f964e3635c567_Out_0, _Multiply_44b7a7abd320474289777104102625d7_Out_2);
            float _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0 = _WindSpeed;
            float _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2);
            float _Add_5236d4953a3147c88bc756d35f145c51_Out_2;
            Unity_Add_float(_Multiply_44b7a7abd320474289777104102625d7_Out_2, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2, _Add_5236d4953a3147c88bc756d35f145c51_Out_2);
            float _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1;
            TriangleWave_float(_Add_5236d4953a3147c88bc756d35f145c51_Out_2, _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1);
            float _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0 = _WindStrenth;
            float _Multiply_642789de8d1c4560848f311ee76485d0_Out_2;
            Unity_Multiply_float_float(_TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1, _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2);
            float _Add_d86e51a612d648418462758f30bf5a46_Out_2;
            Unity_Add_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2, _Add_d86e51a612d648418462758f30bf5a46_Out_2);
            float4 _UV_14cd8fc313be473da647d5d3c819522b_Out_0 = IN.uv0;
            float _Split_524956ebfd064436818e66014c0bb66b_R_1 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[0];
            float _Split_524956ebfd064436818e66014c0bb66b_G_2 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[1];
            float _Split_524956ebfd064436818e66014c0bb66b_B_3 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[2];
            float _Split_524956ebfd064436818e66014c0bb66b_A_4 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[3];
            float _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3;
            Unity_Lerp_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Add_d86e51a612d648418462758f30bf5a46_Out_2, _Split_524956ebfd064436818e66014c0bb66b_G_2, _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3);
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_R_1 = IN.WorldSpacePosition[0];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2 = IN.WorldSpacePosition[1];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3 = IN.WorldSpacePosition[2];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_A_4 = 0;
            float4 _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4;
            float3 _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5;
            float2 _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6;
            Unity_Combine_float(_Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3, _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2, _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3, 0, _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4, _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5, _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6);
            float3 _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1 = TransformWorldToObject(_Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5.xyz);
            description.Position = _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float3 NormalTS;
            float3 Emission;
            float Metallic;
            float Smoothness;
            float Occlusion;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).GetTransformedUV(IN.uv0.xy));
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_R_4 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.r;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_G_5 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.g;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_B_6 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.b;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_A_7 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.a;
            surface.BaseColor = (_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.xyz);
            surface.NormalTS = IN.TangentSpaceNormal;
            surface.Emission = float3(0, 0, 0);
            surface.Metallic = 0;
            surface.Smoothness = 0.5;
            surface.Occlusion = 1;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.WorldSpaceTangent =                          TransformObjectToWorldDir(input.tangentOS.xyz);
            output.ObjectSpaceBiTangent =                       normalize(cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w > 0.0f ? 1.0f : -1.0f) * GetOddNegativeScale());
            output.WorldSpaceBiTangent =                        TransformObjectToWorldDir(output.ObjectSpaceBiTangent);
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.uv0 =                                        input.uv0;
            output.TimeParameters =                             _TimeParameters.xyz;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
            // FragInputs from VFX come from two places: Interpolator or CBuffer.
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
            output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);
        
        
            output.uv0 = input.texCoord0;
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBRGBufferPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }
        
        // Render State
        Cull Off
        ZTest LEqual
        ZWrite On
        ColorMask 0
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 4.5
        #pragma exclude_renderers gles gles3 glcore
        #pragma multi_compile_instancing
        #pragma multi_compile _ DOTS_INSTANCING_ON
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define VARYINGS_NEED_NORMAL_WS
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_SHADOWCASTER
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 WorldSpaceTangent;
             float3 ObjectSpaceBiTangent;
             float3 WorldSpaceBiTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float4 uv0;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float3 interp0 : INTERP0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.interp0.xyz =  input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.normalWS = input.interp0.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1_TexelSize;
        float3 _position;
        float _WindShiftStreth;
        float _WindSpeed;
        float _WindStrenth;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        SAMPLER(sampler_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        
        inline float Unity_SimpleNoise_RandomValue_float (float2 uv)
        {
            float angle = dot(uv, float2(12.9898, 78.233));
            #if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN))
                // 'sin()' has bad precision on Mali GPUs for inputs > 10000
                angle = fmod(angle, TWO_PI); // Avoid large inputs to sin()
            #endif
            return frac(sin(angle)*43758.5453);
        }
        
        inline float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t)
        {
            return (1.0-t)*a + (t*b);
        }
        
        
        inline float Unity_SimpleNoise_ValueNoise_float (float2 uv)
        {
            float2 i = floor(uv);
            float2 f = frac(uv);
            f = f * f * (3.0 - 2.0 * f);
        
            uv = abs(frac(uv) - 0.5);
            float2 c0 = i + float2(0.0, 0.0);
            float2 c1 = i + float2(1.0, 0.0);
            float2 c2 = i + float2(0.0, 1.0);
            float2 c3 = i + float2(1.0, 1.0);
            float r0 = Unity_SimpleNoise_RandomValue_float(c0);
            float r1 = Unity_SimpleNoise_RandomValue_float(c1);
            float r2 = Unity_SimpleNoise_RandomValue_float(c2);
            float r3 = Unity_SimpleNoise_RandomValue_float(c3);
        
            float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
            float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
            float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
            return t;
        }
        void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
        {
            float t = 0.0;
        
            float freq = pow(2.0, float(0));
            float amp = pow(0.5, float(3-0));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(1));
            amp = pow(0.5, float(3-1));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(2));
            amp = pow(0.5, float(3-2));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            Out = t;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void TriangleWave_float(float In, out float Out)
        {
            Out = 2.0 * abs( 2 * (In - floor(0.5 + In)) ) - 1.0;
        }
        
        void Unity_Lerp_float(float A, float B, float T, out float Out)
        {
            Out = lerp(A, B, T);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Split_4943b3735a0e4ba5819db29ad339111a_R_1 = IN.WorldSpacePosition[0];
            float _Split_4943b3735a0e4ba5819db29ad339111a_G_2 = IN.WorldSpacePosition[1];
            float _Split_4943b3735a0e4ba5819db29ad339111a_B_3 = IN.WorldSpacePosition[2];
            float _Split_4943b3735a0e4ba5819db29ad339111a_A_4 = 0;
            float _Split_1df4c052b12745db8c3ab81a1115c945_R_1 = IN.WorldSpacePosition[0];
            float _Split_1df4c052b12745db8c3ab81a1115c945_G_2 = IN.WorldSpacePosition[1];
            float _Split_1df4c052b12745db8c3ab81a1115c945_B_3 = IN.WorldSpacePosition[2];
            float _Split_1df4c052b12745db8c3ab81a1115c945_A_4 = 0;
            float4 _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4;
            float3 _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5;
            float2 _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6;
            Unity_Combine_float(_Split_1df4c052b12745db8c3ab81a1115c945_R_1, _Split_1df4c052b12745db8c3ab81a1115c945_G_2, 0, 0, _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4, _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5, _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6);
            float _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2;
            Unity_SimpleNoise_float(_Combine_c8a8a8a174fe417389fcb951c422011a_RG_6, 500, _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2);
            float _Property_f5800ee87a11435ba90f964e3635c567_Out_0 = _WindShiftStreth;
            float _Multiply_44b7a7abd320474289777104102625d7_Out_2;
            Unity_Multiply_float_float(_SimpleNoise_e8723a906e95458096db370034b18abe_Out_2, _Property_f5800ee87a11435ba90f964e3635c567_Out_0, _Multiply_44b7a7abd320474289777104102625d7_Out_2);
            float _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0 = _WindSpeed;
            float _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2);
            float _Add_5236d4953a3147c88bc756d35f145c51_Out_2;
            Unity_Add_float(_Multiply_44b7a7abd320474289777104102625d7_Out_2, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2, _Add_5236d4953a3147c88bc756d35f145c51_Out_2);
            float _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1;
            TriangleWave_float(_Add_5236d4953a3147c88bc756d35f145c51_Out_2, _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1);
            float _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0 = _WindStrenth;
            float _Multiply_642789de8d1c4560848f311ee76485d0_Out_2;
            Unity_Multiply_float_float(_TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1, _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2);
            float _Add_d86e51a612d648418462758f30bf5a46_Out_2;
            Unity_Add_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2, _Add_d86e51a612d648418462758f30bf5a46_Out_2);
            float4 _UV_14cd8fc313be473da647d5d3c819522b_Out_0 = IN.uv0;
            float _Split_524956ebfd064436818e66014c0bb66b_R_1 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[0];
            float _Split_524956ebfd064436818e66014c0bb66b_G_2 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[1];
            float _Split_524956ebfd064436818e66014c0bb66b_B_3 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[2];
            float _Split_524956ebfd064436818e66014c0bb66b_A_4 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[3];
            float _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3;
            Unity_Lerp_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Add_d86e51a612d648418462758f30bf5a46_Out_2, _Split_524956ebfd064436818e66014c0bb66b_G_2, _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3);
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_R_1 = IN.WorldSpacePosition[0];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2 = IN.WorldSpacePosition[1];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3 = IN.WorldSpacePosition[2];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_A_4 = 0;
            float4 _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4;
            float3 _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5;
            float2 _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6;
            Unity_Combine_float(_Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3, _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2, _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3, 0, _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4, _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5, _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6);
            float3 _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1 = TransformWorldToObject(_Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5.xyz);
            description.Position = _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.WorldSpaceTangent =                          TransformObjectToWorldDir(input.tangentOS.xyz);
            output.ObjectSpaceBiTangent =                       normalize(cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w > 0.0f ? 1.0f : -1.0f) * GetOddNegativeScale());
            output.WorldSpaceBiTangent =                        TransformObjectToWorldDir(output.ObjectSpaceBiTangent);
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.uv0 =                                        input.uv0;
            output.TimeParameters =                             _TimeParameters.xyz;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
            // FragInputs from VFX come from two places: Interpolator or CBuffer.
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShadowCasterPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }
        
        // Render State
        Cull Off
        ZTest LEqual
        ZWrite On
        ColorMask 0
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 4.5
        #pragma exclude_renderers gles gles3 glcore
        #pragma multi_compile_instancing
        #pragma multi_compile _ DOTS_INSTANCING_ON
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHONLY
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 WorldSpaceTangent;
             float3 ObjectSpaceBiTangent;
             float3 WorldSpaceBiTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float4 uv0;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1_TexelSize;
        float3 _position;
        float _WindShiftStreth;
        float _WindSpeed;
        float _WindStrenth;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        SAMPLER(sampler_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        
        inline float Unity_SimpleNoise_RandomValue_float (float2 uv)
        {
            float angle = dot(uv, float2(12.9898, 78.233));
            #if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN))
                // 'sin()' has bad precision on Mali GPUs for inputs > 10000
                angle = fmod(angle, TWO_PI); // Avoid large inputs to sin()
            #endif
            return frac(sin(angle)*43758.5453);
        }
        
        inline float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t)
        {
            return (1.0-t)*a + (t*b);
        }
        
        
        inline float Unity_SimpleNoise_ValueNoise_float (float2 uv)
        {
            float2 i = floor(uv);
            float2 f = frac(uv);
            f = f * f * (3.0 - 2.0 * f);
        
            uv = abs(frac(uv) - 0.5);
            float2 c0 = i + float2(0.0, 0.0);
            float2 c1 = i + float2(1.0, 0.0);
            float2 c2 = i + float2(0.0, 1.0);
            float2 c3 = i + float2(1.0, 1.0);
            float r0 = Unity_SimpleNoise_RandomValue_float(c0);
            float r1 = Unity_SimpleNoise_RandomValue_float(c1);
            float r2 = Unity_SimpleNoise_RandomValue_float(c2);
            float r3 = Unity_SimpleNoise_RandomValue_float(c3);
        
            float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
            float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
            float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
            return t;
        }
        void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
        {
            float t = 0.0;
        
            float freq = pow(2.0, float(0));
            float amp = pow(0.5, float(3-0));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(1));
            amp = pow(0.5, float(3-1));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(2));
            amp = pow(0.5, float(3-2));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            Out = t;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void TriangleWave_float(float In, out float Out)
        {
            Out = 2.0 * abs( 2 * (In - floor(0.5 + In)) ) - 1.0;
        }
        
        void Unity_Lerp_float(float A, float B, float T, out float Out)
        {
            Out = lerp(A, B, T);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Split_4943b3735a0e4ba5819db29ad339111a_R_1 = IN.WorldSpacePosition[0];
            float _Split_4943b3735a0e4ba5819db29ad339111a_G_2 = IN.WorldSpacePosition[1];
            float _Split_4943b3735a0e4ba5819db29ad339111a_B_3 = IN.WorldSpacePosition[2];
            float _Split_4943b3735a0e4ba5819db29ad339111a_A_4 = 0;
            float _Split_1df4c052b12745db8c3ab81a1115c945_R_1 = IN.WorldSpacePosition[0];
            float _Split_1df4c052b12745db8c3ab81a1115c945_G_2 = IN.WorldSpacePosition[1];
            float _Split_1df4c052b12745db8c3ab81a1115c945_B_3 = IN.WorldSpacePosition[2];
            float _Split_1df4c052b12745db8c3ab81a1115c945_A_4 = 0;
            float4 _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4;
            float3 _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5;
            float2 _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6;
            Unity_Combine_float(_Split_1df4c052b12745db8c3ab81a1115c945_R_1, _Split_1df4c052b12745db8c3ab81a1115c945_G_2, 0, 0, _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4, _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5, _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6);
            float _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2;
            Unity_SimpleNoise_float(_Combine_c8a8a8a174fe417389fcb951c422011a_RG_6, 500, _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2);
            float _Property_f5800ee87a11435ba90f964e3635c567_Out_0 = _WindShiftStreth;
            float _Multiply_44b7a7abd320474289777104102625d7_Out_2;
            Unity_Multiply_float_float(_SimpleNoise_e8723a906e95458096db370034b18abe_Out_2, _Property_f5800ee87a11435ba90f964e3635c567_Out_0, _Multiply_44b7a7abd320474289777104102625d7_Out_2);
            float _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0 = _WindSpeed;
            float _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2);
            float _Add_5236d4953a3147c88bc756d35f145c51_Out_2;
            Unity_Add_float(_Multiply_44b7a7abd320474289777104102625d7_Out_2, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2, _Add_5236d4953a3147c88bc756d35f145c51_Out_2);
            float _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1;
            TriangleWave_float(_Add_5236d4953a3147c88bc756d35f145c51_Out_2, _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1);
            float _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0 = _WindStrenth;
            float _Multiply_642789de8d1c4560848f311ee76485d0_Out_2;
            Unity_Multiply_float_float(_TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1, _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2);
            float _Add_d86e51a612d648418462758f30bf5a46_Out_2;
            Unity_Add_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2, _Add_d86e51a612d648418462758f30bf5a46_Out_2);
            float4 _UV_14cd8fc313be473da647d5d3c819522b_Out_0 = IN.uv0;
            float _Split_524956ebfd064436818e66014c0bb66b_R_1 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[0];
            float _Split_524956ebfd064436818e66014c0bb66b_G_2 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[1];
            float _Split_524956ebfd064436818e66014c0bb66b_B_3 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[2];
            float _Split_524956ebfd064436818e66014c0bb66b_A_4 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[3];
            float _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3;
            Unity_Lerp_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Add_d86e51a612d648418462758f30bf5a46_Out_2, _Split_524956ebfd064436818e66014c0bb66b_G_2, _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3);
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_R_1 = IN.WorldSpacePosition[0];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2 = IN.WorldSpacePosition[1];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3 = IN.WorldSpacePosition[2];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_A_4 = 0;
            float4 _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4;
            float3 _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5;
            float2 _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6;
            Unity_Combine_float(_Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3, _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2, _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3, 0, _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4, _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5, _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6);
            float3 _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1 = TransformWorldToObject(_Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5.xyz);
            description.Position = _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.WorldSpaceTangent =                          TransformObjectToWorldDir(input.tangentOS.xyz);
            output.ObjectSpaceBiTangent =                       normalize(cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w > 0.0f ? 1.0f : -1.0f) * GetOddNegativeScale());
            output.WorldSpaceBiTangent =                        TransformObjectToWorldDir(output.ObjectSpaceBiTangent);
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.uv0 =                                        input.uv0;
            output.TimeParameters =                             _TimeParameters.xyz;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
            // FragInputs from VFX come from two places: Interpolator or CBuffer.
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }
        
        // Render State
        Cull Off
        ZTest LEqual
        ZWrite On
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 4.5
        #pragma exclude_renderers gles gles3 glcore
        #pragma multi_compile_instancing
        #pragma multi_compile _ DOTS_INSTANCING_ON
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_TEXCOORD1
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TANGENT_WS
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHNORMALS
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 uv1 : TEXCOORD1;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 normalWS;
             float4 tangentWS;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 TangentSpaceNormal;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 WorldSpaceTangent;
             float3 ObjectSpaceBiTangent;
             float3 WorldSpaceBiTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float4 uv0;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float3 interp0 : INTERP0;
             float4 interp1 : INTERP1;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.interp0.xyz =  input.normalWS;
            output.interp1.xyzw =  input.tangentWS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.normalWS = input.interp0.xyz;
            output.tangentWS = input.interp1.xyzw;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1_TexelSize;
        float3 _position;
        float _WindShiftStreth;
        float _WindSpeed;
        float _WindStrenth;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        SAMPLER(sampler_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        
        inline float Unity_SimpleNoise_RandomValue_float (float2 uv)
        {
            float angle = dot(uv, float2(12.9898, 78.233));
            #if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN))
                // 'sin()' has bad precision on Mali GPUs for inputs > 10000
                angle = fmod(angle, TWO_PI); // Avoid large inputs to sin()
            #endif
            return frac(sin(angle)*43758.5453);
        }
        
        inline float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t)
        {
            return (1.0-t)*a + (t*b);
        }
        
        
        inline float Unity_SimpleNoise_ValueNoise_float (float2 uv)
        {
            float2 i = floor(uv);
            float2 f = frac(uv);
            f = f * f * (3.0 - 2.0 * f);
        
            uv = abs(frac(uv) - 0.5);
            float2 c0 = i + float2(0.0, 0.0);
            float2 c1 = i + float2(1.0, 0.0);
            float2 c2 = i + float2(0.0, 1.0);
            float2 c3 = i + float2(1.0, 1.0);
            float r0 = Unity_SimpleNoise_RandomValue_float(c0);
            float r1 = Unity_SimpleNoise_RandomValue_float(c1);
            float r2 = Unity_SimpleNoise_RandomValue_float(c2);
            float r3 = Unity_SimpleNoise_RandomValue_float(c3);
        
            float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
            float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
            float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
            return t;
        }
        void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
        {
            float t = 0.0;
        
            float freq = pow(2.0, float(0));
            float amp = pow(0.5, float(3-0));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(1));
            amp = pow(0.5, float(3-1));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(2));
            amp = pow(0.5, float(3-2));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            Out = t;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void TriangleWave_float(float In, out float Out)
        {
            Out = 2.0 * abs( 2 * (In - floor(0.5 + In)) ) - 1.0;
        }
        
        void Unity_Lerp_float(float A, float B, float T, out float Out)
        {
            Out = lerp(A, B, T);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Split_4943b3735a0e4ba5819db29ad339111a_R_1 = IN.WorldSpacePosition[0];
            float _Split_4943b3735a0e4ba5819db29ad339111a_G_2 = IN.WorldSpacePosition[1];
            float _Split_4943b3735a0e4ba5819db29ad339111a_B_3 = IN.WorldSpacePosition[2];
            float _Split_4943b3735a0e4ba5819db29ad339111a_A_4 = 0;
            float _Split_1df4c052b12745db8c3ab81a1115c945_R_1 = IN.WorldSpacePosition[0];
            float _Split_1df4c052b12745db8c3ab81a1115c945_G_2 = IN.WorldSpacePosition[1];
            float _Split_1df4c052b12745db8c3ab81a1115c945_B_3 = IN.WorldSpacePosition[2];
            float _Split_1df4c052b12745db8c3ab81a1115c945_A_4 = 0;
            float4 _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4;
            float3 _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5;
            float2 _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6;
            Unity_Combine_float(_Split_1df4c052b12745db8c3ab81a1115c945_R_1, _Split_1df4c052b12745db8c3ab81a1115c945_G_2, 0, 0, _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4, _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5, _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6);
            float _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2;
            Unity_SimpleNoise_float(_Combine_c8a8a8a174fe417389fcb951c422011a_RG_6, 500, _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2);
            float _Property_f5800ee87a11435ba90f964e3635c567_Out_0 = _WindShiftStreth;
            float _Multiply_44b7a7abd320474289777104102625d7_Out_2;
            Unity_Multiply_float_float(_SimpleNoise_e8723a906e95458096db370034b18abe_Out_2, _Property_f5800ee87a11435ba90f964e3635c567_Out_0, _Multiply_44b7a7abd320474289777104102625d7_Out_2);
            float _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0 = _WindSpeed;
            float _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2);
            float _Add_5236d4953a3147c88bc756d35f145c51_Out_2;
            Unity_Add_float(_Multiply_44b7a7abd320474289777104102625d7_Out_2, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2, _Add_5236d4953a3147c88bc756d35f145c51_Out_2);
            float _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1;
            TriangleWave_float(_Add_5236d4953a3147c88bc756d35f145c51_Out_2, _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1);
            float _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0 = _WindStrenth;
            float _Multiply_642789de8d1c4560848f311ee76485d0_Out_2;
            Unity_Multiply_float_float(_TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1, _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2);
            float _Add_d86e51a612d648418462758f30bf5a46_Out_2;
            Unity_Add_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2, _Add_d86e51a612d648418462758f30bf5a46_Out_2);
            float4 _UV_14cd8fc313be473da647d5d3c819522b_Out_0 = IN.uv0;
            float _Split_524956ebfd064436818e66014c0bb66b_R_1 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[0];
            float _Split_524956ebfd064436818e66014c0bb66b_G_2 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[1];
            float _Split_524956ebfd064436818e66014c0bb66b_B_3 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[2];
            float _Split_524956ebfd064436818e66014c0bb66b_A_4 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[3];
            float _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3;
            Unity_Lerp_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Add_d86e51a612d648418462758f30bf5a46_Out_2, _Split_524956ebfd064436818e66014c0bb66b_G_2, _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3);
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_R_1 = IN.WorldSpacePosition[0];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2 = IN.WorldSpacePosition[1];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3 = IN.WorldSpacePosition[2];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_A_4 = 0;
            float4 _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4;
            float3 _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5;
            float2 _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6;
            Unity_Combine_float(_Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3, _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2, _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3, 0, _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4, _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5, _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6);
            float3 _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1 = TransformWorldToObject(_Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5.xyz);
            description.Position = _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 NormalTS;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            surface.NormalTS = IN.TangentSpaceNormal;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.WorldSpaceTangent =                          TransformObjectToWorldDir(input.tangentOS.xyz);
            output.ObjectSpaceBiTangent =                       normalize(cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w > 0.0f ? 1.0f : -1.0f) * GetOddNegativeScale());
            output.WorldSpaceBiTangent =                        TransformObjectToWorldDir(output.ObjectSpaceBiTangent);
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.uv0 =                                        input.uv0;
            output.TimeParameters =                             _TimeParameters.xyz;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
            // FragInputs from VFX come from two places: Interpolator or CBuffer.
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
            output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);
        
        
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/DepthNormalsOnlyPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }
        
        // Render State
        Cull Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 4.5
        #pragma exclude_renderers gles gles3 glcore
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        #pragma shader_feature _ EDITOR_VISUALIZATION
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_TEXCOORD1
        #define ATTRIBUTES_NEED_TEXCOORD2
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_TEXCOORD1
        #define VARYINGS_NEED_TEXCOORD2
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_META
        #define _FOG_FRAGMENT 1
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 uv1 : TEXCOORD1;
             float4 uv2 : TEXCOORD2;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0;
             float4 texCoord1;
             float4 texCoord2;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 WorldSpaceTangent;
             float3 ObjectSpaceBiTangent;
             float3 WorldSpaceBiTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float4 uv0;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float4 interp0 : INTERP0;
             float4 interp1 : INTERP1;
             float4 interp2 : INTERP2;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.interp0.xyzw =  input.texCoord0;
            output.interp1.xyzw =  input.texCoord1;
            output.interp2.xyzw =  input.texCoord2;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.texCoord0 = input.interp0.xyzw;
            output.texCoord1 = input.interp1.xyzw;
            output.texCoord2 = input.interp2.xyzw;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1_TexelSize;
        float3 _position;
        float _WindShiftStreth;
        float _WindSpeed;
        float _WindStrenth;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        SAMPLER(sampler_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        
        inline float Unity_SimpleNoise_RandomValue_float (float2 uv)
        {
            float angle = dot(uv, float2(12.9898, 78.233));
            #if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN))
                // 'sin()' has bad precision on Mali GPUs for inputs > 10000
                angle = fmod(angle, TWO_PI); // Avoid large inputs to sin()
            #endif
            return frac(sin(angle)*43758.5453);
        }
        
        inline float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t)
        {
            return (1.0-t)*a + (t*b);
        }
        
        
        inline float Unity_SimpleNoise_ValueNoise_float (float2 uv)
        {
            float2 i = floor(uv);
            float2 f = frac(uv);
            f = f * f * (3.0 - 2.0 * f);
        
            uv = abs(frac(uv) - 0.5);
            float2 c0 = i + float2(0.0, 0.0);
            float2 c1 = i + float2(1.0, 0.0);
            float2 c2 = i + float2(0.0, 1.0);
            float2 c3 = i + float2(1.0, 1.0);
            float r0 = Unity_SimpleNoise_RandomValue_float(c0);
            float r1 = Unity_SimpleNoise_RandomValue_float(c1);
            float r2 = Unity_SimpleNoise_RandomValue_float(c2);
            float r3 = Unity_SimpleNoise_RandomValue_float(c3);
        
            float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
            float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
            float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
            return t;
        }
        void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
        {
            float t = 0.0;
        
            float freq = pow(2.0, float(0));
            float amp = pow(0.5, float(3-0));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(1));
            amp = pow(0.5, float(3-1));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(2));
            amp = pow(0.5, float(3-2));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            Out = t;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void TriangleWave_float(float In, out float Out)
        {
            Out = 2.0 * abs( 2 * (In - floor(0.5 + In)) ) - 1.0;
        }
        
        void Unity_Lerp_float(float A, float B, float T, out float Out)
        {
            Out = lerp(A, B, T);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Split_4943b3735a0e4ba5819db29ad339111a_R_1 = IN.WorldSpacePosition[0];
            float _Split_4943b3735a0e4ba5819db29ad339111a_G_2 = IN.WorldSpacePosition[1];
            float _Split_4943b3735a0e4ba5819db29ad339111a_B_3 = IN.WorldSpacePosition[2];
            float _Split_4943b3735a0e4ba5819db29ad339111a_A_4 = 0;
            float _Split_1df4c052b12745db8c3ab81a1115c945_R_1 = IN.WorldSpacePosition[0];
            float _Split_1df4c052b12745db8c3ab81a1115c945_G_2 = IN.WorldSpacePosition[1];
            float _Split_1df4c052b12745db8c3ab81a1115c945_B_3 = IN.WorldSpacePosition[2];
            float _Split_1df4c052b12745db8c3ab81a1115c945_A_4 = 0;
            float4 _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4;
            float3 _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5;
            float2 _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6;
            Unity_Combine_float(_Split_1df4c052b12745db8c3ab81a1115c945_R_1, _Split_1df4c052b12745db8c3ab81a1115c945_G_2, 0, 0, _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4, _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5, _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6);
            float _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2;
            Unity_SimpleNoise_float(_Combine_c8a8a8a174fe417389fcb951c422011a_RG_6, 500, _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2);
            float _Property_f5800ee87a11435ba90f964e3635c567_Out_0 = _WindShiftStreth;
            float _Multiply_44b7a7abd320474289777104102625d7_Out_2;
            Unity_Multiply_float_float(_SimpleNoise_e8723a906e95458096db370034b18abe_Out_2, _Property_f5800ee87a11435ba90f964e3635c567_Out_0, _Multiply_44b7a7abd320474289777104102625d7_Out_2);
            float _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0 = _WindSpeed;
            float _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2);
            float _Add_5236d4953a3147c88bc756d35f145c51_Out_2;
            Unity_Add_float(_Multiply_44b7a7abd320474289777104102625d7_Out_2, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2, _Add_5236d4953a3147c88bc756d35f145c51_Out_2);
            float _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1;
            TriangleWave_float(_Add_5236d4953a3147c88bc756d35f145c51_Out_2, _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1);
            float _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0 = _WindStrenth;
            float _Multiply_642789de8d1c4560848f311ee76485d0_Out_2;
            Unity_Multiply_float_float(_TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1, _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2);
            float _Add_d86e51a612d648418462758f30bf5a46_Out_2;
            Unity_Add_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2, _Add_d86e51a612d648418462758f30bf5a46_Out_2);
            float4 _UV_14cd8fc313be473da647d5d3c819522b_Out_0 = IN.uv0;
            float _Split_524956ebfd064436818e66014c0bb66b_R_1 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[0];
            float _Split_524956ebfd064436818e66014c0bb66b_G_2 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[1];
            float _Split_524956ebfd064436818e66014c0bb66b_B_3 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[2];
            float _Split_524956ebfd064436818e66014c0bb66b_A_4 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[3];
            float _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3;
            Unity_Lerp_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Add_d86e51a612d648418462758f30bf5a46_Out_2, _Split_524956ebfd064436818e66014c0bb66b_G_2, _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3);
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_R_1 = IN.WorldSpacePosition[0];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2 = IN.WorldSpacePosition[1];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3 = IN.WorldSpacePosition[2];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_A_4 = 0;
            float4 _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4;
            float3 _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5;
            float2 _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6;
            Unity_Combine_float(_Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3, _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2, _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3, 0, _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4, _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5, _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6);
            float3 _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1 = TransformWorldToObject(_Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5.xyz);
            description.Position = _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float3 Emission;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).GetTransformedUV(IN.uv0.xy));
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_R_4 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.r;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_G_5 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.g;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_B_6 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.b;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_A_7 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.a;
            surface.BaseColor = (_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.xyz);
            surface.Emission = float3(0, 0, 0);
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.WorldSpaceTangent =                          TransformObjectToWorldDir(input.tangentOS.xyz);
            output.ObjectSpaceBiTangent =                       normalize(cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w > 0.0f ? 1.0f : -1.0f) * GetOddNegativeScale());
            output.WorldSpaceBiTangent =                        TransformObjectToWorldDir(output.ObjectSpaceBiTangent);
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.uv0 =                                        input.uv0;
            output.TimeParameters =                             _TimeParameters.xyz;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
            // FragInputs from VFX come from two places: Interpolator or CBuffer.
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
            output.uv0 = input.texCoord0;
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/LightingMetaPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "SceneSelectionPass"
            Tags
            {
                "LightMode" = "SceneSelectionPass"
            }
        
        // Render State
        Cull Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 4.5
        #pragma exclude_renderers gles gles3 glcore
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHONLY
        #define SCENESELECTIONPASS 1
        #define ALPHA_CLIP_THRESHOLD 1
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 WorldSpaceTangent;
             float3 ObjectSpaceBiTangent;
             float3 WorldSpaceBiTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float4 uv0;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1_TexelSize;
        float3 _position;
        float _WindShiftStreth;
        float _WindSpeed;
        float _WindStrenth;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        SAMPLER(sampler_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        
        inline float Unity_SimpleNoise_RandomValue_float (float2 uv)
        {
            float angle = dot(uv, float2(12.9898, 78.233));
            #if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN))
                // 'sin()' has bad precision on Mali GPUs for inputs > 10000
                angle = fmod(angle, TWO_PI); // Avoid large inputs to sin()
            #endif
            return frac(sin(angle)*43758.5453);
        }
        
        inline float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t)
        {
            return (1.0-t)*a + (t*b);
        }
        
        
        inline float Unity_SimpleNoise_ValueNoise_float (float2 uv)
        {
            float2 i = floor(uv);
            float2 f = frac(uv);
            f = f * f * (3.0 - 2.0 * f);
        
            uv = abs(frac(uv) - 0.5);
            float2 c0 = i + float2(0.0, 0.0);
            float2 c1 = i + float2(1.0, 0.0);
            float2 c2 = i + float2(0.0, 1.0);
            float2 c3 = i + float2(1.0, 1.0);
            float r0 = Unity_SimpleNoise_RandomValue_float(c0);
            float r1 = Unity_SimpleNoise_RandomValue_float(c1);
            float r2 = Unity_SimpleNoise_RandomValue_float(c2);
            float r3 = Unity_SimpleNoise_RandomValue_float(c3);
        
            float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
            float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
            float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
            return t;
        }
        void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
        {
            float t = 0.0;
        
            float freq = pow(2.0, float(0));
            float amp = pow(0.5, float(3-0));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(1));
            amp = pow(0.5, float(3-1));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(2));
            amp = pow(0.5, float(3-2));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            Out = t;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void TriangleWave_float(float In, out float Out)
        {
            Out = 2.0 * abs( 2 * (In - floor(0.5 + In)) ) - 1.0;
        }
        
        void Unity_Lerp_float(float A, float B, float T, out float Out)
        {
            Out = lerp(A, B, T);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Split_4943b3735a0e4ba5819db29ad339111a_R_1 = IN.WorldSpacePosition[0];
            float _Split_4943b3735a0e4ba5819db29ad339111a_G_2 = IN.WorldSpacePosition[1];
            float _Split_4943b3735a0e4ba5819db29ad339111a_B_3 = IN.WorldSpacePosition[2];
            float _Split_4943b3735a0e4ba5819db29ad339111a_A_4 = 0;
            float _Split_1df4c052b12745db8c3ab81a1115c945_R_1 = IN.WorldSpacePosition[0];
            float _Split_1df4c052b12745db8c3ab81a1115c945_G_2 = IN.WorldSpacePosition[1];
            float _Split_1df4c052b12745db8c3ab81a1115c945_B_3 = IN.WorldSpacePosition[2];
            float _Split_1df4c052b12745db8c3ab81a1115c945_A_4 = 0;
            float4 _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4;
            float3 _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5;
            float2 _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6;
            Unity_Combine_float(_Split_1df4c052b12745db8c3ab81a1115c945_R_1, _Split_1df4c052b12745db8c3ab81a1115c945_G_2, 0, 0, _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4, _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5, _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6);
            float _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2;
            Unity_SimpleNoise_float(_Combine_c8a8a8a174fe417389fcb951c422011a_RG_6, 500, _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2);
            float _Property_f5800ee87a11435ba90f964e3635c567_Out_0 = _WindShiftStreth;
            float _Multiply_44b7a7abd320474289777104102625d7_Out_2;
            Unity_Multiply_float_float(_SimpleNoise_e8723a906e95458096db370034b18abe_Out_2, _Property_f5800ee87a11435ba90f964e3635c567_Out_0, _Multiply_44b7a7abd320474289777104102625d7_Out_2);
            float _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0 = _WindSpeed;
            float _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2);
            float _Add_5236d4953a3147c88bc756d35f145c51_Out_2;
            Unity_Add_float(_Multiply_44b7a7abd320474289777104102625d7_Out_2, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2, _Add_5236d4953a3147c88bc756d35f145c51_Out_2);
            float _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1;
            TriangleWave_float(_Add_5236d4953a3147c88bc756d35f145c51_Out_2, _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1);
            float _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0 = _WindStrenth;
            float _Multiply_642789de8d1c4560848f311ee76485d0_Out_2;
            Unity_Multiply_float_float(_TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1, _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2);
            float _Add_d86e51a612d648418462758f30bf5a46_Out_2;
            Unity_Add_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2, _Add_d86e51a612d648418462758f30bf5a46_Out_2);
            float4 _UV_14cd8fc313be473da647d5d3c819522b_Out_0 = IN.uv0;
            float _Split_524956ebfd064436818e66014c0bb66b_R_1 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[0];
            float _Split_524956ebfd064436818e66014c0bb66b_G_2 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[1];
            float _Split_524956ebfd064436818e66014c0bb66b_B_3 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[2];
            float _Split_524956ebfd064436818e66014c0bb66b_A_4 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[3];
            float _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3;
            Unity_Lerp_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Add_d86e51a612d648418462758f30bf5a46_Out_2, _Split_524956ebfd064436818e66014c0bb66b_G_2, _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3);
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_R_1 = IN.WorldSpacePosition[0];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2 = IN.WorldSpacePosition[1];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3 = IN.WorldSpacePosition[2];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_A_4 = 0;
            float4 _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4;
            float3 _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5;
            float2 _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6;
            Unity_Combine_float(_Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3, _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2, _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3, 0, _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4, _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5, _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6);
            float3 _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1 = TransformWorldToObject(_Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5.xyz);
            description.Position = _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.WorldSpaceTangent =                          TransformObjectToWorldDir(input.tangentOS.xyz);
            output.ObjectSpaceBiTangent =                       normalize(cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w > 0.0f ? 1.0f : -1.0f) * GetOddNegativeScale());
            output.WorldSpaceBiTangent =                        TransformObjectToWorldDir(output.ObjectSpaceBiTangent);
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.uv0 =                                        input.uv0;
            output.TimeParameters =                             _TimeParameters.xyz;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
            // FragInputs from VFX come from two places: Interpolator or CBuffer.
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "ScenePickingPass"
            Tags
            {
                "LightMode" = "Picking"
            }
        
        // Render State
        Cull Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 4.5
        #pragma exclude_renderers gles gles3 glcore
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHONLY
        #define SCENEPICKINGPASS 1
        #define ALPHA_CLIP_THRESHOLD 1
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 WorldSpaceTangent;
             float3 ObjectSpaceBiTangent;
             float3 WorldSpaceBiTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float4 uv0;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1_TexelSize;
        float3 _position;
        float _WindShiftStreth;
        float _WindSpeed;
        float _WindStrenth;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        SAMPLER(sampler_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        
        inline float Unity_SimpleNoise_RandomValue_float (float2 uv)
        {
            float angle = dot(uv, float2(12.9898, 78.233));
            #if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN))
                // 'sin()' has bad precision on Mali GPUs for inputs > 10000
                angle = fmod(angle, TWO_PI); // Avoid large inputs to sin()
            #endif
            return frac(sin(angle)*43758.5453);
        }
        
        inline float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t)
        {
            return (1.0-t)*a + (t*b);
        }
        
        
        inline float Unity_SimpleNoise_ValueNoise_float (float2 uv)
        {
            float2 i = floor(uv);
            float2 f = frac(uv);
            f = f * f * (3.0 - 2.0 * f);
        
            uv = abs(frac(uv) - 0.5);
            float2 c0 = i + float2(0.0, 0.0);
            float2 c1 = i + float2(1.0, 0.0);
            float2 c2 = i + float2(0.0, 1.0);
            float2 c3 = i + float2(1.0, 1.0);
            float r0 = Unity_SimpleNoise_RandomValue_float(c0);
            float r1 = Unity_SimpleNoise_RandomValue_float(c1);
            float r2 = Unity_SimpleNoise_RandomValue_float(c2);
            float r3 = Unity_SimpleNoise_RandomValue_float(c3);
        
            float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
            float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
            float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
            return t;
        }
        void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
        {
            float t = 0.0;
        
            float freq = pow(2.0, float(0));
            float amp = pow(0.5, float(3-0));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(1));
            amp = pow(0.5, float(3-1));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(2));
            amp = pow(0.5, float(3-2));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            Out = t;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void TriangleWave_float(float In, out float Out)
        {
            Out = 2.0 * abs( 2 * (In - floor(0.5 + In)) ) - 1.0;
        }
        
        void Unity_Lerp_float(float A, float B, float T, out float Out)
        {
            Out = lerp(A, B, T);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Split_4943b3735a0e4ba5819db29ad339111a_R_1 = IN.WorldSpacePosition[0];
            float _Split_4943b3735a0e4ba5819db29ad339111a_G_2 = IN.WorldSpacePosition[1];
            float _Split_4943b3735a0e4ba5819db29ad339111a_B_3 = IN.WorldSpacePosition[2];
            float _Split_4943b3735a0e4ba5819db29ad339111a_A_4 = 0;
            float _Split_1df4c052b12745db8c3ab81a1115c945_R_1 = IN.WorldSpacePosition[0];
            float _Split_1df4c052b12745db8c3ab81a1115c945_G_2 = IN.WorldSpacePosition[1];
            float _Split_1df4c052b12745db8c3ab81a1115c945_B_3 = IN.WorldSpacePosition[2];
            float _Split_1df4c052b12745db8c3ab81a1115c945_A_4 = 0;
            float4 _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4;
            float3 _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5;
            float2 _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6;
            Unity_Combine_float(_Split_1df4c052b12745db8c3ab81a1115c945_R_1, _Split_1df4c052b12745db8c3ab81a1115c945_G_2, 0, 0, _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4, _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5, _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6);
            float _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2;
            Unity_SimpleNoise_float(_Combine_c8a8a8a174fe417389fcb951c422011a_RG_6, 500, _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2);
            float _Property_f5800ee87a11435ba90f964e3635c567_Out_0 = _WindShiftStreth;
            float _Multiply_44b7a7abd320474289777104102625d7_Out_2;
            Unity_Multiply_float_float(_SimpleNoise_e8723a906e95458096db370034b18abe_Out_2, _Property_f5800ee87a11435ba90f964e3635c567_Out_0, _Multiply_44b7a7abd320474289777104102625d7_Out_2);
            float _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0 = _WindSpeed;
            float _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2);
            float _Add_5236d4953a3147c88bc756d35f145c51_Out_2;
            Unity_Add_float(_Multiply_44b7a7abd320474289777104102625d7_Out_2, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2, _Add_5236d4953a3147c88bc756d35f145c51_Out_2);
            float _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1;
            TriangleWave_float(_Add_5236d4953a3147c88bc756d35f145c51_Out_2, _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1);
            float _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0 = _WindStrenth;
            float _Multiply_642789de8d1c4560848f311ee76485d0_Out_2;
            Unity_Multiply_float_float(_TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1, _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2);
            float _Add_d86e51a612d648418462758f30bf5a46_Out_2;
            Unity_Add_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2, _Add_d86e51a612d648418462758f30bf5a46_Out_2);
            float4 _UV_14cd8fc313be473da647d5d3c819522b_Out_0 = IN.uv0;
            float _Split_524956ebfd064436818e66014c0bb66b_R_1 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[0];
            float _Split_524956ebfd064436818e66014c0bb66b_G_2 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[1];
            float _Split_524956ebfd064436818e66014c0bb66b_B_3 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[2];
            float _Split_524956ebfd064436818e66014c0bb66b_A_4 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[3];
            float _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3;
            Unity_Lerp_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Add_d86e51a612d648418462758f30bf5a46_Out_2, _Split_524956ebfd064436818e66014c0bb66b_G_2, _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3);
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_R_1 = IN.WorldSpacePosition[0];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2 = IN.WorldSpacePosition[1];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3 = IN.WorldSpacePosition[2];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_A_4 = 0;
            float4 _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4;
            float3 _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5;
            float2 _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6;
            Unity_Combine_float(_Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3, _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2, _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3, 0, _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4, _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5, _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6);
            float3 _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1 = TransformWorldToObject(_Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5.xyz);
            description.Position = _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.WorldSpaceTangent =                          TransformObjectToWorldDir(input.tangentOS.xyz);
            output.ObjectSpaceBiTangent =                       normalize(cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w > 0.0f ? 1.0f : -1.0f) * GetOddNegativeScale());
            output.WorldSpaceBiTangent =                        TransformObjectToWorldDir(output.ObjectSpaceBiTangent);
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.uv0 =                                        input.uv0;
            output.TimeParameters =                             _TimeParameters.xyz;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
            // FragInputs from VFX come from two places: Interpolator or CBuffer.
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            // Name: <None>
            Tags
            {
                "LightMode" = "Universal2D"
            }
        
        // Render State
        Cull Off
        Blend One Zero
        ZTest LEqual
        ZWrite On
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 4.5
        #pragma exclude_renderers gles gles3 glcore
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define VARYINGS_NEED_TEXCOORD0
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_2D
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 WorldSpaceTangent;
             float3 ObjectSpaceBiTangent;
             float3 WorldSpaceBiTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float4 uv0;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float4 interp0 : INTERP0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.interp0.xyzw =  input.texCoord0;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.texCoord0 = input.interp0.xyzw;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1_TexelSize;
        float3 _position;
        float _WindShiftStreth;
        float _WindSpeed;
        float _WindStrenth;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        SAMPLER(sampler_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        
        inline float Unity_SimpleNoise_RandomValue_float (float2 uv)
        {
            float angle = dot(uv, float2(12.9898, 78.233));
            #if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN))
                // 'sin()' has bad precision on Mali GPUs for inputs > 10000
                angle = fmod(angle, TWO_PI); // Avoid large inputs to sin()
            #endif
            return frac(sin(angle)*43758.5453);
        }
        
        inline float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t)
        {
            return (1.0-t)*a + (t*b);
        }
        
        
        inline float Unity_SimpleNoise_ValueNoise_float (float2 uv)
        {
            float2 i = floor(uv);
            float2 f = frac(uv);
            f = f * f * (3.0 - 2.0 * f);
        
            uv = abs(frac(uv) - 0.5);
            float2 c0 = i + float2(0.0, 0.0);
            float2 c1 = i + float2(1.0, 0.0);
            float2 c2 = i + float2(0.0, 1.0);
            float2 c3 = i + float2(1.0, 1.0);
            float r0 = Unity_SimpleNoise_RandomValue_float(c0);
            float r1 = Unity_SimpleNoise_RandomValue_float(c1);
            float r2 = Unity_SimpleNoise_RandomValue_float(c2);
            float r3 = Unity_SimpleNoise_RandomValue_float(c3);
        
            float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
            float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
            float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
            return t;
        }
        void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
        {
            float t = 0.0;
        
            float freq = pow(2.0, float(0));
            float amp = pow(0.5, float(3-0));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(1));
            amp = pow(0.5, float(3-1));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(2));
            amp = pow(0.5, float(3-2));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            Out = t;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void TriangleWave_float(float In, out float Out)
        {
            Out = 2.0 * abs( 2 * (In - floor(0.5 + In)) ) - 1.0;
        }
        
        void Unity_Lerp_float(float A, float B, float T, out float Out)
        {
            Out = lerp(A, B, T);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Split_4943b3735a0e4ba5819db29ad339111a_R_1 = IN.WorldSpacePosition[0];
            float _Split_4943b3735a0e4ba5819db29ad339111a_G_2 = IN.WorldSpacePosition[1];
            float _Split_4943b3735a0e4ba5819db29ad339111a_B_3 = IN.WorldSpacePosition[2];
            float _Split_4943b3735a0e4ba5819db29ad339111a_A_4 = 0;
            float _Split_1df4c052b12745db8c3ab81a1115c945_R_1 = IN.WorldSpacePosition[0];
            float _Split_1df4c052b12745db8c3ab81a1115c945_G_2 = IN.WorldSpacePosition[1];
            float _Split_1df4c052b12745db8c3ab81a1115c945_B_3 = IN.WorldSpacePosition[2];
            float _Split_1df4c052b12745db8c3ab81a1115c945_A_4 = 0;
            float4 _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4;
            float3 _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5;
            float2 _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6;
            Unity_Combine_float(_Split_1df4c052b12745db8c3ab81a1115c945_R_1, _Split_1df4c052b12745db8c3ab81a1115c945_G_2, 0, 0, _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4, _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5, _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6);
            float _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2;
            Unity_SimpleNoise_float(_Combine_c8a8a8a174fe417389fcb951c422011a_RG_6, 500, _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2);
            float _Property_f5800ee87a11435ba90f964e3635c567_Out_0 = _WindShiftStreth;
            float _Multiply_44b7a7abd320474289777104102625d7_Out_2;
            Unity_Multiply_float_float(_SimpleNoise_e8723a906e95458096db370034b18abe_Out_2, _Property_f5800ee87a11435ba90f964e3635c567_Out_0, _Multiply_44b7a7abd320474289777104102625d7_Out_2);
            float _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0 = _WindSpeed;
            float _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2);
            float _Add_5236d4953a3147c88bc756d35f145c51_Out_2;
            Unity_Add_float(_Multiply_44b7a7abd320474289777104102625d7_Out_2, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2, _Add_5236d4953a3147c88bc756d35f145c51_Out_2);
            float _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1;
            TriangleWave_float(_Add_5236d4953a3147c88bc756d35f145c51_Out_2, _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1);
            float _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0 = _WindStrenth;
            float _Multiply_642789de8d1c4560848f311ee76485d0_Out_2;
            Unity_Multiply_float_float(_TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1, _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2);
            float _Add_d86e51a612d648418462758f30bf5a46_Out_2;
            Unity_Add_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2, _Add_d86e51a612d648418462758f30bf5a46_Out_2);
            float4 _UV_14cd8fc313be473da647d5d3c819522b_Out_0 = IN.uv0;
            float _Split_524956ebfd064436818e66014c0bb66b_R_1 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[0];
            float _Split_524956ebfd064436818e66014c0bb66b_G_2 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[1];
            float _Split_524956ebfd064436818e66014c0bb66b_B_3 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[2];
            float _Split_524956ebfd064436818e66014c0bb66b_A_4 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[3];
            float _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3;
            Unity_Lerp_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Add_d86e51a612d648418462758f30bf5a46_Out_2, _Split_524956ebfd064436818e66014c0bb66b_G_2, _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3);
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_R_1 = IN.WorldSpacePosition[0];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2 = IN.WorldSpacePosition[1];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3 = IN.WorldSpacePosition[2];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_A_4 = 0;
            float4 _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4;
            float3 _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5;
            float2 _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6;
            Unity_Combine_float(_Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3, _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2, _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3, 0, _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4, _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5, _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6);
            float3 _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1 = TransformWorldToObject(_Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5.xyz);
            description.Position = _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).GetTransformedUV(IN.uv0.xy));
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_R_4 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.r;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_G_5 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.g;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_B_6 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.b;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_A_7 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.a;
            surface.BaseColor = (_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.xyz);
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.WorldSpaceTangent =                          TransformObjectToWorldDir(input.tangentOS.xyz);
            output.ObjectSpaceBiTangent =                       normalize(cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w > 0.0f ? 1.0f : -1.0f) * GetOddNegativeScale());
            output.WorldSpaceBiTangent =                        TransformObjectToWorldDir(output.ObjectSpaceBiTangent);
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.uv0 =                                        input.uv0;
            output.TimeParameters =                             _TimeParameters.xyz;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
            // FragInputs from VFX come from two places: Interpolator or CBuffer.
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
            output.uv0 = input.texCoord0;
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBR2DPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "UniversalMaterialType" = "Lit"
            "Queue"="Geometry"
            "ShaderGraphShader"="true"
            "ShaderGraphTargetId"="UniversalLitSubTarget"
        }
        Pass
        {
            Name "Universal Forward"
            Tags
            {
                "LightMode" = "UniversalForward"
            }
        
        // Render State
        Cull Off
        Blend One Zero
        ZTest LEqual
        ZWrite On
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma only_renderers gles gles3 glcore d3d11
        #pragma multi_compile_instancing
        #pragma multi_compile_fog
        #pragma instancing_options renderinglayer
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
        #pragma multi_compile _ LIGHTMAP_ON
        #pragma multi_compile _ DYNAMICLIGHTMAP_ON
        #pragma multi_compile _ DIRLIGHTMAP_COMBINED
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
        #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
        #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
        #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
        #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
        #pragma multi_compile_fragment _ _SHADOWS_SOFT
        #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
        #pragma multi_compile _ SHADOWS_SHADOWMASK
        #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
        #pragma multi_compile_fragment _ _LIGHT_LAYERS
        #pragma multi_compile_fragment _ DEBUG_DISPLAY
        #pragma multi_compile_fragment _ _LIGHT_COOKIES
        #pragma multi_compile _ _CLUSTERED_RENDERING
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_TEXCOORD1
        #define ATTRIBUTES_NEED_TEXCOORD2
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TANGENT_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_VIEWDIRECTION_WS
        #define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        #define VARYINGS_NEED_SHADOW_COORD
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_FORWARD
        #define _FOG_FRAGMENT 1
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 uv1 : TEXCOORD1;
             float4 uv2 : TEXCOORD2;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
             float4 tangentWS;
             float4 texCoord0;
             float3 viewDirectionWS;
            #if defined(LIGHTMAP_ON)
             float2 staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
             float2 dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
             float3 sh;
            #endif
             float4 fogFactorAndVertexLight;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
             float4 shadowCoord;
            #endif
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 TangentSpaceNormal;
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 WorldSpaceTangent;
             float3 ObjectSpaceBiTangent;
             float3 WorldSpaceBiTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float4 uv0;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float3 interp0 : INTERP0;
             float3 interp1 : INTERP1;
             float4 interp2 : INTERP2;
             float4 interp3 : INTERP3;
             float3 interp4 : INTERP4;
             float2 interp5 : INTERP5;
             float2 interp6 : INTERP6;
             float3 interp7 : INTERP7;
             float4 interp8 : INTERP8;
             float4 interp9 : INTERP9;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.interp0.xyz =  input.positionWS;
            output.interp1.xyz =  input.normalWS;
            output.interp2.xyzw =  input.tangentWS;
            output.interp3.xyzw =  input.texCoord0;
            output.interp4.xyz =  input.viewDirectionWS;
            #if defined(LIGHTMAP_ON)
            output.interp5.xy =  input.staticLightmapUV;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
            output.interp6.xy =  input.dynamicLightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.interp7.xyz =  input.sh;
            #endif
            output.interp8.xyzw =  input.fogFactorAndVertexLight;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            output.interp9.xyzw =  input.shadowCoord;
            #endif
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.positionWS = input.interp0.xyz;
            output.normalWS = input.interp1.xyz;
            output.tangentWS = input.interp2.xyzw;
            output.texCoord0 = input.interp3.xyzw;
            output.viewDirectionWS = input.interp4.xyz;
            #if defined(LIGHTMAP_ON)
            output.staticLightmapUV = input.interp5.xy;
            #endif
            #if defined(DYNAMICLIGHTMAP_ON)
            output.dynamicLightmapUV = input.interp6.xy;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.sh = input.interp7.xyz;
            #endif
            output.fogFactorAndVertexLight = input.interp8.xyzw;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            output.shadowCoord = input.interp9.xyzw;
            #endif
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1_TexelSize;
        float3 _position;
        float _WindShiftStreth;
        float _WindSpeed;
        float _WindStrenth;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        SAMPLER(sampler_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        
        inline float Unity_SimpleNoise_RandomValue_float (float2 uv)
        {
            float angle = dot(uv, float2(12.9898, 78.233));
            #if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN))
                // 'sin()' has bad precision on Mali GPUs for inputs > 10000
                angle = fmod(angle, TWO_PI); // Avoid large inputs to sin()
            #endif
            return frac(sin(angle)*43758.5453);
        }
        
        inline float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t)
        {
            return (1.0-t)*a + (t*b);
        }
        
        
        inline float Unity_SimpleNoise_ValueNoise_float (float2 uv)
        {
            float2 i = floor(uv);
            float2 f = frac(uv);
            f = f * f * (3.0 - 2.0 * f);
        
            uv = abs(frac(uv) - 0.5);
            float2 c0 = i + float2(0.0, 0.0);
            float2 c1 = i + float2(1.0, 0.0);
            float2 c2 = i + float2(0.0, 1.0);
            float2 c3 = i + float2(1.0, 1.0);
            float r0 = Unity_SimpleNoise_RandomValue_float(c0);
            float r1 = Unity_SimpleNoise_RandomValue_float(c1);
            float r2 = Unity_SimpleNoise_RandomValue_float(c2);
            float r3 = Unity_SimpleNoise_RandomValue_float(c3);
        
            float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
            float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
            float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
            return t;
        }
        void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
        {
            float t = 0.0;
        
            float freq = pow(2.0, float(0));
            float amp = pow(0.5, float(3-0));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(1));
            amp = pow(0.5, float(3-1));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(2));
            amp = pow(0.5, float(3-2));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            Out = t;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void TriangleWave_float(float In, out float Out)
        {
            Out = 2.0 * abs( 2 * (In - floor(0.5 + In)) ) - 1.0;
        }
        
        void Unity_Lerp_float(float A, float B, float T, out float Out)
        {
            Out = lerp(A, B, T);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Split_4943b3735a0e4ba5819db29ad339111a_R_1 = IN.WorldSpacePosition[0];
            float _Split_4943b3735a0e4ba5819db29ad339111a_G_2 = IN.WorldSpacePosition[1];
            float _Split_4943b3735a0e4ba5819db29ad339111a_B_3 = IN.WorldSpacePosition[2];
            float _Split_4943b3735a0e4ba5819db29ad339111a_A_4 = 0;
            float _Split_1df4c052b12745db8c3ab81a1115c945_R_1 = IN.WorldSpacePosition[0];
            float _Split_1df4c052b12745db8c3ab81a1115c945_G_2 = IN.WorldSpacePosition[1];
            float _Split_1df4c052b12745db8c3ab81a1115c945_B_3 = IN.WorldSpacePosition[2];
            float _Split_1df4c052b12745db8c3ab81a1115c945_A_4 = 0;
            float4 _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4;
            float3 _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5;
            float2 _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6;
            Unity_Combine_float(_Split_1df4c052b12745db8c3ab81a1115c945_R_1, _Split_1df4c052b12745db8c3ab81a1115c945_G_2, 0, 0, _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4, _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5, _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6);
            float _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2;
            Unity_SimpleNoise_float(_Combine_c8a8a8a174fe417389fcb951c422011a_RG_6, 500, _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2);
            float _Property_f5800ee87a11435ba90f964e3635c567_Out_0 = _WindShiftStreth;
            float _Multiply_44b7a7abd320474289777104102625d7_Out_2;
            Unity_Multiply_float_float(_SimpleNoise_e8723a906e95458096db370034b18abe_Out_2, _Property_f5800ee87a11435ba90f964e3635c567_Out_0, _Multiply_44b7a7abd320474289777104102625d7_Out_2);
            float _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0 = _WindSpeed;
            float _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2);
            float _Add_5236d4953a3147c88bc756d35f145c51_Out_2;
            Unity_Add_float(_Multiply_44b7a7abd320474289777104102625d7_Out_2, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2, _Add_5236d4953a3147c88bc756d35f145c51_Out_2);
            float _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1;
            TriangleWave_float(_Add_5236d4953a3147c88bc756d35f145c51_Out_2, _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1);
            float _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0 = _WindStrenth;
            float _Multiply_642789de8d1c4560848f311ee76485d0_Out_2;
            Unity_Multiply_float_float(_TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1, _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2);
            float _Add_d86e51a612d648418462758f30bf5a46_Out_2;
            Unity_Add_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2, _Add_d86e51a612d648418462758f30bf5a46_Out_2);
            float4 _UV_14cd8fc313be473da647d5d3c819522b_Out_0 = IN.uv0;
            float _Split_524956ebfd064436818e66014c0bb66b_R_1 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[0];
            float _Split_524956ebfd064436818e66014c0bb66b_G_2 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[1];
            float _Split_524956ebfd064436818e66014c0bb66b_B_3 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[2];
            float _Split_524956ebfd064436818e66014c0bb66b_A_4 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[3];
            float _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3;
            Unity_Lerp_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Add_d86e51a612d648418462758f30bf5a46_Out_2, _Split_524956ebfd064436818e66014c0bb66b_G_2, _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3);
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_R_1 = IN.WorldSpacePosition[0];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2 = IN.WorldSpacePosition[1];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3 = IN.WorldSpacePosition[2];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_A_4 = 0;
            float4 _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4;
            float3 _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5;
            float2 _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6;
            Unity_Combine_float(_Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3, _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2, _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3, 0, _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4, _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5, _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6);
            float3 _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1 = TransformWorldToObject(_Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5.xyz);
            description.Position = _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float3 NormalTS;
            float3 Emission;
            float Metallic;
            float Smoothness;
            float Occlusion;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).GetTransformedUV(IN.uv0.xy));
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_R_4 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.r;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_G_5 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.g;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_B_6 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.b;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_A_7 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.a;
            surface.BaseColor = (_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.xyz);
            surface.NormalTS = IN.TangentSpaceNormal;
            surface.Emission = float3(0, 0, 0);
            surface.Metallic = 0;
            surface.Smoothness = 0.5;
            surface.Occlusion = 1;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.WorldSpaceTangent =                          TransformObjectToWorldDir(input.tangentOS.xyz);
            output.ObjectSpaceBiTangent =                       normalize(cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w > 0.0f ? 1.0f : -1.0f) * GetOddNegativeScale());
            output.WorldSpaceBiTangent =                        TransformObjectToWorldDir(output.ObjectSpaceBiTangent);
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.uv0 =                                        input.uv0;
            output.TimeParameters =                             _TimeParameters.xyz;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
            // FragInputs from VFX come from two places: Interpolator or CBuffer.
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
            output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);
        
        
            output.uv0 = input.texCoord0;
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBRForwardPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }
        
        // Render State
        Cull Off
        ZTest LEqual
        ZWrite On
        ColorMask 0
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma only_renderers gles gles3 glcore d3d11
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define VARYINGS_NEED_NORMAL_WS
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_SHADOWCASTER
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 WorldSpaceTangent;
             float3 ObjectSpaceBiTangent;
             float3 WorldSpaceBiTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float4 uv0;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float3 interp0 : INTERP0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.interp0.xyz =  input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.normalWS = input.interp0.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1_TexelSize;
        float3 _position;
        float _WindShiftStreth;
        float _WindSpeed;
        float _WindStrenth;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        SAMPLER(sampler_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        
        inline float Unity_SimpleNoise_RandomValue_float (float2 uv)
        {
            float angle = dot(uv, float2(12.9898, 78.233));
            #if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN))
                // 'sin()' has bad precision on Mali GPUs for inputs > 10000
                angle = fmod(angle, TWO_PI); // Avoid large inputs to sin()
            #endif
            return frac(sin(angle)*43758.5453);
        }
        
        inline float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t)
        {
            return (1.0-t)*a + (t*b);
        }
        
        
        inline float Unity_SimpleNoise_ValueNoise_float (float2 uv)
        {
            float2 i = floor(uv);
            float2 f = frac(uv);
            f = f * f * (3.0 - 2.0 * f);
        
            uv = abs(frac(uv) - 0.5);
            float2 c0 = i + float2(0.0, 0.0);
            float2 c1 = i + float2(1.0, 0.0);
            float2 c2 = i + float2(0.0, 1.0);
            float2 c3 = i + float2(1.0, 1.0);
            float r0 = Unity_SimpleNoise_RandomValue_float(c0);
            float r1 = Unity_SimpleNoise_RandomValue_float(c1);
            float r2 = Unity_SimpleNoise_RandomValue_float(c2);
            float r3 = Unity_SimpleNoise_RandomValue_float(c3);
        
            float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
            float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
            float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
            return t;
        }
        void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
        {
            float t = 0.0;
        
            float freq = pow(2.0, float(0));
            float amp = pow(0.5, float(3-0));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(1));
            amp = pow(0.5, float(3-1));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(2));
            amp = pow(0.5, float(3-2));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            Out = t;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void TriangleWave_float(float In, out float Out)
        {
            Out = 2.0 * abs( 2 * (In - floor(0.5 + In)) ) - 1.0;
        }
        
        void Unity_Lerp_float(float A, float B, float T, out float Out)
        {
            Out = lerp(A, B, T);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Split_4943b3735a0e4ba5819db29ad339111a_R_1 = IN.WorldSpacePosition[0];
            float _Split_4943b3735a0e4ba5819db29ad339111a_G_2 = IN.WorldSpacePosition[1];
            float _Split_4943b3735a0e4ba5819db29ad339111a_B_3 = IN.WorldSpacePosition[2];
            float _Split_4943b3735a0e4ba5819db29ad339111a_A_4 = 0;
            float _Split_1df4c052b12745db8c3ab81a1115c945_R_1 = IN.WorldSpacePosition[0];
            float _Split_1df4c052b12745db8c3ab81a1115c945_G_2 = IN.WorldSpacePosition[1];
            float _Split_1df4c052b12745db8c3ab81a1115c945_B_3 = IN.WorldSpacePosition[2];
            float _Split_1df4c052b12745db8c3ab81a1115c945_A_4 = 0;
            float4 _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4;
            float3 _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5;
            float2 _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6;
            Unity_Combine_float(_Split_1df4c052b12745db8c3ab81a1115c945_R_1, _Split_1df4c052b12745db8c3ab81a1115c945_G_2, 0, 0, _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4, _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5, _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6);
            float _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2;
            Unity_SimpleNoise_float(_Combine_c8a8a8a174fe417389fcb951c422011a_RG_6, 500, _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2);
            float _Property_f5800ee87a11435ba90f964e3635c567_Out_0 = _WindShiftStreth;
            float _Multiply_44b7a7abd320474289777104102625d7_Out_2;
            Unity_Multiply_float_float(_SimpleNoise_e8723a906e95458096db370034b18abe_Out_2, _Property_f5800ee87a11435ba90f964e3635c567_Out_0, _Multiply_44b7a7abd320474289777104102625d7_Out_2);
            float _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0 = _WindSpeed;
            float _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2);
            float _Add_5236d4953a3147c88bc756d35f145c51_Out_2;
            Unity_Add_float(_Multiply_44b7a7abd320474289777104102625d7_Out_2, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2, _Add_5236d4953a3147c88bc756d35f145c51_Out_2);
            float _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1;
            TriangleWave_float(_Add_5236d4953a3147c88bc756d35f145c51_Out_2, _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1);
            float _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0 = _WindStrenth;
            float _Multiply_642789de8d1c4560848f311ee76485d0_Out_2;
            Unity_Multiply_float_float(_TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1, _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2);
            float _Add_d86e51a612d648418462758f30bf5a46_Out_2;
            Unity_Add_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2, _Add_d86e51a612d648418462758f30bf5a46_Out_2);
            float4 _UV_14cd8fc313be473da647d5d3c819522b_Out_0 = IN.uv0;
            float _Split_524956ebfd064436818e66014c0bb66b_R_1 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[0];
            float _Split_524956ebfd064436818e66014c0bb66b_G_2 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[1];
            float _Split_524956ebfd064436818e66014c0bb66b_B_3 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[2];
            float _Split_524956ebfd064436818e66014c0bb66b_A_4 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[3];
            float _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3;
            Unity_Lerp_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Add_d86e51a612d648418462758f30bf5a46_Out_2, _Split_524956ebfd064436818e66014c0bb66b_G_2, _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3);
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_R_1 = IN.WorldSpacePosition[0];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2 = IN.WorldSpacePosition[1];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3 = IN.WorldSpacePosition[2];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_A_4 = 0;
            float4 _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4;
            float3 _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5;
            float2 _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6;
            Unity_Combine_float(_Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3, _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2, _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3, 0, _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4, _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5, _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6);
            float3 _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1 = TransformWorldToObject(_Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5.xyz);
            description.Position = _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.WorldSpaceTangent =                          TransformObjectToWorldDir(input.tangentOS.xyz);
            output.ObjectSpaceBiTangent =                       normalize(cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w > 0.0f ? 1.0f : -1.0f) * GetOddNegativeScale());
            output.WorldSpaceBiTangent =                        TransformObjectToWorldDir(output.ObjectSpaceBiTangent);
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.uv0 =                                        input.uv0;
            output.TimeParameters =                             _TimeParameters.xyz;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
            // FragInputs from VFX come from two places: Interpolator or CBuffer.
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShadowCasterPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }
        
        // Render State
        Cull Off
        ZTest LEqual
        ZWrite On
        ColorMask 0
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma only_renderers gles gles3 glcore d3d11
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHONLY
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 WorldSpaceTangent;
             float3 ObjectSpaceBiTangent;
             float3 WorldSpaceBiTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float4 uv0;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1_TexelSize;
        float3 _position;
        float _WindShiftStreth;
        float _WindSpeed;
        float _WindStrenth;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        SAMPLER(sampler_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        
        inline float Unity_SimpleNoise_RandomValue_float (float2 uv)
        {
            float angle = dot(uv, float2(12.9898, 78.233));
            #if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN))
                // 'sin()' has bad precision on Mali GPUs for inputs > 10000
                angle = fmod(angle, TWO_PI); // Avoid large inputs to sin()
            #endif
            return frac(sin(angle)*43758.5453);
        }
        
        inline float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t)
        {
            return (1.0-t)*a + (t*b);
        }
        
        
        inline float Unity_SimpleNoise_ValueNoise_float (float2 uv)
        {
            float2 i = floor(uv);
            float2 f = frac(uv);
            f = f * f * (3.0 - 2.0 * f);
        
            uv = abs(frac(uv) - 0.5);
            float2 c0 = i + float2(0.0, 0.0);
            float2 c1 = i + float2(1.0, 0.0);
            float2 c2 = i + float2(0.0, 1.0);
            float2 c3 = i + float2(1.0, 1.0);
            float r0 = Unity_SimpleNoise_RandomValue_float(c0);
            float r1 = Unity_SimpleNoise_RandomValue_float(c1);
            float r2 = Unity_SimpleNoise_RandomValue_float(c2);
            float r3 = Unity_SimpleNoise_RandomValue_float(c3);
        
            float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
            float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
            float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
            return t;
        }
        void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
        {
            float t = 0.0;
        
            float freq = pow(2.0, float(0));
            float amp = pow(0.5, float(3-0));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(1));
            amp = pow(0.5, float(3-1));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(2));
            amp = pow(0.5, float(3-2));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            Out = t;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void TriangleWave_float(float In, out float Out)
        {
            Out = 2.0 * abs( 2 * (In - floor(0.5 + In)) ) - 1.0;
        }
        
        void Unity_Lerp_float(float A, float B, float T, out float Out)
        {
            Out = lerp(A, B, T);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Split_4943b3735a0e4ba5819db29ad339111a_R_1 = IN.WorldSpacePosition[0];
            float _Split_4943b3735a0e4ba5819db29ad339111a_G_2 = IN.WorldSpacePosition[1];
            float _Split_4943b3735a0e4ba5819db29ad339111a_B_3 = IN.WorldSpacePosition[2];
            float _Split_4943b3735a0e4ba5819db29ad339111a_A_4 = 0;
            float _Split_1df4c052b12745db8c3ab81a1115c945_R_1 = IN.WorldSpacePosition[0];
            float _Split_1df4c052b12745db8c3ab81a1115c945_G_2 = IN.WorldSpacePosition[1];
            float _Split_1df4c052b12745db8c3ab81a1115c945_B_3 = IN.WorldSpacePosition[2];
            float _Split_1df4c052b12745db8c3ab81a1115c945_A_4 = 0;
            float4 _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4;
            float3 _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5;
            float2 _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6;
            Unity_Combine_float(_Split_1df4c052b12745db8c3ab81a1115c945_R_1, _Split_1df4c052b12745db8c3ab81a1115c945_G_2, 0, 0, _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4, _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5, _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6);
            float _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2;
            Unity_SimpleNoise_float(_Combine_c8a8a8a174fe417389fcb951c422011a_RG_6, 500, _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2);
            float _Property_f5800ee87a11435ba90f964e3635c567_Out_0 = _WindShiftStreth;
            float _Multiply_44b7a7abd320474289777104102625d7_Out_2;
            Unity_Multiply_float_float(_SimpleNoise_e8723a906e95458096db370034b18abe_Out_2, _Property_f5800ee87a11435ba90f964e3635c567_Out_0, _Multiply_44b7a7abd320474289777104102625d7_Out_2);
            float _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0 = _WindSpeed;
            float _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2);
            float _Add_5236d4953a3147c88bc756d35f145c51_Out_2;
            Unity_Add_float(_Multiply_44b7a7abd320474289777104102625d7_Out_2, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2, _Add_5236d4953a3147c88bc756d35f145c51_Out_2);
            float _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1;
            TriangleWave_float(_Add_5236d4953a3147c88bc756d35f145c51_Out_2, _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1);
            float _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0 = _WindStrenth;
            float _Multiply_642789de8d1c4560848f311ee76485d0_Out_2;
            Unity_Multiply_float_float(_TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1, _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2);
            float _Add_d86e51a612d648418462758f30bf5a46_Out_2;
            Unity_Add_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2, _Add_d86e51a612d648418462758f30bf5a46_Out_2);
            float4 _UV_14cd8fc313be473da647d5d3c819522b_Out_0 = IN.uv0;
            float _Split_524956ebfd064436818e66014c0bb66b_R_1 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[0];
            float _Split_524956ebfd064436818e66014c0bb66b_G_2 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[1];
            float _Split_524956ebfd064436818e66014c0bb66b_B_3 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[2];
            float _Split_524956ebfd064436818e66014c0bb66b_A_4 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[3];
            float _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3;
            Unity_Lerp_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Add_d86e51a612d648418462758f30bf5a46_Out_2, _Split_524956ebfd064436818e66014c0bb66b_G_2, _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3);
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_R_1 = IN.WorldSpacePosition[0];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2 = IN.WorldSpacePosition[1];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3 = IN.WorldSpacePosition[2];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_A_4 = 0;
            float4 _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4;
            float3 _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5;
            float2 _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6;
            Unity_Combine_float(_Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3, _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2, _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3, 0, _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4, _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5, _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6);
            float3 _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1 = TransformWorldToObject(_Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5.xyz);
            description.Position = _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.WorldSpaceTangent =                          TransformObjectToWorldDir(input.tangentOS.xyz);
            output.ObjectSpaceBiTangent =                       normalize(cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w > 0.0f ? 1.0f : -1.0f) * GetOddNegativeScale());
            output.WorldSpaceBiTangent =                        TransformObjectToWorldDir(output.ObjectSpaceBiTangent);
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.uv0 =                                        input.uv0;
            output.TimeParameters =                             _TimeParameters.xyz;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
            // FragInputs from VFX come from two places: Interpolator or CBuffer.
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }
        
        // Render State
        Cull Off
        ZTest LEqual
        ZWrite On
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma only_renderers gles gles3 glcore d3d11
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_TEXCOORD1
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TANGENT_WS
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHNORMALS
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 uv1 : TEXCOORD1;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 normalWS;
             float4 tangentWS;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 TangentSpaceNormal;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 WorldSpaceTangent;
             float3 ObjectSpaceBiTangent;
             float3 WorldSpaceBiTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float4 uv0;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float3 interp0 : INTERP0;
             float4 interp1 : INTERP1;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.interp0.xyz =  input.normalWS;
            output.interp1.xyzw =  input.tangentWS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.normalWS = input.interp0.xyz;
            output.tangentWS = input.interp1.xyzw;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1_TexelSize;
        float3 _position;
        float _WindShiftStreth;
        float _WindSpeed;
        float _WindStrenth;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        SAMPLER(sampler_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        
        inline float Unity_SimpleNoise_RandomValue_float (float2 uv)
        {
            float angle = dot(uv, float2(12.9898, 78.233));
            #if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN))
                // 'sin()' has bad precision on Mali GPUs for inputs > 10000
                angle = fmod(angle, TWO_PI); // Avoid large inputs to sin()
            #endif
            return frac(sin(angle)*43758.5453);
        }
        
        inline float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t)
        {
            return (1.0-t)*a + (t*b);
        }
        
        
        inline float Unity_SimpleNoise_ValueNoise_float (float2 uv)
        {
            float2 i = floor(uv);
            float2 f = frac(uv);
            f = f * f * (3.0 - 2.0 * f);
        
            uv = abs(frac(uv) - 0.5);
            float2 c0 = i + float2(0.0, 0.0);
            float2 c1 = i + float2(1.0, 0.0);
            float2 c2 = i + float2(0.0, 1.0);
            float2 c3 = i + float2(1.0, 1.0);
            float r0 = Unity_SimpleNoise_RandomValue_float(c0);
            float r1 = Unity_SimpleNoise_RandomValue_float(c1);
            float r2 = Unity_SimpleNoise_RandomValue_float(c2);
            float r3 = Unity_SimpleNoise_RandomValue_float(c3);
        
            float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
            float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
            float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
            return t;
        }
        void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
        {
            float t = 0.0;
        
            float freq = pow(2.0, float(0));
            float amp = pow(0.5, float(3-0));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(1));
            amp = pow(0.5, float(3-1));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(2));
            amp = pow(0.5, float(3-2));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            Out = t;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void TriangleWave_float(float In, out float Out)
        {
            Out = 2.0 * abs( 2 * (In - floor(0.5 + In)) ) - 1.0;
        }
        
        void Unity_Lerp_float(float A, float B, float T, out float Out)
        {
            Out = lerp(A, B, T);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Split_4943b3735a0e4ba5819db29ad339111a_R_1 = IN.WorldSpacePosition[0];
            float _Split_4943b3735a0e4ba5819db29ad339111a_G_2 = IN.WorldSpacePosition[1];
            float _Split_4943b3735a0e4ba5819db29ad339111a_B_3 = IN.WorldSpacePosition[2];
            float _Split_4943b3735a0e4ba5819db29ad339111a_A_4 = 0;
            float _Split_1df4c052b12745db8c3ab81a1115c945_R_1 = IN.WorldSpacePosition[0];
            float _Split_1df4c052b12745db8c3ab81a1115c945_G_2 = IN.WorldSpacePosition[1];
            float _Split_1df4c052b12745db8c3ab81a1115c945_B_3 = IN.WorldSpacePosition[2];
            float _Split_1df4c052b12745db8c3ab81a1115c945_A_4 = 0;
            float4 _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4;
            float3 _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5;
            float2 _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6;
            Unity_Combine_float(_Split_1df4c052b12745db8c3ab81a1115c945_R_1, _Split_1df4c052b12745db8c3ab81a1115c945_G_2, 0, 0, _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4, _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5, _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6);
            float _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2;
            Unity_SimpleNoise_float(_Combine_c8a8a8a174fe417389fcb951c422011a_RG_6, 500, _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2);
            float _Property_f5800ee87a11435ba90f964e3635c567_Out_0 = _WindShiftStreth;
            float _Multiply_44b7a7abd320474289777104102625d7_Out_2;
            Unity_Multiply_float_float(_SimpleNoise_e8723a906e95458096db370034b18abe_Out_2, _Property_f5800ee87a11435ba90f964e3635c567_Out_0, _Multiply_44b7a7abd320474289777104102625d7_Out_2);
            float _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0 = _WindSpeed;
            float _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2);
            float _Add_5236d4953a3147c88bc756d35f145c51_Out_2;
            Unity_Add_float(_Multiply_44b7a7abd320474289777104102625d7_Out_2, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2, _Add_5236d4953a3147c88bc756d35f145c51_Out_2);
            float _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1;
            TriangleWave_float(_Add_5236d4953a3147c88bc756d35f145c51_Out_2, _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1);
            float _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0 = _WindStrenth;
            float _Multiply_642789de8d1c4560848f311ee76485d0_Out_2;
            Unity_Multiply_float_float(_TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1, _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2);
            float _Add_d86e51a612d648418462758f30bf5a46_Out_2;
            Unity_Add_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2, _Add_d86e51a612d648418462758f30bf5a46_Out_2);
            float4 _UV_14cd8fc313be473da647d5d3c819522b_Out_0 = IN.uv0;
            float _Split_524956ebfd064436818e66014c0bb66b_R_1 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[0];
            float _Split_524956ebfd064436818e66014c0bb66b_G_2 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[1];
            float _Split_524956ebfd064436818e66014c0bb66b_B_3 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[2];
            float _Split_524956ebfd064436818e66014c0bb66b_A_4 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[3];
            float _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3;
            Unity_Lerp_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Add_d86e51a612d648418462758f30bf5a46_Out_2, _Split_524956ebfd064436818e66014c0bb66b_G_2, _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3);
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_R_1 = IN.WorldSpacePosition[0];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2 = IN.WorldSpacePosition[1];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3 = IN.WorldSpacePosition[2];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_A_4 = 0;
            float4 _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4;
            float3 _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5;
            float2 _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6;
            Unity_Combine_float(_Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3, _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2, _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3, 0, _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4, _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5, _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6);
            float3 _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1 = TransformWorldToObject(_Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5.xyz);
            description.Position = _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 NormalTS;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            surface.NormalTS = IN.TangentSpaceNormal;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.WorldSpaceTangent =                          TransformObjectToWorldDir(input.tangentOS.xyz);
            output.ObjectSpaceBiTangent =                       normalize(cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w > 0.0f ? 1.0f : -1.0f) * GetOddNegativeScale());
            output.WorldSpaceBiTangent =                        TransformObjectToWorldDir(output.ObjectSpaceBiTangent);
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.uv0 =                                        input.uv0;
            output.TimeParameters =                             _TimeParameters.xyz;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
            // FragInputs from VFX come from two places: Interpolator or CBuffer.
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
            output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);
        
        
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/DepthNormalsOnlyPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }
        
        // Render State
        Cull Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma only_renderers gles gles3 glcore d3d11
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        #pragma shader_feature _ EDITOR_VISUALIZATION
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_TEXCOORD1
        #define ATTRIBUTES_NEED_TEXCOORD2
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_TEXCOORD1
        #define VARYINGS_NEED_TEXCOORD2
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_META
        #define _FOG_FRAGMENT 1
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 uv1 : TEXCOORD1;
             float4 uv2 : TEXCOORD2;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0;
             float4 texCoord1;
             float4 texCoord2;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 WorldSpaceTangent;
             float3 ObjectSpaceBiTangent;
             float3 WorldSpaceBiTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float4 uv0;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float4 interp0 : INTERP0;
             float4 interp1 : INTERP1;
             float4 interp2 : INTERP2;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.interp0.xyzw =  input.texCoord0;
            output.interp1.xyzw =  input.texCoord1;
            output.interp2.xyzw =  input.texCoord2;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.texCoord0 = input.interp0.xyzw;
            output.texCoord1 = input.interp1.xyzw;
            output.texCoord2 = input.interp2.xyzw;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1_TexelSize;
        float3 _position;
        float _WindShiftStreth;
        float _WindSpeed;
        float _WindStrenth;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        SAMPLER(sampler_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        
        inline float Unity_SimpleNoise_RandomValue_float (float2 uv)
        {
            float angle = dot(uv, float2(12.9898, 78.233));
            #if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN))
                // 'sin()' has bad precision on Mali GPUs for inputs > 10000
                angle = fmod(angle, TWO_PI); // Avoid large inputs to sin()
            #endif
            return frac(sin(angle)*43758.5453);
        }
        
        inline float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t)
        {
            return (1.0-t)*a + (t*b);
        }
        
        
        inline float Unity_SimpleNoise_ValueNoise_float (float2 uv)
        {
            float2 i = floor(uv);
            float2 f = frac(uv);
            f = f * f * (3.0 - 2.0 * f);
        
            uv = abs(frac(uv) - 0.5);
            float2 c0 = i + float2(0.0, 0.0);
            float2 c1 = i + float2(1.0, 0.0);
            float2 c2 = i + float2(0.0, 1.0);
            float2 c3 = i + float2(1.0, 1.0);
            float r0 = Unity_SimpleNoise_RandomValue_float(c0);
            float r1 = Unity_SimpleNoise_RandomValue_float(c1);
            float r2 = Unity_SimpleNoise_RandomValue_float(c2);
            float r3 = Unity_SimpleNoise_RandomValue_float(c3);
        
            float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
            float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
            float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
            return t;
        }
        void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
        {
            float t = 0.0;
        
            float freq = pow(2.0, float(0));
            float amp = pow(0.5, float(3-0));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(1));
            amp = pow(0.5, float(3-1));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(2));
            amp = pow(0.5, float(3-2));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            Out = t;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void TriangleWave_float(float In, out float Out)
        {
            Out = 2.0 * abs( 2 * (In - floor(0.5 + In)) ) - 1.0;
        }
        
        void Unity_Lerp_float(float A, float B, float T, out float Out)
        {
            Out = lerp(A, B, T);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Split_4943b3735a0e4ba5819db29ad339111a_R_1 = IN.WorldSpacePosition[0];
            float _Split_4943b3735a0e4ba5819db29ad339111a_G_2 = IN.WorldSpacePosition[1];
            float _Split_4943b3735a0e4ba5819db29ad339111a_B_3 = IN.WorldSpacePosition[2];
            float _Split_4943b3735a0e4ba5819db29ad339111a_A_4 = 0;
            float _Split_1df4c052b12745db8c3ab81a1115c945_R_1 = IN.WorldSpacePosition[0];
            float _Split_1df4c052b12745db8c3ab81a1115c945_G_2 = IN.WorldSpacePosition[1];
            float _Split_1df4c052b12745db8c3ab81a1115c945_B_3 = IN.WorldSpacePosition[2];
            float _Split_1df4c052b12745db8c3ab81a1115c945_A_4 = 0;
            float4 _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4;
            float3 _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5;
            float2 _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6;
            Unity_Combine_float(_Split_1df4c052b12745db8c3ab81a1115c945_R_1, _Split_1df4c052b12745db8c3ab81a1115c945_G_2, 0, 0, _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4, _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5, _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6);
            float _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2;
            Unity_SimpleNoise_float(_Combine_c8a8a8a174fe417389fcb951c422011a_RG_6, 500, _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2);
            float _Property_f5800ee87a11435ba90f964e3635c567_Out_0 = _WindShiftStreth;
            float _Multiply_44b7a7abd320474289777104102625d7_Out_2;
            Unity_Multiply_float_float(_SimpleNoise_e8723a906e95458096db370034b18abe_Out_2, _Property_f5800ee87a11435ba90f964e3635c567_Out_0, _Multiply_44b7a7abd320474289777104102625d7_Out_2);
            float _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0 = _WindSpeed;
            float _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2);
            float _Add_5236d4953a3147c88bc756d35f145c51_Out_2;
            Unity_Add_float(_Multiply_44b7a7abd320474289777104102625d7_Out_2, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2, _Add_5236d4953a3147c88bc756d35f145c51_Out_2);
            float _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1;
            TriangleWave_float(_Add_5236d4953a3147c88bc756d35f145c51_Out_2, _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1);
            float _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0 = _WindStrenth;
            float _Multiply_642789de8d1c4560848f311ee76485d0_Out_2;
            Unity_Multiply_float_float(_TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1, _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2);
            float _Add_d86e51a612d648418462758f30bf5a46_Out_2;
            Unity_Add_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2, _Add_d86e51a612d648418462758f30bf5a46_Out_2);
            float4 _UV_14cd8fc313be473da647d5d3c819522b_Out_0 = IN.uv0;
            float _Split_524956ebfd064436818e66014c0bb66b_R_1 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[0];
            float _Split_524956ebfd064436818e66014c0bb66b_G_2 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[1];
            float _Split_524956ebfd064436818e66014c0bb66b_B_3 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[2];
            float _Split_524956ebfd064436818e66014c0bb66b_A_4 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[3];
            float _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3;
            Unity_Lerp_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Add_d86e51a612d648418462758f30bf5a46_Out_2, _Split_524956ebfd064436818e66014c0bb66b_G_2, _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3);
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_R_1 = IN.WorldSpacePosition[0];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2 = IN.WorldSpacePosition[1];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3 = IN.WorldSpacePosition[2];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_A_4 = 0;
            float4 _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4;
            float3 _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5;
            float2 _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6;
            Unity_Combine_float(_Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3, _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2, _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3, 0, _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4, _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5, _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6);
            float3 _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1 = TransformWorldToObject(_Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5.xyz);
            description.Position = _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float3 Emission;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).GetTransformedUV(IN.uv0.xy));
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_R_4 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.r;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_G_5 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.g;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_B_6 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.b;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_A_7 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.a;
            surface.BaseColor = (_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.xyz);
            surface.Emission = float3(0, 0, 0);
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.WorldSpaceTangent =                          TransformObjectToWorldDir(input.tangentOS.xyz);
            output.ObjectSpaceBiTangent =                       normalize(cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w > 0.0f ? 1.0f : -1.0f) * GetOddNegativeScale());
            output.WorldSpaceBiTangent =                        TransformObjectToWorldDir(output.ObjectSpaceBiTangent);
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.uv0 =                                        input.uv0;
            output.TimeParameters =                             _TimeParameters.xyz;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
            // FragInputs from VFX come from two places: Interpolator or CBuffer.
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
            output.uv0 = input.texCoord0;
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/LightingMetaPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "SceneSelectionPass"
            Tags
            {
                "LightMode" = "SceneSelectionPass"
            }
        
        // Render State
        Cull Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma only_renderers gles gles3 glcore d3d11
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHONLY
        #define SCENESELECTIONPASS 1
        #define ALPHA_CLIP_THRESHOLD 1
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 WorldSpaceTangent;
             float3 ObjectSpaceBiTangent;
             float3 WorldSpaceBiTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float4 uv0;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1_TexelSize;
        float3 _position;
        float _WindShiftStreth;
        float _WindSpeed;
        float _WindStrenth;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        SAMPLER(sampler_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        
        inline float Unity_SimpleNoise_RandomValue_float (float2 uv)
        {
            float angle = dot(uv, float2(12.9898, 78.233));
            #if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN))
                // 'sin()' has bad precision on Mali GPUs for inputs > 10000
                angle = fmod(angle, TWO_PI); // Avoid large inputs to sin()
            #endif
            return frac(sin(angle)*43758.5453);
        }
        
        inline float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t)
        {
            return (1.0-t)*a + (t*b);
        }
        
        
        inline float Unity_SimpleNoise_ValueNoise_float (float2 uv)
        {
            float2 i = floor(uv);
            float2 f = frac(uv);
            f = f * f * (3.0 - 2.0 * f);
        
            uv = abs(frac(uv) - 0.5);
            float2 c0 = i + float2(0.0, 0.0);
            float2 c1 = i + float2(1.0, 0.0);
            float2 c2 = i + float2(0.0, 1.0);
            float2 c3 = i + float2(1.0, 1.0);
            float r0 = Unity_SimpleNoise_RandomValue_float(c0);
            float r1 = Unity_SimpleNoise_RandomValue_float(c1);
            float r2 = Unity_SimpleNoise_RandomValue_float(c2);
            float r3 = Unity_SimpleNoise_RandomValue_float(c3);
        
            float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
            float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
            float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
            return t;
        }
        void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
        {
            float t = 0.0;
        
            float freq = pow(2.0, float(0));
            float amp = pow(0.5, float(3-0));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(1));
            amp = pow(0.5, float(3-1));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(2));
            amp = pow(0.5, float(3-2));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            Out = t;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void TriangleWave_float(float In, out float Out)
        {
            Out = 2.0 * abs( 2 * (In - floor(0.5 + In)) ) - 1.0;
        }
        
        void Unity_Lerp_float(float A, float B, float T, out float Out)
        {
            Out = lerp(A, B, T);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Split_4943b3735a0e4ba5819db29ad339111a_R_1 = IN.WorldSpacePosition[0];
            float _Split_4943b3735a0e4ba5819db29ad339111a_G_2 = IN.WorldSpacePosition[1];
            float _Split_4943b3735a0e4ba5819db29ad339111a_B_3 = IN.WorldSpacePosition[2];
            float _Split_4943b3735a0e4ba5819db29ad339111a_A_4 = 0;
            float _Split_1df4c052b12745db8c3ab81a1115c945_R_1 = IN.WorldSpacePosition[0];
            float _Split_1df4c052b12745db8c3ab81a1115c945_G_2 = IN.WorldSpacePosition[1];
            float _Split_1df4c052b12745db8c3ab81a1115c945_B_3 = IN.WorldSpacePosition[2];
            float _Split_1df4c052b12745db8c3ab81a1115c945_A_4 = 0;
            float4 _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4;
            float3 _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5;
            float2 _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6;
            Unity_Combine_float(_Split_1df4c052b12745db8c3ab81a1115c945_R_1, _Split_1df4c052b12745db8c3ab81a1115c945_G_2, 0, 0, _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4, _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5, _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6);
            float _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2;
            Unity_SimpleNoise_float(_Combine_c8a8a8a174fe417389fcb951c422011a_RG_6, 500, _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2);
            float _Property_f5800ee87a11435ba90f964e3635c567_Out_0 = _WindShiftStreth;
            float _Multiply_44b7a7abd320474289777104102625d7_Out_2;
            Unity_Multiply_float_float(_SimpleNoise_e8723a906e95458096db370034b18abe_Out_2, _Property_f5800ee87a11435ba90f964e3635c567_Out_0, _Multiply_44b7a7abd320474289777104102625d7_Out_2);
            float _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0 = _WindSpeed;
            float _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2);
            float _Add_5236d4953a3147c88bc756d35f145c51_Out_2;
            Unity_Add_float(_Multiply_44b7a7abd320474289777104102625d7_Out_2, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2, _Add_5236d4953a3147c88bc756d35f145c51_Out_2);
            float _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1;
            TriangleWave_float(_Add_5236d4953a3147c88bc756d35f145c51_Out_2, _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1);
            float _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0 = _WindStrenth;
            float _Multiply_642789de8d1c4560848f311ee76485d0_Out_2;
            Unity_Multiply_float_float(_TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1, _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2);
            float _Add_d86e51a612d648418462758f30bf5a46_Out_2;
            Unity_Add_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2, _Add_d86e51a612d648418462758f30bf5a46_Out_2);
            float4 _UV_14cd8fc313be473da647d5d3c819522b_Out_0 = IN.uv0;
            float _Split_524956ebfd064436818e66014c0bb66b_R_1 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[0];
            float _Split_524956ebfd064436818e66014c0bb66b_G_2 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[1];
            float _Split_524956ebfd064436818e66014c0bb66b_B_3 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[2];
            float _Split_524956ebfd064436818e66014c0bb66b_A_4 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[3];
            float _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3;
            Unity_Lerp_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Add_d86e51a612d648418462758f30bf5a46_Out_2, _Split_524956ebfd064436818e66014c0bb66b_G_2, _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3);
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_R_1 = IN.WorldSpacePosition[0];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2 = IN.WorldSpacePosition[1];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3 = IN.WorldSpacePosition[2];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_A_4 = 0;
            float4 _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4;
            float3 _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5;
            float2 _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6;
            Unity_Combine_float(_Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3, _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2, _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3, 0, _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4, _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5, _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6);
            float3 _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1 = TransformWorldToObject(_Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5.xyz);
            description.Position = _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.WorldSpaceTangent =                          TransformObjectToWorldDir(input.tangentOS.xyz);
            output.ObjectSpaceBiTangent =                       normalize(cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w > 0.0f ? 1.0f : -1.0f) * GetOddNegativeScale());
            output.WorldSpaceBiTangent =                        TransformObjectToWorldDir(output.ObjectSpaceBiTangent);
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.uv0 =                                        input.uv0;
            output.TimeParameters =                             _TimeParameters.xyz;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
            // FragInputs from VFX come from two places: Interpolator or CBuffer.
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "ScenePickingPass"
            Tags
            {
                "LightMode" = "Picking"
            }
        
        // Render State
        Cull Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma only_renderers gles gles3 glcore d3d11
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHONLY
        #define SCENEPICKINGPASS 1
        #define ALPHA_CLIP_THRESHOLD 1
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 WorldSpaceTangent;
             float3 ObjectSpaceBiTangent;
             float3 WorldSpaceBiTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float4 uv0;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1_TexelSize;
        float3 _position;
        float _WindShiftStreth;
        float _WindSpeed;
        float _WindStrenth;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        SAMPLER(sampler_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        
        inline float Unity_SimpleNoise_RandomValue_float (float2 uv)
        {
            float angle = dot(uv, float2(12.9898, 78.233));
            #if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN))
                // 'sin()' has bad precision on Mali GPUs for inputs > 10000
                angle = fmod(angle, TWO_PI); // Avoid large inputs to sin()
            #endif
            return frac(sin(angle)*43758.5453);
        }
        
        inline float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t)
        {
            return (1.0-t)*a + (t*b);
        }
        
        
        inline float Unity_SimpleNoise_ValueNoise_float (float2 uv)
        {
            float2 i = floor(uv);
            float2 f = frac(uv);
            f = f * f * (3.0 - 2.0 * f);
        
            uv = abs(frac(uv) - 0.5);
            float2 c0 = i + float2(0.0, 0.0);
            float2 c1 = i + float2(1.0, 0.0);
            float2 c2 = i + float2(0.0, 1.0);
            float2 c3 = i + float2(1.0, 1.0);
            float r0 = Unity_SimpleNoise_RandomValue_float(c0);
            float r1 = Unity_SimpleNoise_RandomValue_float(c1);
            float r2 = Unity_SimpleNoise_RandomValue_float(c2);
            float r3 = Unity_SimpleNoise_RandomValue_float(c3);
        
            float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
            float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
            float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
            return t;
        }
        void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
        {
            float t = 0.0;
        
            float freq = pow(2.0, float(0));
            float amp = pow(0.5, float(3-0));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(1));
            amp = pow(0.5, float(3-1));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(2));
            amp = pow(0.5, float(3-2));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            Out = t;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void TriangleWave_float(float In, out float Out)
        {
            Out = 2.0 * abs( 2 * (In - floor(0.5 + In)) ) - 1.0;
        }
        
        void Unity_Lerp_float(float A, float B, float T, out float Out)
        {
            Out = lerp(A, B, T);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Split_4943b3735a0e4ba5819db29ad339111a_R_1 = IN.WorldSpacePosition[0];
            float _Split_4943b3735a0e4ba5819db29ad339111a_G_2 = IN.WorldSpacePosition[1];
            float _Split_4943b3735a0e4ba5819db29ad339111a_B_3 = IN.WorldSpacePosition[2];
            float _Split_4943b3735a0e4ba5819db29ad339111a_A_4 = 0;
            float _Split_1df4c052b12745db8c3ab81a1115c945_R_1 = IN.WorldSpacePosition[0];
            float _Split_1df4c052b12745db8c3ab81a1115c945_G_2 = IN.WorldSpacePosition[1];
            float _Split_1df4c052b12745db8c3ab81a1115c945_B_3 = IN.WorldSpacePosition[2];
            float _Split_1df4c052b12745db8c3ab81a1115c945_A_4 = 0;
            float4 _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4;
            float3 _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5;
            float2 _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6;
            Unity_Combine_float(_Split_1df4c052b12745db8c3ab81a1115c945_R_1, _Split_1df4c052b12745db8c3ab81a1115c945_G_2, 0, 0, _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4, _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5, _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6);
            float _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2;
            Unity_SimpleNoise_float(_Combine_c8a8a8a174fe417389fcb951c422011a_RG_6, 500, _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2);
            float _Property_f5800ee87a11435ba90f964e3635c567_Out_0 = _WindShiftStreth;
            float _Multiply_44b7a7abd320474289777104102625d7_Out_2;
            Unity_Multiply_float_float(_SimpleNoise_e8723a906e95458096db370034b18abe_Out_2, _Property_f5800ee87a11435ba90f964e3635c567_Out_0, _Multiply_44b7a7abd320474289777104102625d7_Out_2);
            float _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0 = _WindSpeed;
            float _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2);
            float _Add_5236d4953a3147c88bc756d35f145c51_Out_2;
            Unity_Add_float(_Multiply_44b7a7abd320474289777104102625d7_Out_2, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2, _Add_5236d4953a3147c88bc756d35f145c51_Out_2);
            float _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1;
            TriangleWave_float(_Add_5236d4953a3147c88bc756d35f145c51_Out_2, _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1);
            float _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0 = _WindStrenth;
            float _Multiply_642789de8d1c4560848f311ee76485d0_Out_2;
            Unity_Multiply_float_float(_TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1, _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2);
            float _Add_d86e51a612d648418462758f30bf5a46_Out_2;
            Unity_Add_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2, _Add_d86e51a612d648418462758f30bf5a46_Out_2);
            float4 _UV_14cd8fc313be473da647d5d3c819522b_Out_0 = IN.uv0;
            float _Split_524956ebfd064436818e66014c0bb66b_R_1 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[0];
            float _Split_524956ebfd064436818e66014c0bb66b_G_2 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[1];
            float _Split_524956ebfd064436818e66014c0bb66b_B_3 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[2];
            float _Split_524956ebfd064436818e66014c0bb66b_A_4 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[3];
            float _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3;
            Unity_Lerp_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Add_d86e51a612d648418462758f30bf5a46_Out_2, _Split_524956ebfd064436818e66014c0bb66b_G_2, _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3);
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_R_1 = IN.WorldSpacePosition[0];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2 = IN.WorldSpacePosition[1];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3 = IN.WorldSpacePosition[2];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_A_4 = 0;
            float4 _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4;
            float3 _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5;
            float2 _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6;
            Unity_Combine_float(_Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3, _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2, _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3, 0, _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4, _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5, _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6);
            float3 _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1 = TransformWorldToObject(_Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5.xyz);
            description.Position = _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.WorldSpaceTangent =                          TransformObjectToWorldDir(input.tangentOS.xyz);
            output.ObjectSpaceBiTangent =                       normalize(cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w > 0.0f ? 1.0f : -1.0f) * GetOddNegativeScale());
            output.WorldSpaceBiTangent =                        TransformObjectToWorldDir(output.ObjectSpaceBiTangent);
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.uv0 =                                        input.uv0;
            output.TimeParameters =                             _TimeParameters.xyz;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
            // FragInputs from VFX come from two places: Interpolator or CBuffer.
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            // Name: <None>
            Tags
            {
                "LightMode" = "Universal2D"
            }
        
        // Render State
        Cull Off
        Blend One Zero
        ZTest LEqual
        ZWrite On
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma only_renderers gles gles3 glcore d3d11
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define VARYINGS_NEED_TEXCOORD0
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_2D
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 WorldSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 WorldSpaceTangent;
             float3 ObjectSpaceBiTangent;
             float3 WorldSpaceBiTangent;
             float3 ObjectSpacePosition;
             float3 WorldSpacePosition;
             float4 uv0;
             float3 TimeParameters;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float4 interp0 : INTERP0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.interp0.xyzw =  input.texCoord0;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.texCoord0 = input.interp0.xyzw;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        //CBUFFER_START(UnityPerMaterial)
        float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1_TexelSize;
        float3 _position;
        float _WindShiftStreth;
        float _WindSpeed;
        float _WindStrenth;
        //CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        SAMPLER(sampler_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        
        inline float Unity_SimpleNoise_RandomValue_float (float2 uv)
        {
            float angle = dot(uv, float2(12.9898, 78.233));
            #if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) || defined(SHADER_API_VULKAN))
                // 'sin()' has bad precision on Mali GPUs for inputs > 10000
                angle = fmod(angle, TWO_PI); // Avoid large inputs to sin()
            #endif
            return frac(sin(angle)*43758.5453);
        }
        
        inline float Unity_SimpleNnoise_Interpolate_float (float a, float b, float t)
        {
            return (1.0-t)*a + (t*b);
        }
        
        
        inline float Unity_SimpleNoise_ValueNoise_float (float2 uv)
        {
            float2 i = floor(uv);
            float2 f = frac(uv);
            f = f * f * (3.0 - 2.0 * f);
        
            uv = abs(frac(uv) - 0.5);
            float2 c0 = i + float2(0.0, 0.0);
            float2 c1 = i + float2(1.0, 0.0);
            float2 c2 = i + float2(0.0, 1.0);
            float2 c3 = i + float2(1.0, 1.0);
            float r0 = Unity_SimpleNoise_RandomValue_float(c0);
            float r1 = Unity_SimpleNoise_RandomValue_float(c1);
            float r2 = Unity_SimpleNoise_RandomValue_float(c2);
            float r3 = Unity_SimpleNoise_RandomValue_float(c3);
        
            float bottomOfGrid = Unity_SimpleNnoise_Interpolate_float(r0, r1, f.x);
            float topOfGrid = Unity_SimpleNnoise_Interpolate_float(r2, r3, f.x);
            float t = Unity_SimpleNnoise_Interpolate_float(bottomOfGrid, topOfGrid, f.y);
            return t;
        }
        void Unity_SimpleNoise_float(float2 UV, float Scale, out float Out)
        {
            float t = 0.0;
        
            float freq = pow(2.0, float(0));
            float amp = pow(0.5, float(3-0));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(1));
            amp = pow(0.5, float(3-1));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            freq = pow(2.0, float(2));
            amp = pow(0.5, float(3-2));
            t += Unity_SimpleNoise_ValueNoise_float(float2(UV.x*Scale/freq, UV.y*Scale/freq))*amp;
        
            Out = t;
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void TriangleWave_float(float In, out float Out)
        {
            Out = 2.0 * abs( 2 * (In - floor(0.5 + In)) ) - 1.0;
        }
        
        void Unity_Lerp_float(float A, float B, float T, out float Out)
        {
            Out = lerp(A, B, T);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            float _Split_4943b3735a0e4ba5819db29ad339111a_R_1 = IN.WorldSpacePosition[0];
            float _Split_4943b3735a0e4ba5819db29ad339111a_G_2 = IN.WorldSpacePosition[1];
            float _Split_4943b3735a0e4ba5819db29ad339111a_B_3 = IN.WorldSpacePosition[2];
            float _Split_4943b3735a0e4ba5819db29ad339111a_A_4 = 0;
            float _Split_1df4c052b12745db8c3ab81a1115c945_R_1 = IN.WorldSpacePosition[0];
            float _Split_1df4c052b12745db8c3ab81a1115c945_G_2 = IN.WorldSpacePosition[1];
            float _Split_1df4c052b12745db8c3ab81a1115c945_B_3 = IN.WorldSpacePosition[2];
            float _Split_1df4c052b12745db8c3ab81a1115c945_A_4 = 0;
            float4 _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4;
            float3 _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5;
            float2 _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6;
            Unity_Combine_float(_Split_1df4c052b12745db8c3ab81a1115c945_R_1, _Split_1df4c052b12745db8c3ab81a1115c945_G_2, 0, 0, _Combine_c8a8a8a174fe417389fcb951c422011a_RGBA_4, _Combine_c8a8a8a174fe417389fcb951c422011a_RGB_5, _Combine_c8a8a8a174fe417389fcb951c422011a_RG_6);
            float _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2;
            Unity_SimpleNoise_float(_Combine_c8a8a8a174fe417389fcb951c422011a_RG_6, 500, _SimpleNoise_e8723a906e95458096db370034b18abe_Out_2);
            float _Property_f5800ee87a11435ba90f964e3635c567_Out_0 = _WindShiftStreth;
            float _Multiply_44b7a7abd320474289777104102625d7_Out_2;
            Unity_Multiply_float_float(_SimpleNoise_e8723a906e95458096db370034b18abe_Out_2, _Property_f5800ee87a11435ba90f964e3635c567_Out_0, _Multiply_44b7a7abd320474289777104102625d7_Out_2);
            float _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0 = _WindSpeed;
            float _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2;
            Unity_Multiply_float_float(IN.TimeParameters.x, _Property_2fdd8dbe78ae4b2297bbf6b34f703ee0_Out_0, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2);
            float _Add_5236d4953a3147c88bc756d35f145c51_Out_2;
            Unity_Add_float(_Multiply_44b7a7abd320474289777104102625d7_Out_2, _Multiply_f0c1b377597e47f8849b704f4ecc2c7c_Out_2, _Add_5236d4953a3147c88bc756d35f145c51_Out_2);
            float _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1;
            TriangleWave_float(_Add_5236d4953a3147c88bc756d35f145c51_Out_2, _TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1);
            float _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0 = _WindStrenth;
            float _Multiply_642789de8d1c4560848f311ee76485d0_Out_2;
            Unity_Multiply_float_float(_TriangleWave_01dcadccee7b4ab48234b68b75f445c4_Out_1, _Property_43624b326f4e4dc3acb9ce5a3902fc30_Out_0, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2);
            float _Add_d86e51a612d648418462758f30bf5a46_Out_2;
            Unity_Add_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Multiply_642789de8d1c4560848f311ee76485d0_Out_2, _Add_d86e51a612d648418462758f30bf5a46_Out_2);
            float4 _UV_14cd8fc313be473da647d5d3c819522b_Out_0 = IN.uv0;
            float _Split_524956ebfd064436818e66014c0bb66b_R_1 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[0];
            float _Split_524956ebfd064436818e66014c0bb66b_G_2 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[1];
            float _Split_524956ebfd064436818e66014c0bb66b_B_3 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[2];
            float _Split_524956ebfd064436818e66014c0bb66b_A_4 = _UV_14cd8fc313be473da647d5d3c819522b_Out_0[3];
            float _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3;
            Unity_Lerp_float(_Split_4943b3735a0e4ba5819db29ad339111a_R_1, _Add_d86e51a612d648418462758f30bf5a46_Out_2, _Split_524956ebfd064436818e66014c0bb66b_G_2, _Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3);
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_R_1 = IN.WorldSpacePosition[0];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2 = IN.WorldSpacePosition[1];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3 = IN.WorldSpacePosition[2];
            float _Split_c06ebe7f7dc047569bab75f6118dfd21_A_4 = 0;
            float4 _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4;
            float3 _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5;
            float2 _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6;
            Unity_Combine_float(_Lerp_c43e6d18f15949c091a14ef7b88e7ad7_Out_3, _Split_c06ebe7f7dc047569bab75f6118dfd21_G_2, _Split_c06ebe7f7dc047569bab75f6118dfd21_B_3, 0, _Combine_078877d25bd84b1cbf114d348ca3835b_RGBA_4, _Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5, _Combine_078877d25bd84b1cbf114d348ca3835b_RG_6);
            float3 _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1 = TransformWorldToObject(_Combine_078877d25bd84b1cbf114d348ca3835b_RGB_5.xyz);
            description.Position = _Transform_965ac45e5e3e4ca18f4cdf179d8ec7bd_Out_1;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0 = SAMPLE_TEXTURE2D(UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).tex, UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).samplerstate, UnityBuildTexture2DStructNoScale(_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_Texture_1).GetTransformedUV(IN.uv0.xy));
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_R_4 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.r;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_G_5 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.g;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_B_6 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.b;
            float _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_A_7 = _SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.a;
            surface.BaseColor = (_SampleTexture2D_1d16ba9869724f8187d40c4501176aed_RGBA_0.xyz);
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.WorldSpaceNormal =                           TransformObjectToWorldNormal(input.normalOS);
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.WorldSpaceTangent =                          TransformObjectToWorldDir(input.tangentOS.xyz);
            output.ObjectSpaceBiTangent =                       normalize(cross(input.normalOS, input.tangentOS.xyz) * (input.tangentOS.w > 0.0f ? 1.0f : -1.0f) * GetOddNegativeScale());
            output.WorldSpaceBiTangent =                        TransformObjectToWorldDir(output.ObjectSpaceBiTangent);
            output.ObjectSpacePosition =                        input.positionOS;
            output.WorldSpacePosition =                         TransformObjectToWorld(input.positionOS);
            output.uv0 =                                        input.uv0;
            output.TimeParameters =                             _TimeParameters.xyz;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
            // FragInputs from VFX come from two places: Interpolator or CBuffer.
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
            output.uv0 = input.texCoord0;
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/PBR2DPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
    }
    CustomEditorForRenderPipeline "UnityEditor.ShaderGraphLitGUI" "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}