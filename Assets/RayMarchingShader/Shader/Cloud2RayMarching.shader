Shader "RayMarchingShader/Cloud2"
{
    Properties
    {

        _BackGround("Tex",Cube)="white"{}
        _NoiseTexture("NoiseTexture",2D)="white"{}
        _ShadowColor("ShadowColor",Color)=(1,1,1,1)
        _BaseColor("BaseColor",Color)=(1,1,1,1)
        _G("G",Range(-1,1))=0


        _Pos("_Pos",vector)=(0,0,0,0)
        _K("_K",Range(-1,1))=0

        _DarkThreshold("_DarkThreshold",Range(0,1))=0.15

        [KeywordEnum(NONE,ON)] _BEERLAW("BEERLAW",Float)=0
        [KeywordEnum(NONE,ON)] _ANIMATION("ANIMATION",Float)=0

        _BeerLAWAbsorption("BeerLAWAbsorption",Range(0,80))=4
         _Absorption("LightAbsorption",Range(0,100))=80
        


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

            #pragma shader_feature _ANIMATION_ON
            #pragma shader_feature _BEERLAW_ON 
            

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

            #define USE_LOD  1

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

            // TEXTURE2D(_NoiseTexture);
            // SAMPLER(sampler_NoiseTexture);
            float _Absorption,_BeerLAWAbsorption;
            float3 _ShadowColor;
            float3 _BaseColor;
            float _G,_K;
            float3 _Pos;
            float _DarkThreshold;



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

            float sdTorus( float3 p, float2 t )
            {
                float2 q = float2(length(p.xz)-t.x,p.y);
                return length(q)-t.y;
            }

            float scene(in float3 p)
            {
               
                #if defined (_ANIMATION_ON)
                 float3 t = float3(0.0,0.3,0.1)*_Time.y;
                 _Pos=float3(3.5*cos(_Time.y),2.,3.5*sin(_Time.y));
                #else
                 float3 t = 0.0;
                #endif

                float3 q = p - t;
                //float f = fbm(q);
                //float p1= -(length(pos)*0.01-0.01)+FBMNoise3D_ABS(pos*0.12,0.2);
                //float p2= 0.1 - length(p) * 0.05 + fbm(p * 0.3);
                //return 0.1-length(p)*.05+FBMNoise3D_ABS(p*0.9,0.1);
                float d=1-length(p-_Pos)*0.55+ FBMNoise3D_ABS(q,0.35);
                float torus = 0.86 - sdTorus(p * 0.8, float2(3.0, 0.1)) + FBMNoise3D_ABS(q,0.34)*0.8 ;
                // float box= SDFBox(p,float3(1,1,1),0);
                d=min(smin(d,torus,_K),1.0);
                
                return d;
            }


             float HenyeyGreenstein(float3 inLightVector, float3 inViewVector, float inG)
            {
                float cos_angle = dot(normalize(inLightVector), normalize(inViewVector));
                return ((1.0 - inG * inG) / pow((1.0 + inG * inG - 2.0 * inG * cos_angle), 3.0/2.0))
                    / 4.0 * 3.1415;
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
                float4 color = 0;
                float2 uv = (2*input.uv-1.0)*_ScreenParams.xy/_ScreenParams.y;

                // Camera
                // float camDist = 25.0;
                // float2 mo = float2(_Time.y * 0.1, cos(_Time.y * 0.25) * 3.0);
                // float3 ro = camDist * normalize(float3(cos(2.75 - 3.0 * mo.x), 0.7 - 1.0 * (mo.y - 1.0), sin(2.75 - 3.0 * mo.x)));
               
                float3 ro =_WorldSpaceCameraPos;
                //float3 ro = float3(0,0,25.0);
                float3 target =0;
                float3 rd = mul(float3(uv.x,uv.y,1.),Camera_matrix(ro,target));
                Light light = GetMainLight();


                float g = _G;
                
                // For raymarching const values.
                const int sampleCount = 64;
                const int sampleLightCount = 6;

                // Raymarching step settings.
                float zMax =40.0;
                float zstep = zMax / float(sampleCount);

                float zMaxl = 20;
                float zstepl = zMaxl / float(sampleLightCount);

                float3 p = ro;
                // Transmittance
                float T = 1.0;

                // Substantially transparency parameter.
                float absorption = 100.0;

                float densitySum=0;
                float lightAccumulation=0;
                float transmittance=0.97;
                float finalLight=0;
                float lightTransmission =0;
                float shadow =0;
                float transmission;
                float d=0;


                float3 sun_direction =normalize(light.direction);
               
#if defined(_BEERLAW_ON)
                for (int i = 0; i < sampleCount; i++)
                {

                    float3 p =ro+rd*d;
                    float density = scene(p);
                    if (density > 0.00)
                    {
                       
                        densitySum += density;
                    }

                    float3 samplePos = p;

                    for(int j =0;j<sampleLightCount;j++)
                    {
                        samplePos += sun_direction*zstepl;
                        float lightDensity = scene(samplePos);
            
                        lightAccumulation +=max(lightDensity/float(sampleCount),0);
                        
                    }

                    lightTransmission = BeerPower(lightAccumulation);

                    shadow = _DarkThreshold+lightTransmission*(1.0-_DarkThreshold);
                    float   phaseValue = HenyeyGreenstein(sun_direction,rd,g);


                    finalLight += densitySum*transmittance*shadow*phaseValue;
                    //finalLight += exp(densitySum)*lightTransmittance;

                    transmittance *=exp(-densitySum*_BeerLAWAbsorption);

                    d += zstep;
                }
      
                transmission = exp(-densitySum);
                float3 result =  float3(finalLight, transmission, transmittance);

                

                //color.rgb =finalLight;

                if(densitySum>0)
                color.rgb =lerp(_ShadowColor,_BaseColor,saturate(result.x));

                color.rgb = lerp(float3(0,0,0), color.rgb,1.0- result.y);
#else            

                for (int i = 0; i < sampleCount; i++)
                {
                    // Using distance function for density.
                    // So the function not normal value.
                    // Please check it out on the function comment.
                    float density = scene(p);
                    
                    // The density over 0.0 then start cloud ray marching.
                    // Why? because the function will return negative value normally.
                    // But if ray is into the cloud, the function will return positive value.
                    if (density > 0.0)
                    {
                        // Let's start cloud ray marching!
                        
                        // why density sub by sampleCount?
                        // This mean integral for each sampling points.
                        float tmp = density / float(sampleCount);
                        
                        //T *= exp(-tmp * _Absorption);
                        T *=1- (tmp * _Absorption);
                        
                        // Return if transmittance under 0.01. 
                        // Because the ray is almost absorbed.
                        if (T <= 0.01)
                        {
                            break;
                        }

                        float Tl=1.0;
                        float3 lp =p;

                        for(int j =0;j<sampleLightCount;j++)

                        {
                            float lightDensity = scene(lp);
                            if (lightDensity >0)
                            {
                                float tmpl=lightDensity/float(sampleCount);
                                //Tl *=  exp(-tmpl * _Absorption);
                                Tl *= 1- (tmpl * _Absorption);
                            }

                            if (Tl <= 0.01) break;
                            lp +=sun_direction*zstepl;
                        }
                        

                        // Add ambient + light scattering color
                        float opacity = 50.0;
                        float k = opacity * tmp * T;
                        float4 cloudColor =float4( _BaseColor,1);
                        float4 col1 = cloudColor * k;


                        float opacityl =40.0;
                        float kl = opacityl * tmp * Tl*T;
                        float4 lightColor =float4( light.color,1.0);
                        float4 col2 = lightColor * kl;
                        
                       
                        
                        
                        color += col1 + col2;
                    }
                    
                    p += rd * zstep;
                }
#endif
                
                
                return color;
            }
            ENDHLSL
        }
    }
}
