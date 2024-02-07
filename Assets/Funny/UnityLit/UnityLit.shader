Shader "Unlit/UnityLit"
{
    Properties
    {


        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}

        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMap("Specular", 2D) = "white" {}

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}


        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}

        // [HDR] _EmissionColor("Color", Color) = (0,0,0)
        // _EmissionMap("Emission", 2D) = "white" {}

        // _DetailMask("Detail Mask", 2D) = "white" {}
        // _DetailAlbedoMapScale("Scale", Range(0.0, 2.0)) = 1.0
        // _DetailAlbedoMap("Detail Albedo x2", 2D) = "linearGrey" {}
        // _DetailNormalMapScale("Scale", Range(0.0, 2.0)) = 1.0
        // [Normal] _DetailNormalMap("Normal Map", 2D) = "bump" {}

        // // SRP batching compatibility for Clear Coat (Not used in Lit)
        // [HideInInspector] _ClearCoatMask("_ClearCoatMask", Float) = 0.0
        // [HideInInspector] _ClearCoatSmoothness("_ClearCoatSmoothness", Float) = 0.0

        // // Blending state
        // _Surface("__surface", Float) = 0.0
        // _Blend("__blend", Float) = 0.0
        // _Cull("__cull", Float) = 2.0
        // [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        // [HideInInspector] _SrcBlend("__src", Float) = 1.0
        // [HideInInspector] _DstBlend("__dst", Float) = 0.0
        // [HideInInspector] _ZWrite("__zw", Float) = 1.0

        // [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
        // // Editmode props
        // _QueueOffset("Queue offset", Float) = 0.0

        // // ObsoleteProperties
        // [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        // [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        // [HideInInspector] _GlossMapScale("Smoothness", Float) = 0.0
        // [HideInInspector] _Glossiness("Smoothness", Float) = 0.0
        // [HideInInspector] _GlossyReflections("EnvironmentReflections", Float) = 0.0

        // [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        // [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        // [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags{"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "IgnoreProjector" = "True" "ShaderModel"="4.5"}
        LOD 300

        Pass
        {
           
            Name "ForwardLit"
            Tags{"LightMode" = "UniversalForward"}

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            //#pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"


            // #if (defined(_NORMALMAP) || (defined(_PARALLAXMAP) && !defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR))) || defined(_DETAIL)
            // #define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
            // #endif
            #define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 texcoord     : TEXCOORD0;

            };

            struct Varyings
            {
                float2 uv                       : TEXCOORD0;
            
            #if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
                float3 positionWS               : TEXCOORD1;
            #endif
            
                float3 normalWS                 : TEXCOORD2;
            #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
                half4 tangentWS                : TEXCOORD3;    // xyz: tangent, w: sign
            #endif
                float3 viewDirWS                : TEXCOORD4;
                float4 positionCS               : SV_POSITION;

            };


void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    inputData.positionWS = input.positionWS;
#endif

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

#if defined(_NORMALMAP)
    float sgn = input.tangentWS.w;      // should be either +1 or -1
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);

    #if defined(_NORMALMAP)
    inputData.tangentToWorld = tangentToWorld;
    #endif

    inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
#else
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

}

        // Used in Standard (Physically Based) shader
        Varyings LitPassVertex(Attributes input)
        {
            Varyings output = (Varyings)0;

            VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

            output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);

            output.normalWS = normalInput.normalWS;

        #if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
            real sign = input.tangentOS.w * GetOddNegativeScale();
            half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
            output.tangentWS = tangentWS;
            output.positionWS = vertexInput.positionWS;
        #endif


            output.positionCS = vertexInput.positionCS;

            return output;
        }

// Used in Standard (Physically Based) shader
        half4 LitPassFragment(Varyings input) : SV_Target
        {


            SurfaceData surfaceData;
            InitializeStandardLitSurfaceData(input.uv, surfaceData);

            InputData inputData;
            InitializeInputData(input, surfaceData.normalTS, inputData);



            half4 color = UniversalFragmentPBR(inputData, surfaceData);

            return color;
        }
            ENDHLSL
        }
    }

}
