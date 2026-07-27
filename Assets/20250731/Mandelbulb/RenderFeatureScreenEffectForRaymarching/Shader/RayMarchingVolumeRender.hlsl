

float SDFCloudDensity(float3 p, float3 boxSize,float densityModifier)
{
   float3 d = abs(p) - boxSize;
    if(any(d > 2.0)) return 0.0;

    // float3 sphereCenter = float3(sin(_Time.y) * 1.5, 0, cos(_Time.y) * 1.5);
    // float sphereRadius =3.0;
    // float morphStrength =0.2;
    // float cutoffSharpness = 0.01;

    // float sphereSDF = length(p - sphereCenter) - sphereRadius;


    // float sphereCutoff = smoothstep(-cutoffSharpness, cutoffSharpness, sphereSDF);

    float3 noisePos1 = p * 0.3 + float3(_Time.y * 0.15, 0, _Time.y * 0.1);
    float largeDensity = fbm(noisePos1, 3); 

    // 让基础噪声更像云团（Billowy）
    // 将噪声范围从中性推向两极，制造团块感
    largeDensity = remap(largeDensity, 0.2, 1.0, 0.0, 1.0); 

    float3 noisePos2 = p * 1.5 + float3(_Time.y * 0.25, 0, _Time.y * 0.16);
    float detailDensity = fbm(noisePos2, 2);

    // 使用细节噪声“侵蚀”边缘，而不是简单叠加
    // 棉花糖的核心是实的，边缘是碎的
    float density = largeDensity;
    density = remap(density, detailDensity * 0.5, 1.0, 0.0, 1.0);
    
    // 高度衰减
    float heightGradient = 1.0 - saturate((p.y - boxSize.y * 0.5) / (boxSize.y));
    heightGradient = pow(heightGradient, 0.8); 
    
    //边缘柔化
    float3 edgeFade = 1.0 - smoothstep(boxSize * 0.8, boxSize, abs(p));
    float edgeFactor = edgeFade.x * edgeFade.z;  // 只在 XZ 方向衰减
    


    float baseDensity = density * heightGradient * edgeFactor;
    // baseDensity *= sphereCutoff;

    // float cloudSDF = (1.0 - baseDensity) * 0.3 - 0.15; 

    // float morphedSDF = smin(cloudSDF, sphereSDF, morphStrength);

    // float finalDensity = saturate(1.0 - (morphedSDF + 0.2) / 0.5);


    baseDensity = saturate(baseDensity - 0.1) * densityModifier; 
    

    return baseDensity;
}

float SDFTonadorDensity(float3 p, float3 boxSize, float densityModifier)
{
    // 1. 坐标归一化/偏移调整
    // 将 p 的 Y 轴范围调整到便于计算的区间，比如 0 到 1 或 -1 到 1
    // 假设 boxSize 是半尺寸，boxCenter 是原点 (0,0,0)
    float heightPercent = (p.y + boxSize.y) / (boxSize.y * 2.0); 
    
    // 2. 基础形状：倒置圆锥 (Inverted Cone)
    // 龙卷风底部细，顶部宽。
    // 半径随高度增加而增加
    float minRadius = 0.5; // 底部半径
    float maxRadius = boxSize.x * 1.5; // 顶部半径 (稍微大一点)
    float currentRadius = lerp(minRadius, maxRadius, pow(heightPercent, 0.5)); // pow 0.5 让漏斗更弯曲
    
    // 计算当前点到中心轴(Y轴)的距离
    float distToAxis = length(p.xz);
    
    // 3. 扭曲效果 (Vortex Twist)
    // 随着高度旋转 XZ 平面
    // 越到底部旋转越快，或者随时间旋转
    float twistStrength = 10.0;
    float rotationAngle = p.y * twistStrength - _Time.y * 5.0; // 随时间旋转
    float2 rotatedXZ = Rotate2D(p.xz, rotationAngle);
    
    // 构造一个新的坐标系用于噪声采样，包含旋转
    float3 twistedP = float3(rotatedXZ.x, p.y, rotatedXZ.y);

    // 4. 噪声细节 (Turbulence)
    // 使用 twistedP 采样噪声，这样噪声纹理会跟着旋转
    // 加上向上的运动 (_Time.y * speed)
    float3 noisePos = twistedP * 0.8 + float3(0, -_Time.y * 2.0, 0);
    float noise = fbm(noisePos, 3);
    
    // 5. 密度计算
    // A. 距离场衰减：离轴心越远，密度越低，但要减去噪声
    // 核心思想：distToAxis 必须小于 currentRadius
    
    // 让表面有些凹凸不平
    float displace = noise * 1.5; 
    
    // 龙卷风的核心往往是空的（风眼），或者外部密度高内部低
    // 这里做一个实心的龙卷风，边缘模糊
    float mainShape = currentRadius - distToAxis + displace;
    
    // B. 硬切边 + 软过渡
    float density = smoothstep(-1.0, 1.0, mainShape);
    
    // C. 底部接触地面处的尘埃扩散
    // 如果高度很低，增加一点径向的噪声扩散
    if (heightPercent < 0.2)
    {
         float groundDust = (1.0 - heightPercent / 0.2) * fbm(p * 2.0 - float3(0,_Time.y,0), 2);
         density += groundDust * 2.0;
    }
    
    // D. 顶部消散
    // 接近顶部边界时淡出
    density *= smoothstep(1.0, 0.8, heightPercent);
    density *= smoothstep(0.0, 0.1, heightPercent); // 底部也稍微柔和一点
    
    
    // 6. 最终修正
    density = saturate(density * densityModifier);
    
    return density;
}


float HenyeyGreenstein(float3 inLightVector, float3 inViewVector, float g)
{
    float cos_angle = dot(normalize(inLightVector), normalize(inViewVector));
    float g2 = g * g;
    return (1.0 - g2) / (4.0 * 3.1415926 * pow(max(0.001, 1.0 + g2 - 2.0 * g * cos_angle), 1.5));
}


float BeerPower(float light_samples,float powder_coeff)
{

    float powder_sugar_effect = 1.0 - exp(-light_samples * 2.0 * powder_coeff);
    float beers_law = exp(-light_samples);
    float light_energy = 2.0 * beers_law * powder_sugar_effect;
    return light_energy;

}


float4 RayMarchVolumeFog( VolumeParams volume)
{
    float3 fogBoxCenter = volume.boxCenter; 
    float3 fogBoxSize = volume.boxSize;
    float step = volume.stepSize;
    int max_Steps = volume.max_Steps;
    float3 ro = volume.ro;
    float3 rd = volume.rd;
    float3 lightDir = volume.lightDir;
    float g = volume.g;
    float absorption = volume.absorption;
    float scatteringCoeff = volume.scatteringCoeff;
    float ambientLightIntensity = volume.ambientLightIntensity;
    float directLightIntensity = volume.directLightIntensity;
    float3 fogBaseColor = volume.fogBaseColor;
    float3 fogTargetColor = volume.fogTargetColor;
    float3 lightColor = volume.lightColor;
    float maxT = volume.maxDistance;
    float densityModifier = volume.densityModifier;


    float3 roLocal = ro - fogBoxCenter;
    float2 tNearFar = BoxIntersection(roLocal, rd, fogBoxSize);

    tNearFar.x = max(tNearFar.x, 0.0);
    tNearFar.y = min(tNearFar.y, maxT);


    if(tNearFar.x >= tNearFar.y)
        return float4(0, 0, 0, 0);

    float stepSize = step; 
    int maxSteps = (int)((tNearFar.y - tNearFar.x) / stepSize);
    maxSteps = min(maxSteps, max_Steps); 

    float3 accumulatedColor = 0.0;
    float densitySum=0;
    float transmittance = 1.0;//起始能量 透射率

    float3 skyColor = fogTargetColor * 1.2; // 顶部亮色
    float3 groundColor = fogBaseColor * 0.5; // 底部暗色

    float t = tNearFar.x;

    const int lightSampleCount = 4;
    float lightAccumulation=0;

    

    [loop]
    for(int i = 0; i < maxSteps; i++)
    {
        if(transmittance < 0.01) break; 
        
        float3 p = ro + rd * t;
        float3 pLocal = p - fogBoxCenter;

        float density = SDFCloudDensity(pLocal, fogBoxSize,densityModifier);

        if (density > 0.001)
        {
            //光照采样(Shadow Ray)
            float lightAccumulation = 0;
            float3 samplePos = pLocal;

            for(int j = 0; j < lightSampleCount; j++)
            {
                samplePos += lightDir * stepSize;
                float lightDensity = SDFCloudDensity(samplePos, fogBoxSize,densityModifier);
                lightAccumulation += lightDensity * stepSize;  // 累积光路上的密度
            }

            // 计算光照衰减(Beer-Lambert)
            float lightTransmittance = BeerPower(lightAccumulation,2.0);

            // 相位函数
            // 为了棉花糖效果，我们需要很强的正向散射（银边）和一点点反向散射（让云看起来白白的）
            float phaseValue = HenyeyGreenstein(lightDir, -rd, g);
            // 混合一点各向同性散射，防止背光面太黑
            phaseValue = lerp(phaseValue, 0.5, 0.2);
            
            // --- 高度环境光 (Height Gradient Ambient) ---
            // 这是一个非常重要的 Trick：云越高，接受的环境光越多
            float heightRatio = saturate((pLocal.y + fogBoxSize.y * 0.5) / fogBoxSize.y);
            float3 ambientGradient = lerp(groundColor, skyColor, heightRatio);
           
            //局部散射光照
            //  ambientLightIntensity 不仅乘系数，还要乘高度梯度，以及被自身密度遮挡一点（Darker crevices）
            float3 ambientLight = ambientGradient * ambientLightIntensity * (0.5 + 0.5 * exp(-density * 2.0))*SampleSH(float3(0, 1, 0));
            float3 directLight = lightColor * lightTransmittance *directLightIntensity;
            float3 totalLight = directLight + ambientLight;

            //体积散射项
            float scattering = density * stepSize * scatteringCoeff;

            float sunAlignment = dot(normalize(p), normalize(lightDir));
            float3 sunsetColor = lerp(
                groundColor,   
                skyColor,   
                saturate(sunAlignment * 0.5 + 0.5)
            );

             // 核心公式：透射率 * 光照强度 * 材质颜色 * 相位 * 散射系数
            float3 scatteredLight = sunsetColor * totalLight * phaseValue * scattering;
            // 累积颜色(前向后向累积)
            accumulatedColor += transmittance * scatteredLight;

            //更新透射率(当前步的吸收)
            float extinction = density * stepSize * absorption;
            transmittance *= exp(-extinction);
        }

        t += stepSize;
        if(t >= tNearFar.y) break;
    }

    return float4(accumulatedColor, 1.0 - transmittance);
}

