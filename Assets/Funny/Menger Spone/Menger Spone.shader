Shader "Unlit/Menger Spone"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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

            // //#include "../HLSL/LightModels.hlsl"
            #include "../RayMarchingShader/HLSL/MathTools.hlsl"
            #include "../RayMarchingShader/HLSL/RayMarchingShape.hlsl"

            #define SURFANCE_DIS 1e-4
            #define MAX_STEPS 68
            #define MAX_DISTACNE 30

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

            float3x3 Camera_matrix(float3 origin, float3 target)
            {
                float3 forward = normalize(target-origin);
                float3 right = cross(forward,float3(0,1,0));
                float3 up = cross(right,forward);

                return float3x3(-right,up,forward);
            }

            float sdCross( in float3 p )
            {
            float da = SDFBox(p.xyz,float3(10,1.0,1.0),0.1);
            float db = SDFBox(p.yzx,float3(1.0,10,1.0),0.1);
            float dc = SDFBox(p.zxy,float3(1.0,1.0,10),0.1);
            return min(da,min(db,dc));
            }


            float map(float3 p)
            {   
                float d= 0 ;

                //float3 q = (p-1.5) -3.0*clamp(round((p-1.5)/3.0),-1,1);
                d= SDFBox(p, float3(1,1,1),0.01);
                float s = 1.0;
                for( int m=0; m<3; m++ )
                {
                    //float3 a = fmod( abs(p*s), 2.0 )-1.0;//[-1,1]
                    float3 a =(p*s+1.0)- 2.0*round((p*s+1.0)/2.0);
                    s *= 3.0;
                    float3 r = 1.0 - 3.0*abs(a);

                    float c = sdCross(r)/s;
                    d = max(d,c);
                }
                return d;

            }

            float map1(float3 p) 
            {   
                float d= 0 ;
                float da = SDFBox(p.xyz,float3(5,1.0,1.0),0.1);
                float db = SDFBox(p.yzx,float3(1.0,5,1.0),0.1);
                float dc = SDFBox(p.zxy,float3(1.0,1.0,5),0.1);
                return min(da,min(db,dc));
               
             

            }

            float raymarch(float3 ro, float3 rd)
            {
                float d=0;
                for(int i=0; i<MAX_STEPS; i++)
                {
                    float3 pos = ro + d * rd;
                    float t = map(pos);
                    if(d>MAX_DISTACNE||abs(t)<SURFANCE_DIS) break;
                    d += t;
                }
                return d;
            }

            float3 getNormals(float3 p)
            {
                float h = 0.0001; // replace by an appropriate value
                float2 k = float2(1,-1);
                return normalize( k.xyy*map( p + k.xyy*h)+ 
                    k.yyx*map( p + k.yyx*h)+ 
                    k.yxy*map( p + k.yxy*h)+ 
                    k.xxx*map( p + k.xxx*h));

            }


            fragmentData vert (vertexData input)
            {
                fragmentData o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv;
                return o;
            }

            float4  frag (fragmentData input) : SV_Target
            {
                float4 color = 0;
                float2 uv = (2*input.uv-1.0)*_ScreenParams.xy/_ScreenParams.y;

              
                float3 ro =_WorldSpaceCameraPos;
                float3 target =0;
                float3 rd = mul(float3(uv.x,uv.y,1.),Camera_matrix(ro,target));
                Light light = GetMainLight();
                float3 lightDir =light.direction;
                float3 lightColor = light.color;  
                float  lightShadowAtten =light.shadowAttenuation;


                float d = raymarch(ro,rd);
                if (d<30)
                {
                    float3 pos = ro + rd*d;
                    float3 nor = getNormals(pos);

                    float3 refDir = reflect(rd,nor);

                    float diffuse = max(0,dot(nor,lightDir))*lightShadowAtten;

                    color.rgb = diffuse*lightColor;


                    // if( c>d )
                    // {
                    // d = c;
                    // res = vec3( d, 0.2*da*db*dc, (1.0+float(m))/4.0, 0.0 );
                    // }
                    
                }

                //color.rg =uv;
                
                return color;
            }
            ENDHLSL
        }
    }
}
