#ifndef RAYMARCHING_SDFDISTANCE_INCLUDED
#define RAYMARCHING_SDFDISTANCE_INCLUDED


#include "../HLSL/RayMarchingShape.hlsl"
#include "../HLSL/MathTools.hlsl"

#define MAT_DEFAULT 0
#define MAT_GROUND 1
#define MAT_BALL 2
#define MAT_Box 3



float SDFScaleBox(float3 p)
{
    //Symmetry 
    p.y =abs(p.y);
    float3 boxSize = float3(0.22,0.3,0.15);
    float scaleY =lerp(1.0,0.2,smoothstep(-1,1,p.y));
    p.xz/= scaleY;

    float box = SDFBox(p,boxSize,0.05)*scaleY;
    return box;
}

float SDFBoxCombine(float3 p)
{
    float3 offsetP= float3(0,0.93,0);
    p-=offsetP;
    float box = SDFScaleBox(p);
    for (int i =1;i<16;i++)
    {
        p+=offsetP;
        p.xy = mul(Rot(2.0*PI/16.0*i),p.xy);
        p-=offsetP;

        float boxAdd = SDFScaleBox(p);
        box = min(box,boxAdd);
    }
    p+=offsetP;

    return box;
}

float SDFHexCombine(float3 p)
{
    float hex=0;
    float3 offsetP =float3(0,0.9,0);
    p-=offsetP;
    hex= SDHexPrism(p,float2(0.09,0.25));
    for (int i =1;i<8;i++)
    {
        p+=offsetP;
        p.xy = mul(Rot(2.0*PI/8.0*i),p.xy);
        p-=offsetP;

        float hexAdd =SDHexPrism(p,float2(0.09,0.25));
        hex = min(hex,hexAdd);
    }
    p+=offsetP;

    return hex;
}

float SDFRing(float3 p)
{
    float ring =0;
      //Extrusion
   float2 circle1 = float2(length(p.xy)-1.0,abs(p.z)-0.15);
   float Cylinder1 =min(max(circle1.x,circle1.y),0.0) + length(max(circle1,0.0))-0.05;

     ///Extrusion
   float2 circle2 = float2(length(p.xy)-0.7,abs(p.z)-0.3);
   float Cylinder2 =min(max(circle2.x,circle2.y),0.0) + length(max(circle2,0.0))-0.05;

    float3 torusP =abs(p);
    
    torusP.z =abs(torusP.z)-0.2;
    torusP.yz = mul(Rot(PI*0.5),torusP.yz);
    float torus=SDFTorus(torusP,float2(0.75,0.04))*0.9;

    ring =max(Cylinder1,-Cylinder2);
    ring =smin(ring,torus,0.05);
    return ring;

}

float SDFGear(float3 p)
{
   float res=0;
   
    p.xy = mul(Rot(_Time.y),p.xy);

    float boxes= SDFBoxCombine(p);

    float ring =SDFRing(p);

    float hex=SDFHexCombine(p);
    

    ring =min(ring,hex);

    float gear =smin(ring,boxes,0.06);
    res =gear;

    return res;
}

float SDFBoxCombineSmall(float3 p,float count,float3 offset)
{
    float3 offsetP=offset;
    p-=offsetP;
    float box = SDFScaleBox(p);
    int num =count;
    for (int i =1;i<num;i++)
    {
        p+=offsetP;
        p.xy = mul(Rot(2.0*PI/count*i),p.xy);
        p-=offsetP;

        float boxAdd = SDFScaleBox(p);
        box = min(box,boxAdd);
    }
    p+=offsetP;

    return box;
}

float SDFRingSmall(float3 p,float radius)
{
    float ring =0;
      //Extrusion
   float2 circle1 = float2(length(p.xy)-radius,abs(p.z)-0.15);
   float Cylinder1 =min(max(circle1.x,circle1.y),0.0) + length(max(circle1,0.0))-0.05;

    float3 torusP =abs(p);
    
    torusP.z =abs(torusP.z)-0.15;
    torusP.yz = mul(Rot(PI*0.5),torusP.yz);
    float torus=SDFTorus(torusP,float2(radius,radius*0.007))*0.9;

     ///Extrusion
//    float2 circle2 = float2(length(p.xy)-0.1,abs(p.z)-0.3);
//    float Cylinder2 =min(max(circle2.x,circle2.y),0.0) + length(max(circle2,0.0))-0.05;

    // float hex=SDHexPrism(p,float2(0.2,0.25));

    // ring =max(Cylinder1,-hex);
    ring =smin(Cylinder1,torus,0.05);
    return ring;

}

float SDFGearSmall(float3 p,float rotDir,float count,float radius)
{
   float res=0;
   
    p-=float3(1.52,-1.18,0);
    p.xy = mul(Rot(_Time.y*2*rotDir),p.xy);
    //p+=float3(1.55,-1,0);
    float ring =SDFRingSmall(p,radius);
    float box = SDFBoxCombineSmall(p,count,float3(0,radius*0.75,0));

    //Extrusion
    float scale1 =radius*0.5;
   float2 circle2 = float2(length(p.xy)-scale1,abs(p.z)-0.25);
   float Cylinder2 =min(max(circle2.x,circle2.y),0.0) + length(max(circle2,0.0))-0.05;

    float scale2=scale1*0.5;
   float2 circle3 = float2(length(p.xy)-scale2,abs(p.z)-0.25);
   float Cylinder3 =min(max(circle3.x,circle3.y),0.0) + length(max(circle3,0.0))-0.05;

    float scale3=scale2*0.58;
   float2 circle4 = float2(length(p.xy)-scale3,abs(p.z)-0.15);
   float Cylinder4 =min(max(circle4.x,circle4.y),0.0) + length(max(circle4,0.0))-0.05;

    float scale4=scale3*0.9;
   float2 circle5 = float2(length(p.xy)-scale4,abs(p.z)-0.5);
   float Cylinder5 =min(max(circle5.x,circle5.y),0.0) + length(max(circle5,0.0))-0.05;
    // float hex=SDHexPrism(p,float2(0.2,0.25));
    res =smin(ring,box,0.06);
    res =min(Cylinder2,res);
    res = smin(-Cylinder3,res,-0.03);
     res = smin(res,Cylinder4,0.05);
    res = smin(res,Cylinder5,0.05);

    return res;
}





float2 GetDistance_float(float3 p)
{

    float d =0;
    float mat =0;
    float K = 0.2;
    
   // p.xz = mul(Rot(_Time.y*0.25),p.xz);
    float plane = SDFPlane(p,float3(0,1,0))+2.;
   float testBox = SDFBox(p,float3(0.7,0.7,0.7),0.02);


    float gBallScale =1.9;
    float gBall = SDFBallGyroid(p*gBallScale)/gBallScale;
    float gear = SDFGear(p);

    float3 sGPos1 =p;
    float smallGear1 =SDFGearSmall(sGPos1,-1,8.0,0.5);

    float3 sGPos2 =p;
    sGPos2-=float3(-1.22,3.1,0);
    float smallGear2 =SDFGearSmall(sGPos2,-1,8,0.5);

    float3 sGPos3 =p;
    sGPos3.xz = mul(Rot(-0.5*PI),sGPos3.xz);
    sGPos3-=float3(-2.3,1.44,1.22);//(z,y,x)
    float smallGear3 =SDFGearSmall(sGPos3,-1,8.0,0.5);

    float3 sGPos4 =p;
    sGPos4.xz = mul(Rot(0.5*PI),sGPos4.xz);
    sGPos4-=float3(-2.3,1.44,1.22);//(z,y,x)
    float smallGear4 =SDFGearSmall(sGPos4,1,8.0,0.5);

    float3 sGPos5 =p;
    sGPos5-=float3(-3.05,0.,0);
    float smallGear5 =SDFGearSmall(sGPos5,-1,8.0,0.5);

    d= min(gBall,gear);
    
#if defined (_FULLIMAGE)
    d=min(smallGear1,d);
    d=min(smallGear2,d);
    d=min(smallGear3,d);
    d=min(smallGear4,d);
    d=min(smallGear5,d);
#endif
    d=min(plane,d);
    //d=min(plane,testBox);

    //d= testBox;

    if (d ==plane)
    {
        mat=MAT_GROUND;
    }else
    {
        mat=MAT_BALL;
    }


   
   return float2(d,mat) ;
    //return float2(d,d== min( sphere2,max(-sphere,box))?MAT_BALL:MAT_GROUND) ;
}


#endif
