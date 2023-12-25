Shader "Custom/ConstructWorldPos"
{
    Properties
    {

    }
    SubShader
    {
         Tags{
            "RenderPipeline"="UniversalRenderPipeline"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "Queue"="Transparent"
        }
        LOD 100

        ZWrite ON 

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"


             struct Attributes{
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _CameraDepthTexture_TexelSize;

            struct Varings{
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 dirWS:TEXCOORD1;
                float3 dirVS:TEXCOORD2;
                float3 positionWS:TEXCOORD3;
                float2 uv : TEXCOORD4;
            };



            Varings vert(Attributes input){
                Varings o;

                o.positionWS =TransformObjectToWorld(input.positionOS);

                

                o.dirWS= normalize( o.positionWS - _WorldSpaceCameraPos);
                o.dirVS = TransformWorldToViewDir(o.dirWS);
                

                o.positionCS  = TransformObjectToHClip(input.positionOS);

                o.screenPos = ComputeScreenPos(o.positionCS);
                o.uv = input.uv;
                return o;
            }

            float3 offical_reconstruct_method(float2 UV)
            {
                // Sample the depth from the Camera depth texture.
                #if UNITY_REVERSED_Z
                    real depth = SampleSceneDepth(UV);
                #else
                    // Adjust Z to match NDC for OpenGL ([-1, 1])
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(UV));
                #endif

                // Reconstruct the world space positions.
                float3 worldPos = ComputeWorldSpacePosition(UV, depth, UNITY_MATRIX_I_VP);

                return worldPos;

            }


            float3 custom_reconstruct_method(float2 screenPos)
            {
                //screenPos / screenPos.w就是【0,1】的归一化屏幕坐标  //_CameraDepthTexture是获取的深度图
                //Linear01Depth将采样的非线性深度图变成线性的
                float depth = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, screenPos), _ZBufferParams);
                //将【0，1】映射到【-1， 1】上，得到ndcPos的x，y坐标
                float2 ndcPosXY = screenPos * 2 - 1;
                //float3的z值补了一个1，代表远平面的NDC坐标  _ProjectionParams代表view空间的远平面, 我们知道裁剪空间的w和view空间的z相等，
                //相当于做了一次逆向透视除法，得到了远平面的clipPos
                float3 clipPos = float3(ndcPosXY.x, ndcPosXY.y, 1) * _ProjectionParams.z;

                float3 viewPos = mul(unity_CameraInvProjection, clipPos.xyzz).xyz * depth;  //远平面的clipPos转回远平面的viewPos， 再利用depth获取该点在viewPos里真正的位置
                //补一个1变成其次坐标，然后逆的V矩阵变回worldPos
                float4 worldPos = mul(UNITY_MATRIX_I_V, float4(viewPos, 1));

                return worldPos;
            }

            float3 custom_reconstruct_method2(float2 screenPos,float3 dirWS,float3 dirVS)
            {
                //dirVS 是方向向量所
                //利用相似三角形eyeDepth/dirVS.z 的比例来缩放dirWS
                float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, screenPos), _ZBufferParams);

                dirWS*=-depth/dirVS.z;

                float3 worldPos =_WorldSpaceCameraPos + dirWS;
                return worldPos;
            }

            
            float3 custom_reconstruct_method_test(float2 screenPos)
            {

                float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, screenPos);

                float4 positionCS = float4(screenPos * 2.0 - 1.0, depth, 1.0);
                positionCS.y = -positionCS.y;

                float4 worldPos = mul(UNITY_MATRIX_I_VP, positionCS);

                return worldPos/worldPos.w;
            }

            float3 custom_reconstruct_Normal(float3 worldPos)
            {
                float3 dx = ddx(worldPos);
                float3 dy = ddy(worldPos);
 
                #if UNITY_UV_STARTS_AT_TOP
                    return -normalize(cross(dx,dy));
                #else
                    return normalize(cross(dx,dy));
                #endif

            }


            half4 frag(Varings i) : SV_TARGET
            {
                half3 color = half3(1,0,0);
                float2 uv = i.screenPos.xy / i.screenPos.w;

                float depth = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv), _ZBufferParams);
                //float3 worldPos = custom_reconstruct_method2(uv,i.dirWS,i.dirVS);
                float3 worldPos = offical_reconstruct_method(uv);
                float3 normalWS = custom_reconstruct_Normal(worldPos);
                float3 normalOS =TransformWorldToObjectNormal(normalWS);

                uint3 worldIntPos =uint3(floor(abs(worldPos.xyz * 10)));
                bool white = ((worldIntPos.x) & 1) ^ (worldIntPos.y & 1) ^ (worldIntPos.z & 1);
                color = white ? half4(1,1,1,1) : half4(0,0,0,1);

                if(depth > 0.9999) discard;
                //color = depth;

                //color = saturate(dot(normalWS,float3(1,2,3)));
        
                return half4(color,1);
            }
            ENDHLSL
        }



    }
}
