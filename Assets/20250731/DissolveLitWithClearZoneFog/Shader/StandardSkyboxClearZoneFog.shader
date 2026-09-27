Shader "Custom/LiangZhu/Skybox Cubemap ClearZoneFog"
{
    Properties
    {
        _Tint ("Tint Color", Color) = (0.5, 0.5, 0.5, 0.5)
        [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
        _Rotation ("Rotation", Range(0, 360)) = 0
        [NoScaleOffset] _Tex ("Cubemap (HDR)", Cube) = "grey" {}
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "SphereFogCommon.hlsl"

            samplerCUBE _Tex;
            half4 _Tex_HDR;
            half4 _Tint;
            half _Exposure;
            float _Rotation;
            float _SF_AffectSky;
            float _SF_SkyDistance;

            struct Attributes
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 cubemapDirection : TEXCOORD0;
                float3 worldDirection : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float s, c;
                sincos(radians(_Rotation), s, c);
                float3 rotated = float3(c * input.vertex.x - s * input.vertex.z,
                    input.vertex.y, s * input.vertex.x + c * input.vertex.z);
                output.positionCS = UnityObjectToClipPos(rotated);
                output.cubemapDirection = input.vertex.xyz;
                // Fog follows the world view direction, independently of cubemap rotation.
                output.worldDirection = mul((float3x3)unity_ObjectToWorld, rotated);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 encoded = texCUBE(_Tex, input.cubemapDirection);
                half3 color = DecodeHDR(encoded, _Tex_HDR);
                color *= _Tint.rgb * unity_ColorSpaceDouble.rgb;
                color *= _Exposure;
                if (_SF_AffectSky > 0.5 && _SF_Active > 0.5 && _SF_Weight > 0.0)
                {
                    float3 skyPosition = _WorldSpaceCameraPos.xyz
                        + normalize(input.worldDirection) * _SF_SkyDistance;
                    color = SphereFog_Apply(color, skyPosition);
                }
                return half4(color, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
