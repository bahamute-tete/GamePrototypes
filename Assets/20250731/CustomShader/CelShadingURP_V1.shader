Shader "Custom/CelShadingURP_V1"
{
    Properties
    {
        [Header(Base Setting)]
        [MainTexture]_MainTex("Main Texture",2D)="white"{}
        [HideInInspector]_BaseMap("Base Map", 2D) = "white" {}
        [HideInInspector]_BaseColor("Base Color (URP)", Color) = (1, 1, 1, 1)
        
        _ShadowColor ("Shadow Color", Color) = (0.7, 0.7, 0.8)
        _ShadowRange ("Shadow Range", Range(0, 1)) = 0.5
        _ShadowSmooth("Shadow Smooth", Range(0, 1)) = 0.2
        _Brightness("TexBrightness",Range(1,20))=1
        
        [KeywordEnum(OFF,ON)]_FACE_SHADOW_TEX("Face Shadow Texture",float)=0
        [NoScaleOffset]_FaceShadowTex("Face Shadow Texture",2D)="white"{}
        _LerpMax ("Lerp Max",Range(0,1))=1
        
        [KeywordEnum(OFF,ON)]_RAMP_MAP("Ramp Map",float)=0
        [NoScaleOffset][Gamma]_RampTex("Ramp Texture",2D)="white"{}
        [Space(10)][Header(Ramp Colors)]
        _Color1("Color 1 (Dark)",Color)=(0.5,0.5,0.5,0.33)
        _Color2("Color 2 (Mid)",Color)=(0.7,0.7,0.7,0.66)
        _Color3("Color 3 (Bright)",Color)=(1,1,1,1)

        [Space(10)][Header(Rim Light)]
        _RimMin("Rim Min",Range(0,1))=0
        _RimMax("Rim Max",Range(0,1))=1
        _RimSmooth("Rim Smooth",Range(0,1))=0.5
        [HDR]_RimColor("Rim Color",Color)=(1,1,1,1)
        _RimBloomExp("Rim Bloom Exponent",Range(0,20))=1

        [Space(10)][Header(Outline)]
        [Toggle(_TurnOnThickness)]_TurnOnThickness("Turn On Thickness?", float) = 1.0   //新增
        _Thickness("Outline Thickness",Range(0,0.05))=0.01
        _EdgeColor("Outline Color",Color)=(0,0,0,1)
        _EdgeColorInside("ColorInside", Color) = (1,1,1,1)
        
        [Space(10)][Header(Specular)]
        [KeywordEnum(OFF,ON)]_HAIR_SPECULAR("Hair Specular",float)=0
        _Glossiness("Glossiness",Range(0.01,128))=32
        _SpecColor("Specular Color",Color)=(1,1,1,1)
        _SpecularIntensity("Specular Intensity",Range(0,10))=1

        [Space(5)][Header(Hair Specular)]
        [NoScaleOffset]_StretchedNoiseTex("Stretched Noise Texture",2D)="white" {}
        _ShiftTangent("Tangent Shift",Range(0,1))=0.5
        _AnisotropicPowerScale("Power Scale",Range(0,10))=1
        _AnisotropicPowerValue("Power Value",Range(0,128))=64
        
        // URP所需的剪裁属性
        [HideInInspector]_Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [KeywordEnum(OFF,ON)]_MASK_MAP("MASK Map",float)=0
        [HideInInspector]_MaskMap("Mask Map", 2D) = "white" {}

        //溶解
        [KeywordEnum(OFF,ON)] _DISSLOVE ("溶解",float) = 0.0
        _NoiseMap("NoiseMap", 2D) = "white" {}
		[HDR]_DissloveEdgeColor("Edge Color", Color) = (0,0,0,0)
		_EdgeWidth("Edge Width", Float) = 0
		//_CutoffHeight("Cutoff Height", Range( -1.1 , 1.1)) = 0
		_CutoffHeight1("Cutoff Tex", Range( -1.1 , 0)) = 0


        [Space(10)][Header(Clip Plane Settings)]
		[Space(5)][Toggle(_CLIP_PLANE)] _UseClipPlane("UseClipPlane", Float) = 0.0
		[Space(5)][Toggle(_SECOND_CLIP_PLANE_ON)] _UseSecondClipPlane("UseSecondClipPlane", Float) = 0.0
		[Space(5)][Toggle(_PLANE_NORMAL_OS)] _PlaneNormalOS("ClipPlane Normal OS", Float) = 0.0
		[Space(5)]_LineWidth("LineWidth", Range( 0 , 0.1)) = 0.05
		[HDR][ColorUsage(true, true)] _LineColor("LineColor", Color) = (1,1,1,1)
		_ClipPlane("ClipPlane", Vector) = (1,0,0,0)
		_ClipPlane2("ClipPlane2", Vector) = (0,0,1,0)
		_ColorInside("ColorInside", Color) = (1,1,1,1)

        // 渲染设置属性
        [HideInInspector][ToggleOff] _ReceiveShadows("Receive Shadows", Float) = 1.0
        [HideInInspector][ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
    }

    SubShader
    {   
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" "Queue"="Geometry"}
        LOD 200


        /**/
        Pass
        {
            Name "CelShading"
            Tags{"LightMode" = "UniversalForward"}
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // 编译预处理指令优化
            #pragma shader_feature_local _ _RAMP_MAP_ON
            #pragma shader_feature_local _ _MASK_MAP_ON
            #pragma shader_feature_local _ _DISSLOVE_ON
            #pragma shader_feature_local _ _HAIR_SPECULAR_ON
            #pragma shader_feature_local _ _FACE_SHADOW_TEX_ON
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
 			#pragma multi_compile_fragment _ _LIGHT_LAYERS
           
            // URP标准宏
            #pragma multi_compile _ _ALPHAPREMULTIPLY_ON
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            #pragma shader_feature_local_fragment _CLIP_PLANE
			#pragma shader_feature_local_fragment _PLANE_NORMAL_OS
			#pragma shader_feature_local_fragment _SECOND_CLIP_PLANE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 tangentOS : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float4 positionCS : SV_POSITION;
                real fogFactor : TEXCOORD4; 
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            struct GradientColor
            {
                float4 colors[3];
                float type;
                float colorsLength;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);

            // 为URP兼容性添加_BaseMap声明
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
    
            TEXTURE2D(_RampTex);
            SAMPLER(sampler_RampTex);

            TEXTURE2D(_StretchedNoiseTex);
            SAMPLER(sampler_StretchedNoiseTex);

            TEXTURE2D(_FaceShadowTex);
            SAMPLER(sampler_FaceShadowTex);

            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);


            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _BaseMap_ST; // 添加URP必需属性
            float4 _NoiseMap_ST;
            half4 _BaseColor; // 通用渲染管线需要的属性
            float3 _MainBaseColor;
            float _Brightness;
            float _Glossiness;
            float3 _SpecColor;
            float _SpecularIntensity;
            float _ShadowRange;
            float _Cutoff;
            
            #if defined (_RAMP_MAP_ON)
                float4 _Color1, _Color2, _Color3;
            #endif
            float _ShadowSmooth;
            float3 _ShadowColor;
            float _RimMin, _RimMax;
            float _RimSmooth;
            float4 _RimColor;
            float _RimBloomExp;
            float _Thickness;
            float _TurnOnThickness; //新增
            float4 _EdgeColor;
            float4 _DissloveEdgeColor;
            float _EdgeWidth;
            float _CutoffHeight1;
            
            #if defined (_HAIR_SPECULAR_ON)
                float _AnisotropicPowerValue, _AnisotropicPowerScale;
                float _ShiftTangent;
            #endif
            
            #if defined (_FACE_SHADOW_TEX_ON)
                float _LerpMax;
            #endif

            #if defined (_CLIP_PLANE)
                float4 _ClipPlane;
                float4 _ClipPlane2;
                float4 _LineColor;
                float4 _ColorInside;
                float _LineWidth;
            #endif

            #if defined (_PLANE_NORMAL_OS)
                float4 _PlaneNormalOS;
            #endif

            CBUFFER_END

            // 优化的渐变色处理函数
            GradientColor CreateGradient(float4 color1, float4 color2, float4 color3)
            {
                GradientColor g;
                g.colorsLength = 3;
                g.type = 0;
                
                // 预设颜色位置为0.0, 0.5, 1.0
                g.colors[0] = float4(color1.rgb, 0.0);
                g.colors[1] = float4(color2.rgb, 0.5);
                g.colors[2] = float4(color3.rgb, 1.0);
                return g;
            }

            float3 SampleGradient(GradientColor Gradient, float t)
            {
                // 限定t值在0-1范围内
                t = saturate(t);
                
                // 初始化颜色为第一个颜色
                float3 color = Gradient.colors[0].rgb;
                
                // 循环计算渐变颜色
                for (int c = 1; c < 3; c++)
                {
                    float colorPos = saturate((t - Gradient.colors[c-1].w) / (Gradient.colors[c].w - Gradient.colors[c-1].w));
                    color = lerp(color, Gradient.colors[c].rgb, colorPos);
                }
                
                // 线性颜色空间处理
                #ifndef UNITY_COLORSPACE_GAMMA
                    color = SRGBToLinear(color);
                #endif
                
                return color;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // 计算世界空间坐标和法线
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                
                // 保存切线方向和副切线符号
                output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
                
                // 计算雾效参数
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }


            half4 GetColorByLight(Light light,float3 worldNormal,float3 viewDir,float4 texColor,Varyings input,float3 bitangentWS)
            {
                float3 lightDir = normalize(light.direction);
                float3 lightColor = light.color;
                float lightShadowAtten = light.shadowAttenuation;
                
                // 改善半lambert光照模型
                float halfLambert = dot(worldNormal, lightDir) * 0.5 + 0.5;
                float diffuse = min(halfLambert, 1.0) * lightShadowAtten; // 加入阴影衰减
                
                // 基础高光计算 (Blinn-Phong)
                float3 halfVector = normalize(lightDir + viewDir);
                float specularNdotH = max(0, dot(worldNormal, halfVector));
                float specular = pow(specularNdotH, _Glossiness) * _SpecularIntensity;
                
                // 各向异性头发高光 (Kajiya-Kay模型)
                #if defined (_HAIR_SPECULAR_ON)
                    // 基于噪声贴图的切线偏移
                    float noise = SAMPLE_TEXTURE2D(_StretchedNoiseTex, sampler_StretchedNoiseTex, input.uv).r;
                    float shift = noise + _ShiftTangent - 0.5; // 缩放到[-0.5,0.5]范围
                    
                    // 计算偏移后的切线
                    float3 shiftedTangent = normalize(bitangentWS + worldNormal * shift);
                    
                    // Kajiya-Kay 光照模型
                    float dotTH = dot(shiftedTangent, halfVector);
                    float sinTH = sqrt(1 - dotTH * dotTH); // sin(angle) = sqrt(1-cos²(angle))
                    float dirAtten = smoothstep(-1, 0, dotTH); // 避免背面高光
                    
                    // 异方性高光计算
                    specular = dirAtten * pow(sinTH, _AnisotropicPowerValue) * _AnisotropicPowerScale;
                #endif
                
                // 面部阴影处理
                #if defined (_FACE_SHADOW_TEX_ON)
                    // 计算光照朝向因子
                    float3 lightDirH = normalize(float3(lightDir.x, 0, lightDir.z));
                    float3 forward = unity_ObjectToWorld._12_22_32;
                    float3 right = unity_ObjectToWorld._13_23_33;
                    float lightAtten = 1 - (dot(lightDirH, forward) * 0.5 + 0.5);

                     // 采样面部阴影贴图
                    float filpU = sign(dot(lightDirH, right));
                    float3 shaodwRamp = SAMPLE_TEXTURE2D(_FaceShadowTex, sampler_FaceShadowTex,input.uv * float2(filpU, 1)).xyz;

                    float faceShadow = step(lightAtten, shaodwRamp.r);
                    
                    diffuse = faceShadow;
                    // 应用平滑过渡
                    diffuse = smoothstep(_ShadowSmooth, _ShadowSmooth + 0.1, diffuse);
                    // 调整：根据主光源阴影衰减影响最终效果
                    diffuse *= lightShadowAtten;
                #endif
                
                //边缘光 (Rim Light)
                float NdotV = max(0, dot(worldNormal, viewDir));
                float rimFactor = 1.0 - NdotV;
                float rim = smoothstep(_RimMin, _RimMax, rimFactor);
                rim = smoothstep(0, _RimSmooth, rim);
                //边缘光随光照方向变化
                float NdotL = max(0, dot(worldNormal, lightDir));
                float rimLight = pow(abs(rimFactor), _RimBloomExp) * rim * NdotL;
                float3 rimColor = _RimColor.rgb * rimLight * _RimColor.a;
                //float4 rimColor = _RimColor * pow(saturate(1.0 - dot(worldNormal, viewDir)), 1.0 / _RimBloomExp) * _RimColor.a;
                //float4 rimColor = 0;
                float4 finalColor = float4(0, 0, 0, 1);
                // 根据不同渲染模式处理光照
                #if defined (_RAMP_MAP_ON)
                    // 创建渐变色带
                    GradientColor gradient = CreateGradient(_Color1, _Color2, _Color3);
                    
                    // 使用渐变采样，考虑阴影范围
                    float3 rampColor = SampleGradient(gradient, diffuse - _ShadowRange);
                    
                    // 组合最终颜色
                    finalColor.rgb = (rampColor * texColor.rgb) + (specular * _SpecColor) + rimColor;
                #else
                    // 使用基础硬边阴影
                    float ramp = smoothstep(0, _ShadowSmooth, diffuse - _ShadowRange);
                    float3 albedo = lerp(_ShadowColor.rgb, _BaseColor.rgb, ramp);
                    
                    // 组合最终颜色
                    finalColor.rgb = (albedo * texColor.rgb) + (specular * _SpecColor) + rimColor;
                #endif
                
                // 应用光照颜色
                finalColor.rgb *= lightColor;

                float light0 = light.distanceAttenuation * light.shadowAttenuation;
                //finalColor = lerp(finalColor,float4(0,0,0,1),light);
                finalColor = finalColor * light0;

                return finalColor;

            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 WorldPostion = input.positionWS;
                float3 ObjectPosition = mul(unity_WorldToObject, float4(WorldPostion, 1.0)).xyz;
                float4 finalColor = float4(0, 0, 0, 1);
                
                // 采样主纹理
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // 视角和法线
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 worldNormal = normalize(input.normalWS);
                
                // 计算切线空间 (TBN)
                float3 tangentWS = normalize(input.tangentWS.xyz);
                float3 bitangentWS = normalize(cross(worldNormal, tangentWS)) * input.tangentWS.w;

                // 获取主光源
                Light mainLight = GetMainLight();
                finalColor += GetColorByLight(mainLight,worldNormal,viewDir,texColor,input,bitangentWS);

                uint pixelLightCount = GetAdditionalLightsCount();
                
                for (uint lightIndex = 0; lightIndex < min(pixelLightCount, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    Light addLight = GetAdditionalLight(lightIndex, WorldPostion);
                    finalColor += GetColorByLight(addLight,worldNormal,viewDir,texColor,input,bitangentWS);
                }


                // 应用雾效
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);
                finalColor.a = texColor.a;

                #if defined (_MASK_MAP_ON)
                    // 支持剪裁防止穿模
                    float alpha = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv).a;
                    //clip(alpha - 0.1f);
                    clip(1-alpha-0.01f);
                #else
                    // 支持Alpha测试剪裁
                    float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                    clip(alpha - _Cutoff);
                #endif


                #if defined (_DISSLOVE_ON)
					float2 uv_NoiseMap = input.uv.xy * _NoiseMap_ST.xy + _NoiseMap_ST.zw;
					float temp_output_17_0 = ( SAMPLE_TEXTURE2D( _NoiseMap,sampler_NoiseMap, uv_NoiseMap ).r + _CutoffHeight1 );
					_EdgeWidth = _EdgeWidth / 100;
					float3 Emission = ( _DissloveEdgeColor * step( temp_output_17_0 , ( _EdgeWidth ) ) ).rgb;
                    //return float4(1,0,0,1);
                #else
                    float3 Emission = float3(0,0,0);
                #endif

                float Alpha = finalColor.a;

                #if defined(_DISSLOVE_ON)
					float AlphaStep = step( -0.01 , temp_output_17_0 );
					clip(AlphaStep - 0.5f);
                    Alpha = AlphaStep;

                     //clip(Alpha - _Cutoff);
                #endif

                #ifdef _CLIP_PLANE
				float3 normalPlane =normalize(_ClipPlane.xyz);
				float3 normalPlane2 =normalize(_ClipPlane2.xyz);
					#ifdef _PLANE_NORMAL_OS
						float4 clipPlane = float4(TransformWorldToObjectDir(normalPlane), _ClipPlane.w);
						float distance = dot(clipPlane.xyz, ObjectPosition)-clipPlane.w;
						#ifdef _SECOND_CLIP_PLANE_ON
							float4 clipPlane2 = float4(TransformWorldToObjectDir(normalPlane2), _ClipPlane2.w);
							float  distance2 = dot(clipPlane2.xyz, ObjectPosition)-clipPlane2.w;
						#endif
					#else
						float4 clipPlane = float4(normalPlane, _ClipPlane.w);
						float distance = dot(clipPlane.xyz, WorldPostion)-clipPlane.w;
						#ifdef _SECOND_CLIP_PLANE_ON
							float4 clipPlane2 = float4(normalPlane2, _ClipPlane2.w);
							float distance2 = dot(clipPlane2.xyz, WorldPostion)-clipPlane2.w;
						#endif
					#endif			
					//clip(distance);
					#ifdef _SECOND_CLIP_PLANE_ON
						float d = min(distance,distance2);
					#else
						float d = distance;
					#endif
					finalColor*=sign(d)>0?1:_ColorInside;
						
				float t= abs(d)-_LineWidth;
				finalColor += step(t,0)*_LineColor;
				#endif

                finalColor = float4(NeutralTonemap(finalColor.rgb+Emission),Alpha);
                //finalColor = float4(finalColor.rgb+Emission,Alpha);

                return finalColor*_Brightness;
                //return texColor;
            }
            ENDHLSL
        }

   
        //轮廓线绘制Pass
        Pass
        {
            Name "Outline"
            Tags{"LightMode" = "SRPDefaultUnlit"}
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vertOutLine
            #pragma fragment fragOutLine

            #pragma shader_feature_local _ _MASK_MAP_ON
            #pragma shader_feature_local _ _DISSLOVE_ON

            #pragma shader_feature_local_fragment _CLIP_PLANE
			#pragma shader_feature_local_fragment _PLANE_NORMAL_OS
			#pragma shader_feature_local_fragment _SECOND_CLIP_PLANE_ON

            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                real fogFactor : TEXCOORD0;
                float4 color : COLOR;
                float2 uv : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO  
            };

            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);


            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _NoiseMap_ST;
            float _Thickness;
            float _TurnOnThickness; //新增
            float4 _EdgeColor;
            float4 _EdgeColorInside;
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _Cutoff;

            float4 _DissloveEdgeColor;
            float _EdgeWidth;
            float _CutoffHeight1;

            #if defined (_CLIP_PLANE)
                float4 _ClipPlane;
                float4 _ClipPlane2;
                float4 _LineColor;
                float _LineWidth;
            #endif

            #if defined (_PLANE_NORMAL_OS)
                float4 _PlaneNormalOS;
            #endif

            CBUFFER_END

            Varyings vertOutLine(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // 获取视口长宽比
                float4 scaledScreenParams = GetScaledScreenParams();
                float aspectRatio = scaledScreenParams.y / scaledScreenParams.x;

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                // 计算屏幕空间中的法线方向
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 normalCS = TransformWorldToHClipDir(normalWS);

                // 将顶点位置转换到裁剪空间
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                
                // 考虑透视除法后的宽高比校正
                normalCS = normalize(normalCS) * output.positionCS.w;
                normalCS.x *= aspectRatio;
                
                // 顶点沿法线方向扩展，实现描边
                output.positionCS.xy += normalCS.xy * (_TurnOnThickness ? _Thickness : 0 );

                
                // 计算雾效系数
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                
                // 传递顶点颜色以便支持顶点颜色调整描边
                output.color = input.color;

                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                
                return output;
            }

            half4 fragOutLine(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 WorldPostion = input.positionWS;
                float3 ObjectPosition = mul(unity_WorldToObject, float4(WorldPostion, 1.0)).xyz;

                float3 color =_EdgeColor.rgb;
                #ifdef _CLIP_PLANE
				float3 normalPlane =normalize(_ClipPlane.xyz);
				float3 normalPlane2 =normalize(_ClipPlane2.xyz);
					#ifdef _PLANE_NORMAL_OS
						float4 clipPlane = float4(TransformWorldToObjectDir(normalPlane), _ClipPlane.w);
						float distance = dot(clipPlane.xyz, ObjectPosition)-clipPlane.w;
						#ifdef _SECOND_CLIP_PLANE_ON
							float4 clipPlane2 = float4(TransformWorldToObjectDir(normalPlane2), _ClipPlane2.w);
							float  distance2 = dot(clipPlane2.xyz, ObjectPosition)-clipPlane2.w;
						#endif
					#else
						float4 clipPlane = float4(normalPlane, _ClipPlane.w);
						float distance = dot(clipPlane.xyz, WorldPostion)-clipPlane.w;
						#ifdef _SECOND_CLIP_PLANE_ON
							float4 clipPlane2 = float4(normalPlane2, _ClipPlane2.w);
							float distance2 = dot(clipPlane2.xyz, WorldPostion)-clipPlane2.w;
						#endif
					#endif			
					//clip(distance);
					#ifdef _SECOND_CLIP_PLANE_ON
						float d = min(distance,distance2);
					#else
						float d = distance;
					#endif
					color=sign(d)>0?_EdgeColor.rgb:_EdgeColorInside.rgb;
						
				 float t= abs(d)-_LineWidth;
				 color += step(t,0)*_LineColor;
				#endif
                // 获取描边颜色并应用顶点颜色
                //float3 color = sign(d)>0?_EdgeColor.rgb:_EdgeColorInside.rgb;// * input.color.rgb;
                //float3 color = _EdgeColorInside.rgb * input.color.rgb;
                
                // 应用雾效
                color = MixFog(color, input.fogFactor);

                #if defined (_MASK_MAP_ON)
                    // 支持剪裁防止穿模
                    float alpha = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv).a;
                    clip(1-alpha-0.01f);
                #else
                    // 支持Alpha测试剪裁
                    float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                    clip(alpha - _Cutoff);
                #endif

                #if defined (_DISSLOVE_ON)
					float2 uv_NoiseMap = input.uv.xy * _NoiseMap_ST.xy + _NoiseMap_ST.zw;
					float temp_output_17_0 = ( SAMPLE_TEXTURE2D( _NoiseMap,sampler_NoiseMap, uv_NoiseMap ).r + _CutoffHeight1 );
					_EdgeWidth = _EdgeWidth / 100;
					float3 Emission = ( _DissloveEdgeColor * step( temp_output_17_0 , ( _EdgeWidth ) ) ).rgb;
                    //return float4(1,0,0,1);
                #else
                    float3 Emission = float3(0,0,0);
                #endif

                float Alpha = 1;

                #if defined(_DISSLOVE_ON)
					float AlphaStep = step( -0.01 , temp_output_17_0 );
					clip(AlphaStep - 0.5f);
                    Alpha = AlphaStep;
                     //clip(Alpha - _Cutoff);
                #endif

                return float4(color+Emission, Alpha);
            }
            ENDHLSL
        }
        
        
    

        
        // 阴影投射Pass - 使用自定义Pass代替URP的内建Pass
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            // 自定义阴影投射Pass，避免使用URP内置ShadowCasterPass.hlsl
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _Cutoff;
            float _Thickness;
            float _TurnOnThickness; //新增
            float4 _EdgeColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 texcoord     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float3 _LightDirection;
            
            // 完全移除自定义的阴影偏移函数，直接使用URP内置函数

            Varyings ShadowVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                // 获取主光源方向
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                
                // 计算与光源相关的偏移位置
                float3 vertexShadowBias = normalWS * 0.01; // 使用简单固定值作为法线偏移
                positionWS += vertexShadowBias;
                
                // 和光源方向对齐
                float invNdotL = 1.0 - saturate(dot(lightDirectionWS, normalWS));
                float scale = invNdotL * 0.01;
                positionWS -= lightDirectionWS * 0.001; // 小的深度偏移
                positionWS -= normalWS * scale;
                
                output.positionCS = TransformWorldToHClip(positionWS);
                
                // 处理Z裁剪问题
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 ShadowFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                // 支持Alpha测试剪裁
                float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        // 深度仅Pass - 使用自定义Pass代替URP的内建Pass
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 position     : POSITION;
                float2 texcoord     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.positionCS = TransformObjectToHClip(input.position.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                // 支持Alpha测试剪裁
                float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                clip(alpha - _Cutoff);
                
                return 0;
            }
            ENDHLSL
        }

        // 深度法线Pass - 自定义实现
        Pass
        {
            Name "DepthNormals"
            Tags{"LightMode" = "DepthNormals"}

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 position     : POSITION;
                float2 texcoord     : TEXCOORD0;
                float3 normal       : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.positionCS = TransformObjectToHClip(input.position.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normal);
                return output;
            }

            half4 DepthNormalsFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                // 支持Alpha测试剪裁
                float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                clip(alpha - _Cutoff);
                
                // 输出法线
                float3 normalWS = normalize(input.normalWS);
                return float4(PackNormalOctRectEncode(normalWS), 0.0, 0.0);
            }
            ENDHLSL
        }
        
    }
    CustomEditor "CelShadingURP_V1GUI"
}