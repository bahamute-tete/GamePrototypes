//Desktop_PC V3.0
Shader "METAVERSE IMAGINATION/Desktop PC/Standard/URPLit_Standard_MIX"
{
	Properties
	{
		[Enum(UnityEngine.Rendering.CullMode)]_CullMode("CullMode", Float) = 2

		[Space(10)][Main(z0, _KEYWORD, on, off)]_MainProperties("材质属性 --", Float) = 0
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		[SubToggle(z0, _UseAlphaClip)]_UseAlphaClip("启用Alpha Clip", Float) = 1
		[Sub(z0)]_ClipingCutout("ClipingCutout", Range( 0 , 1)) = 0.5
		[KWEnum(z0,ORM_Or_MSO_Mode,_PBR_SHADING_MODE_ORM_OR_MSO_MODE,UnityLit_MetallicAlpha,_PBR_SHADING_MODE_UNITYLIT_METALLICALPHA)] _PBR_Shading_Mode("PBR_Shading_Mode", Float) = 0
		
		[Space(10)][Main(z1, _KEYWORD, on, off)]_BaseMapTex("颜色贴图 --", Float) = 0
		[HideInInspector]_BaseColor("基础颜色", Color) = (1,1,1,0)
		[Tex(z1, _BaseColor)]_MainTex("颜色纹理", 2D) = "white" {}
		[Sub(z1)]_Albedo_Strength("颜色值 - 强度", Float) = 1
		[Sub(z1)]_MainTexUVCoord("主 - 纹理坐标", Vector) = (1,1,0,0)

		[Space(10)][Main(z2, _KEYWORD, on, off)]_NormalMapTex("法线贴图 --", Float) = 0
		[Normal][Tex(z2)]_NormalMap("法线纹理", 2D) = "bump" {}
		[Sub(z2)]_Normal_Scale("法线强度", Float) = 1

		[Space(10)][Main(z3, _KEYWORD, on, off)]_EmissionMapTex("自发光贴图 --", Float) = 0
		[SubToggle(z2,_UseEmissionMap)]_UseEmissionMap("启用自发光贴图", Float) = 0
		[SubToggle(z2,_UV0_OR_UV1)]_UV0_OR_UV1("UV1 或 UV2", Float) = 0
		[Sub(z3)][HDR]_EmissionColor("自发光颜色（无贴图）", Color) = (0,0,0,0)
		[Tex(z3)]_EmissionMap("自发光纹理", 2D) = "black" {}
		[Sub(z3)]_Emission_Intensity("自发光贴图强度", Range( 0 , 5)) = 1

		[Space(10)][Main(z4, _KEYWORD, on, off)]_ORMMapTex("蒙版贴图 --", Float) = 0
		[Title(z4,Texture Setting)]
		[Tex(z4)]_MASEMap("MSO 或 ORM - 纹理", 2D) = "black" {}

		[Space(5)][Title(z4,Metallic Settings)]
		[SubToggle(z4,_UseMetallicMap)]_UseMetallicMap("启用金属度贴图（MSO）", Float) = 0
		[SubToggle(z4,_ORM_Metallic)]_ORM_Metallic("MSO切换为ORM（金属度）", Float) = 1
		[Sub(z4)]_MetallicMap_Intensity("金属度强度（MSO）", Range( 0 , 5)) = 1
		[Sub(z4)]_MetallicMap_ORM_Intensity("金属度强度（ORM）", Range( 0 , 5)) = 1
		[Sub(z4)]_MetallicValue("金属度强度（无贴图）", Range( 0 , 1)) = 0

		[Space(5)][Title(z4,Smoothness Settings)]
		[SubToggle(z4,_UseSmoothnessMap)]_UseSmoothnessMap("启用平滑度贴图（MSO）", Float) = 0
		[SubToggle(z4,_UseRoughnessMap)]_UseRoughnessMap("MSO切换为ORM（平滑度）", Float) = 0
		[Sub(z4)]_SmoothnessMap_Intensity("平滑度强度", Range( 0 , 5)) = 1
		[Sub(z4)]_SmoothnessValue("平滑度强度（无贴图）", Range( 0 , 1)) = 0

		[Space(5)][Title(z4,Occlusion Settings)]
		[SubToggle(z4,_UseAOMap)]_UseAOMap("启用AO贴图（MSO）", Float) = 0
		[SubToggle(z4,_Use_AOMap_MetallicAlpha)]_Use_AOMap_MetallicAlpha("启用AO贴图（MetallicAlpha）", Float) = 0
		[SubToggle(z4,_ORM_AO)]_ORM_AO("MSO切换为ORM（AO）", Float) = 0
		[Sub(z4)]_AOMap_Intensity("AO强度（MSO）", Range( 0 , 5)) = 1
		[Sub(z4)]_AOMap_ORM_Intensity("AO强度（ORM）", Range( 0 , 5)) = 1

		[Space(10)][Toggle(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows("关闭接受阴影", Float) = 0
		[Space(10)][Toggle(_BLINNPHONE_LIGHT)] _BlinnPhongLight("BlinnPhong光照", Float) = 0.0

		[Space(50)]
		//[Toggle(_DISSOLVE)] _Dissolve("溶解", Float) = 0.0
		//[Toggle(_DISSOLVE_UP)] _DissolveUp("向上溶解", Float) = 0.0
		[KeywordEnum(NO_DISSOLVE,DISSOLVE_TEX,DISSOLVE_UP,DISSOLVE_DOWN)] _Dissolve("溶解",Float) = 0.0
		//[KeywordEnum(Red,Green,Blue)] _Color("Color",int) = 0

		_NoiseMap("NoiseMap", 2D) = "white" {}
		[HDR]_EdgeColor("Edge Color", Color) = (0,0,0,0)
		_EdgeWidth("Edge Width", Float) = 0
		_CutoffHeight("Cutoff Height", Range( -1.1 , 1.1)) = 0
		_CutoffHeight1("Cutoff Tex", Range( -1.1 , 0)) = 0
		_DissloveXYZ("Disslove XYZ", Vector) = (0,1,0,0)

		[Sapce(10)][Header(AABB Settings)]
		_BoundsMin("_BoundsMin", Vector) = (0,0,0,0)
		_BoundsSize("_BoundsSize", Vector) = (0,0,0,0)

		[Space(10)][Toggle(_FOGCONTROL)]_UseFogControl("自定义雾强度", Float) = 0.0
		_FogIntensity("FogIntensity", Range( 0 , 1)) = 1

		[Space(10)][Header(Stencil1 Settings)]
		_StencilMask("Stencil mask", Range(0, 255)) = 0
		[Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil Comp", Int) = 0
		[Enum(UnityEngine.Rendering.StencilOp)] _StencilPass("Stencil Pass", Int) = 0
		[Enum(UnityEngine.Rendering.StencilOp)] _StencilFail("Stencil Fail", Int) = 0
		[Enum(UnityEngine.Rendering.StencilOp)] _StencilZFail("Stencil Z Fail", Int) = 0

		// [Space(10)][Header(Stencil1 Settings)]
		// _StencilMask2("Stencil mask2", Range(0, 255)) = 0
		// [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp2("Stencil Comp2", Int) = 0
		// [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass2("Stencil Pass2", Int) = 0
		// [Enum(UnityEngine.Rendering.StencilOp)] _StencilFail2("Stencil Fail2", Int) = 0
		// [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFail2("Stencil Z Fail2", Int) = 0

		[Space(10)][Header(Clip Plane Settings)]
		[Space(5)][Toggle(_CLIP_PLANE)] _UseClipPlane("UseClipPlane", Float) = 0.0
		[Space(5)][Toggle(_SECOND_CLIP_PLANE_ON)] _UseSecondClipPlane("UseSecondClipPlane", Float) = 0.0
		[Space(5)][Toggle(_PLANE_NORMAL_OS)] _PlaneNormalOS("ClipPlane Normal OS", Float) = 0.0
		[Space(5)]
		_LineWidth("LineWidth", Range( 0 , 0.1)) = 0.05
		[Space(5)][Toggle(_INVERSE_COLOR)] _InverseColor("InverseColor", Float) = 0.0
		[HDR][ColorUsage(true, true)] _LineColor("LineColor", Color) = (1,1,1,1)
		_ClipPlane("ClipPlane", Vector) = (1,0,0,0)
		_ClipPlane2("ClipPlane2", Vector) = (0,0,1,0)
		_ColorInside("ColorInside", Color) = (1,1,1,1)


	}

	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

		HLSLINCLUDE
		#pragma target 2.0
		
		ENDHLSL

		
		Pass
		{
			Name "Forward"
			Tags { "LightMode"="UniversalForward" }
			
			ZWrite On
			ZTest LEqual
			Cull [_CullMode]

			Stencil 
		    {
			    Ref [_StencilMask]
			    Comp [_StencilComp]//Less	 //2:Less 5:NotEqual [_StencilComp]
			    Pass [_StencilPass]			//keep
			    Fail [_StencilFail]			//keep
                ZFail [_StencilZFail]
    		}

			HLSLPROGRAM
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE //_MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile _ _SHADOWS_SOFT
			//#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile_fragment _ LIGHTMAP_SHADOW_MIXING	//URP10+版本使用，原指令为：_ _MIXED_LIGHTING_SUBTRACTIVE
			#pragma shader_feature_local _RECEIVE_SHADOWS_OFF
			#pragma shader_feature_local_fragment _BLINNPHONE_LIGHT
			#pragma shader_feature_local_fragment _FOGCONTROL

			#pragma shader_feature_local_fragment _CLIP_PLANE
			#pragma shader_feature_local_fragment _PLANE_NORMAL_OS
			#pragma shader_feature_local_fragment _SECOND_CLIP_PLANE_ON
			#pragma shader_feature_local_fragment _INVERSE_COLOR
			//#pragma shader_feature_local_fragment _DISSOLVE
			//#pragma shader_feature_local_fragment _DISSOLVE_UP

			#pragma multi_compile_instancing
			#pragma multi_compile_fog
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x

			#pragma vertex vert
			#pragma fragment frag

			//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Standard_Lighting.hlsl"
			#include "Standard_Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "URPLit_StandardInput.hlsl"

			

			#pragma shader_feature_local_fragment _PBR_SHADING_MODE_ORM_OR_MSO_MODE _PBR_SHADING_MODE_UNITYLIT_METALLICALPHA
			#pragma shader_feature_local _DISSOLVE_NO_DISSOLVE _DISSOLVE_DISSOLVE_TEX _DISSOLVE_DISSOLVE_UP _DISSOLVE_DISSOLVE_DOWN
			//#pragma shader_feature _COLOR_RED _COLOR_GREEN _COLOR_BLUE

			// float SideOfPlane(half3 point, half4 plane)
			// {
			// 	return dot(plane.xyz, point)-plane.w;
			// }

			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float4 lightmapUVOrVertexSH : TEXCOORD0;
				half4 fogFactorAndVertexLight : TEXCOORD1;
				float4 tSpace0 : TEXCOORD2;
				float4 tSpace1 : TEXCOORD3;
				float4 tSpace2 : TEXCOORD4;
				float4 baseUV : TEXCOORD5;
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
				float4 shadowCoord : TEXCOORD6;
				#endif
				float4 vertexPos : TEXCOORD7;

				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			VertexOutput vert( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				o.baseUV.xy = v.texcoord.xy;
				o.baseUV.zw = v.texcoord1.xyzw.xy;
				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				//float3 positionVS = TransformWorldToView( positionWS );
				float4 positionCS = TransformWorldToHClip( positionWS );
				VertexNormalInputs normalInput = GetVertexNormalInputs( v.normalOS, v.tangentOS );
				o.tSpace0 = float4( normalInput.normalWS, positionWS.x);
				o.tSpace1 = float4( normalInput.tangentWS, positionWS.y);
				o.tSpace2 = float4( normalInput.bitangentWS, positionWS.z);

				OUTPUT_LIGHTMAP_UV( v.texcoord1, unity_LightmapST, o.lightmapUVOrVertexSH.xy );
				OUTPUT_SH( normalInput.normalWS.xyz, o.lightmapUVOrVertexSH.xyz );

				half3 vertexLight = VertexLighting( positionWS, normalInput.normalWS );
				half fogFactor = ComputeFogFactor( positionCS.z );
				o.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
				
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
				VertexPositionInputs vertexInput = (VertexPositionInputs)0;
				vertexInput.positionWS = positionWS;
				vertexInput.positionCS = positionCS;
				o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				
				o.clipPos = positionCS;
				o.vertexPos = v.vertex;
				return o;
			}

			float3 MixFogColorIntensity(half3 fragColor, float3 fogColor, float fogFactor,float intensity)
			{
				#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
				if (IsFogEnabled())
				{
					//float fogIntensity = ComputeFogIntensity(fogFactor) * intensity;
					//float fogIntensity1 = lerp(0,fogIntensity,intensity)
					//fragColor = lerp(fogColor, fragColor, fogIntensity);
    				fragColor = lerp(fragColor, fogColor, intensity);

				}
				#endif
				return fragColor;
			}

			half remap(half x, half t1, half t2, half s1, half s2)
			{
				return (x - t1) / (t2 - t1) * (s2 - s1) + s1;
			}

			half4 frag ( VertexOutput IN) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
				//return half4(0,0,0,1);
				/**/
				float3 WorldNormal = normalize( IN.tSpace0.xyz );
				float3 WorldTangent = IN.tSpace1.xyz;
				float3 WorldBiTangent = IN.tSpace2.xyz;

				float3 WorldPosition = float3(IN.tSpace0.w,IN.tSpace1.w,IN.tSpace2.w);
				float3 ObjectPosition = mul(unity_WorldToObject, float4(WorldPosition, 1.0)).xyz;
				float3 WorldViewDirection = _WorldSpaceCameraPos.xyz  - WorldPosition;
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					ShadowCoords = IN.shadowCoord;
				#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
				#endif
	
				WorldViewDirection = SafeNormalize( WorldViewDirection );

				float2 uv_MainTex = IN.baseUV.xy * (_MainTex_ST.xy * _MainTexUVCoord.xy) + (_MainTex_ST.zw + _MainTexUVCoord.zw);
				half4 mBaseColor = tex2D( _MainTex, uv_MainTex );
				
				float2 uv_NormalMap = IN.baseUV.xy * (_NormalMap_ST.xy * _MainTexUVCoord.xy) + (_NormalMap_ST.zw + _MainTexUVCoord.zw);
				half3 mNorScale = UnpackNormalScale( tex2D( _NormalMap, uv_NormalMap ), _Normal_Scale );
				mNorScale.z = lerp( 1, mNorScale.z, saturate(_Normal_Scale) );
				
				float2 uv_EmissionMap = IN.baseUV.xy * (_EmissionMap_ST.xy * _MainTexUVCoord.xy) + (_EmissionMap_ST.zw + _MainTexUVCoord.zw);
				float2 uv2_EmissionMap = IN.baseUV.zw * _EmissionMap_ST.xy + _EmissionMap_ST.zw;
				half4 ClampEmission = saturate( ( tex2D( _EmissionMap, (( _UV0_OR_UV1 )?( uv2_EmissionMap ):( uv_EmissionMap )) ) * _Emission_Intensity )  );
				
				float2 uv_MASEMap = IN.baseUV.xy * (_MASEMap_ST.xy * _MainTexUVCoord.xy) + (_MASEMap_ST.zw + _MainTexUVCoord.zw);
				half4 MSO_BaseTex = tex2D( _MASEMap, uv_MASEMap );
				half msoMetallicTemp = saturate( ( MSO_BaseTex.r * _MetallicMap_Intensity ));
				half ormMetallicTemp = saturate( ( MSO_BaseTex.b * _MetallicMap_ORM_Intensity ) );
				half mRoughnessTemp = saturate( ( (( _UseRoughnessMap )?( ( 1.0 - MSO_BaseTex.g ) ):( MSO_BaseTex.g )) * _SmoothnessMap_Intensity )  );
				half mAOTemp = saturate( ( MSO_BaseTex.b * _AOMap_Intensity ) );
				half mAOTemp2 = saturate( ( MSO_BaseTex.r * _AOMap_ORM_Intensity ) );
				half3 mORM_MSO_Mode = (half3(((_ORM_Metallic)?(ormMetallicTemp):(((_UseMetallicMap)?(msoMetallicTemp):(_MetallicValue)))) , ((_UseSmoothnessMap)?(mRoughnessTemp):(_SmoothnessValue)) , ((_ORM_AO)?(mAOTemp2):(((_UseAOMap)?(mAOTemp ):(1.0)) ))));

				half3 mMetallicAlpha_Mode = (half3(( _MetallicMap_Intensity * MSO_BaseTex.r) , saturate(MSO_BaseTex.a * _SmoothnessMap_Intensity) , ((_Use_AOMap_MetallicAlpha)?(saturate(( MSO_BaseTex.g * _AOMap_Intensity))):( 1.0 ))));
				
				#if defined(_PBR_SHADING_MODE_ORM_OR_MSO_MODE)
					half3 mShadingMode = mORM_MSO_Mode;
					//return float4(1,0,0,1);
				#elif defined(_PBR_SHADING_MODE_UNITYLIT_METALLICALPHA)
					half3 mShadingMode = mMetallicAlpha_Mode;
					//return float4(0,1,0,1);
				#else
					half3 mShadingMode = mORM_MSO_Mode;
				#endif

				/*
				#if defined(_DISSOLVE_DISSOLVE_TEX)
					return float4(1,0,0,1);
				#elif defined(_DISSOLVE_DISSOLVE_UP)
					return float4(0,1,0,1);
				#else
					//return float4(0,0,1,1);
				#endif*/

				/*
				#ifdef _COLOR_RED
					return float4(1, 0, 0, 1);
                #elif _COLOR_GREEN
					return float4(0, 1, 0, 1);
                #elif _COLOR_BLUE
					return float4(0,0,1,1);
                #endif*/




				half3 msoCollections = mShadingMode;
				
				half3 Albedo = ( _Albedo_Strength * ( mBaseColor * _BaseColor ) ).rgb;
				half3 Normal = mNorScale;
				half3 Emission = (( _UseEmissionMap )?( ClampEmission ):( _EmissionColor )).rgb;
				#if defined(_DISSOLVE_DISSOLVE_TEX)
					float2 uv_NoiseMap = IN.baseUV.xy * _NoiseMap_ST.xy + _NoiseMap_ST.zw;
					//_CutoffHeight1 = remap(_CutoffHeight1,0,1,-1,0.2);
					float temp_output_17_0 = ( tex2D( _NoiseMap, uv_NoiseMap ).r + _CutoffHeight1 );
					_EdgeWidth = _EdgeWidth / 100;
					Emission = Emission + ( _EdgeColor * step( temp_output_17_0 , ( _EdgeWidth ) ) ).rgb;
				#elif defined(_DISSOLVE_DISSOLVE_UP)
					float2 uv_NoiseMap = IN.baseUV.xy * _NoiseMap_ST.xy + _NoiseMap_ST.zw;
					float temp_output_17_0 = ( tex2D( _NoiseMap, uv_NoiseMap ).r + _CutoffHeight );

					float normalizationX =  (IN.vertexPos.xyz.x - _BoundsMin.x) / _BoundsSize.x * _DissloveXYZ.x;
					float normalizationY =  (IN.vertexPos.xyz.y - _BoundsMin.y) / _BoundsSize.y * _DissloveXYZ.y;
					float normalizationZ =  (IN.vertexPos.xyz.z - _BoundsMin.z) / _BoundsSize.z * _DissloveXYZ.z;
					float normalizationCuff = max(max(normalizationX,normalizationY),normalizationZ);

					Emission = Emission + ( _EdgeColor * step( temp_output_17_0 , ( normalizationCuff + _EdgeWidth ) ) ).rgb;
				#elif defined(_DISSOLVE_DISSOLVE_DOWN)
					float2 uv_NoiseMap = IN.baseUV.xy * _NoiseMap_ST.xy + _NoiseMap_ST.zw;
					float temp_output_17_0 = ( tex2D( _NoiseMap, uv_NoiseMap ).r + _CutoffHeight );
					float normalizationX =  (IN.vertexPos.xyz.x - _BoundsMin.x) / _BoundsSize.x * _DissloveXYZ.x;
					float normalizationY =  (IN.vertexPos.xyz.y - _BoundsMin.y) / _BoundsSize.y * _DissloveXYZ.y;
					float normalizationZ =  (IN.vertexPos.xyz.z - _BoundsMin.z) / _BoundsSize.z * _DissloveXYZ.z;
					float normalizationCuff = max(max(normalizationX,normalizationY),normalizationZ);
					Emission = Emission + ( _EdgeColor * step( ( normalizationCuff - _EdgeWidth ) , temp_output_17_0 ) ).rgb;
				#endif
				half3 Specular = 0.5;
				half Metallic = msoCollections.x;
				half Smoothness = msoCollections.y;
				half Occlusion = msoCollections.z;
				half Alpha = (( _UseAlphaClip )?( mBaseColor.a ):( 1.0 ));


				//#ifdef _ALPHATEST_ON
				clip(Alpha - _ClipingCutout);
				//#endif

				#if defined(_DISSOLVE_DISSOLVE_TEX)
					Alpha = step( -0.01 , temp_output_17_0 );
					clip(Alpha*mBaseColor.a - _ClipingCutout);
				#elif defined(_DISSOLVE_DISSOLVE_UP)
					Alpha = step( normalizationCuff , temp_output_17_0 );
					clip(Alpha*mBaseColor.a - _ClipingCutout);
				#elif defined(_DISSOLVE_DISSOLVE_DOWN)
					Alpha = step( temp_output_17_0,normalizationCuff );
					clip(Alpha*mBaseColor.a - _ClipingCutout);
				#endif

				InputData inputData;
				inputData.positionWS = WorldPosition;
				inputData.viewDirectionWS = WorldViewDirection;
				inputData.shadowCoord = ShadowCoords;

				inputData.normalWS = TransformTangentToWorld(Normal, half3x3( WorldTangent, WorldBiTangent, WorldNormal ));
				inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
				inputData.fogCoord = IN.fogFactorAndVertexLight.x;
				inputData.vertexLighting = IN.fogFactorAndVertexLight.yzw;
				float3 SH = IN.lightmapUVOrVertexSH.xyz;
				inputData.bakedGI = SAMPLE_GI( IN.lightmapUVOrVertexSH.xy, SH, inputData.normalWS );

				//half4 color = UniversalFragmentBlinnPhong(inputData, Albedo, half4(Specular,1), Smoothness, Emission, Alpha, Normal);//inputData.normalWS
				//half4 color = UniversalFragmentPBR(inputData, Albedo, Metallic, Specular, Smoothness, Occlusion, Emission, Alpha);

				
				#ifdef _BLINNPHONE_LIGHT
					half4 color = UniversalFragmentBlinnPhong(inputData, Albedo, half4(Specular,1), Smoothness, Emission, Alpha, Normal);//inputData.normalWS
				#else
					//return half4(0,0,0,1);
					half4 color = UniversalFragmentPBR(inputData, Albedo, Metallic, Specular, Smoothness, Occlusion, Emission, Alpha);
				#endif/**/

				#ifdef _FOGCONTROL
					//color.rgb = MixFogIntensity(color.rgb, IN.fogFactorAndVertexLight.x,_FogIntensity);
					//color.rgb = MixFog1(color.rgb, IN.fogFactorAndVertexLight.x);
					color.rgb = MixFogColorIntensity(color.rgb,unity_FogColor.rgb,IN.fogFactorAndVertexLight.x,_FogIntensity);
				#else
					color.rgb = MixFog(color.rgb, IN.fogFactorAndVertexLight.x);
				#endif/**/


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
						float distance = dot(clipPlane.xyz, WorldPosition)-clipPlane.w;
						#ifdef _SECOND_CLIP_PLANE_ON
							float4 clipPlane2 = float4(normalPlane2, _ClipPlane2.w);
							float distance2 = dot(clipPlane2.xyz, WorldPosition)-clipPlane2.w;
						#endif
					#endif
				

					//clip(distance);
					#ifdef _SECOND_CLIP_PLANE_ON
						float d = min(distance,distance2);
					#else
						float d = distance;
					#endif

					#ifdef _INVERSE_COLOR
						color*=sign(d)>0?_ColorInside:1;
					#else
						color*=sign(d)>0?1:_ColorInside;
					#endif
					
				float t= abs(d)-_LineWidth;
				color += step(t,0)*_LineColor;
				#endif
				
				//color.rgb = MixFog(color.rgb, IN.fogFactorAndVertexLight.x);
				return color;
			}
			ENDHLSL
		}

		// Pass
		// {
		// 	Name "Forward2"
		// 	Tags { "LightMode"="SRPDefaultUnlit" }
			
		// 	ZWrite On
		// 	ZTest LEqual
		// 	Cull [_CullMode]

		// 	Stencil 
		//     {
		// 	    Ref [_StencilMask2]
		// 	    Comp [_StencilComp2]//Less	 //2:Less 5:NotEqual [_StencilComp]
		// 	    Pass [_StencilPass2]			//keep
		// 	    Fail [_StencilFail2]			//keep
        //         ZFail [_StencilZFail2]
    	// 	}

		// 	HLSLPROGRAM
		// 	#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE //_MAIN_LIGHT_SHADOWS_SCREEN
		// 	#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
		// 	#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
		// 	#pragma multi_compile _ _SHADOWS_SOFT
		// 	//#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
		// 	#pragma multi_compile _ DIRLIGHTMAP_COMBINED
		// 	#pragma multi_compile _ LIGHTMAP_ON
		// 	#pragma multi_compile_fragment _ LIGHTMAP_SHADOW_MIXING	//URP10+版本使用，原指令为：_ _MIXED_LIGHTING_SUBTRACTIVE
		// 	#pragma shader_feature_local _RECEIVE_SHADOWS_OFF
		// 	#pragma shader_feature_local_fragment _BLINNPHONE_LIGHT
		// 	#pragma shader_feature_local_fragment _FOGCONTROL

		// 	#pragma shader_feature_local_fragment _CLIP_PLANE
		// 	#pragma shader_feature_local_fragment _PLANE_NORMAL_OS

		// 	#pragma multi_compile_instancing
		// 	#pragma multi_compile_fog
		// 	#pragma prefer_hlslcc gles
		// 	#pragma exclude_renderers d3d11_9x

		// 	#pragma vertex vert
		// 	#pragma fragment frag

		// 	//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		// 	//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		// 	#include "Standard_Lighting.hlsl"
		// 	#include "Standard_Core.hlsl"
		// 	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
		// 	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
		// 	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
		// 	#include "URPLit_StandardInput.hlsl"

		// 	#pragma shader_feature_local_fragment _PBR_SHADING_MODE_ORM_OR_MSO_MODE _PBR_SHADING_MODE_UNITYLIT_METALLICALPHA


		// 	struct VertexInput
		// 	{
		// 		float4 vertex : POSITION;
		// 		float3 normalOS : NORMAL;
		// 		float4 tangentOS : TANGENT;
		// 		float4 texcoord1 : TEXCOORD1;
		// 		float4 texcoord : TEXCOORD0;
		// 		UNITY_VERTEX_INPUT_INSTANCE_ID
		// 	};

		// 	struct VertexOutput
		// 	{
		// 		float4 clipPos : SV_POSITION;
		// 		float4 lightmapUVOrVertexSH : TEXCOORD0;
		// 		half4 fogFactorAndVertexLight : TEXCOORD1;
		// 		float4 tSpace0 : TEXCOORD2;
		// 		float4 tSpace1 : TEXCOORD3;
		// 		float4 tSpace2 : TEXCOORD4;
		// 		float4 baseUV : TEXCOORD5;
		// 		#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
		// 		float4 shadowCoord : TEXCOORD6;
		// 		#endif

		// 		UNITY_VERTEX_INPUT_INSTANCE_ID
		// 		UNITY_VERTEX_OUTPUT_STEREO
		// 	};

			
		// 	VertexOutput vert( VertexInput v  )
		// 	{
		// 		VertexOutput o = (VertexOutput)0;
		// 		UNITY_SETUP_INSTANCE_ID(v);
		// 		UNITY_TRANSFER_INSTANCE_ID(v, o);
		// 		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

		// 		o.baseUV.xy = v.texcoord.xy;
		// 		o.baseUV.zw = v.texcoord1.xyzw.xy;
		// 		float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
		// 		//float3 positionVS = TransformWorldToView( positionWS );
		// 		float4 positionCS = TransformWorldToHClip( positionWS );
		// 		VertexNormalInputs normalInput = GetVertexNormalInputs( v.normalOS, v.tangentOS );
		// 		o.tSpace0 = float4( normalInput.normalWS, positionWS.x);
		// 		o.tSpace1 = float4( normalInput.tangentWS, positionWS.y);
		// 		o.tSpace2 = float4( normalInput.bitangentWS, positionWS.z);

		// 		OUTPUT_LIGHTMAP_UV( v.texcoord1, unity_LightmapST, o.lightmapUVOrVertexSH.xy );
		// 		OUTPUT_SH( normalInput.normalWS.xyz, o.lightmapUVOrVertexSH.xyz );

		// 		half3 vertexLight = VertexLighting( positionWS, normalInput.normalWS );
		// 		half fogFactor = ComputeFogFactor( positionCS.z );
		// 		o.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
				
		// 		#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
		// 		VertexPositionInputs vertexInput = (VertexPositionInputs)0;
		// 		vertexInput.positionWS = positionWS;
		// 		vertexInput.positionCS = positionCS;
		// 		o.shadowCoord = GetShadowCoord( vertexInput );
		// 		#endif
				
		// 		o.clipPos = positionCS;
		// 		return o;
		// 	}

		// 	float3 MixFogColorIntensity(half3 fragColor, float3 fogColor, float fogFactor,float intensity)
		// 	{
		// 		#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
		// 		if (IsFogEnabled())
		// 		{
		// 			//float fogIntensity = ComputeFogIntensity(fogFactor) * intensity;
		// 			//float fogIntensity1 = lerp(0,fogIntensity,intensity)
		// 			//fragColor = lerp(fogColor, fragColor, fogIntensity);
    	// 			fragColor = lerp(fragColor, fogColor, intensity);

		// 		}
		// 		#endif
		// 		return fragColor;
		// 	}

			

		// 	half4 frag ( VertexOutput IN) : SV_Target
		// 	{
		// 		UNITY_SETUP_INSTANCE_ID(IN);
		// 		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
		// 		//return half4(0,0,0,1);
		// 		/**/
		// 		float3 WorldNormal = normalize( IN.tSpace0.xyz );
		// 		float3 WorldTangent = IN.tSpace1.xyz;
		// 		float3 WorldBiTangent = IN.tSpace2.xyz;

		// 		float3 WorldPosition = float3(IN.tSpace0.w,IN.tSpace1.w,IN.tSpace2.w);
		// 		float3 ObjectPosition = mul(unity_WorldToObject, float4(WorldPosition, 1.0)).xyz;
		// 		float3 WorldViewDirection = _WorldSpaceCameraPos.xyz  - WorldPosition;
		// 		float4 ShadowCoords = float4( 0, 0, 0, 0 );

		// 		#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
		// 			ShadowCoords = IN.shadowCoord;
		// 		#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
		// 			ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
		// 		#endif
	
		// 		WorldViewDirection = SafeNormalize( WorldViewDirection );

		// 		float2 uv_MainTex = IN.baseUV.xy * (_MainTex_ST.xy * _MainTexUVCoord.xy) + (_MainTex_ST.zw + _MainTexUVCoord.zw);
		// 		half4 mBaseColor = tex2D( _MainTex, uv_MainTex );
				
		// 		float2 uv_NormalMap = IN.baseUV.xy * (_NormalMap_ST.xy * _MainTexUVCoord.xy) + (_NormalMap_ST.zw + _MainTexUVCoord.zw);
		// 		half3 mNorScale = UnpackNormalScale( tex2D( _NormalMap, uv_NormalMap ), _Normal_Scale );
		// 		mNorScale.z = lerp( 1, mNorScale.z, saturate(_Normal_Scale) );
				
		// 		float2 uv_EmissionMap = IN.baseUV.xy * (_EmissionMap_ST.xy * _MainTexUVCoord.xy) + (_EmissionMap_ST.zw + _MainTexUVCoord.zw);
		// 		float2 uv2_EmissionMap = IN.baseUV.zw * _EmissionMap_ST.xy + _EmissionMap_ST.zw;
		// 		half4 ClampEmission = saturate( ( tex2D( _EmissionMap, (( _UV0_OR_UV1 )?( uv2_EmissionMap ):( uv_EmissionMap )) ) * _Emission_Intensity )  );
				
		// 		float2 uv_MASEMap = IN.baseUV.xy * (_MASEMap_ST.xy * _MainTexUVCoord.xy) + (_MASEMap_ST.zw + _MainTexUVCoord.zw);
		// 		half4 MSO_BaseTex = tex2D( _MASEMap, uv_MASEMap );
		// 		half msoMetallicTemp = saturate( ( MSO_BaseTex.r * _MetallicMap_Intensity ));
		// 		half ormMetallicTemp = saturate( ( MSO_BaseTex.b * _MetallicMap_ORM_Intensity ) );
		// 		half mRoughnessTemp = saturate( ( (( _UseRoughnessMap )?( ( 1.0 - MSO_BaseTex.g ) ):( MSO_BaseTex.g )) * _SmoothnessMap_Intensity )  );
		// 		half mAOTemp = saturate( ( MSO_BaseTex.b * _AOMap_Intensity ) );
		// 		half mAOTemp2 = saturate( ( MSO_BaseTex.r * _AOMap_ORM_Intensity ) );
		// 		half3 mORM_MSO_Mode = (half3(((_ORM_Metallic)?(ormMetallicTemp):(((_UseMetallicMap)?(msoMetallicTemp):(_MetallicValue)))) , ((_UseSmoothnessMap)?(mRoughnessTemp):(_SmoothnessValue)) , ((_ORM_AO)?(mAOTemp2):(((_UseAOMap)?(mAOTemp ):(1.0)) ))));

		// 		half3 mMetallicAlpha_Mode = (half3(( _MetallicMap_Intensity * MSO_BaseTex.r) , saturate(MSO_BaseTex.a * _SmoothnessMap_Intensity) , ((_Use_AOMap_MetallicAlpha)?(saturate(( MSO_BaseTex.g * _AOMap_Intensity))):( 1.0 ))));
		// 		#if defined(_PBR_SHADING_MODE_ORM_OR_MSO_MODE)
		// 			half3 mShadingMode = mORM_MSO_Mode;
		// 		#elif defined(_PBR_SHADING_MODE_UNITYLIT_METALLICALPHA)
		// 			half3 mShadingMode = mMetallicAlpha_Mode;
		// 		#else
		// 		half3 mShadingMode = mORM_MSO_Mode;
		// 		#endif
		// 		half3 msoCollections = mShadingMode;
				
		// 		half3 Albedo = ( _Albedo_Strength * ( mBaseColor * _BaseColor ) ).rgb;
		// 		half3 Normal = mNorScale;
		// 		half3 Emission = (( _UseEmissionMap )?( ClampEmission ):( _EmissionColor )).rgb;
		// 		half3 Specular = 0.5;
		// 		half Metallic = msoCollections.x;
		// 		half Smoothness = msoCollections.y;
		// 		half Occlusion = msoCollections.z;
		// 		half Alpha = (( _UseAlphaClip )?( mBaseColor.a ):( 1.0 ));
		// 		//#ifdef _ALPHATEST_ON
		// 		clip(Alpha - _ClipingCutout);
		// 		//#endif

		// 		InputData inputData;
		// 		inputData.positionWS = WorldPosition;
		// 		inputData.viewDirectionWS = WorldViewDirection;
		// 		inputData.shadowCoord = ShadowCoords;

		// 		inputData.normalWS = TransformTangentToWorld(Normal, half3x3( WorldTangent, WorldBiTangent, WorldNormal ));
		// 		inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
		// 		inputData.fogCoord = IN.fogFactorAndVertexLight.x;
		// 		inputData.vertexLighting = IN.fogFactorAndVertexLight.yzw;
		// 		float3 SH = IN.lightmapUVOrVertexSH.xyz;
		// 		inputData.bakedGI = SAMPLE_GI( IN.lightmapUVOrVertexSH.xy, SH, inputData.normalWS );

		// 		//half4 color = UniversalFragmentBlinnPhong(inputData, Albedo, half4(Specular,1), Smoothness, Emission, Alpha, Normal);//inputData.normalWS
		// 		//half4 color = UniversalFragmentPBR(inputData, Albedo, Metallic, Specular, Smoothness, Occlusion, Emission, Alpha);

				
		// 		#ifdef _BLINNPHONE_LIGHT
		// 			half4 color = UniversalFragmentBlinnPhong(inputData, Albedo, half4(Specular,1), Smoothness, Emission, Alpha, Normal);//inputData.normalWS
		// 		#else
		// 			//return half4(0,0,0,1);
		// 			half4 color = UniversalFragmentPBR(inputData, Albedo, Metallic, Specular, Smoothness, Occlusion, Emission, Alpha);
		// 		#endif/**/

		// 		#ifdef _FOGCONTROL
		// 			//color.rgb = MixFogIntensity(color.rgb, IN.fogFactorAndVertexLight.x,_FogIntensity);
		// 			//color.rgb = MixFog1(color.rgb, IN.fogFactorAndVertexLight.x);
		// 			color.rgb = MixFogColorIntensity(color.rgb,unity_FogColor.rgb,IN.fogFactorAndVertexLight.x,_FogIntensity);
		// 		#else
		// 			color.rgb = MixFog(color.rgb, IN.fogFactorAndVertexLight.x);
		// 		#endif/**/


				
		// 		#ifdef _CLIP_PLANE
		// 		float3 normalPlane =normalize(_ClipPlane.xyz);
		// 		float4 clipPlane = 0;
		// 		float distance =0;
		// 			#ifdef _PLANE_NORMAL_OS
		// 				normalPlane = TransformWorldToObjectDir(normalPlane);
		// 				clipPlane = float4(normalPlane, _ClipPlane.w);
		// 				distance = dot(clipPlane.xyz, ObjectPosition)-clipPlane.w;
		// 			#else
		// 				normalPlane = normalPlane;
		// 				clipPlane = float4(normalPlane, _ClipPlane.w);
		// 				distance = dot(clipPlane.xyz, WorldPosition)-clipPlane.w;
		// 			#endif	
		// 	    clip(-distance);
		// 		float t= abs(distance)-_LineWidth*0.5;
		// 		color*=_ColorInside;
		// 		color += step(t,0)*_LineColor;
		// 		#endif
				
		// 		//color.rgb = MixFog(color.rgb, IN.fogFactorAndVertexLight.x);
		// 		return color;
		// 	}
		// 	ENDHLSL
		// }

		Pass
		{
			Name "ShadowCaster"
			Tags { "LightMode"="ShadowCaster" }

			ZWrite On
			ZTest LEqual
			ColorMask 0
			Cull [_CullMode]

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "URPLit_StandardInput.hlsl"

			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float3 worldPos : TEXCOORD0;
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) //&& defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord : TEXCOORD1;
				#endif
				float4 ase_texcoord2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			VertexOutput vert ( VertexInput v )
			{
				VertexOutput o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );

				o.ase_texcoord2.xy = v.ase_texcoord.xy;
				o.ase_texcoord2.zw = 0;

				o.worldPos = TransformObjectToWorld( v.vertex.xyz );
				float3 normalWS = TransformObjectToWorldDir(v.normalOS);
				float4 clipPos = TransformWorldToHClip( ApplyShadowBias( o.worldPos, normalWS, _LightDirection ) );

				#if UNITY_REVERSED_Z
					clipPos.z = min(clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE);
				#else
					clipPos.z = max(clipPos.z, clipPos.w * UNITY_NEAR_CLIP_VALUE);
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					VertexPositionInputs vertexInput = (VertexPositionInputs)0;
					vertexInput.positionWS = o.worldPos;
					vertexInput.positionCS = clipPos;
					o.shadowCoord = GetShadowCoord( vertexInput );
				#endif
				o.clipPos = clipPos;
				return o;
			}

			half4 frag(	VertexOutput IN) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );
				
				float3 WorldPosition = IN.worldPos;
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					ShadowCoords = IN.shadowCoord;
				#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
				#endif

				float2 uv_MainTex = IN.ase_texcoord2.xy * (_MainTex_ST.xy * _MainTexUVCoord.xy) + (_MainTex_ST.zw + _MainTexUVCoord.zw);
				half4 mBaseColor = tex2D( _MainTex, uv_MainTex );
				half Alpha = (( _UseAlphaClip )?( mBaseColor.a ):( 1.0 ));
				clip(Alpha - _ClipingCutout);

				return 0;
			}
			ENDHLSL
		}

		Pass
		{
			Name "DepthOnly"
			Tags { "LightMode"="DepthOnly" }

			ZWrite On
			ColorMask 0
			ZTest LEqual
			Cull [_CullMode]

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "URPLit_StandardInput.hlsl"

			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float4 ase_texcoord2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			VertexOutput vert ( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				UNITY_SETUP_INSTANCE_ID(v);
				//UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

				o.ase_texcoord2.xy = v.ase_texcoord.xy;
				o.ase_texcoord2.zw = 0;
				float3 positionWS = TransformObjectToWorld( v.vertex.xyz );
				float4 positionCS = TransformWorldToHClip( positionWS );
				o.clipPos = positionCS;
				return o;
			}

			half4 frag(	VertexOutput IN) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );
				float2 uv_MainTex = IN.ase_texcoord2.xy * (_MainTex_ST.xy * _MainTexUVCoord.xy) + (_MainTex_ST.zw + _MainTexUVCoord.zw);
				half4 mBaseColor = tex2D( _MainTex, uv_MainTex );
				half Alpha = (( _UseAlphaClip )?( mBaseColor.a ):( 1.0 ));

				//#ifdef _ALPHATEST_ON
					clip(Alpha - _ClipingCutout);
				//#endif

				return 0;
			}
			ENDHLSL
		}
		
		Pass
		{
			Name "Meta"
			Tags { "LightMode"="Meta" }
			Cull Off

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "URPLit_StandardInput.hlsl"

			struct VertexInput
			{
				float4 vertex : POSITION;
				float3 normalOS : NORMAL;
				float4 texcoord1 : TEXCOORD1;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VertexOutput
			{
				float4 clipPos : SV_POSITION;
				float3 worldPos : TEXCOORD0;
				float4 ase_texcoord2 : TEXCOORD1;
			};
			
			VertexOutput vert( VertexInput v  )
			{
				VertexOutput o = (VertexOutput)0;
				o.ase_texcoord2.xy = v.ase_texcoord.xy;
				o.ase_texcoord2.zw = v.texcoord1.xy;
				o.worldPos = TransformObjectToWorld( v.vertex.xyz );
				o.clipPos = MetaVertexPosition( v.vertex, v.texcoord1.xy, v.texcoord1.xy, unity_LightmapST, unity_DynamicLightmapST );
				return o;
			}

			half4 frag(VertexOutput IN) : SV_TARGET
			{
				float2 uv_MainTex = IN.ase_texcoord2.xy * (_MainTex_ST.xy * _MainTexUVCoord.xy) + (_MainTex_ST.zw + _MainTexUVCoord.zw);
				half4 mBaseColor = tex2D( _MainTex, uv_MainTex );
				float2 uv_EmissionMap = IN.ase_texcoord2.xy * (_EmissionMap_ST.xy * _MainTexUVCoord.xy) + (_EmissionMap_ST.zw + _MainTexUVCoord.zw);
				float2 uv2_EmissionMap = IN.ase_texcoord2.zw * _EmissionMap_ST.xy + _EmissionMap_ST.zw;
				half4 ClampEmission = clamp( ( tex2D( _EmissionMap, (( _UV0_OR_UV1 )?( uv2_EmissionMap ):( uv_EmissionMap )) ) * _Emission_Intensity ) , half4( 0,0,0,0 ) , half4( 1,1,1,0 ) );	
				half3 Albedo = ( _Albedo_Strength * ( mBaseColor * _BaseColor ) ).rgb;
				half3 Emission = (( _UseEmissionMap )?( ClampEmission ):( _EmissionColor )).rgb;
				half Alpha = (( _UseAlphaClip )?( mBaseColor.a ):( 1.0 ));

				//#ifdef _ALPHATEST_ON
				clip(Alpha - _ClipingCutout);
				//#endif

				MetaInput metaInput = (MetaInput)0;
				metaInput.Albedo = Albedo;
				metaInput.Emission = Emission;
				
				return MetaFragment(metaInput);
			}
			ENDHLSL
		}
	}
	CustomEditor "LWGUI.LWGUI"
	Fallback "Hidden/InternalErrorShader"
}
