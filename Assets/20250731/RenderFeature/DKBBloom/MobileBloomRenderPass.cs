using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class MobileBloomRenderPass : ScriptableRenderPass
{
    private const int kMaxIterations = 4;

    private static readonly int _ThresholdId   = Shader.PropertyToID("_Threshold");
    private static readonly int _ParamsId      = Shader.PropertyToID("_Params");
    private static readonly int _BloomTexId    = Shader.PropertyToID("_BloomTexture");
    private static readonly int _PreviousMipId = Shader.PropertyToID("_PreviousMip");
    private static readonly int _IntensityId   = Shader.PropertyToID("_Intensity");
    private static readonly int _TintId        = Shader.PropertyToID("_Tint");

    private readonly Material _material;
    private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Mobile Bloom");

    private RTHandle _source;
    private MobileBloomVolumeComponent _component;

    private readonly RTHandle[] _downRTs = new RTHandle[kMaxIterations];
    private readonly RTHandle[] _upRTs   = new RTHandle[kMaxIterations];
    private RTHandle _composeRT;
    private readonly Vector2Int[] _downSizes = new Vector2Int[kMaxIterations];

    public MobileBloomRenderPass(Material material) { _material = material; }

    public void SetSource(RTHandle source) => _source = source;
    public void SetVolumeComponent(MobileBloomVolumeComponent comp) => _component = comp;

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        if (_component == null) return;

        var desc = renderingData.cameraData.cameraTargetDescriptor;
        desc.depthBufferBits = 0;
        desc.msaaSamples = 1;
        // 移动 HDR 友好格式,4 字节/像素,带宽减半
        desc.colorFormat = RenderTextureFormat.RGB111110Float;

        int iterations = Mathf.Clamp(_component.iterations.value, 1, kMaxIterations);
        int startDiv   = _component.halfResolutionStart.value ? 2 : 1;

        // ---- Down chain ----
        int w = Mathf.Max(1, desc.width  / startDiv);
        int h = Mathf.Max(1, desc.height / startDiv);
        for (int i = 0; i < iterations; i++)
        {
            var d = desc; d.width = w; d.height = h;
            RenderingUtils.ReAllocateIfNeeded(ref _downRTs[i], d,
                FilterMode.Bilinear, TextureWrapMode.Clamp, name: $"_BloomDown{i}");
            _downSizes[i] = new Vector2Int(w, h);
            w = Mathf.Max(1, w / 2);
            h = Mathf.Max(1, h / 2);
        }

        // ---- Up chain (iterations - 1 个,镜像 down 的尺寸) ----
        for (int i = 0; i < iterations - 1; i++)
        {
            int mirrorIdx = iterations - 2 - i;
            var size = _downSizes[mirrorIdx];
            var d = desc; d.width = size.x; d.height = size.y;
            RenderingUtils.ReAllocateIfNeeded(ref _upRTs[i], d,
                FilterMode.Bilinear, TextureWrapMode.Clamp, name: $"_BloomUp{i}");
        }

        // ---- Compose RT (与 source 同尺寸,避免读写同一张 RT) ----
        var composeDesc = renderingData.cameraData.cameraTargetDescriptor;
        composeDesc.depthBufferBits = 0;
        composeDesc.msaaSamples = 1;
        RenderingUtils.ReAllocateIfNeeded(ref _composeRT, composeDesc,
            FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_BloomCompose");
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (_material == null || _source == null || _component == null) return;

        var cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, _profilingSampler))
        {
            int iterations = Mathf.Clamp(_component.iterations.value, 1, kMaxIterations);

            // 阈值打包 (软膝盖)
            float t = _component.threshold.value;
            float k = Mathf.Max(_component.softKnee.value * t, 1e-4f);
            _material.SetVector(_ThresholdId, new Vector4(t, t - k, 2f * k, 0.25f / k));

            // ---- Pass 0: Prefilter + 第一次下采样 ----
            SetTexelParams(_downSizes[0]);
            Blitter.BlitCameraTexture(cmd, _source, _downRTs[0], _material, 0);

            // ---- Pass 1: Dual Kawase 下采样链 ----
            for (int i = 1; i < iterations; i++)
            {
                SetTexelParams(_downSizes[i - 1]);
                Blitter.BlitCameraTexture(cmd, _downRTs[i - 1], _downRTs[i], _material, 1);
            }

            // ---- Pass 2: Dual Kawase 上采样链 (多 band 融合) ----
            RTHandle currentUp = _downRTs[iterations - 1];
            Vector2Int currentSize = _downSizes[iterations - 1];
            for (int i = 0; i < iterations - 1; i++)
            {
                int bandIdx = iterations - 2 - i;
                RTHandle target = _upRTs[i];

                SetTexelParams(currentSize);
                _material.SetTexture(_PreviousMipId, _downRTs[bandIdx]);
                Blitter.BlitCameraTexture(cmd, currentUp, target, _material, 2);

                currentUp = target;
                currentSize = _downSizes[bandIdx];
            }

            // ---- Pass 3: Composite ----
            _material.SetTexture(_BloomTexId, currentUp);
            _material.SetFloat(_IntensityId, _component.intensity.value);
            _material.SetColor(_TintId, _component.tint.value);

            Blitter.BlitCameraTexture(cmd, _source, _composeRT, _material, 3);
            Blitter.BlitCameraTexture(cmd, _composeRT, _source);
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    private void SetTexelParams(Vector2Int srcSize)
    {
        Vector4 p = new Vector4(
            0.5f / srcSize.x,
            0.5f / srcSize.y,
            _component.scatter.value,
            0f);
        _material.SetVector(_ParamsId, p);
    }

    public void Dispose()
    {
        for (int i = 0; i < _downRTs.Length; i++) _downRTs[i]?.Release();
        for (int i = 0; i < _upRTs.Length; i++)   _upRTs[i]?.Release();
        _composeRT?.Release();
    }
}
