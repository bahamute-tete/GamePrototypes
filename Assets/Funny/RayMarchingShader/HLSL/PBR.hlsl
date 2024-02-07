#ifndef RAYMARCHING_PBR_INCLUDED
#define RAYMARCHING_PBR_INCLUDED

//D
float D_DistributionGGX(float3 N,float3 H,float Roughness)
{
    float a             = Roughness*Roughness;
    float a2            = a*a;
    float NH            = saturate(dot(N,H));
    float NH2           = NH*NH;
    float nominator     = a2;
    float denominator   = (NH2*(a2-1.0)+1.0);
    denominator         = PI * denominator*denominator;
    
    return              nominator/ max(denominator,0.001) ;//防止分母为0
}

float D_DistributionBeckmann(float3 N,float3 H,float Roughness)
{
    float rsq = Roughness*Roughness;
    float NdotH = saturate(dot(N,H));

    float r1 = 1. / ( 4. * rsq * pow(NdotH, 4.));
	float r2 = (NdotH * NdotH - 1.) / (rsq * NdotH * NdotH);
	float rough = r1 * exp(r2);
    return rough;
}

//F
float3 F_FrenelSchlick(float3 h,float3 v,float3 F0)
{
    float HV=saturate( dot(h,v));
    float3 res=0;
    return res= F0 +(1 - F0)*pow(1-HV,5);
}

//G
float GeometrySchlickGGX(float3 n,float3 v,float Roughness)
{
    float NV=saturate( dot(n,v));
    float r = Roughness +1.0;
    float k = r*r / 8.0;      //直接光
    float nominator = NV;
    float denominator = k + (1.0-k) * NV;
    return nominator/ max(denominator,0.001) ;//防止分母为0
}

float G_GeometrySmith(float3 N,float3 V,float3 L,float Roughness)
{
    float NV = saturate(dot(N,V));
    float NL = saturate(dot(N,L));

    float ggx1 = GeometrySchlickGGX(N,V,Roughness);
    float ggx2 = GeometrySchlickGGX(N,L,Roughness);

    return ggx1*ggx2;

}

float GeometricAttenuation(float3 N,float3 V,float3 L,float3 H)
{
    float NdotL = saturate(dot(N,L));
    float NdotV = saturate(dot(N,V));
    float NdotH = saturate(dot(N,H));
    float VdotH = saturate(dot(V,H));

    float NH2   = 2. * NdotH / VdotH;
	float geo_b = (NH2 * NdotV );
	float geo_c = (NH2 * NdotL );
	float geo   = min( 1., min( geo_b, geo_c ) );
    return geo;

}

float3 FresnelSchlickRoughness(float NV,float3 F0,float Roughness)
{
    return F0 + (max(float3(1.0 - Roughness, 1.0 - Roughness, 1.0 - Roughness), F0) - F0) * pow(1.0 - NV, 5.0);
}

float2 EnvBRDFApprox_UE4(float Roughness, float NoV )
{
    // [ Lazarov 2013, "Getting More Physical in Call of Duty: Black Ops II" ]
    // Adaptation to fit our G term.
    const float4 c0 = { -1, -0.0275, -0.572, 0.022 };
    const float4 c1 = { 1, 0.0425, 1.04, -0.04 };
    float4 r = Roughness * c0 + c1;
    float a004 = min( r.x * r.x, exp2( -9.28 * NoV ) ) * r.x + r.y;
    float2 AB = float2( -1.04, 1.04 ) * a004 + r.zw;
    return AB;
}


#endif
