using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;

public class MobileBloomMixerBehaviour : PlayableBehaviour
{
    // ---- 原始值备份(首帧捕获,OnPlayableDestroy 恢复) ----
    private bool _hasOriginal;
    private float _origIntensity, _origThreshold, _origScatter, _origSoftKnee;
    private Color _origTint;
    private bool  _ovIntensity, _ovThreshold, _ovScatter, _ovSoftKnee, _ovTint;

    private MobileBloomVolumeComponent _lastComp;

    public override void OnPlayableCreate(Playable playable)
    {
        _hasOriginal = false;
        _lastComp = null;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var volume = playerData as Volume;
        if (volume == null || volume.profile == null) return;
        // 注意:.profile 返回的是 runtime instance,不会脏化 sharedProfile 资源
        if (!volume.profile.TryGet(out MobileBloomVolumeComponent comp)) return;
        _lastComp = comp;

        // ---- 首帧捕获原值 ----
        if (!_hasOriginal)
        {
            _origIntensity = comp.intensity.value;
            _origThreshold = comp.threshold.value;
            _origScatter   = comp.scatter.value;
            _origSoftKnee  = comp.softKnee.value;
            _origTint      = comp.tint.value;
            _ovIntensity = comp.intensity.overrideState;
            _ovThreshold = comp.threshold.overrideState;
            _ovScatter   = comp.scatter.overrideState;
            _ovSoftKnee  = comp.softKnee.overrideState;
            _ovTint      = comp.tint.overrideState;
            _hasOriginal = true;
        }

        // ---- 累加每个属性的加权值与权重 ----
        float sumIntensity = 0f, sumThreshold = 0f, sumScatter = 0f, sumSoftKnee = 0f;
        Color sumTint = Color.clear;        // 必须用 clear,不要 white
        float wIntensity = 0f, wThreshold = 0f, wScatter = 0f, wSoftKnee = 0f, wTint = 0f;

        int n = playable.GetInputCount();
        for (int i = 0; i < n; i++)
        {
            float w = playable.GetInputWeight(i);
            if (w <= 0f) continue;
            var b = ((ScriptPlayable<MobileBloomBehaviour>)playable.GetInput(i)).GetBehaviour();

            if (b.overrideIntensity) { sumIntensity += b.intensity * w; wIntensity += w; }
            if (b.overrideThreshold) { sumThreshold += b.threshold * w; wThreshold += w; }
            if (b.overrideScatter)   { sumScatter   += b.scatter   * w; wScatter   += w; }
            if (b.overrideSoftKnee)  { sumSoftKnee  += b.softKnee  * w; wSoftKnee  += w; }
            if (b.overrideTint)      { sumTint      += b.tint      * w; wTint      += w; }
        }

        // ---- 边界恢复:未覆盖部分用原值补齐 ----
        comp.intensity.value = sumIntensity + _origIntensity * (1f - Mathf.Clamp01(wIntensity));
        comp.threshold.value = sumThreshold + _origThreshold * (1f - Mathf.Clamp01(wThreshold));
        comp.scatter.value   = sumScatter   + _origScatter   * (1f - Mathf.Clamp01(wScatter));
        comp.softKnee.value  = sumSoftKnee  + _origSoftKnee  * (1f - Mathf.Clamp01(wSoftKnee));
        comp.tint.value      = sumTint      + _origTint      * (1f - Mathf.Clamp01(wTint));

        // 任一被 Track 驱动的属性必须打开 overrideState,才会进 VolumeStack 混合
        if (wIntensity > 0f) comp.intensity.overrideState = true;
        if (wThreshold > 0f) comp.threshold.overrideState = true;
        if (wScatter   > 0f) comp.scatter.overrideState   = true;
        if (wSoftKnee  > 0f) comp.softKnee.overrideState  = true;
        if (wTint      > 0f) comp.tint.overrideState      = true;
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        if (!_hasOriginal || _lastComp == null) return;
        _lastComp.intensity.value = _origIntensity;
        _lastComp.threshold.value = _origThreshold;
        _lastComp.scatter.value   = _origScatter;
        _lastComp.softKnee.value  = _origSoftKnee;
        _lastComp.tint.value      = _origTint;
        _lastComp.intensity.overrideState = _ovIntensity;
        _lastComp.threshold.overrideState = _ovThreshold;
        _lastComp.scatter.overrideState   = _ovScatter;
        _lastComp.softKnee.overrideState  = _ovSoftKnee;
        _lastComp.tint.overrideState      = _ovTint;
    }
}
