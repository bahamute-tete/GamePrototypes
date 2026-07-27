// =============================================================================
//  DissolveTrack.cs
//  文件名必须 == 类名 (DissolveTrack)。
//
//  Mixer 工作流:
//    1. 优先用 active clip 的 curve.Evaluate(t) 加权混合
//    2. 没有 active clip (weightSum==0) 时,看 mixer 自己的播放头时间相对于
//       "整条 track 上 clip 的总体范围" (firstClipStart .. lastClipEnd):
//         - 在第一个 clip 之前 → amount = 0
//         - 在最后一个 clip 之后 → amount = 1
//         - 在 clip 之间的 gap → 保持上一帧 (不动)
//    3. 临界值咬合 (< 0.001 → 0, > 0.999 → 1)
//
//  这样设计能解决两个问题:
//    A. 快速 scrub 后释放,播放头飞出 clip,因为跳帧导致 mixer 最后一次
//       evaluate 停在中间值 → 离开 clip 后自动咬合到边界
//    B. 端点浮点精度漂移 (0.9999 / 0.0001 这种)
//
//  Track 在 CreateTrackMixer 里把 clip 范围信息塞给 mixer,
//  Timeline 修改 clip 时 graph 会重建,信息会更新。
// =============================================================================

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

#if UNITY_EDITOR
using System.ComponentModel;
#endif

public class DissolveMixerBehaviour : PlayableBehaviour
{
    // 兜底:Edit Mode 某些路径下 playerData 可能短暂为 null
    DissolveController _cached;

    // 由 DissolveTrack.CreateTrackMixer 在建图时写入。整条 track 上所有 clip 的
    // 起止时间合集,用来判断播放头是否在 clip 之外的左/右边。
    public double firstClipStart;
    public double lastClipEnd;
    public bool   hasClips;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var controller = (playerData as DissolveController) ?? _cached;
        if (controller != null) _cached = controller;
        if (controller == null) return;

        int   inputCount = playable.GetInputCount();
        float weightSum  = 0f;
        float blended    = 0f;

        for (int i = 0; i < inputCount; i++)
        {
            float w = playable.GetInputWeight(i);
            if (w <= 0f) continue;

            var sp = (ScriptPlayable<DissolveBehaviour>)playable.GetInput(i);
            var b  = sp.GetBehaviour();
            if (b == null || b.curve == null) continue;

            double localTime = sp.GetTime();
            double duration  = sp.GetDuration();
            float  t = (duration > 1e-6) ? Mathf.Clamp01((float)(localTime / duration)) : 0f;
            float  v = Mathf.Clamp01(b.curve.Evaluate(t));

            blended   += v * w;
            weightSum += w;
        }

        float finalAmount;

        if (weightSum >= 0.001f)
        {
            // 有 active clip,用混合后的值
            finalAmount = blended;
        }
        else if (hasClips)
        {
            // 没有 active clip,用 mixer 自己的播放头时间判断"在 clip 之前还是之后"
            // mixer.GetTime() 等于 track 上的当前播放头时间,跟 TimelineClip.start/.end 同坐标系
            double t = playable.GetTime();

            const double EPS = 1e-4;
            if (t >= lastClipEnd - EPS)
                finalAmount = 1f;                  // 飞过最后一个 clip 的右端
            else if (t <= firstClipStart + EPS)
                finalAmount = 0f;                  // 还在第一个 clip 的左边
            else
                return;                            // 卡在两个 clip 之间的 gap,保持上一帧
        }
        else
        {
            return;                                // track 上完全没有 clip
        }

        // 端点精度咬合
        if (finalAmount < 0.001f)      finalAmount = 0f;
        else if (finalAmount > 0.999f) finalAmount = 1f;

        controller.SetAmount(finalAmount);
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        _cached = null;
    }
}

[TrackColor(0.85f, 0.45f, 0.65f)]
[TrackClipType(typeof(DissolveClip))]
[TrackBindingType(typeof(DissolveController))]
#if UNITY_EDITOR
[DisplayName("Custom/Dissolve Track")]
#endif
public class DissolveTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        var playable = ScriptPlayable<DissolveMixerBehaviour>.Create(graph, inputCount);
        var mixer    = playable.GetBehaviour();

        // 把整条 track 上所有 clip 的起止时间塞给 mixer。
        // Timeline 在 Editor 改动 clip (拖、加、删) 时 graph 会重建,这里会再走一遍,所以总是新的。
        double firstStart = double.MaxValue;
        double lastEnd    = double.MinValue;
        bool   any        = false;

        foreach (var c in GetClips())
        {
            if (c.start < firstStart) firstStart = c.start;
            if (c.end   > lastEnd)    lastEnd    = c.end;
            any = true;
        }

        mixer.firstClipStart = any ? firstStart : 0.0;
        mixer.lastClipEnd    = any ? lastEnd    : 0.0;
        mixer.hasClips       = any;

        return playable;
    }
}
