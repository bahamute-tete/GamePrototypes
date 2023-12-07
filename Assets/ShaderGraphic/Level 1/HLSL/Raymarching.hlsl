#ifndef RAYMARCHING_INCLUDED
#define RAYMARCHING_INCLUDED

#define SURFANCE_DIS 1e-2
#define MAX_STEPS 100
#define MAX_DISTACNE 100
#define ZERO 0
#define PLANEHEIGHT 0.5

float3 Camera_float(float2 uv,float3 origin, float3 look_at,float zoom,out float3 cameraDirection)
{

    float3 forward = normalize(look_at -origin);
    float3 right = cross(forward,float3(0,1,0));
    float3 up = cross(right,forward);

    float3 center = origin + forward * zoom;
    float3 intersection = center + right * uv.x+ up * uv.y;
    cameraDirection =intersection-origin;
    return cameraDirection;
}

float checkersGradBox( float2 p, float2 dpdx, float2 dpdy )
{
    // filter kernel
    float2 w = abs(dpdx)+abs(dpdy) + 0.001;
    // analytical integral (box filter)
    float2 i = 2.0*(abs(frac((p-0.5*w)*0.5)-0.5)-abs(frac((p+0.5*w)*0.5)-0.5))/w;
    // xor pattern
    return 0.5 - 0.5*i.x*i.y;                  
}



float SDFSphere_float(float3 p, float3 center, float radius)
{
    return length(p - center) - radius;
}

float SDFBox_float(float3 p, float3 boxSize, float radius)
{
    return length(max(abs(p)-boxSize,0)) - radius;
}


float SDFRing_float(float3 p, float3 center, float radius1, float radius2)
{   
    float2 v=0;
    v.x = length(p.xz-center.xz)-radius1;
    v.y = p.y;

    float d = length(v)-radius2;
    return d;  
}


float SDFPlane_float(float3 p, float3 n, float height)
{
    return dot(p,normalize(n))+height;
}
//////////////////////////////////////////////////////////////////////



/////////////////////////////////////////////////////////////////////
float GetDistance_float(float3  p,int index)
{

    float d =0;
    [branch]switch (index)
    {
        case 0:
            d = SDFSphere_float(p,float3(0.,0.,0.),1);
            break;

        case 1:
            d= SDFBox_float(p,float3(1,1,1),0.05);
            break;

        case 2:
            d= SDFRing_float(p,float3(0,0,0),1,0.3);\
            break;

        case 3:
            d= SDFPlane_float(p,float3(1,1,0),0);
            break;
        
        default:
            d = SDFSphere_float(p,float3(0.,0.,0.),1);
            break;
    }
    
    return d =min(SDFPlane_float(p,float3(0,1,0),PLANEHEIGHT),d);

    
}


float RayMarching_float(float3 ray_origin, float3 ray_direction,  float index,out float distance)
{
    float  d=0.0;

    for (int i = 0; i < MAX_STEPS; i++)
    {
        float3 p = ray_origin + ray_direction * d;
        float currentDistance =GetDistance_float(p,index) ;
        if (currentDistance < SURFANCE_DIS || d > MAX_DISTACNE)
            break;

        d += currentDistance;
    }

    return distance = d;
}


float3 GetNormal(float3 p,float index) // for function f(p)
{
    float h = 0.0001; // replace by an appropriate value
    float2 k = float2(1,-1);
    return normalize( k.xyy*GetDistance_float( p + k.xyy*h,index ) + 
                      k.yyx*GetDistance_float( p + k.yyx*h,index ) + 
                      k.yxy*GetDistance_float( p + k.yxy*h,index ) + 
                      k.xxx*GetDistance_float( p + k.xxx*h,index ) );
}

float3 GetNormal1(float3 p,float index)
{
    float d = GetDistance_float(p,index);
    float2 e = float2 (0.01,0);

    float3 n = d- float3(
                            GetDistance_float(p-e.xyy,index),
                            GetDistance_float(p-e.yxy,index),
                            GetDistance_float(p-e.yyx,index));
    return normalize(n);
}

float softshadow1 (float3 ro,float3 rd,float mint,float maxt,float k,float index)
{
    float d = mint;
    float res = 1.0;
    for (int i = 0; i < MAX_STEPS && d <maxt; i++)
    {
        float current_d = GetDistance_float(ro + rd * d,index);
        if (current_d < 0.001) return 0.0;
        d += current_d;
        //距离物体越近 raymarching的圈的半径就越小，也就是 currentD越小
        res =min(res,k*current_d/d);
    }
    return res;
}

float softshadow( float3 ro, float3 rd, float mint, float maxt, float w,float index )
{
//w = solid angle
    float res = 1.0;
    float t = mint;
    for( int i=0; i<256 && t<maxt; i++ )
    {
        float h = GetDistance_float(ro + rd*t,index);
        res = min( res, h/(w*t) );
        t += clamp(h, 0.005, 0.50);
        if( res<-1.0 || t>maxt ) break;
    }
    res = max(res,-1.0);
    return 0.25*(1.0+res)*(1.0+res)*(2.0-res);
}

float softshadow2( float3 ro, float3 rd, float mint, float maxt, float w,float index )
{
    float res = 1.0;
    float ph = 1e20;//big make first iteration y =0
    float t = mint;
    for( int i=0; i<256 && t<maxt; i++ )
    {
        float h = GetDistance_float(ro + rd*t,index);
        if( h<0.001 )
            return 0.0;

        float y = h*h/(2.0*ph);//on second iteration ph =h  so h*h/2h=h/2
        float d = sqrt(h*h-y*y);
        res = min( res, d/(w*max(0.0,t-y)));
        ph = h;
        t += h;
    }
    res = clamp( res, 0.0, 1.0 );
    return res*res*(3.0-2.0*res);
}

// https://iquilezles.org/articles/nvscene2008/rwwtt.pdf
float calcAO( float3 pos, float3 nor ,float index)
{
	float occ = 0.0;
    float sca = 1.0;
    for( int i=ZERO; i<5; i++ )
    {
        //沿着法线方向给定5个步长
        float h = 0.01 + 0.12*float(i)/4.0;
        //计算 这5个采样点的距离
        float d = GetDistance_float( pos + h*nor ,index);
        occ += (h-d)*sca;
        sca *= 0.95;
        if( occ>0.35 ) break;
    }
    return clamp( 1.0 - 3.0*occ, 0.0, 1.0 ) * (0.5+0.5*nor.y);
}

float GetLight (float3 p,float3 lightPos,float3 viewDirection, float index)
{
    float3 lin=0;
    float3 n = GetNormal(p,index);
    float3 l = normalize(lightPos - p);
    float3  hal = normalize( l+viewDirection );
    float spe = pow( clamp( dot( n, hal ), 0.0, 1.0 ),16.0);


    float diff = saturate(dot(n,l));
    spe*=diff;
    spe *= 0.04+0.96*pow(clamp(1.0-dot(hal,l),0.0,1.0),5.0);

    lin += diff*float3(1.30,1.00,0.70);
    lin += 5.00*spe*float3(1.30,1.00,0.70);

    diff *=softshadow(p,l,0.01,10,0.2,index);
    diff *=calcAO(p,n,index);

    /*
    float3 start =p +n*SURFANCE_DIS;
    float distance=0;
    float d= RayMarching_float(start,l,index,distance);
    if (d < length(_lightPos - p)) dif *=0.1;
    */

    
    return  diff;
}




float3 Shading_float(float3 ray_origin, float3 ray_direction, float sdfDistance, float3 lightPos,float index,float ambientIntensity, out float3 shadingColor)
{
    float3 p = ray_origin + ray_direction * sdfDistance;
    float diff = GetLight(p,lightPos,-ray_direction,index)+ambientIntensity;



    if (sdfDistance>MAX_DISTACNE)
    {
        //background
        float3 col = float3(0.7, 0.7, 0.9) - max(ray_direction.y,0.0)*0.3;
        shadingColor =col;
    }
    else
    {
        shadingColor.rgb = saturate(diff);
    }
    
    return shadingColor;

}

#endif