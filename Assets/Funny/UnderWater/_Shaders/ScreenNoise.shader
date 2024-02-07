Shader "Unlit/ScreenNoise"
{
    Properties
    {
        [MainTexture] _MainTex("Albedo", 2D) = "white" {}

        _NoiseScale("NoiseScale",float)=1
        _NoiseFrequency("NoiseFrequency",float)=1
        _NoiseOffset("NoiseOffset",Vector)=(0,0,0,0)
        _NoiseSpeed("NoiseSpeed",Float)=1.0

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

            #include "../_Shaders/noiseSimplex.cginc"

            CBUFFER_START(UnityPerMaterial)
            float _NoiseScale,_NoiseFrequency,_NoiseSpeed;
            float4 _NoiseOffset;
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

            half4 frag (v2f i):COLOR
            {

                float depth_Raw = SampleSceneDepth( i.scrPos/i.scrPos.w);
                float depth01= Linear01Depth(depth_Raw, _ZBufferParams)*_ProjectionParams.z;

                float depthValue =1-saturate( (depth01-_FogStart)/(_FogEnd-_FogStart));
               


                float3 screenPos = float3(i.scrPos.xy, 0.0) * _NoiseFrequency;
                screenPos.z += _Time.x*_NoiseSpeed;

                float noise = _NoiseScale*(snoise(screenPos)*0.5+0.5);

                float c,s;
                sincos(noise*6.2831853,s,c);

                float2 noiseDirections =normalize( float2(c, s));

                float2 uv = (i.scrPos.xy/i.scrPos.w);

                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv+noiseDirections*0.005*depthValue);

                return col;
            }
            ENDHLSL
        }
    }
}
