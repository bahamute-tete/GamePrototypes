#ifndef MAGIC_HORIZON_GLOW_INCLUDED
#define MAGIC_HORIZON_GLOW_INCLUDED

// Global params — set by MagicWaterController via Shader.SetGlobalXXX
// Skybox 和 Water 共享这一组参数
// 前缀 _MW_ 避免与项目其它 shader 的全局命名冲突（如某粒子 shader 把 _HaloFalloff 注册成 Texture）
half4 _MW_HorizonColor; // HDR
half  _MW_HorizonIntensity;
half  _MW_HorizonFalloff;
half  _MW_HaloIntensity;
half  _MW_HaloFalloff;

// dir: 世界空间方向向量（已归一化）
// y = 1 天顶，y = 0 水平线，y = -1 正下方
half3 ComputeHorizonGlow(float3 dir)
{
    half d = abs(dir.y);
    half core = exp(-d * _MW_HorizonFalloff) * _MW_HorizonIntensity; // 锐利亮线
    half halo = exp(-d * _MW_HaloFalloff)    * _MW_HaloIntensity;    // 柔和光晕
    return (core + halo) * _MW_HorizonColor.rgb;
}

#endif
