#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    StructuredBuffer<float3> positionBuffer;
#endif

float3 position;


void ConfigureProcedural()
{
    #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    position =positionBuffer[unity_InstanceID];
    #endif

}

void ShaderGraphicFun_float(out float3 PositionOut)
{
    PositionOut = position;

}