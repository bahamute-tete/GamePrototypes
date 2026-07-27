Shader "Hidden/SphereFog"
{
    Properties
    {
        _NoiseTex ("Noise Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "SphereFog"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _USE_DRAW_PROCEDURAL

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "SphereFogInclude.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float  _FogShape;        // 0 = Sphere, 1 = Box
                float3 _FogCenter;       // 世界空间中心

                float  _SphereRadius;    // 球：世界空间半径

                float4 _BoxAxisX;        // xyz = 归一化右轴, w = 世界半长X
                float4 _BoxAxisY;        // xyz = 归一化上轴, w = 世界半长Y
                float4 _BoxAxisZ;        // xyz = 归一化前轴, w = 世界半长Z

                float  _Smoothness;
                float  _Density;
                float4 _FogColor;
                float  _NoiseScale;
                float  _NoiseStrength;
                float3 _NoiseSpeed;
                float  _SkyFogAmount;
            CBUFFER_END

            // ---------- SDF ----------

            // 球体 SDF：负数在内，正数在外
            float sdSphere(float3 p, float3 center, float r)
            {
                return length(p - center) - r;
            }

            // 定向包围盒 OBB SDF（全世界空间，无需矩阵变换）
            float sdOBB(float3 p, float3 center,
                        float3 axX, float halfX,
                        float3 axY, float halfY,
                        float3 axZ, float halfZ)
            {
                float3 d = p - center;
                // 投影到盒体的三个局部轴
                float3 local = float3(dot(d, axX), dot(d, axY), dot(d, axZ));
                float3 half3 = float3(halfX, halfY, halfZ);
                float3 q = abs(local) - half3;
                return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
            }

            float ComputeSDF(float3 worldPos)
            {
                // 同一 drawcall 所有像素走相同分支，GPU 不会有 divergence 问题
                if (_FogShape < 0.5)
                {
                    return sdSphere(worldPos, _FogCenter, _SphereRadius);
                }
                else
                {
                    return sdOBB(worldPos, _FogCenter,
                                 _BoxAxisX.xyz, _BoxAxisX.w,
                                 _BoxAxisY.xyz, _BoxAxisY.w,
                                 _BoxAxisZ.xyz, _BoxAxisZ.w);
                }
            }

            // ---------- Noise ----------

            float SampleTriplanarNoise(float3 pos, float3 timeOffset)
            {
                float3 p = pos * _NoiseScale + timeOffset;
                float nx = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, p.yz).r;
                float ny = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, p.xz).r;
                float nz = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, p.xy).r;

                float3 w = abs(normalize(pos + 1e-5));
                w /= (w.x + w.y + w.z);
                return nx * w.x + ny * w.y + nz * w.z;
            }

            // ---------- Fragment ----------

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float rawDepth  = SampleSceneDepth(uv);
                float3 worldPos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                bool isSky      = rawDepth < 1e-4;

                // SDF：负=体积内(清晰)，正=体积外(起雾)
                float sdf = ComputeSDF(worldPos);

                // 噪声扰动 SDF 边界
                float3 dir = normalize(worldPos - _FogCenter + 1e-5);
                float  n   = SampleTriplanarNoise(dir * 5.0, _Time.y * _NoiseSpeed);
                sdf -= (n - 0.5) * 2.0 * _NoiseStrength;

                // 平滑过渡：sdf 从 -s/2 到 +s/2 之间线性过渡
                float halfS = max(_Smoothness * 0.5, 1e-4);
                float fog   = smoothstep(-halfS, halfS, sdf);
                fog        *= _Density;

                fog = isSky ? _SkyFogAmount * _Density : fog;

                return half4(lerp(sceneColor.rgb, _FogColor.rgb, saturate(fog)), sceneColor.a);
            }
            ENDHLSL
        }
    }
}