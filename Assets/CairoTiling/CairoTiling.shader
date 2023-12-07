Shader "Unlit/CairoTiling"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Angle("Angle",Range(0.5,1))=0
        _LineWidth("LineWidth",Float)=0.01
        _Scale("Scale",Float)=1
        _Degrees("Degrees",Range(0,360))=0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        
        ZTest On 

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag


            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define PI 3.14159265359
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            TEXTURE2D(_MainTex);
            float4 _MainTex_ST;

            CBUFFER_START(UnityPerMaterial)
            SAMPLER(sampler_MainTex);
            float _Angle,_LineWidth,_Scale,_Degrees;

            CBUFFER_END

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // sample the texture
                half4 col = 0;
                float2 uv = (i.uv-0.5);
                uv*= _Scale;

                float2 id = abs(floor(uv));
                float check = fmod(id.x+id.y,2);
                uv.xy = (check==1)? uv.yx:uv.xy;

                uv= frac(uv)-0.5;

                uv = abs(uv);
                float a = (_Angle*0.5+.5)*PI;
                float b =radians(_Degrees);
                float2 n= float2(sin(a),cos(a));

                float d =0;
               
                d = dot(uv-0.5,n);
                d=min(d, uv.x);
                d = max(d,-uv.y);
                d =min(d, dot(uv-0.5,float2(n.y,-n.x)));
                
                d= abs(d);
                
               

                
                col +=smoothstep(fwidth(d),0,d-_LineWidth);
                col +=d;

                //if (max(uv.x,uv.y)>0.49) col =float4(1,0,0,1);

                
                return col;
            }
            ENDHLSL
        }
    }
}
