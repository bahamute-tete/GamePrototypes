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

void Camera_matrix_float(float3 origin, float3 target,out float3x3 cameraMatrix)
{
    float3 forward = normalize(target -origin);
    float3 right = cross(forward,float3(0,1,0));
    float3 up = cross(right,forward);

    cameraMatrix = float3x3(right,up,forward);
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



float SDFSphere_float(float3 p,float radius)
{

    return length(p) - radius;
}



float SDFBox_float(float3 p, float3 boxSize, float radius)
{
    return length(max(abs(p)-boxSize,0)) - radius;
}


float SDFRing_float(float3 p,float radius1, float radius2)
{   
    float2 v=0;
    v.x = length(p.xz)-radius1;
    v.y = p.y;

    float d = length(v)-radius2;
    return d;  
}

float sdCylinder( float3 p, float3 c ) {
    return length(p.xz - c.xy) - c.z;
}


float SDFPlane_float(float3 p, float3 n, float height)
{
    return dot(p,normalize(n))+height;
}
//////////////////////////////////////////////////////////////////////



/////////////////////////////////////////////////////////////////////
float GetDistance_float(float3 p,float index,float3 center,float3 radius)
{

    float d =0;
    [branch]switch (index)
    {
        case 0:
            d = SDFSphere_float(p-center,radius.x);
            break;

        case 1:
            d= SDFBox_float(p-center,radius,0.05);
            break;

        case 2:
            d= SDFRing_float(p-center,radius.x,radius.y);
            break;

        case 3:
            d= sdCylinder(p-center,radius);
            break;
        
        default:
            d = SDFSphere_float(p-center,radius.x);
            break;
    }
    

    return d =min(SDFPlane_float(p,float3(0,1,0),PLANEHEIGHT),d);

    
}




float RayMarching_float(float3 ray_origin, float3 ray_direction, float index,float3 center,float3 radius, out float distance)
{
    float  d=0.0;

    for (int i = 0; i < MAX_STEPS; i++)
    {
        float3 p = ray_origin + ray_direction * d;
        float currentDistance =GetDistance_float(p,index,center,radius);
        if (abs(currentDistance) < SURFANCE_DIS || d > MAX_DISTACNE)
            break;

        d += currentDistance;
    }

    return distance = d;
}




float3 GetNormal(float3 p,float index,float3 center,float3 radius) // for function f(p)
{
    float h = 0.0001; // replace by an appropriate value
    float2 k = float2(1,-1);
    return normalize( k.xyy*GetDistance_float( p + k.xyy*h,index ,center,radius ) + 
                      k.yyx*GetDistance_float( p + k.yyx*h,index ,center,radius) + 
                      k.yxy*GetDistance_float( p + k.yxy*h,index ,center,radius) + 
                      k.xxx*GetDistance_float( p + k.xxx*h,index ,center,radius) );
}



// float3 GetNormal1(float3 p,float index)
// {
//     float d = GetDistance_float(p,index);
//     float2 e = float2 (0.01,0);

//     float3 n = d- float3(
//                             GetDistance_float(p-e.xyy,index),
//                             GetDistance_float(p-e.yxy,index),
//                             GetDistance_float(p-e.yyx,index));
//     return normalize(n);
// }

float softshadow1 (float3 ro,float3 rd,float mint,float maxt,float k,float index,float3 center,float3 radius)
{
    float d = mint;
    float res = 1.0;
    for (int i = 0; i < MAX_STEPS && d <maxt; i++)
    {
        float current_d = GetDistance_float(ro + rd * d,index,center,radius);
        if (current_d < 0.001) return 0.0;
        d += current_d;
        //距离物体越近 raymarching的圈的半径就越小，也就是 currentD越小
        res =min(res,k*current_d/d);
    }
    return res;
}

float softshadow( float3 ro, float3 rd, float mint, float maxt, float w,float index,float3 center,float3 radius )
{
//w = solid angle
    float res = 1.0;
    float t = mint;
    for( int i=0; i<256 && t<maxt; i++ )
    {
        float h = GetDistance_float(ro + rd*t,index,center,radius);
        res = min( res, h/(w*t) );
        t += clamp(h, 0.005, 0.50);
        if( res<-1.0 || t>maxt ) break;
    }
    res = max(res,-1.0);
    return 0.25*(1.0+res)*(1.0+res)*(2.0-res);
}

float softshadow2( float3 ro, float3 rd, float mint, float maxt, float w,float index,float3 center,float3 radius )
{
    float res = 1.0;
    float ph = 1e20;//big make first iteration y =0
    float t = mint;
    for( int i=0; i<256 && t<maxt; i++ )
    {
        float h = GetDistance_float(ro + rd*t,index,center,radius);
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
float calcAO( float3 pos, float3 nor ,float index,float3 center,float3 radius )
{
	float occ = 0.0;
    float sca = 1.0;
    for( int i=ZERO; i<5; i++ )
    {
        //沿着法线方向给定5个步长
        float h = 0.01 + 0.12*float(i)/4.0;
        //计算 这5个采样点的距离
        float d = GetDistance_float( pos + h*nor ,index,center,radius);
        occ += (h-d)*sca;
        sca *= 0.95;
        if( occ>0.35 ) break;
    }
    return clamp( 1.0 - 3.0*occ, 0.0, 1.0 ) * (0.5+0.5*nor.y);
}

float3 GetLight (float3 p,float3 lightPos,float3 viewDirection, float index,float3 center,float3 radius,
                float3 matColor,float3 lightColor,float3 F0,float roughness,float K)
{
    //CookTorranc

    float3 n = GetNormal(p,index,center,radius);
    float3 l = normalize(lightPos - p);
    float3  h = normalize( l+viewDirection );

	float NdotL = clamp( dot( n, l ),0.,1. );
	float NdotH = clamp( dot( n, h ),0.,1. );
	float NdotV = clamp( dot( n, viewDirection ),0.,1. );
	float VdotH = clamp( dot( h, viewDirection ),0.,1. );

    float rsq = roughness * roughness;

	// Geometric Attenuation
    float NH2   = 2. * NdotH / VdotH;
	float geo_b = (NH2 * NdotV );
	float geo_c = (NH2 * NdotL );
	float geo   = min( 1., min( geo_b, geo_c ) );


    // Roughness
	// Beckmann distribution function
	float r1 = 1. / ( 4. * rsq * pow(NdotH, 4.));
	float r2 = (NdotH * NdotH - 1.) / (rsq * NdotH * NdotH);
	float rough = r1 * exp(r2);
	
	// Fresnel			
	float fres = pow( 1.0 - VdotH, 5. );
	fres *= ( 1.0 - F0 );
	fres += F0;
	
	float3 spec = (NdotV * NdotL==0.) ? 
                float3(0,0,0) : 
                float3 ( fres * geo * rough,fres * geo * rough,fres * geo * rough ) / ( NdotV * NdotL );

	float3 res = NdotL * ( (1.-K)*spec + K*matColor) * lightColor;
	





    //BlinPhone
    // float spe = pow( clamp( dot( n, h ), 0.0, 1.0 ),16.0);
    // float3 res =float3(0.7,0.6,0.3) *saturate(dot(n,l))+spe;


    res *=softshadow(p,l,0.01,10,0.2,index,center,radius);
    res *=calcAO(p,n,index,center,radius);

    /*
    float3 start =p +n*SURFANCE_DIS;
    float distance=0;
    float d= RayMarching_float(start,l,index,distance);
    if (d < length(_lightPos - p)) dif *=0.1;
    */

    
    return  res;
}




float3 Shading_float(float3 ray_origin, float3 ray_direction, float sdfDistance, float3 lightPos,float index,float3 center,float3 radius,float ambientIntensity, 
                    float3 matColor,float3 lightColor,float3 F0,float roughness,float K,
                    out float3 shadingColor)
{
    float3 p = ray_origin + ray_direction * sdfDistance;
    float3 res = GetLight(p,lightPos,-ray_direction,index,center,radius,matColor,lightColor,F0,roughness,K)+ambientIntensity;



    if (sdfDistance>MAX_DISTACNE)
    {
        //background
        float3 col = float3(0.7, 0.7, 0.9) - max(ray_direction.y,0.0)*0.3;
        shadingColor =col;
    }
    else
    {
        shadingColor.rgb = res;
    }
    
    return shadingColor;

}

#endif