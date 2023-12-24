Shader "RayMarchingShader/Noise"
{
    Properties
    {

        _BackGround("Tex",Cube)="white"{}
        _NoiseTexture("NoiseTexture",2D)="white"{}

        _DarkThreshold("_DarkThreshold",Range(0,1))=0.15
        _Absorption("LightAbsorption",Range(0,100))=80

        _ShadowColor("ShadowColor",Color)=(1,1,1,1)
        _BaseColor("BaseColor",Color)=(1,1,1,1)


    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

               Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"

            //#include "../HLSL/LightModels.hlsl"
            #include "../HLSL/MathTools.hlsl"
            #include "../HLSL/RayMarchingShape.hlsl"


            #define SURFANCE_DIS 1e-4
            #define MAX_STEPS 68
            #define MAX_DISTACNE 30
            #define ZERO 0
            #define PLANEHEIGHT 0.5
            #define IOR 1.45


            #define MAT_DEFAULT 0
            #define MAT_GROUND 1
            #define MAT_BALL 2
            #define MAT_Box 3

            #define TEXTURE_FROM_2D
           // #define _VALUE_NOISE
            #define _SIMPLEX_NOISE
           
            struct vertexData
            {
                float4 positionOS : POSITION;
                float3 normalOS:NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct fragmentData
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
                float3 normalWS:TEXCOORD2;
                
            };



            TEXTURECUBE(_BackGround);
            SAMPLER(sampler_BackGround);

            float _DarkThreshold;
            float _Absorption;

            float3 _ShadowColor;
            float3 _BaseColor;


            fragmentData vert (vertexData input)
            {
                fragmentData o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv;
                return o;
            }

            float3x3 Camera_matrix(float3 origin, float3 target)
            {
                float3 forward = normalize(target - origin);
                float3 right = cross(forward,float3(0,1,0));
                float3 up = cross(right,forward);

                return float3x3(right,up,forward);
            }



            float GetDistance(float3 p)
            {
                float d = 0;
                float sphere =length(p)-1;
                float sphere2 =length(p-float3(1,0,0))-1;
                d= min(sphere,sphere2);
                //d =sphere;
        

                return d;

            }

            float RayMarching(float3 ro, float3 rd)
            {
                float  d=0.0;


                for (int i = 0; i < MAX_STEPS; i++)
                {
                    float3 p = ro + rd * d;

                    float currentDistance =GetDistance(p).x;
                
                    if (abs(currentDistance) < SURFANCE_DIS || d > MAX_DISTACNE)
                    break;

                    d += currentDistance;
                }

                return  d;
            }

           float3 GetNormal(float3 p) // for function f(p)
            {
                float h = 0.0001; // replace by an appropriate value
                float2 k = float2(1,-1);
                return normalize( k.xyy*GetDistance( p + k.xyy*h) + 
                                k.yyx*GetDistance( p + k.yyx*h) + 
                                k.yxy*GetDistance( p + k.yxy*h) + 
                                k.xxx*GetDistance( p + k.xxx*h));
            }

            float CalcAO( float3 pos, float3 nor )
            {
                float occ = 0.0;
                float sca = 1.0;
                for( int i=ZERO; i<5; i++ )
                {
                //沿着法线方向给定5个步长
                float h = 0.01 + 0.12*float(i)/4.0;
                //计算 这5个采样点的距离
                float d = GetDistance( pos + h*nor).x;
                occ += (h-d)*sca;
                sca *= 0.95;
                if( occ>0.35 ) break;
                }
                return clamp( 1.0 - 3.0*occ, 0.0, 1.0 ) * (0.5+0.5*nor.y);
            }

             float3x3 m = float3x3(  0.00,  0.80,  0.60,
                                    -0.80,  0.36, -0.48,
                                     -0.60, -0.48,  0.64);

            float hash(float n)
            {
                return frac(sin(n)*43758.5453);
            }


            float noise(in float3 x)
            {
                //x.xz =mul(Rot(_Time.y),x.xz);
                //x.y +=_Time.y;
                float3 p = floor(x);
                float3 f = frac(x);
                
                f = f * f * (3.0 - 2.0 * f);
                
                float n = p.x + p.y * 32.0 + 100.0 * p.z;
                
                float res = lerp(lerp(lerp(hash(n +   0.0), hash(n +   1.0), f.x),
                                    lerp(hash(n +  57.0), hash(n +  58.0), f.x), f.y),
                                lerp(lerp(hash(n + 113.0), hash(n + 114.0), f.x),
                                    lerp(hash(n + 170.0), hash(n + 171.0), f.x), f.y), f.z);
                return res;
            }


            float fbm(float3 p)
            {
               // p+=_Time.y;
                float f=0;
                f  += 0.5000 * noise(p); p =mul(m,p) * 2.02; 
                f += 0.2500 * noise(p); p = mul(m,p) * 2.03;
                f += 0.1250 * noise(p);p = mul(m,p) * 2.01;
                //f += 0.06250*noise( p );
                return f;
            }


            float map(float3 p)
            {
                p-=float3(0,0.2,0.5)*_Time.y;

                return (FBMNoise3D_ABS(p*0.75,0.25)*0.5+0.5);
            }



            float BeerPower(float light_samples)
            {
                float powder_sugar_effect = 1.0 - exp(-light_samples * 2.0);
                float beers_law = exp(-light_samples);
                float light_energy = 2.0 * beers_law * powder_sugar_effect;
                return light_energy;

            }


            half4 frag (fragmentData input) : SV_Target
            {
                // sample the texture
                half4 col = 0;
                float2 uv = (input.uv-0.5)*_ScreenParams.xy/_ScreenParams.y;
               
                float3 ro =_WorldSpaceCameraPos;
                float3 target =0;
                float3 rd = mul(float3(uv.x,uv.y,1.0),Camera_matrix(ro,target));
                Light light = GetMainLight();
                float3 lightDir = normalize(light.direction);


                float3 boxMin = float3(-14,-6,-14);
                float3 boxMax = float3(14,6,14);
                float2 rayBoxDst = RayBoxDst(boxMin, boxMax, ro, rd);

                float marchingLength=0;
                float marchingLengthLight=0;
                float totalDensity = 0;
                float lightAccumulation =0;

                float transmission=0;
                float transmittance =1.0;
                float finalLight=0;


                

                int maxSteps =32;
                float steps =rayBoxDst.y/float(maxSteps);

                int maxLightSteps=8;
                

                float3 startPos = ro + rd * rayBoxDst.x;
                if (startPos.x>boxMin.x || startPos.x<boxMax.x || startPos.y>boxMin.y || startPos.y<boxMax.y)
                {

                    float d = 0;

                    for (int i = 0; i < maxSteps; i++)
                    {
                        marchingLength+=steps;

                        float3 p = startPos + rd * d;

                        if (marchingLength>rayBoxDst.y) break;

                        totalDensity +=map(p)*steps;



                        float3 lightPos =p;
                        float2 rayBoxDstlight = RayBoxDst(boxMin, boxMax, lightPos,lightDir);
                        float lightSteps =rayBoxDstlight.y/float(maxLightSteps);



                        for(int j=0;j<maxLightSteps;j++)
                        {
                            lightPos+=lightDir*lightSteps;
                            marchingLengthLight+=lightSteps;
                            float lightDensity = map(lightPos);
                            if (marchingLengthLight>rayBoxDstlight.y) break;

                            lightAccumulation+=lightDensity*lightSteps;
                            
                        }
                        float lightTransmission = exp(-lightAccumulation);
                        float shadow = _DarkThreshold+(1-_DarkThreshold)*lightTransmission;

                        finalLight += totalDensity*shadow*transmittance;

                        transmittance*=exp(-totalDensity*_Absorption);

                        
                        d+=steps;
                    }

                    transmission = exp(-totalDensity);


                    col.rgb = lerp(_ShadowColor,_BaseColor,finalLight);



                    col.rgb = lerp(float3(0,0,0),col.rgb,1-transmission);
                }

                // col.rgb =FBMNoise2DFromTexture(uv,2);
                //col.rgb =SAMPLE_TEXTURECUBE_LOD(_BackGround,sampler_BackGround, rd,2);
                //col.rg = uv;

                return col;
            }
            ENDHLSL
        }
    }
}
