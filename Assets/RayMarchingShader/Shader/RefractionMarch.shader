Shader "RayMarchingShader/RefracionRayMarching"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BackGround("Tex",Cube)="white"{}


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
            #include "../HLSL/LightModels.hlsl"
            #include "../HLSL/MathTools.hlsl"
            #include "../HLSL/RayMarchingShape.hlsl"



            
            #define SURFANCE_DIS 1e-3
            #define MAX_STEPS 68
            #define MAX_DISTACNE 30
            #define ZERO 0
            #define PLANEHEIGHT 0.5
            #define IOR 1.45


            #define MAT_DEFAULT 0
            #define MAT_GROUND 1
            #define MAT_BALL 2
            #define MAT_Box 3
           
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

            sampler2D _MainTex;
            float4 _MainTex_ST;


            //samplerCUBE _BackGround;
            TEXTURECUBE(_BackGround);
            SAMPLER(sampler_BackGround);

            fragmentData vert (vertexData input)
            {
                fragmentData o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return o;
            }





            float3x3 Camera_matrix(float3 origin, float3 target)
            {
                float3 forward = normalize(target - origin);
                float3 right = cross(forward,float3(0,1,0));
                float3 up = cross(right,forward);

                return float3x3(right,up,forward);
            }


            float df_obj(float3 p)
            {

            float a = (length(p.xz)-1.0-p.y*.15)*.85;
            a = max(abs(p.y)-1.0,a);
            float a2 = (length(p.xz)-0.9-p.y*.15)*.85;
            a = max(a,-max(-.8-p.y,a2));
            a = max(a,-length(p+float3(.0,4.0,.0))+3.09);
            a = a;

            float3 p2 = p; p2.xz*=(1.0-p.y*.15);
            float angle = atan2(p2.x,p2.z);
            float mag = length(p2.xz);
            angle = fmod(angle,3.14159*.125)-3.14159*.125*.5;
            p2.xz = float2(cos(angle),sin(angle))*mag;
            a = max(a,(-length(p2+float3(-7.0,0.0,0.0))+6.05)*.85);

             return a;
            }

            float noise_3(float3 p) 
            {
                float3 i = floor(p);
                float3 f = frac(p);	
                float3 u = f*f*(3.0-2.0*f);

                float2 ii = i.xy + i.z * float2(5.0,5.0);
                float a = hash21( ii + float2(0.0,0.0) );
                float b = hash21( ii + float2(1.0,0.0) );    
                float c = hash21( ii + float2(0.0,1.0) );
                float d = hash21( ii + float2(1.0,1.0) ); 
                float v1 = lerp(lerp(a,b,u.x), lerp(c,d,u.x), u.y);

                ii += float2(5.0,5.0);
                a = hash21( ii + float2(0.0,0.0) );
                b = hash21( ii + float2(1.0,0.0) );    
                c = hash21( ii + float2(0.0,1.0) );
                d = hash21( ii + float2(1.0,1.0) );
                float v2 = lerp(lerp(a,b,u.x), lerp(c,d,u.x), u.y);

                return max(lerp(v1,v2,u.z),0.0);
            }

            // fBm
            float fbm3(float3 p, float a, float f) {
                p.xz =mul(Rot(_Time.y),p.xz);
                return noise_3(p);
            }

            float fbm3_high(float3 p, float a, float f) {
                //p.xz =mul(Rot(_Time.y),p.xz);
                float ret = 0.0;    
                float amp = 1.0;
                float frq = 1.0;
                for(int i = 0; i < 5; i++) {
                float n = pow(noise_3(p * frq),2.0);
                ret += n * amp;
                frq *= f;
                amp *= a * (pow(n,0.2));
                }
            return ret;
}

            float boolSmoothIntersect(float a, float b, float k ) {
                float h = clamp(0.5+0.5*(b-a)/k, 0.0, 1.0);
                return lerp(a,b,h) + k*h*(1.0-h);
            }
            float boolSmoothSub(float a, float b, float k ) {
                return boolSmoothIntersect(a,-b,k);
            }	


            float Rock(float3 p) 
            {    
                float d = SDFSphere(p,1.0);
                for(int i = 0; i < 9; i++) {
                    float ii =float( i);
                    float r = 2.5 + hash11(ii);
                    float3 v = normalize(hash31(ii) * 2.0 - 1.0);

                    d = boolSmoothSub(d,SDFSphere(p+v*r,r * 0.8),0.05);
      
                }
                return d;
            }


            float2 GetDistance_float(float3 p)
            {

                float d =0;
                float mat =0;
                float K = 0.2;

                float plane = SDFPlane(p,float3(0,1,0))+2.;
                float testBox = SDFBox(p-float3(-2.5,0,0),float3(0.7,0.7,0.7),0.01);
                testBox+=fbm3_high(p*2,0.3,3.)*0.1;

                float rock = Rock(p)+fbm3(p*5,0.4,3.)*0.1;

                float sphere = SDFSphere(p-float3(2.5,0,0),1.0);
                sphere+=fbm3_high(p*4,0.2,3.)*0.1;

                float cup = df_obj(p);

                d=min(testBox,rock);
                d=min(d,cup);
                d=min(d,sphere);
                

                if (d ==plane)
                {
                    mat=MAT_GROUND;
                }
                else if(d ==testBox||d==sphere)
                {
                   mat=MAT_BALL;

                }
                else if (d==cup || d ==rock)
                {
                    mat =MAT_Box;
                }else
                {
                    mat=MAT_DEFAULT;
                }
                return float2(d,mat) ;
            }

            
            float2 GetDistance_Hight(float3 p)
            {

                float d =0;
                float mat =0;
                float K = 0.2;

                float plane = SDFPlane(p,float3(0,1,0))+2.;
                float testBox = SDFBox(p-float3(-2.5,0,0),float3(0.7,0.7,0.7),0.01);
                //testBox+=fbm3_high(p,0.4,3.)*0.1;

                float rock = Rock(p)+fbm3_high(p*2,0.4,3.)*0.1;

                float sphere = SDFSphere(p-float3(2.5,0,0),1.0);


                float cup = df_obj(p);

                d=min(testBox,rock);
                d=min(d,cup);
                d=min(d,sphere);
                

                if (d ==plane)
                {
                    mat=MAT_GROUND;
                }
                else if(d ==testBox || d==sphere)
                {
                   mat=MAT_BALL;

                }
                else if (d==cup || d ==rock)
                {
                    mat =MAT_Box;
                }else
                {
                    mat=MAT_DEFAULT;
                }
                return float2(d,mat) ;
            }





            float2 RayMarching(float3 ro, float3 rd,float side)
            {
                float  d=0.0;
                float mat=0.0;

                for (int i = 0; i < MAX_STEPS; i++)
                {
                    float3 p = ro + rd * d;

                    float currentDistance =GetDistance_float(p).x*side;
                    mat =GetDistance_float(p).y;

                    if (abs(currentDistance) < SURFANCE_DIS || d > MAX_DISTACNE)
                        break;

                    d += currentDistance;
                }

                return  float2(d,mat);
            }


            float3 GetNormal(float3 p) // for function f(p)
            {
                float h = 0.0001; // replace by an appropriate value
                float2 k = float2(1,-1);
                return normalize( k.xyy*GetDistance_float( p + k.xyy*h).x + 
                                k.yyx*GetDistance_float( p + k.yyx*h).x + 
                                k.yxy*GetDistance_float( p + k.yxy*h).x + 
                                k.xxx*GetDistance_float( p + k.xxx*h).x);
            }


             float3 GetNormal2(float3 p) // for function f(p)
            {
                float h = 0.0001; // replace by an appropriate value
                float2 k = float2(1,-1);
                return normalize( k.xyy*GetDistance_Hight( p + k.xyy*h).x + 
                                k.yyx*GetDistance_Hight( p + k.yyx*h).x + 
                                k.yxy*GetDistance_Hight( p + k.yxy*h).x + 
                                k.xxx*GetDistance_Hight( p + k.xxx*h).x);
            }

            half4 frag (fragmentData input) : SV_Target
            {
                // sample the texture
                half4 col = 0;
                float2 uv = (input.uv-0.5)*_ScreenParams.xy/_ScreenParams.y;
                float3 ro =_WorldSpaceCameraPos;
                float3 target =0;
                float3 rd = mul(float3(uv.x,uv.y,1.0),Camera_matrix(ro,target));

                float2 d = RayMarching(ro,rd,1);
                float matID=d.y;
                Light light = GetMainLight();

                //col =SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, rd, 0);
                col.rgb =SAMPLE_TEXTURECUBE_LOD(_BackGround,sampler_BackGround, rd,2);


                if (d.x<MAX_DISTACNE)
                {
                    float3 p =  ro + rd*d.x;
                    float3 n = GetNormal(p);
                    float3 l = light.direction;
                    float3 v = -rd;
                    float3 h = normalize(l+v);
                    float3 reflD = reflect(rd,n);
                    float3 refrD1 = refract(rd,n,rcp(IOR));



                    float nh = saturate(dot(n,h));
                    float nl = saturate(dot(n,l));
                    float spe = pow(nh,128)*1;

                    if (matID==MAT_GROUND)
                    {
                        col.rgb = checkersGradBox(p.xz,ddx(p),ddy(p));
                    }
                    else if (matID==MAT_BALL)
                    {

                        float3 pEnter = p-n*SURFANCE_DIS*3;

                        float2 dIn = RayMarching(pEnter,refrD1,-1);

                        float3 pExit =  pEnter + refrD1*dIn.x;
                        float3 nExit = -GetNormal2(pExit);

                        float3 rdOut =refract(refrD1,nExit,IOR);

                        if (dot(rdOut,rdOut)==0)
                        {
                            rdOut =reflect(refrD1,n);
                        }

                        //col.rgb =texCUBE(_BackGround, rdOut);
                         col.rgb =SAMPLE_TEXTURECUBE_LOD(_BackGround,sampler_BackGround, rdOut,0);

                        float3 pOut =pExit-nExit*SURFANCE_DIS*3;
                        float2  resOut =RayMarching(pOut,rdOut,1);
                        float matOut =resOut.y;

                        if (resOut.x<MAX_DISTACNE)
                        {
                        if (matOut==MAT_GROUND)
                        {
                            float3 p =  pOut + rdOut*resOut.x;
                            p*=1;
                            col.rgb = checkersGradBox(p.xz,ddx(p),ddy(p));
                        }
                        }

                       
                    }else if (matID==MAT_Box)
                    {

                        float3 pEnter1 = p-n*SURFANCE_DIS*3;

                        float2 dIn1 = RayMarching(pEnter1,refrD1,-1);

                        float3 pExit1 =  pEnter1 + refrD1*dIn1.x;
                        float3 nExit1 = -GetNormal2(pExit1);

                        float3 refrD2 =refract(refrD1,nExit1,IOR);

                        if (dot(refrD2,refrD2)==0)
                        {
                            refrD2 =reflect(refrD1,n);
                        }

                        //col.rgb =texCUBE(_BackGround, refrD2)*1;
                         col.rgb =SAMPLE_TEXTURECUBE_LOD(_BackGround,sampler_BackGround, refrD2,0);



                        float3 pExit2 = pExit1-nExit1*SURFANCE_DIS*3;

                        float2 dIn2 = RayMarching(pExit2,refrD2,1);

                        if (dIn2.x<MAX_DISTACNE)
                        {
                            float3 pIn2 =  pExit2 + refrD2*dIn2.x;
                            float3 nIn2 = GetNormal2(pIn2);

                            float3 refrD3 =refract(refrD2,nIn2,rcp(IOR));

                            if (dot(refrD3,refrD3)==0)
                            {
                                refrD3 =reflect(refrD2,n);
                            }

                            //col.rgb =texCUBE(_BackGround, refrD3)*1;
                            col.rgb =SAMPLE_TEXTURECUBE_LOD(_BackGround,sampler_BackGround, refrD3,0);


                            float3 pEnter3 = pIn2-nIn2*SURFANCE_DIS*3;

                            float2 dIn3 = RayMarching(pEnter3,refrD3,-1);

                            if (dIn3.x<MAX_DISTACNE)
                            {
                                float3 pExit3 =  pEnter3 + refrD3*dIn3.x;
                                float3 nExit3 = -GetNormal2(pExit3);

                                float3 refrD4 =refract(refrD3,nExit3,IOR);


                                if (dot(refrD4,refrD4)==0)
                                {
                                    refrD4 =reflect(refrD3,n);
                                }


                                //col.rgb =texCUBE(_BackGround, refrD4)*1;
                                 col.rgb =SAMPLE_TEXTURECUBE_LOD(_BackGround,sampler_BackGround, refrD4,0);

                            }
                        }

                    }else
                    {   
                        float3 l = normalize(float3(1,2,3));
                        col.rgb = saturate(dot(n,l));

                    }





                }




                return col;
            }
            ENDHLSL
        }
    }
}
