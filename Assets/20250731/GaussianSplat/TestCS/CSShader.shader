Shader "Unlit/CSShader"
{
    Properties
    {
       _Color ("Color", Color) = (1,1,1,1)
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
           
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv:TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS : NORMAL;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                StructuredBuffer<float3> _PositionBuffer;
            #endif

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            void setup()
            {
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    float3 instancePosition = _PositionBuffer[unity_InstanceID];
                     float scale = 0.1;
                    unity_ObjectToWorld = float4x4(
                                                    scale, 0,     0,     instancePosition.x,
                                                    0,     scale, 0,     instancePosition.y,
                                                    0,     0,     scale, instancePosition.z,
                                                    0,     0,     0,             1
                    );
                #endif
            }


            Varyings vert(Attributes v, uint instanceID : SV_InstanceID)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                
                o.uv = v.uv;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(i.normalWS, mainLight.direction));
                float3 color = _Color.rgb;
                color *= lerp(0.5, 1.0, ndotl);
      
                return float4(color,1);
            }
            ENDHLSL
        }
    }
}
