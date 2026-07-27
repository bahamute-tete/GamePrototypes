Shader "Custom/GodRay/RayMarchingVolumetricLight"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}

    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        // Pass 0：Ray Marching 计算体积光
        Pass
        {
            Name "RayMarchingGodRay"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CamParams; // x:near y:far
                float3 _LightDir; 
                float4 _LightColor; 
                float _G; 
                float _Density; 
                float _StepSize; 
                float _MaxDistance;
                float _JitterStrength;
                float _MaxSteps;
                float _Decay; 
                float _Intensity;
                float _ShadowOcclusionContrast;
                float _FrameIndex; // 帧索引用于时间抖动
                float _UseTemporalAccumulation;
                float _TAA_Blend; // TAA混合系数 
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            
            float HenyeyGreenstein(float cosTheta, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) / (4.0 * PI * pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5));
            }

           
            float InterleavedGradientNoise(float2 uv)
            {
                float3 magic = float3(0.06711056, 0.00583715, 52.9829189);
                return frac(magic.z * frac(dot(uv, magic.xy)));
            }

            float2 Halton23(int index)
            {
                float x = 0.0, y = 0.0;
                float f = 0.5;
                int i = index;
                
                // Base 2
                while (i > 0)
                {
                    if (i % 2 == 1) x += f;
                    i /= 2;
                    f /= 2.0;
                }
                
                // Base 3
                f = 1.0 / 3.0;
                i = index;
                while (i > 0)
                {
                    y += f * float(i % 3);
                    i /= 3;
                    f /= 3.0;
                }
                
                return float2(x, y);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;

                return o;
            }

     
         

            
            half4 frag (v2f i) : SV_Target
            {
                // 采样场景深度
                float depth = SampleSceneDepth(i.uv);

                // 世界空间重建射线方向（兼容透视/正交，适配 Reversed-Z）
                #if UNITY_REVERSED_Z
                float rawNear = 1.0;
                float rawFar = 0.0;
                #else
                float rawNear = 0.0;
                float rawFar = 1.0;
                #endif
                float3 nearWS = ComputeWorldSpacePosition(i.uv, rawNear, UNITY_MATRIX_I_VP);
                float3 farWS = ComputeWorldSpacePosition(i.uv, rawFar, UNITY_MATRIX_I_VP);
                float3 perspectiveRayWS = normalize(farWS - nearWS);
                float3 orthoRayWS = normalize(mul((float3x3)unity_CameraToWorld, float3(0.0, 0.0, -1.0)));
                float3 viewRayWS = normalize(lerp(perspectiveRayWS, orthoRayWS, unity_OrthoParams.w));

                // 计算射线起点到场景片段的距离
                float3 ro = lerp(_WorldSpaceCameraPos, nearWS, unity_OrthoParams.w);
                float3 rd = viewRayWS;
                float startDist = lerp(_CamParams.x, 0.0, unity_OrthoParams.w);

                float3 sceneWS = ComputeWorldSpacePosition(i.uv, depth, UNITY_MATRIX_I_VP);
                float sceneDist = distance(ro, sceneWS);
                float rayLength = min(sceneDist, _MaxDistance);

                #if UNITY_REVERSED_Z
                bool isSky = depth <= 0.00001;
                #else
                bool isSky = depth >= 0.99999;
                #endif

                if (isSky)
                {
                    rayLength = _MaxDistance;
                }

                // 如果近裁剪面超出片段距离，跳过
                if (startDist >= rayLength)
                    return half4(0, 0, 0, 0);

                // 相位函数计算（Heney-Greenstein）
                float3 ld = normalize(_LightDir);
                float3 viewDir = -rd;
                float cosTheta = dot(viewDir, ld);
                float phase = HenyeyGreenstein(cosTheta, _G);

                // 自适应步长
                float stepSize = max(0.02, _StepSize);
                int maxStepCount = max(1, (int)_MaxSteps);
                int numSteps = max(1, int((rayLength - startDist) / stepSize));
                numSteps = min(numSteps, maxStepCount);
                stepSize = (rayLength - startDist) / max(1, numSteps);

                // 时间抖动 + 空间抖动（配合 TAA 减少噪点）
                float timeDither = (_UseTemporalAccumulation > 0.5) ? Halton23(int(_FrameIndex) % 8).x : 0.0;
                float spatialDither = InterleavedGradientNoise(i.positionCS.xy);
                float dither = ((spatialDither * 0.8) + (timeDither * 0.2)) * stepSize * _JitterStrength;
                float currentDist = startDist + dither;

                float3 accumulatedLight = float3(0, 0, 0);
                float transmittance = 1.0;

                // Ray Marching 循环
                [loop]
                for (int j = 0; j < numSteps && currentDist < rayLength; j++)
                {
                    float3 samplePos = ro + rd * currentDist;

                    // 计算阴影衰减
                    float4 shadowCoord = TransformWorldToShadowCoord(samplePos);
                    Light mainLight = GetMainLight(shadowCoord);
                    float shadowAtten = mainLight.shadowAttenuation;
                    shadowAtten = saturate((shadowAtten - 0.5) * _ShadowOcclusionContrast + 0.5);

                    if (shadowAtten > 0.01)
                    {
                        // 距离衰减
                        float distAtten = exp(-currentDist * _Decay);

                        // 体积密度
                        float density = _Density * shadowAtten * distAtten;

                        // 光线贡献
                        float3 lightContrib = _LightColor.rgb * density * phase;

                        // 累积（考虑透射率）
                        accumulatedLight += lightContrib * transmittance * stepSize;

                        // 更新透射率
                        transmittance *= exp(-density * stepSize);

                        // 早期退出优化
                        if (transmittance < 0.02) break;
                    }

                    currentDist += stepSize;
                }

                float3 finalColor = accumulatedLight * _Intensity;
                return float4(finalColor, 1);
            }
            ENDHLSL
        }

        // Pass 1：时间累积（TAA）
        Pass
        {
            Name "TemporalAccumulation"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _TAA_Blend;
                float4 _PrevFrame_TexelSize;
            CBUFFER_END

            TEXTURE2D(_MainTex); // 当前帧
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_PrevFrame); // 上一帧累积结果
            SAMPLER(sampler_PrevFrame);

            Varyings vert (Attributes IN)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return o;
            }

            // 3x3 邻域钳制（减少重影）
            float3 ClipHistory(float3 history, float3 current, float2 uv, float2 texelSize)
            {
                float3 minColor = current;
                float3 maxColor = current;

                // 采样 3x3 邻域
                const float2 offsets[8] = {
                    float2(-1, -1), float2(0, -1), float2(1, -1),
                    float2(-1,  0),                float2(1,  0),
                    float2(-1,  1), float2(0,  1), float2(1,  1)
                };

                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    float2 sampleUV = uv + offsets[i] * texelSize;
                    float3 neighbor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV).rgb;
                    minColor = min(minColor, neighbor);
                    maxColor = max(maxColor, neighbor);
                }

                // 钳制历史帧
                return clamp(history, minColor, maxColor);
            }

            half4 frag (Varyings i) : SV_Target
            {
                // 当前帧结果
                half3 currentColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;

                // 上一帧颜色（假设相机静止，UV 不变）
                float2 prevUV = i.uv;
                half3 historyColor = SAMPLE_TEXTURE2D(_PrevFrame, sampler_PrevFrame, prevUV).rgb;

                // 邻域钳制减少重影
                historyColor = ClipHistory(historyColor, currentColor, i.uv, _PrevFrame_TexelSize.xy);

                // 时间混合
                float blendFactor = _TAA_Blend;

                // 边界检查：如果超出屏幕，使用当前帧
                if (prevUV.x < 0 || prevUV.x > 1 || prevUV.y < 0 || prevUV.y > 1)
                {
                    blendFactor = 0.0;
                }

                half3 result = lerp(currentColor, historyColor, blendFactor);

                return half4(result, 1.0);
            }
            ENDHLSL
        }


        Pass
        {
            Name "BlendGodRay"
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionHCS : SV_POSITION;
                 float2 positionSS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _TintColor;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_GodRayTex);
            SAMPLER(sampler_GodRayTex);

            Varyings vert (Attributes IN)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv = TRANSFORM_TEX(IN.uv, _MainTex);

                float4 screenPos = ComputeScreenPos(o.positionHCS);
                screenPos.xy /= screenPos.w;
                o.positionSS = screenPos.xy;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 originalCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 godRayCol = SAMPLE_TEXTURE2D(_GodRayTex, sampler_GodRayTex, i.uv);

                // 直接混合，GodRay 已经在 RayMarching Pass 中计算了阴影
                half3 finalColor = originalCol.rgb + godRayCol.rgb * _TintColor;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}