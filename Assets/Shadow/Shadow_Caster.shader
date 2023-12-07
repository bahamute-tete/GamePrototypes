Shader "Unlit/Shadow_Caster"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (RGB) Smoothness / Alpha (A)", 2D) = "white" {}
        [MainColor]   _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        _Cutoff("Alpha Clipping", Range(0.0, 1.0)) = 0.5

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _SpecColor("Specular Color", Color) = (0.5, 0.5, 0.5, 0.5)
        _SpecGlossMap("Specular Map", 2D) = "white" {}
        _SmoothnessSource("Smoothness Source", Float) = 0.0
        _SpecularHighlights("Specular Highlights", Float) = 1.0

        [HideInInspector] _BumpScale("Scale", Float) = 1.0
        [NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}

        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0)
        [NoScaleOffset]_EmissionMap("Emission Map", 2D) = "white" {}

        // Blending state
        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0

        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
        // Editmode props
        _QueueOffset("Queue offset", Float) = 0.0

        // ObsoleteProperties
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Shininess("Smoothness", Float) = 0.0
        [HideInInspector] _GlossinessSource("GlossinessSource", Float) = 0.0
        [HideInInspector] _SpecSource("SpecularHighlights", Float) = 0.0

        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
        [Toggle]_ALPHATEST("AlphaTest",float)=0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "SimpleLit" "IgnoreProjector" = "True" "ShaderModel"="2.0"}
        

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            LOD 100
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling procedural:ConfigureProcedural 

            #define _SPECULAR_SETUP
            #define _EMISSION
            #define _SPECGLOSSMAP _SPECULAR_COLOR
            #define _SPECULARHIGHLIGHTS_OFF


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitForwardPass.hlsl"




            UNITY_INSTANCING_BUFFER_START(unityInstancing)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)

            UNITY_INSTANCING_BUFFER_END(unityInstancing)
      

            half _Smoothness;
            float3 _LightDirection;

            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
            StructuredBuffer<float3> positionBuffer;
            StructuredBuffer<float4x4> matrixBuffer;
            float _step;
            #endif
            
            float TriangleWave(float In)
            {
                float  Out = abs( 2 * (In - floor(0.5 + In)));
                Out =min( abs(0.25*In - floor(0.5 + 0.25*In))+0.5,1.0);
                return Out;
            }

            float TriangleWave2(float In)
            {
                float  Out = 2*abs( 2 * (In - floor(0.5 + In)))-1.0;
                return Out;
            }

            float2x2 Rot (float a)
            {

                return float2x2(cos(a), sin(a), -sin(a), cos(a));
            }

            float hash(float2 p)
            {

                float3 p3  = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 19.19);
                return frac((p3.x + p3.y) * p3.z);
            }

            void ConfigureProcedural()
            {
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                float3 positions = positionBuffer[unity_InstanceID];
/*
                unity_ObjectToWorld =0;
                unity_ObjectToWorld._m03_m13_m23_m33 =float4(positions,1);
                unity_ObjectToWorld._m00_m11_m22= _step;
                */

              
    
               float ID =hash(floor(float2(unity_InstanceID,unity_InstanceID)));

                unity_ObjectToWorld._m00_m11_m22=TriangleWave((_Time.y+ID*4.0));
                unity_ObjectToWorld._m03_m13_m23_m33 =
                    float4(TriangleWave2((_Time.y+ID)),TriangleWave2((_Time.y+ID)),TriangleWave2((_Time.y+ID)),1);
                unity_ObjectToWorld =mul(matrixBuffer[unity_InstanceID],unity_ObjectToWorld);
                #endif
              
            }


            

            Varyings vert (Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                UNITY_TRANSFER_INSTANCE_ID(input, output);

                //output.positionWS = TransformObjectToWorld(input.positionOS.xyz);

               float3 centerPos = TransformObjectToWorld(0);
               float ID =hash(floor(centerPos.xz));
                
               //input.positionOS.xz =mul( Rot(5.*_Time.y*ID),input.positionOS.xz);
               //output.positionCS = TransformObjectToHClip(input.vertex.xyz);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);


                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionWS.xyz = vertexInput.positionWS;

                output.positionCS = vertexInput.positionCS;
                output.normalWS = NormalizeNormalPerVertex(normalInput.normalWS);

                OUTPUT_SH(output.normalWS.xyz, output.vertexSH);
                output.fogFactor = fogFactor;

                return output;
            }




            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                SurfaceData surfaceData;
                InitializeSimpleLitSurfaceData(input.uv, surfaceData);

                InputData inputData;
                InitializeInputData(input, surfaceData.normalTS, inputData);
                SETUP_DEBUG_TEXTURE_DATA(inputData, input.uv, _BaseMap);


                half4 color = UniversalFragmentBlinnPhong(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = OutputAlpha(color.a, _Surface);



                return color;
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
                float4 positionOS : POSITION;
                float4 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : NORMAL;
                float3 positionWS: TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 _LightDirection;
            float4 _ShadowBias; // x: depth bias, y: normal bias
            half4 _MainLightShadowParams;
            sampler2D _MainTex;

            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
            StructuredBuffer<float3> positionBuffer;
            StructuredBuffer<float4x4> matrixBuffer;
            float _step;
            #endif

                float TriangleWave(float In)
            {
                float  Out = abs( 2 * (In - floor(0.5 + In)));
                 Out =min( abs(0.25*In - floor(0.5 + 0.25*In))+0.5,1.0);
                return Out;
            }

            float TriangleWave2(float In)
            {
                float  Out = 2*abs( 2 * (In - floor(0.5 + In)))-1.0;
                return Out;
            }

             float2x2 Rot (float a)
            {

                return float2x2(cos(a), sin(a), -sin(a), cos(a));
            }

                float hash(float2 p)
            {

                float3 p3  = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 19.19);
                return frac((p3.x + p3.y) * p3.z);
            }

             void ConfigureProcedural()
            {
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                float3 positions = positionBuffer[unity_InstanceID];

                //unity_ObjectToWorld =0;
                //unity_ObjectToWorld._m03_m13_m23_m33 =float4(positions,1);
                //unity_ObjectToWorld._m00_m11_m22= _step;
                float ID =hash(floor(float2(unity_InstanceID,unity_InstanceID)));

                unity_ObjectToWorld._m00_m11_m22=TriangleWave((_Time.y+ID*4.0));
                unity_ObjectToWorld._m03_m13_m23_m33 =
                    float4(TriangleWave2((_Time.y+ID)),TriangleWave2((_Time.y+ID)),TriangleWave2((_Time.y+ID)),1);
                unity_ObjectToWorld =mul(matrixBuffer[unity_InstanceID],unity_ObjectToWorld);
                #endif
              
            }

           

            float3 ApplyShadowBias(float3 positionWS, float3 normalWS, float3 lightDirection)
            {
                float invNdotL = 1.0 - saturate(dot(lightDirection, normalWS));
                float scale = invNdotL * _ShadowBias.y;

                // normal bias is negative since we want to apply an inset normal offset
                positionWS = lightDirection * _ShadowBias.xxx + positionWS;
                positionWS = normalWS * scale.xxx + positionWS;
                return positionWS;
            }

            v2f vert (appdata v)
            {
                v2f o = (v2f)0;
                UNITY_SETUP_INSTANCE_ID(v);

                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float3 wpos = TransformObjectToWorld(0);
                float ID =hash(floor(wpos.xz));
                //v.positionOS.xz =mul( Rot(_Time.y*ID),v.positionOS.xz);

                float3 worldPos = TransformObjectToWorld(v.positionOS.xyz);
                half3 normalWS = TransformObjectToWorldNormal(v.normalOS.xyz);
                worldPos = ApplyShadowBias(worldPos, normalWS, _LightDirection);

                o.positionCS = TransformWorldToHClip(worldPos);

                #if UNITY_REVERSED_Z
                o.positionCS.z = min(o.positionCS.z, o.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                o.positionCS.z = max(o.positionCS.z, o.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                o.uv= v.uv;
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

       //UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
