#ifndef RAYMARCHING_LIGHTMODELS_INCLUDED
#define RAYMARCHING_LIGHTMODELS_INCLUDED



#include "../HLSL/PBR.hlsl"

#define _COOK_TORRANCE_GGX

float HalfLambert(float3 n,float3 l)
{

    float nl = dot(n,l);
    return pow(nl*0.5+0.5,2);;
}

float BlinnPhong(float3 n,float3 l,float3 v,float shiness,float specularScaler)
{

    float3 h = normalize(l+v);
    float nh = saturate(dot(n,h));
    float nl = saturate(dot(n,l));
    float spe = pow(nh,shiness)*specularScaler;
    float diff = nl;
    return  diff + spe;
}

float WrapLight(float3 N,float3 L,float3 V,float wrapValue)
{

    float NL = dot(N,L);
    float3 H = normalize(L+V);
    float NH = dot(N,H);
    float NLWrap = (NL + wrapValue)/(1 + wrapValue);

    float diffuse = max(NLWrap,0);

    return  diffuse;
}


float Phong(float3 n,float3 l,float3 v,float shiness,float specularScaler)
{
    float3 r = reflect(-l,n);
    float vr = saturate(dot(v,r));
    float nl = saturate(dot(n,l));
    float spe = pow(vr,shiness)*specularScaler;
    float diff = nl;
    return  diff + spe;
}

float BackLight(float3 n,float3 l,float3 v,float wrapValue,float shiness,float specularScaler,float sssValue,float powerValue,float scaleValue)
{

    //WrapLight
    float wrapLight = pow(dot(n,l)*wrapValue+(1.0-wrapValue),2);
    //Blin-Phong
    float3 h = normalize(v+l);
    float NH = saturate(dot(n,h));
    float NL = saturate(dot(n,l));
    float Spe = pow(NH,shiness)*specularScaler;
    float Diff =wrapLight;// saturate(dot(n,l))*0.5+0.5;
    //模拟透射现象
    //沿着光线方向上偏移法线，最后在取反
    float3 H = normalize((l+n*sssValue));
    float BackLight = pow(saturate( dot(v,-H)) ,powerValue)*scaleValue;
    return Diff + Spe+BackLight ;
}


float3 GetEnviromentIndirectLight(Light light,float3 N,float3 V,float metallic,float roughness,float3 baseColor)
{

    float3 IndirectLight =0;

    float3 R = reflect(-V,N);
    float3 F0 = baseColor;
    float3 NV= saturate(dot(N,V));
    float3 F_IndirectLight = FresnelSchlickRoughness(NV,F0,roughness);

        //Diffuse           
    float3 KD_IndirectLight = 1 - F_IndirectLight;
    KD_IndirectLight *= 1 - metallic;

    float3 irradianceSH = SampleSH(N);
    float3 Diffuse_Indirect = irradianceSH * baseColor / PI *KD_IndirectLight;


    //Specular Part1
    //imageBasedLighting.hlsl
    ////UNITY_SPECCUBE_LOD_STEPS 在  "UnityStandardConfig.cginc"中 
    // #define UNITY_SPECCUBE_LOD_STEPS 6
    float mip = roughness*(1.7 - 0.7*roughness) * UNITY_SPECCUBE_LOD_STEPS ;
    float4 rgb_mip =SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, R, mip);
    ////unity_SpecCube0_HDR储存的是 最近的ReflectionProbe
    float3 EnvSpecularPrefilted = DecodeHDREnvironment(rgb_mip, unity_SpecCube0_HDR);
    // //Specular Part2
    float2 env_brdf = EnvBRDFApprox_UE4(roughness,NV);
    float3 Specular_Indirect = EnvSpecularPrefilted  * (F_IndirectLight * env_brdf.r + env_brdf.g);



    IndirectLight = Diffuse_Indirect + Specular_Indirect;

    return IndirectLight;

}

float3 PBR_Cook_Torrance_Direct(float3 N,float3 V,float3 L,float3 H,float3 F0,float metallic,float roughness,float3 baseColor,Light light)
{
#if defined(_COOK_TORRANCE_GGX)
    float D = D_DistributionGGX(N,H,roughness);
    float G = G_GeometrySmith(N,V,L,roughness);
#else
    float D = D_DistributionBeckmann(N,H,roughness);
    float G = GeometricAttenuation(N,V,L,H);
#endif

    float3 F = F_FrenelSchlick(H,V,F0);

    float NV= saturate(dot(N,V));
    float NL= saturate(dot(N,L));

    float3 KS = F;
    float3 KD = 1-KS;
    KD*=1-metallic;
    float3 nominator = D*F*G;
    float denominator = max(4*NV*NL,0.001);
    float3 spe = nominator/denominator;

    float3 diff = KD * baseColor / PI;
    float3 DirectLight = (diff + spe)*dot(N,L) *light.color;



    return  DirectLight.rgb;

}



#endif
