#ifndef RAYMARCHING_GODRAY_INCLUDED
#define RAYMARCHING_GODRAY_INCLUDED




struct GodRayParams
{
    float3 ro;          
    float3 rd;          
    float maxDist;    
    float3 lightDir;   
    float stepSize;     
    int maxSteps;
    int maxLightSamples;       
    float density;      
    float intensity;    
    float decay;
    float dither;    
    float g;         
};



float3 CalculateGodRays(GodRayParams params)
{
    float3 accumFog = float3(0, 0, 0);
    float t = 0.0;
    
    t += params.dither * params.stepSize;

    float scattering = HenyeyGreenstein(params.lightDir,params.rd,params.g);

    scattering = max(scattering, 0.15);
     
    float prevVisibility = 1.0;
    float shadowContrast = 0.0;

    for(int i = 0; i < params.maxSteps; i++)
    {
        if(t >= params.maxDist) break;

        float3 currentPos = params.ro + params.rd * t;


        float visibility = Softshadow(currentPos, params.lightDir,0.05,50.0, 8.0,params.maxLightSamples);
        visibility = max(visibility, 0.05);

        if(visibility > SURFANCE_DISTANCE)
        {
            float visibilityChange = abs(visibility - prevVisibility);
            shadowContrast += visibilityChange;
        
            float distDecay = exp(-t * params.decay); 
            accumFog +=  visibility * distDecay;
        }
    
    prevVisibility = visibility;
    t += params.stepSize;
}


    float scatteringCoeff = 0.1; 

    return accumFog * params.stepSize * params.density * params.intensity * scatteringCoeff * scattering;
}

#endif