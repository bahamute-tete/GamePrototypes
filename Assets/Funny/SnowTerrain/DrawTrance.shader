Shader "Unlit/DrawTrance"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Texcoord ("Texcoord", Vector) = (0,0,0,0)
        _Color ("Color", Color) = (1,1,1,1)
        _BrushSize("BrushSize",Range(1,50)) =50
        _BrushStrenth("BrushStrenth",Range(0.0,1)) =0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
         

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
             
                float4 vertex : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Texcoord,_Color;
            float _BrushSize,_BrushStrenth;
            CBUFFER_END

            TEXTURE2D(_MainTex);            SAMPLER(sampler_MainTex);

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
               
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {

                half4 col = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex, i.uv);
                
                //float draw =smoothstep(_BrushSize,-_BrushSize ,length(i.uv-_Texcoord.xy));
                float draw =pow(saturate(1-length(i.uv-_Texcoord.xy)),1500/_BrushSize);
                float4 drawColor = _Color*draw*_BrushStrenth;
                return saturate(col+drawColor);
            }
            ENDHLSL
        }
    }
}
