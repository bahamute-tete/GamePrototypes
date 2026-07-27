using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// ====================================================================
//   Horizon Glow Transition Clip
//
//   AnimationCurve 含义：
//     X = clip 内归一化时间 (0 = clip 开始, 1 = clip 结束)
//     Y = 插值进度 (0 = startBlend, 1 = endBlend)
//     线性 → 匀速 / S 曲线 → ease-in-out / 阶梯 → 瞬切等
//
//   驱动 MagicWaterController.horizonBlend
//   Controller 内部会按这个值 lerp 全套 Horizon Glow A/B 参数
// ====================================================================

[System.Serializable]
[DisplayName("Horizon Glow Transition Clip")]
public class HorizonGlowTransitionClip : PlayableAsset, ITimelineClipAsset
{
    [Range(0, 1)] public float startBlend = 0f;
    [Range(0, 1)] public float endBlend   = 1f;

    [Tooltip("X 轴 = clip 归一化时间 (0–1)；Y 轴 = 插值进度 (0–1)\n" +
             "线性 = 匀速；S 曲线 = ease-in-out；自由曲线 = 任意节奏")]
    public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<HorizonGlowTransitionBehaviour>.Create(graph);
        var b = playable.GetBehaviour();
        b.startBlend = startBlend;
        b.endBlend   = endBlend;
        b.curve      = curve;
        return playable;
    }
}
