#ifndef ADJUSTUV_INCLUDED
#define ADJUSTUV_INCLUDED
float2 AdjustUV_float(float2 inputUV,float2 aspect, out float2 outputUV)
{
    //outputUV = (inputUV-0.5)*_ScreenParams/_ScreenParams.y 
    outputUV = (inputUV-0.5)*aspect ;
    return  outputUV ;
}

float SDFCircle_float(float2 center,float2 uv, float radius,out float distance)
{
    distance=length(uv -center)-radius;
    return distance;
}

float3 OutColor_float(float distance,float3 outerColor,float3 innerColor, out float3 outputColor)
{
   return outputColor = distance>0?outerColor:innerColor;
}




#endif