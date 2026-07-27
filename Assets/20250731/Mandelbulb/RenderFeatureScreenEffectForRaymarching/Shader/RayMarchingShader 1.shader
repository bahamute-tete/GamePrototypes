Shader "Custom/RayMarchingShader"
{
    Properties
    {
        _SDFTexture ("Texture", 3D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "RayMarchingSurfacePass"

            
            HLSLPROGRAM
            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING
            #pragma vertex vert
            #pragma fragment frag

            #define RAYMARCHING_MAX_STEPS 256

            #define RAYMARCHING_MAX_DISTANCE 100

            #define SURFANCE_DISTANCE 5e-4

            #define VOLUME_MAX_STEPS 32

            #define SHADOW_MAX_STEPS 128

            #define GODRAY_MAX_STEPS 16
            #define GODRAY_LIGHTSAMPLE_MAX_STEPS 6

            // #pragma multi_compile _USE_3D_TEXTURE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RayMarchingInputs.hlsl"
            #include "RayMarchingLighting.hlsl"
            #include "RayMarchingVolumeRender.hlsl"
            #include "RayMarchingGodRay.hlsl"

            


            float3 CaculateSceneLighting(float3 p, float3 n,float3 v, float3 lightDir,float3 lightColor,int pointLightCount, float4 pointLightPosRanges[4], float4 pointLightColors[4])
            {
                float3 totalLight = float3(0,0,0);
                float3 _lightDir = normalize(lightDir);
                float diff = max(0, dot(n, lightDir));

                float specularStrength = 1;
                float specularPower = 32;
                float3 h = normalize(lightDir+v);
                float nh = saturate(dot(n,h));
                float spe = pow(nh,specularPower)*specularStrength;
                float3 specular = specularStrength * spe * lightColor;

                totalLight += lightColor * diff + specular;

                
                for(int j = 0; j < pointLightCount; j++)
                {
                    float3 lightPos = pointLightPosRanges[j].xyz;
                    float range = pointLightPosRanges[j].w;
                    float3 lightColor = pointLightColors[j].rgb;

                    float3 dirToLight = lightPos - p;
                    float distToLight = length(dirToLight);

                    if(distToLight < range)
                    {
                        float3 lPointDir = normalize(dirToLight);
                        float diffPoint = max(0, dot(n, lPointDir));
                        
                        
                        float atten = saturate(1.0 - (distToLight / range));
                        atten = 1.0 / (1.0 + distToLight * distToLight); 

                        totalLight += lightColor * diffPoint * atten;
                    }
                }

                return totalLight;

            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }
            

            half4 frag (Varyings i) : SV_Target
            {
                float sceneDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, i.uv).r;
                float sceneLinearDepth = LinearEyeDepth(sceneDepth, _ZBufferParams);
               
                float4 clipPos = float4(i.uv * 2.0 - 1.0, 0.0, 1.0);
                
                float4 worldPosH = mul(_ClipToWorld, clipPos);
                float3 worldPos = worldPosH.xyz / worldPosH.w;

                float maxDist = _CamParams.w; 
                float startDist = _CamParams.z;
                
                float3 ro = _CameraPos;
                float3 rd = normalize(worldPos - ro);

                float2 dAndMat = RayMarchWithMat(ro, rd);
                float d = dAndMat.x;
                float matID = dAndMat.y;

/////////////////////////////////////////////////////////////////////
                
                float4 volumeColor = float4(0,0,0,0);

                float maxVolumeDistance = min(sceneLinearDepth, d);
               
                VolumeParams volume = (VolumeParams)0;
                volume.ro = ro;  
                volume.rd = rd; 
                volume.boxCenter = _FogBoxCenter;  
                volume.boxSize = _FogBoxSize;  
                volume.g = _HenyeyGreenstein_G;// 向前散射参数 
                volume.absorption = _Absorption;// 吸收系数
                volume.scatteringCoeff = _ScatteringCoeff;// 散射系数
                volume.ambientLightIntensity = _AmbientLightIntensity;// 环境光系数
                volume.directLightIntensity = _DirectLightIntensity;// 直接光系数
                volume.lightDir = _LightDirection.xyz;
                volume.lightColor = _LightColor.rgb;
                volume.stepSize = _StepSize;
                volume.max_Steps = VOLUME_MAX_STEPS;
                volume.maxDistance = maxVolumeDistance;
                volume.fogBaseColor = _FogBaseColor;
                volume.fogTargetColor = _FogTargetColor;
                volume.densityModifier = _FogDensity;



                volumeColor =RayMarchVolumeFog(volume);
                
                half4 col = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, i.uv);
                col.rgb = volumeColor.rgb + col.rgb * (1.0-volumeColor.a);
/////////////////////////////////////////////////////////////////////  
                float maxGodRayDist = min(d, sceneLinearDepth);
                if(maxGodRayDist > RAYMARCHING_MAX_DISTANCE) maxGodRayDist = RAYMARCHING_MAX_DISTANCE;

                float2 screenUV = i.uv * _ScreenParams.xy;
                float ditherValue = frac(sin(dot(screenUV, float2(12.9898, 78.233))) * 43758.5453);

                GodRayParams godRayParams;
                godRayParams.ro = ro;
                godRayParams.rd = rd;
                godRayParams.maxDist = maxGodRayDist;
                godRayParams.lightDir = normalize(_LightDirection.xyz);
                godRayParams.stepSize = _StepSize * 4.0; 
                godRayParams.maxSteps = GODRAY_MAX_STEPS; 
                godRayParams.maxLightSamples = GODRAY_LIGHTSAMPLE_MAX_STEPS;
                godRayParams.density = 10.0; 
                godRayParams.intensity = _DirectLightIntensity;
                godRayParams.decay = 0.15; 
                godRayParams.dither = ditherValue;
                godRayParams.g = 0.6; 

                float3 godRayColor = CalculateGodRays(godRayParams) * _LightColor.rgb;    
       
     
///////////////////////////////////////////////////////////////////// 

                float ao = 0.0;
                float shadow = 0.0;
                
                if (d < startDist)
                {
                    d = startDist;
                }
                
                if(d < RAYMARCHING_MAX_DISTANCE && d < sceneLinearDepth) 
                {
                    float3 p = ro + rd * d;
                   
                    float3 n = GetNormalAdaptive(p, d);

                    float3 r= Reflect(rd,n);

                    float3 v = -rd;

                    float3 sdfColors = GetMaterialColor(matID, p, n); 
                    
                    float3 lightDir = normalize(_LightDirection.xyz);
                    
                    // half4 envRef = SAMPLE_TEXTURECUBE(_EnvironmentCubemap, sampler_EnvironmentCubemap, r);
                    float shadow = Softshadow(p + n * 0.01, lightDir, 0.02, 100.0, 64, SHADOW_MAX_STEPS);
                    float ao = CalcAO(p, n);
                   
                    float3 totalLight = CaculateSceneLighting(
                                                                p, 
                                                                n,
                                                                v, 
                                                                lightDir, 
                                                                _LightColor.rgb, 
                                                                _PointLightCount,
                                                                _PointLightPosRanges, 
                                                                _PointLightColors
                                                            );
                    float3 ambient = SampleSH(n);

                    float3 surfaceColor = sdfColors * (ambient * ao + totalLight * shadow * ao);

                    surfaceColor += ReflectionWithFresnel(ro, rd, p, n);
                    // float3 reflection = TraceReflection(ro, rd, 3);

                    
                    col.rgb = surfaceColor.rgb *(1.0-volumeColor.a)  + volumeColor.rgb;
                   
                    
                   
                }

                 float backgroundLuminance = dot(col.rgb, float3(0.299, 0.587, 0.114));
                float brightnessMask = saturate(1.5- backgroundLuminance * 2.0);
                float cloudMask = volumeColor.a;
                // float finalMask = brightnessMask * cloudMask;
                //return brightnessMask;
                // return finalMask;
                
                
                
                col.rgb += godRayColor*cloudMask;
                
                //return half4(godRayColor, 1.0); 
               
                return pow(col, 1.0); // Gamma correction
            }
            ENDHLSL
        }

      
    }
}
