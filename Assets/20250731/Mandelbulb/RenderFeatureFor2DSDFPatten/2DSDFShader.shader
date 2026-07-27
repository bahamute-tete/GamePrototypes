Shader "Custom/2DSDFShader"
{
    Properties
    {
        _OutterColor("OutterColor", Color) = (1,0,0,1)
        _InnerColor("InnerColor", Color) = (1,1,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "2DSDFPass"

            
            HLSLPROGRAM
            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../RenderFeatureScreenEffectForRaymarching/Shader/RayMarchingInputs.hlsl"
            #include "../RenderFeatureScreenEffectForRaymarching/Shader/RayMarchingUtils.hlsl"
            #include "../RenderFeatureScreenEffectForRaymarching/Shader/Sdg2D.hlsl"

            #define HEXAGON_COUNT 32
            
            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }
            
            float3 _OutterColor, _InnerColor;

            float _MusicFrequencies[8] = (float[8])(1.0,1.0,1.0,1.0,1.0,1.0,1.0,1.0);

            float3 HueToRGB(float hue)
            {
                float3 rgb;
                hue = hue - floor(hue);
                rgb.r = 1.0 - abs(2.0 * hue - 1.0);
                rgb.g = 1.0 - abs(2.0 * hue - 0.5);
                rgb.b = 1.0 - abs(2.0 * hue - 1.5);
                return rgb;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // float4 clipPos = float4(i.uv * 2.0 - 1.0, 0.0, 1.0);
                // float2 ndcPos = clipPos.xy / clipPos.w;
                // float2 screenPos = ndcPos * 0.5 + 0.5;

                float uvScaler = 3.0;
                float2 uv =(i.uv * 2.0 - 1.0)*_ScreenParams.xy/_ScreenParams.y; //i.uv;
                uv = uv *uvScaler;

                float4 col = float4(0,0,0,1);

                // float2 crossuv = uv ;
                // crossuv = Rotate2D(crossuv, _MusicFrequencies[4]);   
                // float3 cross = sdgCross(crossuv,float2(1.5,0.3)*_MusicFrequencies[3]*1.5)-0.1;
                // float crossDist =cross.x;
                // float2 crossGrad = cross.yz;

                float hexDistCombine =1e10;
                float2 hexGradCombine = float2(0,0);
                float3 colorAccum = float3(0,0,0);
                float weightSum = 0.0;
     
                for(uint i=0;i<HEXAGON_COUNT;i++)
                {
                    float r =2.0*_MusicFrequencies[i%8]+1;
                    float delta = 2*pi/HEXAGON_COUNT;
                    float x = r*cos(delta*(float)i);
                    float y = r*sin(delta*(float)i);
                    float2 offset = float2(x,y);

                    float2 hexPos = uv + offset;
                    hexPos +=fbm(float3(hexPos,0.0),3,5,0.5)*0.2;
                    hexPos =Rotate2D(hexPos,delta*(float)i+_Time*_MusicFrequencies[i%8]*100);

                    float3 hex = sdgHexagon(hexPos, 0.2+_MusicFrequencies[i%8]*0.2);
                    // float hexdist = abs(hex.x)-0.05;
                    // float2 hexgrad = sign(hex.x)*hex.yz;
                    float hexdist = hex.x-max(_MusicFrequencies[i%8]*0.5,0.1);
                    float2 hexgrad = hex.yz;

                    // float hue = (float)i / (float)HEXAGON_COUNT;
                    // float3 hexColor = HueToRGB(hue);
                    float3 hexColor =saturate(float3(
                        _MusicFrequencies[i%8],
                        _MusicFrequencies[(i+3)%8],
                        _MusicFrequencies[(i+6)%8]
                    ));
                    hexColor = normalize(hexColor + 0.1);

                    // float3 hexColor = lerp(_InnerColor, _OutterColor, hue);

                    float weight = smoothstep(0.1, -0.1, hexdist);
                    colorAccum += hexColor * weight;
                    weightSum += weight;

                    hexDistCombine = smin(hexDistCombine, hexdist,0.1);
                    hexGradCombine += (hexdist<0.0)? hexgrad: float2(0,0);
                }

                float d = hexDistCombine;
                float2 grad = hexGradCombine/10.0;
                
                
                float3 baseColor = (weightSum > 0.0) ? colorAccum / weightSum: _OutterColor;
                // return weightSum/10;
                float3 color1 = (d>0.0) ? _OutterColor: baseColor;
                color1 *= 1.0 - 0.5*exp(-16.0*abs(d));//mask
                // color1 *= 1.0 + float3(0.5*grad.x,0.5*grad.y,0.0);
                //  color1 *= 0.3 + 0.1*cos(80*d);
                color1 = lerp( color1, float3(1,1,1), 1.0-smoothstep(0.0,0.01,abs(d)) );
                col =float4(color1,1.0);


                
                return pow(col, 1.0);
            }
            ENDHLSL
        }

      
    }
}

