#ifndef CUSTOM_RAINNY_INCLUDED
#define CUSTOM_RAINNY_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "HLSLSupport.cginc"//UNITY_DECLARE_TEX2D UNITY_SAMPLE_TEX2D
//#include "UnityCG.cginc"
#define S(a,b,t) smoothstep(a,b,t)
#define PI 3.14159265359
StructuredBuffer<float> buffer ;

 //Constant buffers aren't supported on all platforms
cbuffer perMat
{
    float4 _baseColor;
};



struct appdata
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
};

struct v2f
{
    float4 vertex : SV_POSITION;
    float4 screenPos: TEXCOORD0;
    float2 uv : TEXCOORD1;
};

CBUFFER_START(Rainny_cbuffer)
   
    //SAMPLER(sampler_MainTex);
    SamplerState sampler_MainTex
    {
        Filter = MIN_MAG_MIP_LINEAR;
        AddressU = Wrap;
        AddressV = Wrap;
    };
     //TEXTURE2D(_MainTex);

    float4 _MainTex_ST;

    UNITY_DECLARE_TEX2D(_MainTex2);
    sampler2D _MainTex3;
    
    float _Size;
    float _T;
    float _Distoration;
    float _Blur;
CBUFFER_END
    Texture2D _MainTex;
    SAMPLER(_CameraOpaqueTexture);


v2f vert (appdata v)
{
    v2f o;
    o.vertex = TransformObjectToHClip(v.vertex);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    o.screenPos = ComputeScreenPos(o.vertex);
    return o;
}

float N21(float2 p)
{
    p = frac(p*float2(123.34, 345.45));
    p +=dot(p,p+34.5);
    return frac(p.x * p.y);
}

float3 Layers(float2 UV, float t)
{
    float2 aspect = float2(2,1);
    float2 uv = UV*_Size*aspect;
    uv.y += t*0.25;//UV 整体移动
    float2 gv = frac(uv)-0.5;;
    float2 id = floor(uv);

    float2 n=N21(id);//[0,1]
    t+=n*PI*2;//2PI 实现循环

    float w = UV.y*10;
    float x =(n-0.5)*0.8;//[-0.4,0.4]
     x +=(0.4-abs(x))*sin(3*w)*pow(sin(w),6)*0.45 ;
    float y = -sin(t+sin(t+sin(t)*0.5))*0.45;//实现先慢后快的运动
    y-=(gv.x-x)*(gv.x-x);//Shape

    
        float2  dropPos = (gv -float2(x,y))/aspect;
        float drop = S(0.05,0.03,length(dropPos));

        float2 trailPos = (gv-float2(x,t*0.25))/aspect;
        trailPos.y =(frac(trailPos.y*8.0)-0.5)/8.0;
        float trail = S(0.03,0.01,length(trailPos));
        float fogTrail=S(-0.05,0.05,dropPos.y);
        fogTrail*=S(.5,y,gv.y);
        trail *=fogTrail ;
        fogTrail *=S(0.05,0.04,abs(dropPos.x));
        
        //col+=fogTrail*0.5;
        //col += trail;
        //col += drop;
    

    float2 offset =drop*dropPos+trail*trailPos;

    return float3(offset,fogTrail);
}

half4 frag (v2f i) : SV_Target
{
    /*
    half4 col = _MainTex.Sample(sampler_MainTex, i.uv);//采样
    half4 col2 = UNITY_SAMPLE_TEX2D(_MainTex2,i.uv);
    float4 col3= tex2D(_MainTex3,i.uv);
    //_MainTex.Sample(sampler_MainTex, i.uv)=col2;
    float4 sampleColor = _MainTex.Load(int3(150,150,0));//fetch 当前位置的像素值
    float4 sampleColor2 = _MainTex.Gather(sampler_MainTex,float2(0.5,0.5),int2(0,0));//4个邻居像素的R值
    float hash= noise(i.uv);
    */
    
    half4 col = 0;

    float  t =fmod( _Time.y+_T,7200);
    float3 drops = Layers(i.uv,t);
    drops +=Layers(i.uv*1.27+7.54,t);
    drops +=Layers(i.uv*1.77+1.54,t);
    drops +=Layers(i.uv*1.77-7.54,t);
   
    float fade =1-saturate(fwidth(i.uv)*50);
    float blur =_Blur*(1-drops.z*fade);
    //col.rgb =_MainTex.Sample(sampler_MainTex, i.uv+drops.xy*_Distoration,0,blur);

    blur*=.01;
    //float2 screenUV= i.vertex.xy/_ScreenParams.xy;
    //half2 screenUV2 = GetNormalizedScreenSpaceUV(i.vertex);
    float2 sUV =i.screenPos.xy/i.screenPos.w;
    sUV +=drops.xy*_Distoration;
    const float numbers=16;
    float a =N21(i.uv)*PI*2;

    for(float i=0;i<numbers;i++)
    {
        float2 offset= float2(sin(a),cos(a))*blur;
        float d =frac(sin((i+1)*546.01)*4456.78);
        d =sqrt(d);
        offset *=d;
        col.rgb +=tex2D(_CameraOpaqueTexture,sUV+offset).rgb;
        a++;
    }
    col.rgb /=numbers;
    
   

 
    
    //col.rg=gv;

    //if (gv.x>0.48|| gv.y >0.49)col = float4(1,0,0,1);

    //col*=0;
    //col.rgb= fade;
    return col*0.8;
}

#endif
