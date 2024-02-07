Shader "Unlit/UnderWaterFog"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FogColor ("Fog Color", Color) = (1,0,0,1)
        _FogStart ("Fog Start", float) = 0
        _FogEnd ("Fog End", float) = 100
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _FogColor;
            float _FogEnd,_FogStart;

            CBUFFER_END

            TEXTURE2D(_MainTex);            SAMPLER(sampler_MainTex);


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 scrPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);

                o.scrPos = ComputeScreenPos(o.vertex);
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i):SV_TARGET
            {

                float depth_Raw = SampleSceneDepth( i.scrPos/i.scrPos.w);
                float depth01= Linear01Depth(depth_Raw, _ZBufferParams)*_ProjectionParams.z;

                
                float fogFactor =saturate( (depth01-_FogStart)/(_FogEnd-_FogStart));
                float4 fogColor = _FogColor*fogFactor;

                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.scrPos/i.scrPos.w);

                col = lerp(col,fogColor,fogFactor);

                // lerp(col,fogColor,depth01)

                return col;
            }
            ENDHLSL
        }
    }
}
