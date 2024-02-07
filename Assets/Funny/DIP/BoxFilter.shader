Shader "Unlit/BoxFilter"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _blurStrenth("BlurStrenth",float)=1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZWrite Off

        Pass
        {
            Name "VerticalBlur"
            Tags{"LightMode" = "UniversalForward"}
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
                float4 positionCS : SV_POSITION;
            };

            TEXTURE2D (_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4  _MainTex_TexelSize;

            float _blurStrenth;

            v2f vert (appdata v)
            {
                v2f o;
                o.positionCS = TransformObjectToHClip(v.vertex.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
             
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 col =0;

                int sampleCount =int(2.0*_blurStrenth+1.0);

                for(int y =0; y<sampleCount; y++)
                {
                    float2 offset = float2(0,y-_blurStrenth)*_MainTex_TexelSize.xy;
                    col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv+offset);
                }
                return col/sampleCount;
            }
            ENDHLSL
        }

         Pass
        {
            Name "HorizontalBlur"
            Tags{"LightMode" = "UniversalForward"}
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
                float4 positionCS : SV_POSITION;
            };

            TEXTURE2D (_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4  _MainTex_TexelSize;

            float _blurStrenth;

            v2f vert (appdata v)
            {
                v2f o;
                o.positionCS = TransformObjectToHClip(v.vertex.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
             
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 col =0;

                int sampleCount =int(2*_blurStrenth+1);

                for(int x =0; x<sampleCount; x++)
                {
                    float2 offset = float2(x-_blurStrenth,0)*_MainTex_TexelSize.xy;
                    col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv+offset);
                }
                return col/sampleCount;
            }
            ENDHLSL
        }
    }
}
