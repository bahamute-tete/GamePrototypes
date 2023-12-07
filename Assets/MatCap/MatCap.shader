Shader "Unlit/MatCap"
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
            // make fog work
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal:NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                //输出视角法线
                float3	viewNormal : TEXCOORD0;
                //输出视角空间顶点
                float3	viewPos : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.viewPos=TransformWorldToView( TransformObjectToWorld(v.vertex));
                o.viewNormal =((mul(UNITY_MATRIX_IT_MV, float4(v.normal,1.0)).xyz));
               

                //float3 eyeDir =float3(0,0,1);
                //float3 refView = normalize(reflect(eyeDir,vNormal));
                //o.matUV.xyz =normalize(refView+eyeDir);


                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // sample the texture
               
                float3 newViewNormal =  normalize(i.viewNormal)*0.5+0.5;
                half4 col = tex2D(_MainTex, newViewNormal.xy);
                // apply fog
              
                return col;
            }
            ENDHLSL
        }
    }
}
