Shader "Unlit/SphericalMask"
{
    Properties
    {
        _MainTex ("MainTexture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)

        _EmissionTex ("EmissionTexture", 2D) = "white" {}
        [HDR]_EmissionColor("EmissionColor",Color)= (1,1,1,1)
        _EmissionIntensity("EmissionIntensity",Range(0,100))=5

        _Strenth("Strength",Range(0,100))=5
        // _Position("Position",Vector)=(0,0,0,0)
        // _Radius("Radius",Range(0,100))=5
        // _Softness("Softness",Range(0,100))=5
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
            

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ACES.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 positionCS : SV_POSITION;
            };

            
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Color;
            float _Radius,_Softness;
            float4 _Position;
            float _Strenth;

            float4 _EmissionColor;
            float _EmissionIntensity;
            CBUFFER_END
            
            TEXTURE2D( _MainTex);          SAMPLER(sampler_MainTex);
            TEXTURE2D( _EmissionTex);      SAMPLER(sampler_EmissionTex);

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
              
                return output;
            }

            float rgb_2_grey(float3 rgb)
            {
                return dot(rgb, float3(0.305306011, 0.682171111, 0.012522878));
            }

            half4 frag (Varyings input) : SV_Target
            {
                half4 col=0;

                half4 emission = SAMPLE_TEXTURE2D(_EmissionTex,sampler_EmissionTex, input.uv)*_EmissionColor;
                half4 col_origin = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex, input.uv)*_Color;
                half4 emissionColor =col_origin+ emission*_EmissionIntensity;


                half4 col_gray = rgb_2_grey(col_origin.rgb);

                float dist = distance(input.positionWS,_Position.xyz);
                //float lerpFactor = smoothstep(_Radius,_Radius-_Softness,dist);
                float lerpFactor = saturate((dist-_Radius)/-_Softness);

                col = lerp(col_gray,emissionColor*_Strenth,lerpFactor);


                return col;
            }
            ENDHLSL
        }
    }
}
