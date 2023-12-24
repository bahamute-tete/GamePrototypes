#ifndef MYLIGHT_INCLUDED
#define MYLIGHT_INCLUDED

void MyLight_float(out float3 lightDir)
{
    Light ligth = GetMainLight();
    Color lightColor = light.Color;
    lightDir = light.Direction;
}

#endif