Shader "Unlit/TestShader"
{


     Properties
    {
        _TargetColor ("Target Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "CustomColorPass"
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TargetColor;
            CBUFFER_END

            #ifdef UNITY_DOTS_INSTANCING_ENABLED

            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
            UNITY_DOTS_INSTANCED_PROP(float4, _TargetColor)
            UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

            #endif

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings i) : SV_Target
            {
                 #ifdef UNITY_DOTS_INSTANCING_ENABLED
                    UNITY_SETUP_INSTANCE_ID(i);
                    return UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT( float4, _TargetColor );
                #else
                    return _TargetColor;
                #endif
            }

           
            ENDHLSL
         }
       
    }
    
}
