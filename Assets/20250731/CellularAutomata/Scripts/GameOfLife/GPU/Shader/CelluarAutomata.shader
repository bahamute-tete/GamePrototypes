Shader "Custom/CelluarAutomata"
{
    Properties
    {
        _AliveColor ("Alive Color", Color) = (1,1,1,1)
        _DeadColor ("Dead Color", Color) = (0,0,0,1)
        _StateTexture ("State Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalRenderPipeline" }
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_StateTexture);
            SAMPLER(sampler_StateTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _AliveColor;
                float4 _DeadColor;
                float4 _StateTexture_ST;
            CBUFFER_END

            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                StructuredBuffer<float3> _PositionBuffer;
                StructuredBuffer<float2> _UVBuffer;
            #endif

            void setup()
            {
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    float3 instancePosition = _PositionBuffer[unity_InstanceID];
                    //unity_ObjectToWorld._m03_m13_m23_m33 = float4(instancePosition, 1);
                    float scale = 0.9;
                    unity_ObjectToWorld = float4x4(
                                                    scale, 0,  0, instancePosition.x,
                                                    0, 0, -scale, instancePosition.y,
                                                    0, scale,  0, instancePosition.z,
                                                    0, 0,  0, 1
                    );

                    float invScale = 1.0 / scale;
                    unity_WorldToObject = float4x4(
                        invScale, 0,         0,        -instancePosition.x * invScale,
                        0,        0,         invScale, -instancePosition.z * invScale,
                        0,        -invScale, 0,         instancePosition.y * invScale,
                        0,        0,         0,         1
                    );
                #endif
            }

            Varyings vert(Attributes v, uint instanceID : SV_InstanceID)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    o.uv = _UVBuffer[instanceID];
                #else
                    o.uv = float2(0, 0);
                #endif

                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float state = SAMPLE_TEXTURE2D(_StateTexture, sampler_StateTexture, i.uv).r;
                half4 color = lerp(_DeadColor, _AliveColor, step(0.5, state));

                float3 lightDir = normalize(float3(0.5, 1, 0.5));
                float ndotl = saturate(dot(i.normalWS, lightDir));
                color.rgb *= lerp(0.5, 1.0, ndotl);
                
                return color;
            }
            ENDHLSL
        }
    }
}
