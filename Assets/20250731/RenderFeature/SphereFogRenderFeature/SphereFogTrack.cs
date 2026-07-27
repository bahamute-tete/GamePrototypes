// =============================================================================
//  SphereFogTrack.cs
//  自定义 Timeline Track,绑定一个 SphereFogVolume,
//  每帧按权重混合 active Clip 的参数,写入 Volume。Volume 的 LateUpdate 会把
//  这些参数推到 Shader globals,所以 Edit Mode 下 scrub 也能即时预览。
//
//  设计要点:
//    1. 第一帧 ProcessFrame 缓存 Volume 的原始值,Graph 销毁 (Timeline 停止/退出
//       预览) 时还原,避免在场景里留下被 Timeline 改过的脏数据
//    2. 没有 active Clip → 不动 Volume (保留上一帧值,Timeline 标准行为)
//    3. Transform 只有当 active Clip 里至少一个勾了 driveTransform 才会被改,
//       否则 Volume.transform 保持场景设置,跟 Clip 解耦
//
//  文件名必须 == 类名 (SphereFogTrack)。
// =============================================================================

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

#if UNITY_EDITOR
using System.ComponentModel;
#endif

public class SphereFogMixerBehaviour : PlayableBehaviour
{
    // playerData 偶尔为 null 时的兜底
    SphereFogVolume _cached;

    // 原始值缓存,OnPlayableDestroy 时还原
    bool    _hasOrig;
    float   _oSmooth, _oDensity, _oSkyDist, _oNoiseScale, _oNoiseStrength;
    Color   _oFogColor;
    Vector3 _oNoiseSpeed;
    Vector3 _oPos, _oRot, _oScale;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var volume = (playerData as SphereFogVolume) ?? _cached;
        if (volume != null) _cached = volume;
        if (volume == null) return;

        // ===== 第一帧缓存原值 =====
        if (!_hasOrig)
        {
            _oSmooth        = volume.smoothness;
            _oDensity       = volume.density;
            _oFogColor      = volume.fogColor;
            _oSkyDist       = volume.skyDistance;
            _oNoiseScale    = volume.noiseScale;
            _oNoiseStrength = volume.noiseStrength;
            _oNoiseSpeed    = volume.noiseSpeed;

            var t = volume.transform;
            _oPos   = t.position;
            _oRot   = t.eulerAngles;
            _oScale = t.localScale;

            _hasOrig = true;
        }

        // ===== 加权混合 active clip =====
        int   inputCount = playable.GetInputCount();
        float weightSum  = 0f;

        float   density = 0f, smoothness = 0f, skyDist = 0f;
        float   noiseScale = 0f, noiseStrength = 0f;
        Color   fogColor = Color.clear;
        Vector3 noiseSpeed = Vector3.zero;

        float   transformWeight = 0f;
        Vector3 position = Vector3.zero, rotationEuler = Vector3.zero, scale = Vector3.zero;

        for (int i = 0; i < inputCount; i++)
        {
            float w = playable.GetInputWeight(i);
            if (w <= 0f) continue;

            var sp = (ScriptPlayable<SphereFogBehaviour>)playable.GetInput(i);
            var b  = sp.GetBehaviour();
            if (b == null) continue;

            density       += b.density       * w;
            smoothness    += b.smoothness    * w;
            fogColor      += b.fogColor      * w;
            skyDist       += b.skyDistance   * w;
            noiseScale    += b.noiseScale    * w;
            noiseStrength += b.noiseStrength * w;
            noiseSpeed    += b.noiseSpeed    * w;

            if (b.driveTransform)
            {
                position      += b.position      * w;
                rotationEuler += b.rotationEuler * w;
                scale         += b.scale         * w;
                transformWeight += w;
            }

            weightSum += w;
        }

        // 没有 active clip → 不动 (保留上一帧)
        if (weightSum < 0.001f) return;

        // ===== 写入 Fog 参数 =====
        volume.smoothness    = smoothness;
        volume.density       = density;
        volume.fogColor      = fogColor;
        volume.skyDistance   = skyDist;
        volume.noiseScale    = noiseScale;
        volume.noiseStrength = noiseStrength;
        volume.noiseSpeed    = noiseSpeed;

        // ===== 写入 Transform (只有当至少一个 active clip 勾了 driveTransform) =====
        if (transformWeight > 0.001f)
        {
            // 按 transform 自己的 weight 归一化,避免没勾 driveTransform 的 clip 稀释结果
            float invW = 1f / transformWeight;
            var   t    = volume.transform;
            t.position    = position      * invW;
            t.eulerAngles = rotationEuler * invW;     // 欧拉角线性插值;大角度差异请加密关键 Clip
            t.localScale  = scale         * invW;
        }
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        // Timeline 退出预览 / 停止播放 / 切场景 时还原 Volume 到原始状态
        if (_hasOrig && _cached != null)
        {
            _cached.smoothness    = _oSmooth;
            _cached.density       = _oDensity;
            _cached.fogColor      = _oFogColor;
            _cached.skyDistance   = _oSkyDist;
            _cached.noiseScale    = _oNoiseScale;
            _cached.noiseStrength = _oNoiseStrength;
            _cached.noiseSpeed    = _oNoiseSpeed;

            var t = _cached.transform;
            t.position    = _oPos;
            t.eulerAngles = _oRot;
            t.localScale  = _oScale;
        }
        _cached  = null;
        _hasOrig = false;
    }
}

[TrackColor(0.40f, 0.70f, 0.85f)]                       // 偏蓝绿,跟 Dissolve 的粉紫区分
[TrackClipType(typeof(SphereFogClip))]
[TrackBindingType(typeof(SphereFogVolume))]
#if UNITY_EDITOR
[DisplayName("Custom/Fog/Sphere Fog Track")]
#endif
public class SphereFogTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<SphereFogMixerBehaviour>.Create(graph, inputCount);
    }
}
