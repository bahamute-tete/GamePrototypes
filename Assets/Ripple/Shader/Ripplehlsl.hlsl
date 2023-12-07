
#ifndef CUSTOM_RIPPLE_INCLUDED
#define CUSTOM_RIPPLE_INCLUDED
#include "Common.hlsl"

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
float4 _MainTex_ST;
float _RippleSpeed,_RippleAmount,_RippleFreq;
float4 _Color;
float _WaveAmp1=1,_WaveAmp2=0.9,_WaveAmp3=0.8,_WaveAmp4=0.7,_WaveAmp5=0.6,_WaveAmp6=0.5,_WaveAmp7=0.4,_WaveAmp8=0.3;
float _WaveX1,_WaveZ1,_WaveX2,_WaveZ2, _WaveX3,_WaveZ3, _WaveX4,_WaveZ4,_WaveX5,_WaveZ5,_WaveX6,_WaveZ6,_WaveX7,_WaveZ7,_WaveX8,_WaveZ8;
float4 _cood;

v2f UnlitPassVertex (appdata v) {

    v2f o;
    float2 center = float2(v.positionOS.x,v.positionOS.z) ;
    //half offset = dot(center,center);
    //half value1  = _RippleAmount * sin((offset+(v.positionOS.x*_WaveX1)+(v.positionOS.z*_WaveZ1))*_RippleFreq +_Time.y*_RippleSpeed);
     //half value1 = _RippleAmount*sin(((pow(v.positionOS.x + _cood.x ,2) + pow(v.positionOS.z + _cood.z , 2))*_RippleFreq)+( _Time.w*_RippleSpeed));
    //v.positionOS.y += value1;
    //v.normalOS.y +=value1;

    half offset = (v.positionOS.x * v.positionOS.x) + (v.positionOS.z*v.positionOS.z);
    half value1  = _RippleAmount * sin((offset+(v.positionOS.x*-0.5)+(v.positionOS.z*-0.5))*_RippleFreq -_Time.w*_RippleSpeed);
    half value2  = _RippleAmount * sin((offset+(v.positionOS.x*0.5)+(v.positionOS.z*0.5))*_RippleFreq -_Time.w*_RippleSpeed);
    half value3  = _RippleAmount * sin((offset+(v.positionOS.x*_WaveX3)+(v.positionOS.z*_WaveZ3))*_RippleFreq -_Time.w*_RippleSpeed);
    half value4  = _RippleAmount * sin((offset+(v.positionOS.x*_WaveX4)+(v.positionOS.z*_WaveZ4))*_RippleFreq -_Time.w*_RippleSpeed);
    half value5  = _RippleAmount * sin((offset+(v.positionOS.x*_WaveX5)+(v.positionOS.z*_WaveZ5))*_RippleFreq -_Time.w*_RippleSpeed);
    half value6  = _RippleAmount * sin((offset+(v.positionOS.x*_WaveX6)+(v.positionOS.z*_WaveZ6))*_RippleFreq -_Time.w*_RippleSpeed);
    half value7  = _RippleAmount * sin((offset+(v.positionOS.x*_WaveX7)+(v.positionOS.z*_WaveZ7))*_RippleFreq -_Time.w*_RippleSpeed);
    half value8  = _RippleAmount * sin((offset+(v.positionOS.x*_WaveX8)+(v.positionOS.z*_WaveZ8))*_RippleFreq -_Time.w*_RippleSpeed);
    
    v.positionOS.y += lerp(0,value1,pow(1-length(center),6)*0.1);
    //v.normalOS.y +=value1*2;

    v.positionOS.y += lerp(0,value2,pow(1-length(center),6)*0.1);
    v.normalOS.y +=value2*0.8;

/*
    v.positionOS.y += value3*0.5;
    v.normalOS.y +=value3*0.5;

/*
    v.positionOS.y += value4*0;
    v.normalOS.y +=value4*0;

/*
    v.positionOS.y += value5*_WaveAmp5;
    v.normalOS.y +=value5*_WaveAmp5;


    v.positionOS.y += value6*_WaveAmp6;
    v.normalOS.y +=value6*_WaveAmp6;


    v.positionOS.y += value7*_WaveAmp7;
    v.normalOS.y +=value7*_WaveAmp7;


    v.positionOS.y += value8*_WaveAmp8;
    v.normalOS.y +=value8*_WaveAmp8;

*/
    o.normalWS = TransformObjectToWorldNormal(v.normalOS,true);
    o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
    o.uv = TRANSFORM_TEX(v.uv,_MainTex);
	
	return o;
}



float4 UnlitPassFragment (v2f i) : SV_Target
{
    
    float4 col =0;
    col = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*_Color;

    float diffuse = max(dot(i.normalWS,normalize(float3(1,3,4))),0);
    col.rgb *= diffuse;
    return col;
}


#endif
