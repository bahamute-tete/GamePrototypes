Shader "Unlit/warping1"
{
    Properties
    {
        _MainTex ("Noise Texture", 2D) = "white" {}
        _Speed ("Animation Speed", Float) = 1.0
        _NoiseScale ("Noise Scale", Float) = 1.0
        _Intensity ("Effect Intensity", Float) = 1.0
        _AlphaFalloff ("Alpha Falloff", Float) = 2.0
        _Color1 ("Base Color 1", Color) = (0.2, 0.1, 0.4, 1)
        _Color2 ("Base Color 2", Color) = (0.3, 0.05, 0.05, 1)
        _Color3 ("Highlight Color", Color) = (0.9, 0.9, 0.9, 1)
        _Color4 ("Mid Color", Color) = (0.4, 0.3, 0.3, 1)
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

            static const float2x2 m = float2x2(0.80, 0.60, -0.60, 0.80);

            float noise(float2 p)
            {
                return sin(p.x) * sin(p.y);
            }

            float fbm4(float2 p)
            {
                float f = 0.0;
                f += 0.5000 * noise(p); p = mul(m, p) * 2.02;
                f += 0.2500 * noise(p); p = mul(m, p) * 2.03;
                f += 0.1250 * noise(p); p = mul(m, p) * 2.01;
                f += 0.0625 * noise(p);
                return f / 0.9375;
            }

            float fbm6(float2 p)
            {
                float f = 0.0;
                f += 0.500000 * (0.5 + 0.5 * noise(p)); p = mul(m, p) * 2.02;
                f += 0.250000 * (0.5 + 0.5 * noise(p)); p = mul(m, p) * 2.03;
                f += 0.125000 * (0.5 + 0.5 * noise(p)); p = mul(m, p) * 2.01;
                f += 0.062500 * (0.5 + 0.5 * noise(p)); p = mul(m, p) * 2.04;
                f += 0.031250 * (0.5 + 0.5 * noise(p)); p = mul(m, p) * 2.01;
                f += 0.015625 * (0.5 + 0.5 * noise(p));
                return f / 0.96875;
            }

            float2 fbm4_2(float2 p)
            {
                return float2(fbm4(p), fbm4(p + float2(7.8, 0)));
            }

            float2 fbm6_2(float2 p)
            {
                return float2(fbm6(p + float2(16.8, 0)), fbm6(p + float2(11.5, 0)));
            }

            float func(float2 q, out float4 ron)
            {
                float time = _Time.y * _Speed;
                q += 0.03 * sin(float2(0.27, 0.23) * time + length(q) * float2(4.1, 4.3));

                float2 o = fbm4_2(0.9 * q);

                o += 0.04 * sin(float2(0.12, 0.14) * time + length(o));

                float2 n = fbm6_2(3.0 * o);

                ron = float4(o, n);

                float f = 0.5 + 0.5 * fbm4(1.8 * q + 6.0 * n);

                return lerp(f, f * f * f * 3.5, f * abs(n.x));
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
                // Convert UV to centered coordinates similar to Shadertoy
                float2 p = (2.0 * i.uv - 1.0) * _NoiseScale;
                float e = 2.0 / 512.0; // Approximate derivative step

                float4 on = float4(0.0, 0.0, 0.0, 0.0);
                float f = func(p, on);

                // Color mixing based on the original algorithm
                float3 col = float3(0.0, 0.0, 0.0);
                col = lerp(_Color1.rgb, _Color2.rgb, f);
                col = lerp(col, _Color3.rgb, dot(on.zw, on.zw));
                col = lerp(col, _Color4.rgb, 0.2 + 0.5 * on.y * on.y);
                col = lerp(col, _Color5.rgb, 0.5 * smoothstep(1.2, 1.3, abs(on.z) + abs(on.w)));
                col = saturate(col * f * 2.0);

                // Manual derivatives for normal calculation
                float4 kk;
                float3 nor = normalize(float3(
                    func(p + float2(e, 0.0), kk) - f,
                    2.0 * e,
                    func(p + float2(0.0, e), kk) - f
                ));

                // Lighting calculation
                float3 lig = normalize(float3(0.9, 0.2, -0.4));
                float dif = saturate(0.3 + 0.7 * dot(nor, lig));
                float3 lin = float3(0.70, 0.90, 0.95) * (nor.y * 0.5 + 0.5) + float3(0.15, 0.10, 0.05) * dif;
                col *= 1.2 * lin;

                // Final color processing
                col = 1.0 - col;
                col = 1.1 * col * col;
                col *= _Intensity;

                // Calculate alpha based on U coordinate
                // UV.x = 0 or 1: alpha = 0 (transparent)
                // UV.x = 0.5: alpha = 1 (opaque)
                float distFromCenter = abs(i.uv.x - 0.5) * 2.0; // 0 at center, 1 at edges
                float alpha = pow(1.0 - distFromCenter, _AlphaFalloff);

                return float4(col, alpha);
            }
            ENDHLSL
        }
    }
}



