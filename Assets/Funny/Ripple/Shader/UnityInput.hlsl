
#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

/*
float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 unity_MatrixInvV;
float4x4 unity_prev_MatrixM;
float4x4 unity_prev_MatrixIM;
float4x4 glstate_matrix_projection;
real4 unity_WorldTransformParams;
*/

struct appdata
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    
    float2 uv:TEXCOORD0;
};

struct v2f
{
    float2 uv : TEXCOORD0;
    float3 normalWS: TEXCOORD1;
    float4 positionCS : SV_POSITION;
};


#endif
