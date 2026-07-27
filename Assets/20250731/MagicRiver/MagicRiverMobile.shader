// ============================================================================
//  MagicRiverMobile.shader
//
//  Fantasy / magic energy river for mobile VR (Quest 2/3, Pico 4 class).
//  Lineage: shadertoy "tfGczm" (volumetric cos-lattice raymarch) ported to a
//           UV-driven surface shader.
//
//  Engine:        Unity 2022.3 LTS + URP 14.x
//  XR mode:       Single Pass Instanced
//  Mesh setup:    UV unwrap with V along the flow direction.
//                 MESH MUST HAVE TANGENTS (Import settings -> Calculate Tangents).
//
//  -----------------------------------------------------------------------
//  Two material-level dropdowns:
//
//  Parallax Mode:
//      Off       — flat 2D pattern.                ~50 ALU baseline.
//      Single    — single-offset parallax.         ~52 ALU (+5%).
//      Raymarch  — dual-layer pseudo-volumetric.   doubles EvalIntensity cost.
//
//  Lattice Layer Count:
//      L2        — 2 layers (baseline).
//      L3        — 3 layers (+~7 ALU per EvalIntensity call).
//      L4        — 4 layers (+~14 ALU per EvalIntensity call).
//
//  Worst case (L4 + Raymarch) is ~120 ALU/frag — usable but only when the
//  river occupies a modest fraction of screen.
//
//  Runtime keyword switching (C#):
//      material.DisableKeyword("_PARALLAX_OFF");
//      material.EnableKeyword("_PARALLAX_SINGLE");
//      material.DisableKeyword("_LAYERCOUNT_L2");
//      material.EnableKeyword("_LAYERCOUNT_L3");
// ============================================================================

Shader "Custom/LiangZhu/MagicRiverMobile"
{
    Properties
    {
        [KeywordEnum(Off, Single, Raymarch)] _Parallax    ("Parallax Mode",       Float) = 1
        [KeywordEnum(L2, L3, L4)]            _LayerCount  ("Lattice Layer Count", Float) = 0

        [Header(Flow)]
        _UVTiling           ("UV Tiling (X across, Y along flow)", Vector) = (4, 16, 0, 0)
        _FlowSpeed          ("Flow Speed (V per second)",          Float)  = 0.4
        _TurbulenceSpeed    ("Turbulence Speed",                   Float)  = 1.0
        _Stretch            ("Y Stretch",                          Range(0.05, 2.0)) = 0.4

        [Header(Pattern)]
        _Thickness          ("Lattice Thickness",                  Range(0.0, 1.0))   = 0.2
        _Softness           ("Lattice Softness",                   Range(0.001, 0.5)) = 0.15
        _Bias               ("Min Bias",                           Range(0.001, 0.1)) = 0.01
        _LayerScale         ("Per-Layer Frequency Multiplier",     Range(1.0, 3.0))   = 1.7
        _LayerOffset        ("Per-Layer Phase Increment",          Range(0.0, 6.28))  = 2.3

        [Header(Parallax)]
        _ParallaxDepth      ("Parallax Depth",                     Range(0.0, 2.0)) = 0.4
        _BackLayerDepth     ("Back Layer Z Offset (Raymarch only)",Range(0.0, 5.0)) = 1.5
        _BackLayerStrength  ("Back Layer Strength (Raymarch only)",Range(0.0, 1.0)) = 0.6

        [Header(Color)]
        [HDR] _ColorA       ("Color A",                            Color)  = (0.000, 1.000, 0.984, 1)
        [HDR] _ColorB       ("Color B",                            Color)  = (0.733, 0.961, 0.239, 1)
        _GradientAxis       ("Gradient Axis (0 = U / 1 = V)",      Range(0, 1)) = 0
        _Brightness         ("Brightness",                         Range(0.0, 20.0)) = 4.0

        [Header(Output)]
        _EdgeFade           ("UV Edge Fade Width",                 Range(0.001, 0.5)) = 0.05
        _AlphaScale         ("Alpha Scale",                        Range(0.0, 2.0)) = 1.0
        _AlphaFloor         ("Alpha Floor",                        Range(0.0, 1.0)) = 0.15

        _AllFade            ("All Fade",                           Range(0.0, 1.0)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
        }
        LOD 200

        Pass
        {
            Name "MagicRiverForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // VR Single Pass Instanced
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON

            // Material-level dropdowns (multi_compile_local so runtime C# switching works)
            #pragma multi_compile_local _PARALLAX_OFF _PARALLAX_SINGLE _PARALLAX_RAYMARCH
            #pragma multi_compile_local _LAYERCOUNT_L2 _LAYERCOUNT_L3 _LAYERCOUNT_L4

            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ---------------------------------------------------------------
            //  Tunables
            // ---------------------------------------------------------------
            // 2..4. 3 is Quest 2 / Pico 4 sweet spot.
            #define TURB_OCTAVES 3

            // sin(0.4) / cos(0.4) — rotation angle inside turbulence() is fixed.
            #define R04_S 0.389418
            #define R04_C 0.921061

            // Lattice layer count resolved from keyword to a compile-time constant
            // so the unroll fully expands. Without this the [unroll] would have to
            // either fall back to dynamic looping or fail to compile on some targets.
            #if defined(_LAYERCOUNT_L4)
                #define LATTICE_LAYERS 4
            #elif defined(_LAYERCOUNT_L3)
                #define LATTICE_LAYERS 3
            #else
                #define LATTICE_LAYERS 2
            #endif

            // ---------------------------------------------------------------
            //  Material constants
            // ---------------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4 _UVTiling;
                float  _FlowSpeed;
                float  _TurbulenceSpeed;
                half   _Stretch;

                half   _Thickness;
                half   _Softness;
                half   _Bias;
                half   _LayerScale;
                half   _LayerOffset;

                half   _ParallaxDepth;
                half   _BackLayerDepth;
                half   _BackLayerStrength;

                half4  _ColorA;
                half4  _ColorB;
                half   _GradientAxis;
                half   _Brightness;

                half   _EdgeFade;
                half   _AlphaScale;
                half   _AlphaFloor;

                half   _AllFade;
            CBUFFER_END

            // ---------------------------------------------------------------
            //  Vertex IO
            // ---------------------------------------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 qSeed       : TEXCOORD1;
                float3 viewDirTS   : TEXCOORD2;  // populated always; consumed only by parallax keyword
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ---------------------------------------------------------------
            //  Helpers
            // ---------------------------------------------------------------

            // Compile-time-constant 0.4-radian rotation.
            float2 Rotate04(float2 v)
            {
                return float2(R04_C * v.x - R04_S * v.y,
                              R04_S * v.x + R04_C * v.y);
            }

            // 3-octave turbulence domain warp. p stays fp32 — accumulated phase
            // needs precision or you'll see banding/snapping on mobile GPUs.
            float3 Turbulence(float3 p, float t)
            {
                float freq = 1.0;
                float amp  = 1.0;

                [unroll]
                for (int i = 0; i < TURB_OCTAVES; ++i)
                {
                    p.xz  = Rotate04(p.xz);
                    p    += cos(p.zxy * freq - t * (float)i * 2.0) * amp;
                    freq *= 2.0;
                    amp  *= 0.5;
                }
                return p;
            }

            // N-layer lattice intensity. N is a compile-time constant per variant.
            // Each layer is the base cos lattice at a progressively higher frequency
            // and shifted phase. min() over layers means every layer's bright lines
            // show through — visual richness, not just brightness.
            half EvalIntensity(float3 qSeed, float t)
            {
                float3 q = Turbulence(qSeed, t);

                // Layer 0 — base lattice.
                half d = length((half2)cos(q.xz)) - _Thickness;

                // Layers 1..N-1 — progressive frequency and phase shift.
                float scale  = 1.0;
                float offset = 0.0;

                [unroll]
                for (int i = 1; i < LATTICE_LAYERS; ++i)
                {
                    scale  *= _LayerScale;
                    offset += _LayerOffset;

                    half2 c  = (half2)cos(q.xz * scale + offset);
                    half  di = length(c) - _Thickness;
                    d = min(d, di);
                }

                // abs-trick: |SDF| reads as soft glow instead of a hard edge.
                d = abs(d) * _Softness + _Bias;
                return rcp(d);
            }

            // ---------------------------------------------------------------
            //  Vertex
            // ---------------------------------------------------------------
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInput = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInput.positionCS;
                OUT.uv          = IN.uv;

                // UV pre-tiling and time-shifting — moved out of fragment.
                float flowT = _Time.y * _FlowSpeed;
                OUT.qSeed.x = IN.uv.x * _UVTiling.x;
                OUT.qSeed.y = (IN.uv.y * _UVTiling.y + flowT) * _Stretch;
                OUT.qSeed.z = 0.0;

                // Tangent-space view direction. Always computed regardless of
                // parallax mode — keeps mesh attributes / Varyings stable across
                // keyword variants. Cost is ~10 ALU/vertex, effectively free.
                VertexNormalInputs nrmInput = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                float3 viewDirWS = GetWorldSpaceViewDir(posInput.positionWS);
                OUT.viewDirTS = normalize(float3(
                    dot(nrmInput.tangentWS,   viewDirWS),
                    dot(nrmInput.bitangentWS, viewDirWS),
                    dot(nrmInput.normalWS,    viewDirWS)
                ));

                return OUT;
            }

            // ---------------------------------------------------------------
            //  Fragment
            // ---------------------------------------------------------------
            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // --- parallax offset (zero in OFF mode) -------------------
                #if defined(_PARALLAX_OFF)
                    float2 pOffset = float2(0.0, 0.0);
                #else
                    float2 pOffset = IN.viewDirTS.xy * _ParallaxDepth;
                #endif

                // --- near layer (always evaluated) ------------------------
                float3 qFront = IN.qSeed;
                qFront.x += pOffset.x;
                qFront.z += pOffset.y;

                float turbT    = _Time.y * _TurbulenceSpeed;
                half  intensity = EvalIntensity(qFront, turbT);

                // --- back layer (Raymarch mode only) ----------------------
                // Stronger parallax on this layer + a fixed Z-shift gives the
                // visual "depth" cue. Time is also slightly offset so the two
                // layers desynchronize and read as separate physical strata.
                #if defined(_PARALLAX_RAYMARCH)
                    float3 qBack = IN.qSeed;
                    qBack.x += pOffset.x * 2.0;
                    qBack.z += pOffset.y * 2.0 + _BackLayerDepth;
                    half intensityBack = EvalIntensity(qBack, turbT + 0.7);
                    intensity += intensityBack * _BackLayerStrength;
                #endif

                // --- color gradient ---------------------------------------
                half  g   = lerp((half)IN.uv.x, (half)IN.uv.y, _GradientAxis);
                half3 col = lerp(_ColorA.rgb, _ColorB.rgb, saturate(g));
                col *= intensity * _Brightness * (half)0.01;

                // --- Reinhard tonemap -------------------------------------
                col = col * rcp(col + (half)1.0);

                // --- edge fade (linear ramps, no smoothstep) --------------
                half invEdge = rcp(max(_EdgeFade, (half)1e-4));
                half fadeU = saturate(IN.uv.x * invEdge) *
                             saturate(((half)1.0 - IN.uv.x) * invEdge);
                half fadeV = saturate(IN.uv.y * invEdge) *
                             saturate(((half)1.0 - IN.uv.y) * invEdge);
                half edge  = fadeU * fadeV;

                // --- alpha -----------------------------------------------
                half alphaCore = saturate(intensity * (half)0.05);
                half alpha     = saturate(alphaCore * _AlphaScale + _AlphaFloor) * edge;

                return half4(col, alpha*_AllFade);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
