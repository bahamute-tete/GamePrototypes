Shader "Unlit/GeoShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _SizeW("Size W", Range(0,1)) = 1
        _SizeH("Size H", Range(0,1)) = 1
        _Angle("Angle", Range(0,6.28))=0
        [HDR]_Color("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent" }
        LOD 100

        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha
        

        Pass
        {
            HLSLPROGRAM 
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry geom

            #pragma shader_feature_local _ _VERTICAL_CONSTRAINT
            // make fog work
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

             #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

            #include "../RayMarchingShader/HLSL/RandomTools.hlsl"



            //#define _VERTICAL_CONSTRAINT
            // struct inputVertex
            // {
            //     float4 vertex : POSITION;
            //     float3 normal:NORMAL;
            //     float2 uv : TEXCOORD0;
            // };

            struct v2g
            {
                float4 vertex : TEXCOORD0;
                float3 normal:TEXCOORD1;
                float2 uv : TEXCOORD2;
            };



            struct g2f
            {
                float4 vertex : SV_POSITION;
                float2 uv:TEXCOORD0;
                float3 normal:TEXCOORD1;
            };




            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _SizeW,_SizeH;
            float _Angle;

            float4 _Right,_Up;

            float4 _Color;


            float3x3 Camera_matrix(float3 origin, float3 target)
            {
                float3 forward = normalize(target - origin);
                float3 right = cross(forward,float3(0,1,0));
                float3 up = cross(right,forward);

                return float3x3(-right,up,forward);
            }

            v2g vert (float4 positionOS:POSITION ,float3 normalOS:NORMAL,float2 uv:TEXCOORD0)
            {
                v2g g = (v2g)0;
                //g.vertex =TransformObjectToHClip(v.vertex);
                g.vertex.xyz =TransformObjectToWorld(positionOS.xyz);
                g.normal = TransformObjectToWorldNormal(normalOS);

                // g.vertex= positionOS;
                // g.normal = normalOS;
                g.uv = uv;
                return g;
            }

            //  v2g vert (float4 positionOS:POSITION ,float3 normalOS:NORMAL,float2 uv:TEXCOORD0)
            // {
            //     v2g g = (v2g)0;
            //     g.vertex =TransformObjectToHClip(positionOS.xyz);
                
            //     g.normal = TransformObjectToWorldNormal(normalOS);

            //     g.uv = uv;
            //     return g;
            // }



            // [maxvertexcount(9)]
            // void geom (triangle v2g input[3], inout TriangleStream<g2f> outStream)
            // {

            //     g2f o[4];

            //     v2g p[4];

            //     float3 faceNormal =normalize( cross(input[1].vertex.xyz - input[0].vertex.xyz, input[2].vertex.xyz - input[0].vertex.xyz));

            //     float3 centerPos = (input[0].vertex.xyz + input[1].vertex.xyz + input[2].vertex.xyz) /3.0;

            //     centerPos += faceNormal *0.1;

            //     p[0].vertex= input[0].vertex;
            //     p[1].vertex= input[1].vertex;
            //     p[2].vertex= input[2].vertex;
            //     p[3].vertex= float4(centerPos,1.0);

            //     p[0].uv=  input[0].uv;
            //     p[1].uv=  input[1].uv;
            //     p[2].uv= input[2].uv;
            //     p[3].uv=  (input[0].uv + input[1].uv + input[2].uv) /3.0;


            //     for (int i = 0; i < 4; i++)
            //     {
                    
            //         //o[i].vertex =TransformObjectToHClip(input[i].vertex.xyz);

                   


            //         o[i].vertex =TransformWorldToHClip(p[i].vertex.xyz);
                    

            //         //o.vertex =input[i].vertex;
            //         o[i].uv = p[i].uv;

                   
            //     }

            //     outStream.Append(o[0]);
            //     outStream.Append(o[1]);
            //     outStream.Append(o[3]);
            //     outStream.RestartStrip();

            //     outStream.Append(o[0]);
            //     outStream.Append(o[3]);
            //     outStream.Append(o[2]);
            //     outStream.RestartStrip();

            //     outStream.Append(o[1]);
            //     outStream.Append(o[2]);
            //     outStream.Append(o[3]);
            //     outStream.RestartStrip();


            // }

            float2x2 Rot (float a)
            {
                float c = cos(a);
                float s = sin(a);
                return float2x2(c, -s, s, c);
            }

            [maxvertexcount(6)]
            void geom (point v2g input[1], uint id :SV_PrimitiveID, inout TriangleStream<g2f> outStream)
            {

                g2f o[4]; o[0]=(g2f)0; o[1]=(g2f)0; o[2]=(g2f)0; o[3]=(g2f)0;
                v2g p[4]; 
                float g=9.8;
                float t =0;

                


                float3 random =hash13((float)id);
                float3 centerPos =input[0].vertex.xyz+random;
                float size =0.1;

                float3 ro =_WorldSpaceCameraPos;
                float3 target =centerPos;
                float3 forward = normalize(target - ro);
                float3 right = cross(forward,float3(0,1,0));
                float3 up = cross(forward,right);

                float3 Q1= 0;
                float3 Q2= 0;
                float3 Q3= 0;
                float3 Q4= 0;
                float3 X=0;
                float3 Y=0;

#if defined(_VERTICAL_CONSTRAINT)
                X = (_SizeW*0.5*cos(_Angle))*right+(_SizeW*0.5*sin(_Angle))*up;
                Y = (-_SizeH*0.5*sin(_Angle))*right+(_SizeH*0.5*cos(_Angle))*up;

                Q1= centerPos+X+Y;
                Q2= centerPos-X+Y;
                Q3= centerPos-X-Y;
                Q4= centerPos+X-Y;
#else
                X =ro-centerPos;
                X.xz =mul(Rot(0.5*PI),X.xz);
                X.y =0;
                //X = float3(target.z-ro.z,0,ro.x-target.x);
                X = normalize(X);
                Y =float3(0,_SizeH*0.5,0);
                Q1= centerPos+_SizeW*0.5*X+Y;
                Q2= centerPos-_SizeW*0.5*X+Y;
                Q3= centerPos-_SizeW*0.5*X-Y;
                Q4= centerPos+_SizeW*0.5*X-Y;

#endif

                // t= 0.5*g*_Time.y*_Time.y;
                // centerPos+=float3(0,-1,0)*t;

                // t = _Time.x+hash11((float)id);
                // float ani = 2*abs(2*(t-floor(t+0.5)))-1.0;

                // centerPos+=float3(0,ani,0);
              

                p[0].vertex.xyz = Q1;
                p[1].vertex.xyz = Q2;
                p[2].vertex.xyz = Q3;
                p[3].vertex.xyz = Q4;
#if defined(_VERTICAL_CONSTRAINT)
                p[0].uv = float2(0,0);
                p[1].uv = float2(1,0);
                p[2].uv = float2(1,1);
                p[3].uv = float2(0,1);
#else
                p[0].uv = float2(1,1);
                p[1].uv = float2(0,1);
                p[2].uv = float2(0,0);
                p[3].uv = float2(1,0);
#endif


                p[0].normal = input[0].normal;
                p[1].normal = input[0].normal;
                p[2].normal = input[0].normal;
                p[3].normal = input[0].normal;

                for (int i = 0; i < 4; i++)
                {

                    o[i].vertex =TransformWorldToHClip(p[i].vertex.xyz);
                    o[i].uv = p[i].uv;
                }

                    outStream.Append(o[0]);
                    outStream.Append(o[3]);
                    outStream.Append(o[2]);
                    outStream.RestartStrip();
                    outStream.Append(o[0]);
                    outStream.Append(o[2]);
                    outStream.Append(o[1]);
                    outStream.RestartStrip();

            }

            //  [maxvertexcount(2)]
            // void geom (line v2g input[2], uint id :SV_PrimitiveID, inout LineStream<g2f> outStream)
            // {

            //     g2f o[2];
        
            //     //o.vertex =TransformWorldToHClip(input[0].vertex.xyz);
            //     o[0].vertex =input[0].vertex;
            //     o[0].uv =input[0].uv;

            //     o[1].vertex =input[1].vertex;
            //     o[1].uv =input[1].uv;

            //     outStream.Append(o[0]);
            //     outStream.Append(o[1]);
            //     outStream.RestartStrip();

            // }

            // [maxvertexcount(1)]
            // void geom (point v2g input[1], uint id :SV_PrimitiveID, inout PointStream<g2f> outStream)
            // {

            //     g2f o;
        
            //     //o.vertex =TransformWorldToHClip(input[0].vertex.xyz);
            //     o.vertex =input[0].vertex;
            //     o.uv =input[0].uv;

            //     // o[1].vertex =input[1].vertex;
            //     // o[1].uv =input[1].uv;

            //     // outStream.Append(o[0]);
            //     outStream.Append(o);

            // }
             

            float4 frag (g2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv)*_Color;

                Light light = GetMainLight();
                float3 lightDir =-light.direction;
                float3 lightColor = light.color;  
                //float  lightShadowAtten =light.shadowAttenuation;

                float diffuse = max(0,dot(i.normal,lightDir));


              
                return col;
            }
            ENDHLSL
            
        }
    }
}
