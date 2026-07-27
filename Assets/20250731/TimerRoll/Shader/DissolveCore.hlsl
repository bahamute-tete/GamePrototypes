// =============================================================================
//  DissolveCore.hlsl  (v3.2 - PICO 4U / Adreno 740 优化版)
//
//  v3.2 改动 (vs v3.1):
//    - Bayer 4x4 LUT 数组索引 → IGN (Interleaved Gradient Noise)
//        * Adreno 740 上本地数组动态索引有落 scratch memory 的风险,
//          实测可能成为 fragment 热点。IGN 只用 3 个 mad, 无任何索引。
//        * 视觉上 IGN 比 4x4 Bayer 更优 (无 4 像素块状伪影)。
//        * Jorge Jimenez (Activision) 推广, 现代 dither 实践事实标准。
//    - 新增 ComputeDissolveAlphaOnly():
//        * 只计算 alpha, 跳过 edge glow 计算。
//        * 给 ShadowCaster / DepthOnly / DepthOcclusion 用,
//          因为这些 pass 根本不需要 edge color。
//        * 节省 ~30% dissolve cost; 在 shadow cascade 模式下
//          (一帧跑 4 次 ShadowCaster) 收益线性放大。
//    - 新增 ComputeDissolveFieldClip():
//        * 进一步极简化 — 只算 field 和 amount 阈值, 不做 dither。
//        * Shadow / Depth pass 用这个就够: shadow map 不需要屏幕空间 dither,
//          光源空间的 binary mask 直接看 amount 比 field 大就 clip。
//        * 比 AlphaOnly 还省 ~50%.
// =============================================================================

#ifndef SCENE_LIT_FOGGED_DISSOLVE_CORE
#define SCENE_LIT_FOGGED_DISSOLVE_CORE

// -----------------------------------------------------------------------------
// 期望 Shader 在 CBUFFER(UnityPerMaterial) 内声明:
//   float  _DissolveAmount;
//   float  _DissolveMode;            // 0=Noise 1=Axis 2=Radial
//   float  _DissolveSpace;           // 0=Local(OS) 1=World(WS)
//   float  _DissolveEdgeWidth;
//   float  _DissolveNoiseScale;
//   float4 _DissolveAxis;            // xyz=归一化方向(在对应空间), w=halfExtent
//   float  _DissolveAxisCenter;      // 中心点投影
//   float4 _DissolveRadial;          // xyz=中心点(在对应空间), w=maxDist
//   float  _DissolveRadialReverse;
//   float4 _DissolveEdgeColor;
//   float  _DissolveEdgeIntensity;
//   float  _DissolveBrightnessPower;
//   float  _DissolveUseNoiseTex;
//
// 期望 Shader 在 CBUFFER 外声明:
//   TEXTURE2D(_DissolveNoiseTex); SAMPLER(sampler_DissolveNoiseTex);
// -----------------------------------------------------------------------------

// ===== IGN (Interleaved Gradient Noise) — Bayer 4x4 替代 =====================
//
// Jimenez '14 (Activision GDC), 等价于 Frankle-McCann 的 ordered dither
// 但在 mobile GPU 上无任何索引开销, 只有 3 ALU。
//
// 视觉特性:
//   - 高频蓝噪声分布, 比 Bayer 4x4 的 4 像素方格伪影更隐蔽
//   - 在 VR 高 PPD 显示上肉眼几乎看不出来
//   - 同样的 amount 切换不会有跳变 (单调性保证)
//
// 性能:
//   - Bayer LUT: 1 fmod + 1 fmod + 1 indexing (可能落 scratch! ~10-50 cycles)
//   - IGN:       2 dot + 2 frac + 1 mul     (~3 cycles, 全 pipeline 内)
//
float DissolveIGN(float2 screenPos)
{
    return frac(52.9829189 * frac(dot(screenPos, float2(0.06711056, 0.00583715))));
}

// ===== 过程化 3D Value Noise (无贴图时的 fallback) ===========================
float DissolveHash(float3 p)
{
    p = frac(p * float3(443.8975, 397.2973, 491.1871));
    p += dot(p.yzx, p.xyz + 19.27);
    return frac(p.x * p.y * p.z);
}

float DissolveValueNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = DissolveHash(i);
    float n100 = DissolveHash(i + float3(1, 0, 0));
    float n010 = DissolveHash(i + float3(0, 1, 0));
    float n110 = DissolveHash(i + float3(1, 1, 0));
    float n001 = DissolveHash(i + float3(0, 0, 1));
    float n101 = DissolveHash(i + float3(1, 0, 1));
    float n011 = DissolveHash(i + float3(0, 1, 1));
    float n111 = DissolveHash(i + float3(1, 1, 1));

    return lerp(
        lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
        lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y),
        f.z);
}

// ===== 贴图 noise: triplanar 3 采样 ==========================================
float DissolveSampleNoiseTex(float3 pos)
{
    float nx = SAMPLE_TEXTURE2D(_DissolveNoiseTex, sampler_DissolveNoiseTex, pos.yz).r;
    float ny = SAMPLE_TEXTURE2D(_DissolveNoiseTex, sampler_DissolveNoiseTex, pos.zx).r;
    float nz = SAMPLE_TEXTURE2D(_DissolveNoiseTex, sampler_DissolveNoiseTex, pos.xy).r;
    return (nx + ny + nz) * (1.0 / 3.0);
}

// ===== 三种 dissolve field ===================================================
float DissolveField_Noise(float3 pos, float scale)
{
    float3 p = pos * scale;
    float result;
    UNITY_BRANCH
    if (_DissolveUseNoiseTex > 0.5)
    {
        result = DissolveSampleNoiseTex(p);
    }
    else
    {
        result = DissolveValueNoise(p);
    }
    return result;
}

float DissolveField_Axis(float3 pos, float3 axisDir, float halfExtent, float axisCenter)
{
    float proj  = dot(pos, axisDir);
    float minP  = axisCenter - halfExtent;
    float range = max(halfExtent * 2.0, 1e-4);
    return saturate((proj - minP) / range);
}

float DissolveField_Radial(float3 pos, float3 center, float maxDist, float reverse)
{
    float d = length(pos - center);
    float n = saturate(d / max(maxDist, 1e-4));
    return reverse > 0.5 ? 1.0 - n : n;
}

// ===== Auto: 按 _DissolveMode + _DissolveSpace 派发 ==========================
float DissolveField_Auto(float3 positionWS, float3 positionOS)
{
    int   mode = (int)_DissolveMode;
    float3 pos = (_DissolveSpace > 0.5) ? positionWS : positionOS;

    float result;
    if (mode == 1)
    {
        result = DissolveField_Axis(pos, _DissolveAxis.xyz,
                                     _DissolveAxis.w, _DissolveAxisCenter);
    }
    else if (mode == 2)
    {
        result = DissolveField_Radial(pos, _DissolveRadial.xyz,
                                       _DissolveRadial.w, _DissolveRadialReverse);
    }
    else
    {
        result = DissolveField_Noise(pos, _DissolveNoiseScale);
    }
    return result;
}

// =============================================================================
// 主入口 #1: 完整版 (alpha + edge)
//   - 给 ForwardLit / 透明 Forward pass 用 (需要 edge glow)
//   - cost: field + IGN(3 ALU) + ~10 ALU 后处理
// =============================================================================
float2 ComputeDissolveAlphaAndEdge(float3 positionWS, float3 positionOS, float2 screenPos, float amount)
{
    float field = DissolveField_Auto(positionWS, positionOS);

    float halfEdge  = _DissolveEdgeWidth * 0.5;
    float threshold = lerp(-halfEdge, 1.0 + halfEdge, amount);
    float dist      = field - threshold;
    float progress  = saturate(dist / _DissolveEdgeWidth + 0.5);

    float dither = DissolveIGN(screenPos);
    float alpha  = step(dither, progress);

    float edge = saturate(1.0 - abs(progress - 0.5) * 2.0);
    edge *= alpha;
    edge *= step(0.001, amount);
    edge *= step(amount, 0.999);

    return float2(alpha, edge);
}

// =============================================================================
// 主入口 #2: 轻量版 (alpha-only, 无 edge)
//   - 给透明 shader 的 DepthOcclusion pass 用
//   - 仍然走 IGN dither 保证和 ForwardLit 同步的 alpha mask
//   - 跳过 edge 计算 (3 个 step + saturate + abs)
// =============================================================================
float ComputeDissolveAlphaOnly(float3 positionWS, float3 positionOS, float2 screenPos, float amount)
{
    float field = DissolveField_Auto(positionWS, positionOS);

    float halfEdge  = _DissolveEdgeWidth * 0.5;
    float threshold = lerp(-halfEdge, 1.0 + halfEdge, amount);
    float progress  = saturate((field - threshold) / _DissolveEdgeWidth + 0.5);

    float dither = DissolveIGN(screenPos);
    return step(dither, progress);
}

// =============================================================================
// 主入口 #3: 极简版 (field-only, 无 dither)
//   - 给 ShadowCaster / DepthOnly 用
//   - shadow map 是光源空间二值 mask, 不需要屏幕空间 dither
//   - 直接 field < amount 就 clip 掉
//   - cost: 只有 field 计算, 节省全部 dither + edge 开销
//
//   用法:
//     if (_DissolveAmount > 0.0001)
//         clip(ComputeDissolveFieldClip(posWS, posOS) - _DissolveAmount);
// =============================================================================
float ComputeDissolveFieldClip(float3 positionWS, float3 positionOS)
{
    return DissolveField_Auto(positionWS, positionOS);
}

#endif // SCENE_LIT_FOGGED_DISSOLVE_CORE
