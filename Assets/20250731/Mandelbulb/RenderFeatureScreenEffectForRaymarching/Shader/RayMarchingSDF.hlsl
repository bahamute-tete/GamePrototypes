#include "RayMarchingUtils.hlsl"


#define MANDELBULB_MAX_ITERATIONS 10

#define MAT_PIGHEAD 1
#define MAT_CUBES 2
#define MAT_MANDELBULB 3
#define MAT_BOOTH 4
#define MAT_VOLUME_FOG 5


float SDFSphere(float3 p, float4 sphere) 
{
    return length(p - sphere.xyz) - sphere.w;
}

float SDFPlane(float3 p, float4 plane) 
{
    return dot(p, plane.xyz) + plane.w;
}

float SDFBox(float3 p, float3 boxSize, float radius)
{
    float3 d = abs(p) -boxSize;
    return length(max(d,0)) - radius + min(max(d.x,max(d.y,d.z)),0.0);
}

float SDFMandelbulb(float3 p, float power,out float4 trap)
{
    float3 z = p;
    float dr = 1.0;
    float r = 0.0;

    trap = float4(10000.0, 10000.0, 10000.0, 10000.0);

    for(int i=0; i<MANDELBULB_MAX_ITERATIONS; i++) 
    {
        r = length(z);

        if(r>4.0) break;

        // trap.x：捕获到原点的最小距离 → 产生同心圆状颜色
        // trap.y：捕获到平面的最小距离 → 产生条纹状颜色
        // trap.z：捕获到球体的最小距离 → 产生环状高光
        // trap.w：记录迭代次数 → 产生深度感

        // trap.x: 到原点的距离
        trap.x = min(trap.x, r);
         // trap.y: 到某个平面的距离（例如 y=0 平面）
        trap.y = min(trap.y, abs(z.y));
        // trap.z: 到某个球体的距离
        float sphereDist = length(z - float3(0.0, 4.0, 0.0)) - 0.2;
        trap.z = min(trap.z, abs(sphereDist));
        // trap.w: 记录迭代次数
        trap.w = float(i) / float(MANDELBULB_MAX_ITERATIONS);

        //dr = |f'(z)| * dr + 1.0; 求导近似
        dr = pow(r, power-1.0) * power * dr + 1.0;
        float theta = acos(z.z/r);
        float phi = atan2(z.y,z.x);
        float zr = pow(r,power);

        theta = theta*power;
        phi = phi*power;

        z = zr * float3(sin(theta)*cos(phi), sin(phi)*sin(theta), cos(theta));
        z += p;
    }
    //使用距离估算公式 (Distance Estimator)
    //https://iquilezles.org/articles/mandelbulb/
    float dist = 0.5 * log(r) * r / dr;
    return dist;
}

float SDFPigHead(float3 p,float3 scale)
 {   
    // SDFBox 边界防护：使用 SDFBox 计算外部距离，光线也能正确地一步步逼近盒子，而不会直接跳过。
    // Saturate UV：对 UV 做 saturate (Clamp 0~1)，保证在盒子边缘采样到的是边缘的距离值，实现平滑过渡。
    // 分离缩放：保留了 distToBox 的真实物理尺度。这既解决了黑斑问题，又保证了Raymarching在接近物体前的效率
    float3 s = scale;
    //计算到包围盒的精确有向距离 (Signed Distance)
    //保证包围盒外部，SDF是连续且正确的，不会出现跳变
    float3 boxSize = s;
    float distToBox = SDFBox(p, boxSize, 0.0);

    if(distToBox > 0.1) return distToBox;

    float3 uvw = (p / s) * 0.5 + 0.5;
    // float3 clampedUVW = saturate(uvw); 

    float d = SAMPLE_TEXTURE3D_LOD(_SDFTexture,sampler_SDFTexture,uvw, 0).r;
    
    float dIn = d * s.x ;
   
    // 如果在外部(distToBox > 0)：返回 "到盒子距离 + 盒子表面采样距离"
    // 如果在内部(distToBox <= 0)：只返回 "盒子内部采样距离" (max(0, distToBox)为0)
    return  dIn + max(0.0, distToBox);
}

float SDFTexture(float3 p,float3 scale)
{
    float3 s = scale;
    float3 boxSize = s;
    float distToBox = SDFBox(p, boxSize, 0.0);

    if(distToBox > 0.1) return distToBox;

    float3 uvw = (p / s) * 0.5 + 0.5;
    float d = SAMPLE_TEXTURE3D_LOD(_SDFTexture,sampler_SDFTexture,uvw, 0).r;
     d*=s.x ;
    return  d + max(0.0, distToBox);
}



float2 GetDistAndMat(float3 p)
{
    float4 cubeParams = float4(0.5,0.05,0.5,0.05);
    float4 mandelbulbParams = float4(0,4,0,8);
    float4 boothParams = float4(0,1.1,0,1.15);

    float3 fogBoxCenter = float3(0, 2, 0); 
    float3 fogBoxSize = float3(3, 2, 3); 
    float3 pFog = p - fogBoxCenter;
    float fogBox = SDFBox(pFog, fogBoxSize, 0.0);
    float2 fogBoxAndMat = float2(fogBox, MAT_VOLUME_FOG); 
    
    float3 boothCenter = p - boothParams.xyz;
    float booth = SDFTexture(boothCenter, boothParams.www);
    booth = opDilate(booth, 0.01);
    float2 boothAndMat = float2(booth, MAT_BOOTH);

    float combineCubes=10000;
    float gap = 0.11;
    [loop]
    for(uint i=0; i<25; i++)
    {
        uint col = i % 5;
        uint row = i / 5;
        float cellSize = cubeParams.x * 2 + gap;
        float3 offset = float3((col - 2) * cellSize, 0, (row - 2) * cellSize);
        float3 pLocal = p - offset;
        float cube = SDFBox(pLocal, cubeParams.xyz, cubeParams.w);
        combineCubes = min(combineCubes, cube);
    }
    float2 combineCubesAndMat = float2(combineCubes, MAT_CUBES);
    
    p-=mandelbulbParams.xyz;
    p-=float3(0, sin(_Time.y)*0.5,0);
    p.yz = mul(Rot(_Time.y), p.yz);
    p.xz = mul(Rot(_Time.y*1.5), p.xz);
    float4 traps;
    float mandelbulb = SDFMandelbulb(p, mandelbulbParams.w, traps);
    float2 mandelbulbAndMat = float2(mandelbulb, MAT_MANDELBULB);


    float2 minDistAndMat = opUnion(combineCubesAndMat, boothAndMat);
    minDistAndMat = opUnion(minDistAndMat, mandelbulbAndMat);
    // minDistAndMat = opUnion(minDistAndMat, fogBoxAndMat);
    
    // float d = smin(pighead, combineCubes, 0.1);
    // d = smin(d, mandelbulb, 0.1);
    
  
    return float2(minDistAndMat.x, minDistAndMat.y);


}

float GetDist(float3 p) 
{
       return GetDistAndMat(p).x;
}

float3 GetMandelbulbColor(float3 p, float power)
{
    float3 center = float3(0, 4, 0); 
    p -= center;

    float4 trap;
    SDFMandelbulb(p, power, trap);
    // return float3(trap.x, trap.y, trap.z);
    // return float3(trap.xyz*0.001);

    // Orbit Trap 的原理是记录轨道中最接近某几何体的距离。
    // distance 越小（越接近 0），说明被捕获这越强，应该越亮。
    // 使用 1.0 - trap 配合 pow 来制作发光效果。
    float3 finalCol = float3(0.0, 0.0, 0.0);
    
    
    float t1 = pow(saturate(1.0 - trap.x * 0.5), 3.0);
    float t2 = pow(saturate(1.0 - trap.y), 3.0);
    float t3 = pow(saturate(1.0 - trap.z), 2.0);
    
    finalCol += float3(1.0, 0.3, 0.1) * t1;        // 橙红色
    finalCol += float3(0.1, 0.5, 1.0) * t2;        // 蓝色
    finalCol += float3(1.0, 1.0, 0.2) * t3 * 1;  // 黄色高光
    
    finalCol *= (0.3 + 0.7 * trap.w);
    finalCol += float3(0.02, 0.02, 0.05);

   
    // float hue = frac(trap.x * 2.0 + trap.y * 0.5);
    // float3 col = float3(
    //     abs(hue * 6.0 - 3.0) - 1.0,
    //     2.0 - abs(hue * 6.0 - 2.0),
    //     2.0 - abs(hue * 6.0 - 4.0)
    // );
    // col = saturate(col);
    

    // float brightness = 0.5 + 0.5 * trap.w;
    // brightness *= (1.0 - trap.z * 0.3); // 球体陷阱添加阴影
    // col *= brightness;

    return saturate(finalCol);

}


float3 GetMaterialColor(float matID, float3 p,float3 n)
{
    //  MAT_PIGHEAD 1
    //  MAT_CUBES 2
    //  MAT_MANDELBULB 3
    float3 col= float3(1,1,1);
    switch(matID)
    {
        case MAT_BOOTH:
            col = float3(1, 1, 1);
            break;
        case MAT_PIGHEAD:
            col = float3(1, 1, 1);
            break;
        case MAT_CUBES:
            col = float3(1, 1, 1);
            break;
        case MAT_MANDELBULB:
            col = GetMandelbulbColor(p, 8.0);
            // col = float3(1,1,1);
            break;
    }

    return col;  
}