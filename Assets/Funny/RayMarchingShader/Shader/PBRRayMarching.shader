Shader "RayMarchingShader/PBRRayMarching"
{
    Properties
    {
        [Header(TriplannerMapping)]
        [Space(30)]
        _MainTex ("MainTex", 2D) = "white" {}
        _Color("BaseColor",Color) = (1,1,1,1)
        _MapScale ("Map Scale", Float) = 1
        [NoScaleOffset] _MOSMap ("MOS", 2D) = "white" {}
        [NoScaleOffset] _NormalMap ("Normals", 2D) = "white" {}

        [NoScaleOffset] _TopMainTex ("Top Albedo", 2D) = "white" {}
        [NoScaleOffset] _TopMOHSMap ("Top MOHS", 2D) = "white" {}
        [NoScaleOffset] _TopNormalMap ("Top Normals", 2D) = "white" {}
        // [KeywordEnum(None, ON)] _TRIPLANNAR("Triplanar",float)=0
        // [KeywordEnum(None, ON)] _SEPARATE_TOP_MAPS("TOP_Maps",float)=0

        
        _Metallic("Metallic",Range(0,1))=0.2
        _Roughness("Roughness",Range(0.13,1))=0.3

        //[Header(Test)]
        
        _WrapValue("WrapValue",Range(0,2))=0.5
        _Shiness("Shiness",Range(0,64)) = 16
        _SpecularScaler("SpecularScaler",Range(1,10))=1
        _SSSValue("SSSValue",Range(0,1)) = 0.5
        _PowerValue("PowerValue",Range(1,32)) = 2
        _ScaleValue("ScaleValue",Range(0,3))=1

        [HDR]_GlowColor("GlowColor",Color)=(1,1,1,1)
        _GlowIntensity("GlowIntensity",Range(0,1))=0.5

        [Toggle]_Reflection("Reflection",float)=0
        [Toggle]_FullImage("FullGeometry",float)=0


    }
    SubShader
    {
        Tags { "RenderType"="Opaque"}
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../HLSL/LightShading.hlsl"

            #pragma shader_feature _ _COOK_TORRANCE_GGX
            #pragma shader_feature _ _TRIPLANNAR
            #pragma shader_feature _ _SEPARATE_TOP_MAPS
            #pragma shader_feature _ _RAYMARCHING_REFLECTION
            #pragma shader_feature _ _FULLIMAGE

            
          
            struct vertexData
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct fragmentData
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };



            float3 _CameraPosition;
            float3 _TargetPosition;
        

            fragmentData vert (vertexData input)
            {
                fragmentData o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);

                return o;
            }

            half4 frag (fragmentData input) : SV_Target
            {
                // sample the texture
                half4 col = 0;
                float2 uv = (input.uv-0.5)*_ScreenParams.xy/_ScreenParams.y;

                float3 ro =_WorldSpaceCameraPos;
                float3 target =_TargetPosition;
                float3 rd = mul(float3(uv.x,uv.y,1.0),Camera_matrix(ro,target));

                col.rgb= Shading(ro,rd,uv);
                
                //col.rgb= rd;
                return col;
            }
            ENDHLSL
        }
    }

    CustomEditor "PBRRayMarchingGUI"
}
