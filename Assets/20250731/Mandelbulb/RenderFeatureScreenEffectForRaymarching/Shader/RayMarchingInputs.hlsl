
struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float2 uv : TEXCOORD0;
};

TEXTURE2D(_CameraOpaqueTexture);
SAMPLER(sampler_CameraOpaqueTexture);

TEXTURE2D(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

TEXTURE3D(_SDFTexture);
SAMPLER(sampler_SDFTexture);

TEXTURECUBE(_EnvironmentCubemap);
SAMPLER(sampler_EnvironmentCubemap);
            
float4x4 _ClipToWorld;
float3 _CameraPos;
float4 _CamParams; // x: near, y: far, z: fov, w: aspect
float3 _LightDirection;
float3 _LightColor;


float4 _SHAr;
float4 _SHAg;
float4 _SHAb;
float4 _SHBr;
float4 _SHBg;
float4 _SHBb;
float4 _SHC;


int _PointLightCount;
float4 _PointLightPosRanges[4]; // xyz: pos, w: range
float4 _PointLightColors[4];


float _AOIntensity;

float3 _FogBoxCenter;
float3 _FogBoxSize;
float _StepSize;
float _FogDensity;
float3 _FogBaseColor,_FogTargetColor;
float  _Absorption,_ScatteringCoeff,_HenyeyGreenstein_G;

float _AmbientLightIntensity,_DirectLightIntensity ;

struct VolumeParams
{
    float3 ro;
    float3 rd;
    float maxT;
    float3 boxCenter;
    float3 boxSize;
    float g;
    float absorption;
    float scatteringCoeff;
    float ambientLightIntensity;
    float directLightIntensity;
    float3 lightDir;
    float3 fogBaseColor;
    float3 fogTargetColor;
    float3 lightColor;
    float stepSize;
    int max_Steps;
    float maxDistance;
    float densityModifier;
};

