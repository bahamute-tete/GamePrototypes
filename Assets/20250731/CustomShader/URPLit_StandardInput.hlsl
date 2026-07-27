#ifndef URPLIT_STANDARDINPUT_INCLUDED
#define URPLIT_STANDARDINPUT_INCLUDED

	CBUFFER_START(UnityPerMaterial)

	half _UseRoughnessMap;
	half _UseAOMap;
	half _Use_AOMap_MetallicAlpha;
	half _UseSmoothnessMap;
	half _UV0_OR_UV1;
	half _UseEmissionMap;
	half _UseMetallicMap;
	half _UseAlphaClip;
	half _UseFogControl;

	half4 _EmissionColor;
	half4 _BaseColor;

	float4 _NormalMap_ST;
	float4 _EmissionMap_ST;
	float4 _MainTex_ST;
	float4 _MASEMap_ST;
	float4 _MainTexUVCoord;
	
	half _AOMap_Intensity;
	half _MetallicMap_Intensity;
	half _SmoothnessMap_Intensity;
	half _AOMap_ORM_Intensity;
	half _SmoothnessValue;
	half _MetallicMap_ORM_Intensity;
	half _Emission_Intensity;
	half _Albedo_Strength;
	half _MetallicValue;

	half _ORM_AO;
	half _MainProperties;
	half _ORM_Metallic;
	half _ORMMapTex;
	half _EmissionMapTex;
	half _NormalMapTex;
	half _BaseMapTex;
	
	half _ClipingCutout;

	half _Normal_Scale;
	half4 _UVCoords;
	half _Metallic;
	half _Smoothness;

	half _FogIntensity;

	float4 _EdgeColor;
	float4 _NoiseMap_ST;
	float _CutoffHeight;
	float _CutoffHeight1;
	float _EdgeWidth;
	float4 _BoundsMin;
	float4 _BoundsSize;
	float4 _DissloveXYZ;


	#ifdef _CLIP_PLANE
		float _LineWidth;
		float4 _ColorInside;
		float4 _LineColor;
		float4 _ClipPlane;
		float4 _ClipPlane2;
	#endif
	
	CBUFFER_END

	sampler2D _MainTex;
	sampler2D _NormalMap;
	sampler2D _EmissionMap;
	sampler2D _MASEMap;
	float3 _LightDirection;
	sampler2D _NoiseMap;



#endif
