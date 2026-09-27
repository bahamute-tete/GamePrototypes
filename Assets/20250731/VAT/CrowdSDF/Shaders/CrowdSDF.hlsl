#ifndef VAT_CROWD_SDF_INCLUDED
#define VAT_CROWD_SDF_INCLUDED

// SDF convention: negative inside solid obstacles, distances divided by
// the longest bake-box side (the Unity MeshToSDFBaker convention).
float CrowdHash(uint n)
{
    n ^= n >> 16; n *= 0x7feb352du; n ^= n >> 15;
    n *= 0x846ca68bu; n ^= n >> 16;
    return (n & 0x00ffffffu) / 16777216.0;
}
float3 CrowdBezier(float t, float3 a, float3 b, float3 c, float3 d)
{
    float u = 1-t;
    return u*u*u*a + 3*u*u*t*b + 3*u*t*t*c + t*t*t*d;
}
float3 CrowdFlat(float3 v)
{
    v.y = 0;
    return v * rsqrt(max(dot(v,v), 1e-8));
}
float CrowdDistance(VFXSampler3D field, float3 p, float3 center, float3 extent)
{
    float3 uv = (p-center)/extent + 0.5;
    // Outside the texture is forbidden, not empty space.
    float edge = min(min(min(uv.x,1-uv.x)*extent.x, min(uv.z,1-uv.z)*extent.z), 1000.0);
    return min(SampleSDF(field, saturate(uv))*max(extent.x,max(extent.y,extent.z)), edge);
}
float3 CrowdNormal(VFXSampler3D field, float3 p, float3 center, float3 extent, float epsilon)
{
    float3 x = float3(epsilon,0,0), z = float3(0,0,epsilon);
    return CrowdFlat(float3(
        CrowdDistance(field,p+x,center,extent)-CrowdDistance(field,p-x,center,extent), 0,
        CrowdDistance(field,p+z,center,extent)-CrowdDistance(field,p-z,center,extent)));
}
float CrowdClearance(VFXSampler3D field, float3 p, float3 dir, float reach,
                     float3 center, float3 extent)
{
    float clearance = 10000;
    [unroll] for (int k=1;k<=4;k++)
        clearance = min(clearance, CrowdDistance(field,p+dir*(reach*k/4.0),center,extent));
    return clearance;
}

// Shared by the VFX update block and the GPU validation compute shader.
// Owns all position integration: the VFX context's Update Position must be off.
void CrowdStep(VFXSampler3D field, float3 center, float3 extent,
    float3 p0,float3 p1,float3 p2,float3 p3,
    float speed,float radius,float lookAhead,float avoidStrength,float turnRate,
    float bodyHeight,float groundY,float frameCount,float fps,float playbackSpeed,
    float referenceSpeed,float deltaTime,uint id,
    inout float3 pos,inout float3 vel,inout float pathT,inout float phase,
    inout float heading,inout float side,inout float laps)
{
    float dt = min(deltaTime,0.1);
    if (dt<=0) return;
    float personalSpeed = max(0,speed)*(0.9+0.2*CrowdHash(id+71u));
    float margin = 0.08;
    float safeRadius = max(0.05,radius)+margin;
    float3 initial = pos;
    float3 sampleOffset = float3(0,bodyHeight,0);

    // Project onto the local forward part of the curve. Progress comes from
    // actual movement, never age/time, so it cannot run ahead while blocked.
    float bestT=pathT, bestD=1e20;
    [unroll] for(int k=0;k<=12;k++)
    {
        float t=saturate(pathT+k*0.0025);
        float3 q=CrowdBezier(t,p0,p1,p2,p3); q.y=groundY;
        float ds=dot(pos-q,pos-q);
        if(ds<bestD){bestD=ds;bestT=t;}
    }
    if (length(vel)>0.025) pathT=bestT;
    float curveSpeed=max(length(3*(1-pathT)*(1-pathT)*(p1-p0)+
        6*(1-pathT)*pathT*(p2-p1)+3*pathT*pathT*(p3-p2)),1);
    float targetT=saturate(pathT+max(lookAhead,1.2)/curveSpeed);
    float3 target=CrowdBezier(targetT,p0,p1,p2,p3); target.y=groundY;
    float3 desired=CrowdFlat(target-pos);
    if(dot(desired,desired)<0.1) desired=float3(0,0,1);
    float3 samplePos=pos+sampleOffset;
    float reach=max(lookAhead,safeRadius*2);
    float forward= CrowdClearance(field,samplePos,desired,reach,center,extent);
    float3 left=float3(-desired.z,0,desired.x);
    float3 leftDir=CrowdFlat(desired*0.45+left);
    float3 rightDir=CrowdFlat(desired*0.45-left);
    float lc=CrowdClearance(field,samplePos,leftDir,reach,center,extent);
    float rc=CrowdClearance(field,samplePos,rightDir,reach,center,extent);
    float danger=saturate((safeRadius+0.9-forward)/0.9);
    // Persist a side while near an obstacle; unequal exits can override it.
    if(danger>0.1 && abs(lc-rc)>safeRadius*1.5) side=lc>rc?1:-1;
    float3 normal=CrowdNormal(field,samplePos,center,extent,0.12);
    float wallDistance=CrowdDistance(field,samplePos,center,extent);
    float proximity=saturate((safeRadius+0.65-wallDistance)/0.65);
    float3 escape=(side>0?leftDir:rightDir);
    desired=CrowdFlat(lerp(desired,escape,saturate(danger*avoidStrength))+
        normal*proximity*avoidStrength);
    float targetSpeed=personalSpeed*lerp(1,0.65,danger);
    float3 wanted=desired*targetSpeed;
    vel=lerp(vel,wanted,1-exp(-max(0.1,turnRate)*dt)); vel.y=0;

    // Short spatial steps + iterative projection give sliding, zero bounce,
    // zero lifetime loss. Integrate once; remove only inward normal velocity.
    int steps=clamp((int)ceil(length(vel)*dt/max(safeRadius*0.45,0.025)),1,12);
    float h=dt/steps;
    [loop] for(int s=0;s<steps;s++)
    {
        float3 next=pos+vel*h; next.y=groundY;
        [unroll] for(int j=0;j<4;j++)
        {
            float d=CrowdDistance(field,next+sampleOffset,center,extent);
            if(d>=safeRadius) break;
            float3 n=CrowdNormal(field,next+sampleOffset,center,extent,0.12);
            if(dot(n,n)<0.1) {next=pos;vel=0;break;}
            next+=n*(safeRadius-d+0.002);
            vel-=min(dot(vel,n),0)*n;
        }
        if(CrowdDistance(field,next+sampleOffset,center,extent)>=safeRadius-0.02) pos=next;
        else vel=0;
    }
    pos.y=groundY;
    float3 actual=(pos-initial)/dt;
    float actualSpeed=length(actual);
    if(actualSpeed>0.025)
    {
        float yaw=atan2(actual.x,actual.z);
        float diff=atan2(sin(yaw-heading),cos(yaw-heading));
        heading+=diff*(1-exp(-max(0.1,turnRate)*dt));
    }
    phase=fmod(phase+actualSpeed/max(referenceSpeed,0.01)*fps*playbackSpeed*dt,max(frameCount,1));
    if(pathT>0.95 && distance(pos.xz,p3.xz)<max(0.75,radius*2))
    {
        pathT=0; pos=p0;pos.y=groundY;vel=0;laps+=1;
        side=CrowdHash(id+193u)>0.5?1:-1;
    }
}
#endif
