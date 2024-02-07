Shader "Unlit/ScreenBlurShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Spread("Standard Deviation (Spread)", Float) = 1
        _GridSize("Grid Size", int) = 3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        LOD 100
        HLSLINCLUDE
        #pragma shader_feature _ _GAUSSIAN_FILTER

        #define KERNELSIZE 3
        #define PI 3.14159265358979323846


        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

         struct appdata
        {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct v2f
        {
            float2 uv : TEXCOORD0;
            float4 positionCS : SV_POSITION;
        };

        sampler2D _MainTex;

        CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST;
        float4  _MainTex_TexelSize;
        uint _GridSize;
        float _Spread;
        CBUFFER_END

        float CreateGaussianKernel(int x)
        {
            float sigmaSqu = _Spread * _Spread;
            float k =1.0 / sqrt(2*PI * sigmaSqu);
            return k* exp( -(x * x) / (2 * sigmaSqu));
        }

        void CreateGaussianKernal2(float sig, out float kernel[KERNELSIZE],out float sum)
        {
            int m = ceil(sig*6+0.5);
            int mid = (m - 1) / 2;

            float K = 1. / (2. * PI * pow(sig, 2));
            //float K =1;

            for (int s = 0; s < m; s++)
            {
                for (int t = 0; t < m; t++)
                {
                    float squareR = pow(s - mid, 2) + pow(t - mid, 2);
                    float index = -squareR / (2.0 * pow(sig, 2));

                    if (t==mid) 
                    {
                        kernel[s] = K * exp(index);
                        sum += K * exp(index);
                        
                    }
                }
            }
        }




         v2f vert (appdata v)
        {
            v2f o;
            o.positionCS = TransformObjectToHClip(v.vertex.xyz);
            o.uv = TRANSFORM_TEX(v.uv, _MainTex);
            
            return o;
        }



        ENDHLSL

        Pass
        {
            Name "Vertical"

            Tags{"LightMode" = "UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
        
            float4 frag (v2f i) : SV_Target
            {
                float3 col = float3(0.0f, 0.0f, 0.0f);
                float gridSum = 0.0f;

                int upper = ((_GridSize - 1) / 2);
                int lower = -upper;

                for (int x = lower; x < upper; x++)
                {

                    #if defined(_GAUSSIAN_FILTER)
                    float kernel = CreateGaussianKernel(x);
                    #else
                    float kernel = 1.0;
                    #endif
                    gridSum += kernel;
                    float2 uv = i.uv + float2(_MainTex_TexelSize.x * x, 0.0f);
                    col += kernel * tex2D(_MainTex, uv).xyz;
                }

                col /= gridSum;
                return float4(col, 1.0f);
            }
            ENDHLSL
        }

        
        Pass
        {
            Name "Horizontal"
            Tags{"LightMode" = "UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
        
            float4 frag (v2f i) : SV_Target
            {
                float3 col = float3(0.0f, 0.0f, 0.0f);
                float gridSum = 0.0f;

                int upper = ((_GridSize - 1) / 2);
                int lower = -upper;

                for (int y = lower; y <upper; y++)
                {

                    #if defined(_GAUSSIAN_FILTER)
                    float kernel = CreateGaussianKernel(y);
                    #else
                    float kernel = 1.0;
                    #endif
                    gridSum += kernel;
                    float2 uv = i.uv + float2(0.0f, _MainTex_TexelSize.y * y);
                    col += kernel * tex2D(_MainTex, uv).xyz;
                }

                col /= gridSum;
                return float4(col, 1.0f);
            }
            ENDHLSL
        }
    }
}
