Shader "Unlit/warping"
{
    Properties
    {
        _MainTex ("Noise Texture", 2D) = "white" {}
        _Speed ("Animation Speed", Float) = 1.0
        _NoiseScale ("Noise Scale", Float) = 0.004
        _Color1 ("Color 1", Color) = (0.1, 0.4, 0.4, 1)
        _Color2 ("Color 2", Color) = (0.5, 0.7, 0.0, 1)
        _Color3 ("Color 3", Color) = (0.35, 0.0, 0.1, 1)
        _Color4 ("Color 4", Color) = (0, 0.2, 1, 1)
        _Color5 ("Color 5", Color) = (0.3, 0, 0, 1)
        _Color6 ("Color 6", Color) = (0, 0.5, 0, 1)
        _EdgeFadeWidth ("Edge Fade Width", Range(0.01, 0.5)) = 0.1
        _EdgeSoftness ("Edge Softness", Range(0.1, 2.0)) = 1.0
        [Toggle] _UseParallax ("Use Parallax", Float) = 1
        _ParallaxStrength ("Parallax Strength", Range(0.0, 0.2)) = 0.02
        _ParallaxLayers ("Parallax Layers", Range(1, 32)) = 8
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
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 viewDirTangent : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Speed;
                float _NoiseScale;
                float4 _Color1;
                float4 _Color2;
                float4 _Color3;
                float4 _Color4;
                float4 _Color5;
                float4 _Color6;
                float _EdgeFadeWidth;
                float _EdgeSoftness;
                float _UseParallax;
                float _ParallaxStrength;
                int _ParallaxLayers;
            CBUFFER_END

            float2 hash2(float n)
            {
                return frac(sin(float2(n, n + 1.0)) * float2(13.5453123, 31.1459123));
            }

            float noise(float2 x)
            {
                float2 p = floor(x);
                float2 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                
                float2 coord1 = frac((p + float2(0.5, 0.5)) / 256.0);
                float2 coord2 = frac((p + float2(1.5, 0.5)) / 256.0);
                float2 coord3 = frac((p + float2(0.5, 1.5)) / 256.0);
                float2 coord4 = frac((p + float2(1.5, 1.5)) / 256.0);
                
                float a = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, coord1, 0).x;
                float b = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, coord2, 0).x;
                float c = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, coord3, 0).x;
                float d = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, coord4, 0).x;
                
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            static const float2x2 mtx = float2x2(0.80, 0.60, -0.60, 0.80);

            float fbm(float2 p)
            {
                float f = 0.0;
                f += 0.500000 * noise(p); p = mul(mtx, p) * 2.02;
                f += 0.250000 * noise(p); p = mul(mtx, p) * 2.03;
                f += 0.125000 * noise(p); p = mul(mtx, p) * 2.01;
                f += 0.062500 * noise(p); p = mul(mtx, p) * 2.04;
                // f += 0.031250 * noise(p); p = mul(mtx, p) * 2.01;
                //f += 0.015625 * noise(p);
                return f / 0.9375;
            }

            float pattern(float2 p, float t, float2 uv, out float2 q, out float2 r, out float2 g)
            {
                q = float2(fbm(p), fbm(p + float2(10, 1.3)));
                
                float s = dot(uv.x + 0.5, uv.y + 0.5);
                r = float2(
                    fbm(p + 4.0 * q + float2(t, t) + float2(1.7, 9.2)),
                    fbm(p + 4.0 * q + float2(t, t) + float2(8.3, 2.8))
                );
                g = float2(
                    fbm(p + 2.0 * r + float2(t * 20.0, t * 20.0) + float2(2, 6)),
                    fbm(p + 2.0 * r + float2(t * 10.0, t * 10.0) + float2(5, 3))
                );
                return fbm(p + 5.5 * g + float2(-t * 7.0, -t * 7.0));
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                // 计算切线空间的视图方向
                float3 worldPos = TransformObjectToWorld(v.vertex.xyz);
                float3 worldViewDir = GetWorldSpaceViewDir(worldPos);
                
                float3 worldNormal = TransformObjectToWorldNormal(v.normal);
                float3 worldTangent = TransformObjectToWorldDir(v.tangent.xyz);
                float3 worldBinormal = cross(worldNormal, worldTangent) * v.tangent.w;
                
                float3x3 tangentToWorld = float3x3(worldTangent, worldBinormal, worldNormal);
                o.viewDirTangent = mul(tangentToWorld, worldViewDir);
                
                return o;
            }

            float2 ParallaxMapping(float2 texCoords, float3 viewDir, float time)
            {
                const float minLayers = 8.0;
                const float maxLayers = 32.0;
                float numLayers = lerp(maxLayers, minLayers, abs(dot(float3(0, 0, 1), viewDir)));
                numLayers = clamp(numLayers, minLayers, _ParallaxLayers);
                
                float layerDepth = 1.0 / numLayers;
                float currentLayerDepth = 0.0;
                
                float2 P = viewDir.xy * _ParallaxStrength;
                float2 deltaTexCoords = P / numLayers;
                
                float2 currentTexCoords = texCoords;
                float2 q, r, g;
                float currentDepthMapValue = pattern(currentTexCoords / _NoiseScale, time, currentTexCoords, q, r, g);
                
                while(currentLayerDepth < currentDepthMapValue)
                {
                    currentTexCoords -= deltaTexCoords;
                    currentDepthMapValue = pattern(currentTexCoords / _NoiseScale, time, currentTexCoords, q, r, g);
                    currentLayerDepth += layerDepth;
                }
                
                // 线性插值优化
                float2 prevTexCoords = currentTexCoords + deltaTexCoords;
                float afterDepth = currentDepthMapValue - currentLayerDepth;
                float beforeDepth = pattern(prevTexCoords / _NoiseScale, time, prevTexCoords, q, r, g) - currentLayerDepth + layerDepth;
                
                float weight = afterDepth / (afterDepth - beforeDepth);
                float2 finalTexCoords = prevTexCoords * weight + currentTexCoords * (1.0 - weight);
                
                return finalTexCoords;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 viewDir = normalize(i.viewDirTangent);
                float time = _Time.y * _Speed * 0.007;
                
                // 根据开关决定是否使用视差映射
                float2 finalUV = i.uv;
                if(_UseParallax > 0.5)
                {
                    finalUV = ParallaxMapping(i.uv, viewDir, time);
                }
                
                float2 q, r, g;
                float noiseValue = pattern(finalUV / _NoiseScale, time, finalUV, q, r, g);
                
                // Base color based on main noise
                float3 col = lerp(_Color1.rgb, _Color2.rgb, smoothstep(0.0, 1.0, noiseValue));
                
                // Other lower-octave colors and mixes
                col = lerp(col, _Color3.rgb, dot(q, q) * 1.0);
                col = lerp(col, _Color4.rgb, 0.2 * g.y * g.y);
                col = lerp(col, _Color5.rgb, smoothstep(0.0, 0.6, 0.6 * r.g * r.g));
                col = lerp(col, _Color6.rgb, 0.1 * g.x);
                
                // Contrast
                col *= noiseValue * 2.0;
                
                // U方向边缘透明化处理
                float fadeWidth = _EdgeFadeWidth;
                float softness = _EdgeSoftness;
                
                float2 maskUV = frac(finalUV);
                float leftFade = smoothstep(0.0, fadeWidth * softness, maskUV.x);
                float rightFade = smoothstep(1.0, 1.0 - fadeWidth * softness, maskUV.x);
                float uFade = min(leftFade, rightFade);
                
                return float4(col, uFade);
            }
            ENDHLSL
        }
    }
}



