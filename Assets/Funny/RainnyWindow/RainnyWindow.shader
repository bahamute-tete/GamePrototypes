Shader "Custom/RainnyWindow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

        _Size("Size",float)= 1
        _T("Time",float) =1
        _Distoration("Distoration",Range(0,1)) = 0
        _Blur("Blur",Range(0,7))=0

        [Header(Keyword)]
        [KeywordEnum(None, ON)] _EFFECT("Glass",float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent" }
        LOD 100

        //Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0

            #pragma shader_feature _ _EFFECT_ON

            #include "Rainny.hlsl"
            ENDHLSL
        }
    }
}
