Shader "Unlit/warping2"
{
    Properties
    {
        _MainTex ("Noise Texture", 2D) = "white" {}
        _Speed ("Animation Speed", Float) = 1.0
        _NoiseScale ("Noise Scale", Float) = 1.0
        _Intensity ("Effect Intensity", Float) = 1.0
        _AlphaFalloff ("Alpha Falloff", Float) = 2.0
        _Color1 ("Base Color", Color) = (0.2, 0.1, 0.4, 1)
        _Color2 ("Mix Color", Color) = (0.3, 0.05, 0.05, 1)
        _Color3 ("Highlight Color", Color) = (0.9, 0.9, 0.9, 1)
        _Color4 ("Mid Color", Color) = (0.5, 0.2, 0.2, 1)
        _Color5 ("Edge Color", Color) = (0.0, 0.2, 0.4, 1)
    }
    SubShader
    {
        Tags { 
            "RenderType"="Transparent" 
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
        }
        LOD 100
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Speed;
                float _NoiseScale;
                float _Intensity;
                float _AlphaFalloff;
                float4 _Color1;
                float4 _Color2;
                float4 _Color3;
                float4 _Color4;
                float4 _Color5;
            CBUFFER_END

            static const float2x2 mtx = float2x2(0.80, 0.60, -0.60, 0.80);

            float hash(float2 p)
            {
                p = frac(p * 0.6180339887);
                p *= 25.0;
                return frac(p.x * p.y * (p.x + p.y));
            }

            float noise(float2 x)
            {
                float2 p = floor(x);
                float2 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(p + float2(0, 0));
                float b = hash(p + float2(1, 0));
                float c = hash(p + float2(0, 1));
                float d = hash(p + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm4(float2 p)
            {
                float f = 0.0;
                f += 0.5000 * (-1.0 + 2.0 * noise(p)); p = mul(mtx, p) * 2.02;
                f += 0.2500 * (-1.0 + 2.0 * noise(p)); p = mul(mtx, p) * 2.03;
                f += 0.1250 * (-1.0 + 2.0 * noise(p)); p = mul(mtx, p) * 2.01;
                f += 0.0625 * (-1.0 + 2.0 * noise(p));
                return f / 0.9375;
            }

            float fbm6(float2 p)
            {
                float f = 0.0;
                f += 0.500000 * noise(p); p = mul(mtx, p) * 2.02;
                f += 0.250000 * noise(p); p = mul(mtx, p) * 2.03;
                f += 0.125000 * noise(p); p = mul(mtx, p) * 2.01;
                f += 0.062500 * noise(p); p = mul(mtx, p) * 2.04;
                f += 0.031250 * noise(p); p = mul(mtx, p) * 2.01;
                f += 0.015625 * noise(p);
                return f / 0.96875;
            }

            float2 fbm4_2(float2 p)
            {
                return float2(fbm4(p + float2(1.0, 0)), fbm4(p + float2(6.2, 0)));
            }

            float2 fbm6_2(float2 p)
            {
                return float2(fbm6(p + float2(9.2, 0)), fbm6(p + float2(5.7, 0)));
            }

            float func(float2 q, out float2 o, out float2 n)
            {
                float time = _Time.y * _Speed;
                q += 0.05 * sin(float2(0.11, 0.13) * time + length(q) * 4.0);
                
                q *= 0.7 + 0.2 * cos(0.05 * time);

                o = 0.5 + 0.5 * fbm4_2(q);
                
                o += 0.02 * sin(float2(0.13, 0.11) * time * length(o));

                n = fbm6_2(4.0 * o);

                float2 p = q + 2.0 * n + 1.0;

                float f = 0.5 + 0.5 * fbm4(2.0 * p);

                f = lerp(f, f * f * f * 3.5, f * abs(n.x));

                f *= 1.0 - 0.5 * pow(0.5 + 0.5 * sin(8.0 * p.x) * sin(8.0 * p.y), 8.0);

                return f;
            }

            float funcs(float2 q)
            {
                float2 t1, t2;
                return func(q, t1, t2);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // 移除UV限制，允许重复
                float2 q = i.uv * _NoiseScale;

                float2 o, n;
                float f = func(q, o, n);
                
                // Color mixing based on original algorithm
                float3 col = _Color1.rgb;
                // col = lerp(col, _Color2.rgb, f);
                // col = lerp(col, _Color3.rgb, dot(n, n));
                // col = lerp(col, _Color4.rgb, 0.5 * o.y * o.y);
                // col = lerp(col, _Color5.rgb, 0.5 * smoothstep(1.2, 1.3, abs(n.y) + abs(n.x)));
                col *= f * 2.0;

                // Calculate normals for lighting
                float ex = 1.0 / 512.0;
                float ey = 1.0 / 512.0;
                float3 nor = normalize(float3(
                    funcs(q + float2(ex, 0)) - f,
                    ex,
                    funcs(q + float2(0, ey)) - f
                ));
                
                // Lighting calculation
                float3 lig = normalize(float3(0.9, -0.2, -0.4));
                float dif = saturate(0.3 + 0.7 * dot(nor, lig));

                float3 lin = float3(0.85, 0.90, 0.95) * (nor.y * 0.5 + 0.5);
                lin += float3(0.15, 0.10, 0.05) * dif;

                col *= lin;
                col = float3(1.0, 1.0, 1.0) - col;
                col = col * col;
                col *= float3(1.2, 1.25, 1.2);
                col *= _Intensity;

                // Vignette effect - 使用frac确保UV在0-1范围内进行渐晕计算
                float2 p = frac(i.uv);
                col *= 0.5 + 0.5 * sqrt(16.0 * p.x * p.y * (1.0 - p.x) * (1.0 - p.y));

                // Calculate alpha based on U coordinate - 使用frac处理重复的UV
                float distFromCenter = abs(frac(i.uv.x) - 0.5) * 2.0;
                float alpha = pow(1.0 - distFromCenter, _AlphaFalloff);

                return float4(col, alpha);
            }
            ENDHLSL
        }
    }
}



