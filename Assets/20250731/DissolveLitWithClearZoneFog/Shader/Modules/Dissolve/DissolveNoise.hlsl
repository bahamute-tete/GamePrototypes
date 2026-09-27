#ifndef LZ_DISSOLVE_NOISE_INCLUDED
#define LZ_DISSOLVE_NOISE_INCLUDED
// Requires the host pipeline's TEXTURE2D_PARAM / SAMPLE_TEXTURE2D macros.
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
// Equal-weight three-plane projection, preserving the original sampling pattern.
// Implicit texture derivatives: call the texture path from a fragment shader.
float DissolveSampleNoise(float3 pos, TEXTURE2D_PARAM(noiseTex, noiseSampler))
{
    float nx = SAMPLE_TEXTURE2D(noiseTex, noiseSampler, pos.yz).r;
    float ny = SAMPLE_TEXTURE2D(noiseTex, noiseSampler, pos.zx).r;
    float nz = SAMPLE_TEXTURE2D(noiseTex, noiseSampler, pos.xy).r;
    return (nx + ny + nz) * (1.0 / 3.0);
}
#endif
