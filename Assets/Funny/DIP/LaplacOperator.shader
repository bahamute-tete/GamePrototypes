Shader "Unlit/LaplacOperator"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _blurStrenth("BlurStrenth",float)=1
        _Spread("Standard Deviation (Spread)", Float) = 1
        _GridSize("Grid Size", int) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZWrite Off

        HLSLINCLUDE
        #define TWO_PI 6.2831853
        #define E 2.718281828
        #define MAXCOUNT 55
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
        float _blurStrenth;
        CBUFFER_END


        v2f vert (appdata v)
        {
            v2f o;
            o.positionCS = TransformObjectToHClip(v.vertex.xyz);
            o.uv = TRANSFORM_TEX(v.uv, _MainTex);
            
            return o;
        }

        float gaussian(int x)
        {
            float sigmaSqu = _Spread * _Spread;
            return (1 / sqrt(TWO_PI * sigmaSqu)) * exp( -(x * x) / (2 * sigmaSqu));
        }

        float4 LaplaceOperator(float2 uv)
        {
            float4 res =0;
            res += tex2D(_MainTex, uv + float2(-1, 0)*_MainTex_TexelSize.xy);
            res += tex2D(_MainTex, uv + float2(1, 0)*_MainTex_TexelSize.xy);
            res += tex2D(_MainTex, uv + float2(0, 1)*_MainTex_TexelSize.xy);
            res += tex2D(_MainTex, uv + float2(0, -1)*_MainTex_TexelSize.xy);

            res += tex2D(_MainTex, uv + float2(-1, -1)*_MainTex_TexelSize.xy);
            res += tex2D(_MainTex, uv + float2(-1, 1)*_MainTex_TexelSize.xy);
            res += tex2D(_MainTex, uv + float2(1, -1)*_MainTex_TexelSize.xy);
            res += tex2D(_MainTex, uv + float2(1, 1)*_MainTex_TexelSize.xy);

            
            res += tex2D(_MainTex, uv )*-8.0;

            return res;
        }

        
        float3x3 GaussianKernal()
        {
            float3x3 guassianKernel ={  { 0.3679, 0.6065, 0.3679 },
                                        { 0.6065, 1.0,    0.6065},
                                        { 0.3679, 0.6065, 0.3679 } };
            guassianKernel*=rcp(4.8976);

            return guassianKernel;
        }

        float4 GaussianBlur(float2 uv,float3x3 kernel , out float4 neighbor[8])
        {

            float4 m11 = tex2D(_MainTex, uv )*kernel._m11;

            float4 m01= tex2D(_MainTex, uv + float2(0, 1)*_MainTex_TexelSize.xy)*kernel._m01;
            float4 m21= tex2D(_MainTex, uv + float2(0, -1)*_MainTex_TexelSize.xy)*kernel._m21;

            float4 m10= tex2D(_MainTex, uv + float2(-1, 0)*_MainTex_TexelSize.xy)*kernel._m10;
            float4 m12= tex2D(_MainTex, uv + float2(1, 0)*_MainTex_TexelSize.xy)*kernel._m12;

                float4 m00= tex2D(_MainTex, uv + float2(-1, 1)*_MainTex_TexelSize.xy)*kernel._m00;
                float4 m20= tex2D(_MainTex, uv + float2(-1, -1)*_MainTex_TexelSize.xy)*kernel._m20;
                float4 m02 = tex2D(_MainTex, uv + float2(1, 1)*_MainTex_TexelSize.xy)*kernel._m02;
                float4 m22 = tex2D(_MainTex, uv + float2(1, -1)*_MainTex_TexelSize.xy)*kernel._m22;

                neighbor[0]=m01;
                neighbor[1]=m21;
                neighbor[2]=m10;
                neighbor[3]=m12;

                neighbor[4]=m00;
                neighbor[5]=m20;
                neighbor[6]=m02;
                neighbor[7]=m22;


             return m11 + m01 + m21 + m10 + m12 + m00 + m20 + m02 + m22;
         }


        void CreateGaussianKernal(float sig, out float kernel[MAXCOUNT],out float sum)
        {
            int m = MAXCOUNT;
            int n = MAXCOUNT;

            int midr = (m - 1) / 2;
            int midc = (n - 1) / 2;
            float K = 1. / (2. * PI * pow(sig, 2));
            //float K =1;

            for (int s = 0; s < m; s++)
            {
                for (int t = 0; t < n; t++)
                {
                    float squareR = pow(s - midr, 2) + pow(t - midc, 2);
                    float index = -squareR / (2.0 * pow(sig, 2));

                    if (t==midc) 
                    {
                        kernel[s] = K * exp(index);
                        sum += K * exp(index);
                        
                    }
                }
            }
        }



        ENDHLSL

/*
        Pass
        {
            Name "VerticalBlur"
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

                for (int x = lower; x <= upper; ++x)
                {
                    float gauss = gaussian(x);
                    gridSum += gauss;
                    float2 uv = i.uv + float2(_MainTex_TexelSize.x * x, 0.0f);
                    col += gauss * tex2D(_MainTex, uv).xyz;
                }

                col /= gridSum;
                return float4(col, 1.0f);
            }
            ENDHLSL
        }


          Pass
        {
            Name "HorizontalBlur"
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

                for (int y = lower; y <= upper; ++y)
                {
                    float gauss = gaussian(y);
                    gridSum += gauss;
                    float2 uv = i.uv + float2(0.0f, _MainTex_TexelSize.y * y);
                    col += gauss * tex2D(_MainTex, uv).xyz;
                }

                col /= gridSum;
                return float4(col, 1.0f);
            }
            ENDHLSL
        }
*/

          Pass
        {
            Name "VerticalBlur"
            Tags{"LightMode" = "UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
        
            float4 frag (v2f i) : SV_Target
            {
                float3 col = float3(0.0, 0.0, 0.0);
                float gridSum = 0.0;

                float kernel[MAXCOUNT];
                CreateGaussianKernal(_Spread,kernel,gridSum);

                int upper = ((MAXCOUNT - 1) / 2);
                int lower = -upper;

                for (int x = lower; x < upper; x++)
                {
                   float2 uv = i.uv + float2(_MainTex_TexelSize.x * x, 0.0);
                   col+= tex2D(_MainTex, uv).rgb * kernel[x+upper];
                }
                col/=gridSum;
                return float4(col, 1.0f);
            }
            ENDHLSL
        }

           Pass
        {
            Name "HorizontalBlur"
            Tags{"LightMode" = "UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
        
            float4 frag (v2f i) : SV_Target
            {
                float3 col = float3(0.0, 0.0, 0.0);
                float gridSum = 0.0;

                float kernel[MAXCOUNT];
                CreateGaussianKernal(_Spread,kernel,gridSum);

                int upper = ((MAXCOUNT - 1) / 2);
                int lower = -upper;

                for (int y = lower; y < upper; y++)
                {
                    float2 uv = i.uv + float2(0.0,y*_MainTex_TexelSize.y);
                    col+= tex2D(_MainTex, uv).rgb * kernel[y+upper];
                }
                col/=gridSum;
                return float4(col, 1.0f);
            }
            ENDHLSL
        }
    }
}
