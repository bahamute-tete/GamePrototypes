#ifndef RAYMARCHING_LIGHTSHADING_INCLUDED
#define RAYMARCHING_LIGHTSHADING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ACES.hlsl"

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"

#include "../HLSL/RayMarchingTools.hlsl"
#include "../HLSL/LightModels.hlsl"
#include "../HLSL/MathTools.hlsl"
#include "../HLSL/TriplanerMapping.hlsl"





//#define _RAYMARCHING_REFLECTION

struct geometryData
{
    float3 position;
    float3 normal;
    float3 color;
};

TEXTURECUBE(_CubemapEnvironment);
SAMPLER(sampler_CubemapEnvironment);


float3 _Color;
float _Shiness;
float _SpecularScaler;


float _WrapValue;
float _SSSValue,_PowerValue,_ScaleValue;

float _Roughness;
float _Metallic;
float3 _F0;

float3 _GlowColor;
float _GlowIntensity;




float3 Shading(float3 ro,float3 rd,float2 uv)
{

    geometryData  g= (geometryData)0;
    g.color = _Color;
    Light light = GetMainLight();

#if defined(_TRIPLANNAR)
    TriplanarOutput triOut;
#endif
    float3 res =0;

    float2 d = RayMarching(ro,rd);
    float matID =d.y;

    float3 shade=0;
    float3 shadow=0;
    float3 ao=0;
    
    float3 texCol=1;


    float metallic = 0;
    float roughness = 0;




    if (d.x<MAX_DISTACNE)
    {

        float3 p = g.position = ro + rd*d.x;
        float3 n = g.normal = GetNormal(p);
        float3 l = light.direction;
        float3 v = -rd;
        float3 r = reflect(rd,n);
        float3 h = normalize(l+v);


            
        #if defined(_TRIPLANNAR)
            triOut = TriplanarMapping(p,n); 
            metallic = triOut.control.r;
            roughness =triOut.control.a;
        #else
            metallic = _Metallic;
            roughness = _Roughness;
        #endif

        float3 F0 = 0.01;
        F0 = lerp(F0, _Color,  metallic);


        shadow = Softshadow(p,l,0.01,10.,0.4);
        ao = CalcAO(p,n);

        if (matID==MAT_GROUND)
        {
           // res =0;
            //res =checkersGradBox(p.xz,ddx(p),ddy(p))*0.005;


        #if defined(_RAYMARCHING_REFLECTION)
            ro = p+n*SURFANCE_DIS*3;
            rd = r;
            float d = RayMarching(ro,rd).x;
            if (d<MAX_DISTACNE)
            {
                float3 p =ro + rd*d;
                float3 n =GetNormal(p);
               
                #if defined(_TRIPLANNAR) 
                    float3 color =triOut.texColor*_Color;
                    res +=BlinnPhong(n,l,v,_Shiness,_SpecularScaler)*color;
                    res*=0.05;
                #else    
                 float3 color =_Color;
                res +=  PBR_Cook_Torrance_Direct(n,v,l,h,F0, metallic, roughness,color,light);
                res += GetEnviromentIndirectLight(light,n,v, metallic, roughness,color);
                res*=0.05;
                #endif
            }
        #else
            res =0;
        #endif




        }else if (matID ==MAT_BALL)
        {

            #if defined(_TRIPLANNAR)           
                float3 color =triOut.texColor*_Color;
                n =triOut.normal;
                res =BackLight(n,l,v,_WrapValue,_Shiness,_SpecularScaler,_SSSValue,_PowerValue,_ScaleValue)*color;
            #else
                float3 color =_Color;
                res =  PBR_Cook_Torrance_Direct(n,v,l,h,F0, metallic, roughness,color,light);
                res += GetEnviromentIndirectLight(light,n,v, metallic, roughness,color);
            #endif

            //res = n*0.5+0.5;
        }

    }

     
    res *=1*ao;

//glowLight
    float squareCenterUVDistance =dot(uv,uv);
    float3 lightColor = _GlowColor;
    float centerlight =1e-4*_GlowIntensity/squareCenterUVDistance;
    res.rgb += centerlight*smoothstep(0,0.5,(d.x-length(_WorldSpaceCameraPos)))*lightColor;
//glowLightGlare
    float s = SDFBallGyroid(normalize(ro));
    res.rgb += centerlight*smoothstep(0.,0.1,s)*lightColor;

//volumLight
    float3 intersectionPlanePoint = RayPlane(ro,rd,ro,normalize(ro));
    float starburst = SDFBallGyroid(normalize(intersectionPlanePoint));
    starburst *= smoothstep(0.2,0.02,length(uv));
    res.rgb +=max(starburst,0)*lightColor*_GlowIntensity;
   
   


    return ACESToneMapping(res);

}

#endif
