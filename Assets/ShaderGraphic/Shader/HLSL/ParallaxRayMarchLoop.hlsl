#define MAXITERATIONS 20
#define MAXSEARCHSTEPS 4

void ParallaxLoopDefault_float(Texture2D HeighMap,SamplerState HeightMapSampler, float2 TexUV,float2 UVOffset,float2 UVDelta,float StepHeight,float StepSize, out float2 Out)
{

    float SurfaceHeight =SAMPLE_TEXTURE2D(HeighMap,HeightMapSampler,TexUV).g;

    for(int i=0;i<MAXITERATIONS && StepHeight > SurfaceHeight;i++)
    {     
        UVOffset -=UVDelta;
        StepHeight -=StepSize;
        SurfaceHeight =SAMPLE_TEXTURE2D(HeighMap,HeightMapSampler,TexUV+UVOffset).g;
    }
    Out = UVOffset;
}


void ParallaxLoopInterpolate_float(Texture2D HeighMap,SamplerState HeightMapSampler, float2 TexUV,float2 UVOffset,float2 UVDelta,float StepHeight,float StepSize, out float2 Out)
{

    float SurfaceHeight =SAMPLE_TEXTURE2D(HeighMap,HeightMapSampler,TexUV).g;


    float2 prevUVOffset =UVOffset;
    float prevStepHeight =StepHeight;
    float prevSurfaceHeight =SurfaceHeight;

    for(int i=0;i<MAXITERATIONS && StepHeight > SurfaceHeight;i++)
    {
        prevUVOffset =UVOffset;
        prevStepHeight =StepHeight;
        prevSurfaceHeight =SurfaceHeight;

            
        UVOffset -=UVDelta;
        StepHeight -=StepSize;

        SurfaceHeight =SAMPLE_TEXTURE2D(HeighMap,HeightMapSampler,TexUV+UVOffset).g;

    }


    float prevDifference = prevStepHeight-prevSurfaceHeight;
    float difference = SurfaceHeight -StepHeight;
    float t = prevDifference/(prevDifference+difference);
    UVOffset= lerp(prevUVOffset,UVOffset,t);

    Out = UVOffset;
}


void ParallaxLoopSearch_float(Texture2D HeighMap,SamplerState HeightMapSampler, float2 TexUV,float2 UVOffset,float2 UVDelta,float StepHeight,float StepSize, out float2 Out)
{

    float SurfaceHeight =SAMPLE_TEXTURE2D(HeighMap,HeightMapSampler,TexUV).g;
    for(int i=0;i<MAXITERATIONS && StepHeight > SurfaceHeight;i++)
    { 
        UVOffset -=UVDelta;
        StepHeight -=StepSize;
        SurfaceHeight =SAMPLE_TEXTURE2D(HeighMap,HeightMapSampler,TexUV+UVOffset).g;
    }

    for (int i = 0; i < MAXSEARCHSTEPS; i++) 
    {
        UVDelta*=0.5;
        StepHeight*=0.5;
        if (StepHeight<SurfaceHeight)
        {
            UVOffset+=UVDelta;
            StepHeight += StepSize;
        }
        else
        {
            UVOffset -= UVDelta;
            StepHeight -= StepSize;
        }
        SurfaceHeight =SAMPLE_TEXTURE2D(HeighMap,HeightMapSampler,TexUV+UVOffset).g;
    }
    Out = UVOffset;

}
