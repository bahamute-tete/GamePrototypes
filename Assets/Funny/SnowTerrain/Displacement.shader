Shader "Unlit/Displacement"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)

        _GroundMap("GroundTexture", 2D) = "white" {}
        _GroundColor("GroundColor", Color) = (1,1,1,1)

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0
        
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}

        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMap("Specular", 2D) = "white" {}

        _NormalMap ("NormalMap", 2D) = "bump" {}
        _NormalScale("NormalScale", Float) = 1.0

        _DisplacementMap("DisplacementMap", 2D) = "white" {}
        _DisplacementStrength("DisplacementStrength", Range(0, 1)) = 0.5

        _TessellationUniform("_TessellationUniform",Range(1,64))=1
        _TessellationEdgeLength("_TessellationEdgeLength",Range(5,100))=5//pixel

        _UVScale("_UVScale",float) = 1

    }
    SubShader
    {
        Tags { "RenderType"="Opaque"  "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "IgnoreProjector" = "True" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags{"LightMode" = "UniversalForward"}
            HLSLPROGRAM

            #pragma target 5.0



            #pragma vertex MyTessellationVertexProgram
            #pragma hull MyHullProgram
            #pragma domain MyDomainProgram
            #pragma fragment frag


            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
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
            

            #define _NORMALMAP

            #include"../SnowTerrain/HLSL/LightInput.hlsl"
            #include"../SnowTerrain/HLSL/Displacement_Input.hlsl"

//////////////////////////////////////////////////////////////////////////////

////////////////////////////////////////////////////////////////////////////// 

 TessellationControlPoint MyTessellationVertexProgram (Attributes input)
 {
     TessellationControlPoint output = (TessellationControlPoint)0;

     VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);   
     VertexNormalInputs  normalInputs = GetVertexNormalInputs(input.normalOS);

     output.uv =  input.uv;
     output.positionOS = input.positionOS;

     output.normalOS= input.normalOS;
     output.normalWS= normalInputs.normalWS;
     output.tangentOS= input.tangentOS;


     half4 tangentWS = half4(normalInputs.tangentWS.xyz, input.tangentOS.w);
     output.tangentWS= tangentWS;

     return output;
 }  

 
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

half4 frag (Varyings input) : SV_Target
{


    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    SurfaceData surfaceData ;
    InitializeStandardLitSurfaceData(input.uv*_UVScale,surfaceData);
    
    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    
    half4 color = UniversalFragmentPBR(inputData, surfaceData);

    half4 groundColor = SampleAlbedoAlpha(input.uv*_UVScale,_GroundMap, sampler_GroundMap)*_GroundColor;


    half4 finalColor = lerp (color, groundColor, input.texlerpFactor);

    // Light light = GetMainLight();
    // float3 lightDir =light.direction;


    // float sgn = input.tangentWS.w;      // should be either +1 or -1
    // float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    // half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
    // inputData.tangentToWorld = tangentToWorld;
    // inputData.normalWS = TransformTangentToWorld(surfaceData.normalTS, tangentToWorld);


    // inputData.normalWS = surfaceData.normalTS;

    // float diffuse = saturate(dot(inputData.normalWS, lightDir));
    // color=diffuse;


    return finalColor;

}
ENDHLSL 
  }
        
 }
}
