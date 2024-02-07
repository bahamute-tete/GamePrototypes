Shader "Unlit/OutLineObject"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        OutlineThickness("OutlineThickness",Range(0,1))=0.1
        DepthSensitivity("DepthSensitivity",Range(0,1))=0.1
        NormalSensitivity("NormalSensitivity",Range(0,1))=0.1

        
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Tags{"LightMode" = "UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            //#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 scrPos:TEXCOORD1;
                float4 vertex : SV_POSITION;
            };


             TEXTURE2D(_MainTex);
             SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);
            float4 _CameraDepthTexture_TexelSize;

            TEXTURE2D(_CameraDepthNormalsTexture);
            SAMPLER(sampler_CameraDepthNormalsTexture);

           

            float OutlineThickness,DepthSensitivity,NormalSensitivity;

            float3 DecodeNormal(float4 enc)
            {
                float kScale = 1.7777;
                float3 nn = enc.xyz*float3(2*kScale,2*kScale,0) + float3(-kScale,-kScale,1);
                float g = 2.0 / dot(nn.xyz,nn.xyz);
                float3 n;
                n.xy = g*nn.xy;
                n.z = g-1;
                return n;
            }


            float EdgeFromDepthAndNormal(float2 uv,float OutlineThickness, float DepthSensitivity,float NormalSensitivity)
            {
                float halfScaleFloor = floor(OutlineThickness * 0.5);
                float halfScaleCeil = ceil(OutlineThickness * 0.5);

                float2 uvSamples[4];
                float depthSamples[4];
                float3 normalSamples[4];

                // Vector4(1 / width, 1 / height, width, height)
                uvSamples[0] = uv -float2(_CameraDepthTexture_TexelSize.x, _CameraDepthTexture_TexelSize.y)*halfScaleFloor;
                uvSamples[1] = uv +float2(_CameraDepthTexture_TexelSize.x, _CameraDepthTexture_TexelSize.y)*halfScaleCeil;
                uvSamples[2] = uv +float2(_CameraDepthTexture_TexelSize.x*halfScaleCeil, -_CameraDepthTexture_TexelSize.y*halfScaleFloor);
                uvSamples[3] = uv +float2(-_CameraDepthTexture_TexelSize.x*halfScaleFloor, _CameraDepthTexture_TexelSize.y*halfScaleCeil);

                for (int i = 0; i < 4; i++)
                {
                    depthSamples[i] = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, uvSamples[i]).r;
                    normalSamples[i]=DecodeNormal(SAMPLE_TEXTURE2D(_CameraDepthNormalsTexture,sampler_CameraDepthNormalsTexture,uvSamples[i]));

                }

                //Depth
                float depthDiff0 =depthSamples[1]-depthSamples[0];
                float depthDiff1 =depthSamples[3]-depthSamples[2];

                float edgeDepth =sqrt(pow(depthDiff0,2)+pow(depthDiff1,2))*100;
                float depthThreshold =(1/DepthSensitivity)*depthSamples[0];
                edgeDepth = edgeDepth > depthThreshold ? 1 : 0;

                //Normal
                float3 normalDiff0 = normalSamples[1]-normalSamples[0];
                float3 normalDiff1 = normalSamples[3]-normalSamples[2];

                float3 edgeNormal=sqrt(dot(normalDiff0,normalDiff0)+dot(normalDiff1,normalDiff1));
                edgeNormal = edgeNormal > (1/NormalSensitivity) ? 1 : 0;

                float edge = max(edgeDepth,edgeNormal);

                return edge;

            }

            float SampleDepth(float2 uv)
            {
#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                return SAMPLE_TEXTURE2D_ARRAY(_CameraDepthTexture, sampler_CameraDepthTexture, uv, unity_StereoEyeIndex).r;
#else
                return SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
#endif
            }   

            float sobel (float2 uv)
            {
                float2 delta = float2(OutlineThickness, OutlineThickness);
                float hr = 0;
                float vt = 0;

                hr += SampleDepth(uv + float2(-1.0, -1.0) * delta) * 1.0;
                hr += SampleDepth(uv + float2( 1.0, -1.0) * delta) * -1.0;
                hr += SampleDepth(uv + float2(-1.0, 0.0) * delta) * 2.0;
                hr += SampleDepth(uv + float2( 1.0, 0.0) * delta) * -2.0;
                hr += SampleDepth(uv + float2(-1.0, 1.0) * delta) * 1.0;
                hr += SampleDepth(uv + float2( 1.0, 1.0) * delta) * -1.0; 
                vt += SampleDepth(uv + float2(-1.0, -1.0) * delta) * 1.0;
                vt += SampleDepth(uv + float2( 0.0, -1.0) * delta) * 2.0;
                vt += SampleDepth(uv + float2( 1.0, -1.0) * delta) * 1.0;
                vt += SampleDepth(uv + float2(-1.0, 1.0) * delta) * -1.0;
                vt += SampleDepth(uv + float2( 0.0, 1.0) * delta) * -2.0;
                vt += SampleDepth(uv + float2( 1.0, 1.0) * delta) * -1.0; 
                return sqrt(hr * hr + vt * vt);

            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.scrPos = ComputeScreenPos(o.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float4 col=0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float s = pow(1 - saturate(sobel(i.uv)), 50);
                // sample the texture
                //half4 col = SAMPLE_TEXTURE2D(_CameraDepthNormalsTexture,sampler_CameraDepthNormalsTexture, i.uv);
                float4 edge = EdgeFromDepthAndNormal(i.scrPos/i.scrPos.w,OutlineThickness,DepthSensitivity,NormalSensitivity);
                float4 main = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex, i.uv);

                //col = main*(1-edge);
                col =main*s; 
                //col=0;
                return col;
            }
            ENDHLSL
        }
    }
}
