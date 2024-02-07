#ifndef DISPLACEMENT_INPUT_INCLUDED
#define DISPLACEMENT_INPUT_INCLUDED



#define _TESSELLATION_EDGE

CBUFFER_START(UnityPerMaterial_Displacement)
float _DisplacementStrength;
float _TessellationUniform;
float _TessellationEdgeLength;
float  _UVScale;
CBUFFER_END

TEXTURE2D(_DisplacementMap);    SAMPLER(sampler_DisplacementMap);

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 uv     : TEXCOORD0;

};


struct TessellationControlPoint
{
    float4 positionOS:INTERNALTESSPOS;
    float3 normalOS:TEXCOORD0;
    float3 normalWS: TEXCOORD1;
    float4 tangentOS:TEXCOORD2;
    float4 tangentWS:TEXCOORD3;
    float2 uv:TEXCOORD4;

    
};

struct Varyings
{

    float4 positionCS : SV_POSITION;
    float3 positionWS: TEXCOORD0;
    float3 normalWS: TEXCOORD1;
    float2 uv:TEXCOORD2;
    half4 tangentWS:TEXCOORD3;
    float4 shadowCoord : TEXCOORD4;
    float  texlerpFactor : TEXCOORD5;
};

struct TessellationFactors {
    float edges[3] : SV_TessFactor;
    float inside : SV_InsideTessFactor;
};

bool TriangleIsBelowClipPlane(float3 p0, float3 p1, float3 p2,int index,float bias)
{
    float4 plane = unity_CameraWorldClipPlanes[index];
    return dot(float4(p0,1.0),plane) < bias && dot(float4(p1,1.0),plane) < bias && dot(float4(p2,1.0),plane) < bias;

   

}

bool TriangleIsCulled(float3 p0, float3 p1, float3 p2,float bias)
{
    //return p0.x < 0 && p1.x < 0 && p2.x < 0;
    return  TriangleIsBelowClipPlane(p0, p1, p2,0,bias)||
            TriangleIsBelowClipPlane(p0, p1, p2,1,bias)||
            TriangleIsBelowClipPlane(p0, p1, p2,2,bias)||
            TriangleIsBelowClipPlane(p0, p1, p2,3,bias);

}

float TessellationEdgeFactor (TessellationControlPoint cp0, TessellationControlPoint cp1) 
{
    #if defined(_TESSELLATION_EDGE)
        #if defined(_TESSELLATION_EDGE_SCREEN_SPACE)
        float4 p0 = ComputeScreenPos(TransformObjectToHClip(cp0.positionOS.xyz));
        float3 scrP0 = float3(p0.xy / p0.w, p0.z/ p0.w);
        float4 p1 = ComputeScreenPos(TransformObjectToHClip(cp1.positionOS.xyz));
        float3 scrP1 = float3(p1.xy / p1.w, p1.z/ p1.w);
        float edgeLength = distance(scrP0, scrP1);
        return edgeLength*_ScreenParams.y / _TessellationEdgeLength;
        #endif

        float3 p0 = TransformObjectToWorld(cp0.positionOS.xyz);
        float3 p1 = TransformObjectToWorld(cp1.positionOS.xyz);
        float3 edgeCenter = (p0+p1)*0.5;
        float edgeLength = distance(p0, p1);
        float viewDistance = distance(edgeCenter, _WorldSpaceCameraPos);
        return edgeLength*_ScreenParams.y*rcp(_TessellationEdgeLength * viewDistance);

    #else
        return _TessellationUniform;
    #endif
}


TessellationFactors MyPatchConstantFunction (InputPatch<TessellationControlPoint,3> patch)
{
    float3 p0 = TransformObjectToWorld(patch[0].positionOS.xyz);
	float3 p1 = TransformObjectToWorld(patch[1].positionOS.xyz);
	float3 p2 = TransformObjectToWorld(patch[2].positionOS.xyz);
    TessellationFactors f;

    float bias =-0.5 * _DisplacementStrength;;
    if (TriangleIsCulled(p0,p1,p2,bias))
    {
        f.edges[0] = 0.0;
        f.edges[1] = 0.0; 
        f.edges[2] = 0.0; 
        f.inside = 0.0;// culled triangle
    }
    else
    {
        f.edges[0] = TessellationEdgeFactor(patch[1], patch[2]);
        f.edges[1] = TessellationEdgeFactor(patch[2], patch[0]);
        f.edges[2] = TessellationEdgeFactor(patch[0], patch[1]);
        f.inside = (f.edges[0] + f.edges[1] + f.edges[2])*rcp(3.0);
    }
    return f;
}




[domain("tri")]//deal with triangle
[outputcontrolpoints(3)] //fingerout ever control point control ever angle in triangle
[outputtopology("triangle_cw")]//fingerout triangle direction
[partitioning("fractional_even")]//fingerout how to partition the domain
[patchconstantfunc("MyPatchConstantFunction")]//define function to partition triangle //erver patch call onece not ever control point
TessellationControlPoint MyHullProgram(InputPatch<TessellationControlPoint,3> patch,
                                        uint id :SV_OutputControlPointID)
{
    return patch[id];
}




[domain("tri")]
Varyings MyDomainProgram(    TessellationFactors factors,
                             OutputPatch<TessellationControlPoint,3> patch,
                             float3 barycentricCoord :SV_DomainLocation)
{

    #define DOMAIN_PROGRAM_INTERPOLATE(fieldName)   patch[0].fieldName * barycentricCoord.x + \
                                                    patch[1].fieldName * barycentricCoord.y + \
                                                    patch[2].fieldName * barycentricCoord.z;
    Varyings output = (Varyings)0;                                         
                                                  
    float4 positionOS =  DOMAIN_PROGRAM_INTERPOLATE(positionOS);
    float3 normalOS   =  DOMAIN_PROGRAM_INTERPOLATE(normalOS);
    float2 uv         =  DOMAIN_PROGRAM_INTERPOLATE(uv);
    float4 tangentOS  =  DOMAIN_PROGRAM_INTERPOLATE(tangentOS);
    float3 normalWS   =  DOMAIN_PROGRAM_INTERPOLATE(normalWS);
    float4 tangentWS  =  DOMAIN_PROGRAM_INTERPOLATE(tangentWS);

    
    uv = TRANSFORM_TEX(uv, _BaseMap);
    
    float displacement  = SAMPLE_TEXTURE2D_LOD(_DisplacementMap,sampler_DisplacementMap,uv,0).r;
    float displacement2 = SAMPLE_TEXTURE2D_LOD(_DisplacementMap,sampler_DisplacementMap,uv+float2(0.0001,0),0).r;
    float displacement3 = SAMPLE_TEXTURE2D_LOD(_DisplacementMap,sampler_DisplacementMap,uv+float2(0,0.0001),0).r;
    //displacement = (displacement - 0.5) * _DisplacementStrength;
    output.texlerpFactor = displacement;
    displacement  = displacement  * _DisplacementStrength;
    displacement2 = displacement2 * _DisplacementStrength;
    displacement3 = displacement3 * _DisplacementStrength;

    float3 v0 =positionOS.xyz;
    float3 bitangentOS =normalize( cross(normalOS,tangentOS.xyz))*tangentOS.w;
    float3 v1 =v0 + tangentOS.xyz*0.01;
    float3 v2 =v0 + bitangentOS*0.01;
    v0.xyz +=normalOS*displacement;
    v1.xyz +=normalOS*displacement2;
    v2.xyz +=normalOS*displacement3;
 
    normalOS = normalize(normalOS);
    positionOS.xyz -= normalOS*displacement;
    positionOS.xyz += normalOS*_DisplacementStrength;



    float3 constructNormal =normalize(cross(v2-v0,v1-v0));

    output.positionCS = TransformObjectToHClip(positionOS.xyz);
    //output.normalWS =normalWS;
    output.normalWS =TransformObjectToWorldNormal(constructNormal);
    output.uv = uv;
    output.tangentWS = tangentWS;

    return output;
}





#endif