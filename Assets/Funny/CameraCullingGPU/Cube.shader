Shader "Unlit/Cube"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling procedural:ConfigureProcedural

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma shader_feature _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "HLSLSupport.cginc"
            #include "../RayMarchingShader/HLSL/RandomTools.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"



            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal:NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                float4 vertex : SV_POSITION;
                 UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;

            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
			StructuredBuffer<float4> _Positions;
            Buffer<int> _Res;
		    #endif

            void ConfigureProcedural () 
            {
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    float4 position = _Positions[unity_InstanceID];
                    
                    unity_ObjectToWorld = 0.0;
                    unity_ObjectToWorld._m03_m13_m23_m33 = position;
                    unity_ObjectToWorld._m00_m11_m22 = 0.85;

			    #endif
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 worldPos = TransformObjectToWorld(v.vertex.xyz);
                // float c,s;
                // sincos(-PI*0.5, s, c);
                // float2x2 R = float2x2(c, s, -s, c);
                // v.vertex.yz=mul(R,v.vertex.yz);
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normal);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.shadowCoord = TransformWorldToShadowCoord(worldPos);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // sample the texture
                UNITY_SETUP_INSTANCE_ID(i);

                float2 uv = i.uv/4;

                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                int indeX =fmod(unity_InstanceID,50);
                int indeY =(unity_InstanceID-indeX)/50;
                float2 uv2 = float2(indeX,indeY)/50.0;

                float2 rID =hash22(uv2)*0.5+0.5;
               
                float2 offset =float2 (rID.x,rID.y)*4.0;
                uv = i.uv/4+int2(offset)*0.25; 
                #endif
                half4 col = tex2D(_MainTex, uv)*_Color;

                 Light light = GetMainLight();
                float3 lightDir =light.direction;
                float3 lightColor = light.color;  
                float  lightShadowAtten =light.shadowAttenuation;
                float diffuse = max(0,dot(i.normalWS,lightDir))*lightShadowAtten;

                 half shadow = MainLightRealtimeShadow(i.shadowCoord); 
                 half3 ambient = half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);

                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                if (_Res[unity_InstanceID]==1)
                    discard;
                #endif

                return col*diffuse*shadow;
            }
            ENDHLSL
        }


         Pass
        {
            Tags {"LightMode"="ShadowCaster"}

             HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma shader_feature _ALPHATEST_ON

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling procedural:ConfigureProcedural 



            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

             struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal:NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 vertex : SV_POSITION;
                 UNITY_VERTEX_INPUT_INSTANCE_ID
            };


            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;

            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
			StructuredBuffer<float4> _Positions;
            // Buffer<int> _Res;
		    #endif

         
            void ConfigureProcedural () 
            {
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    float4 position = _Positions[unity_InstanceID];

                    unity_ObjectToWorld = 0.0;
                    unity_ObjectToWorld._m03_m13_m23_m33 = position;
                    unity_ObjectToWorld._m00_m11_m22 = 0.85;
			    #endif
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normal);

                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                #if defined(_ALPHATEST_ON)
                half4 col = tex2D(_MainTex, i.uv);
                clip(col.a - 0.01);
                #endif

                return 0;
            }
            ENDHLSL



        }
    }
}
