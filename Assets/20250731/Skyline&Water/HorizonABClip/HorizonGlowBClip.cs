using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// ====================================================================
//   Horizon Glow B Clip
//   定义一段时间内 B 组参数从 startState 过渡到 endState
//   Curve 控制过渡速率
// ====================================================================

[System.Serializable]
[DisplayName("Horizon Glow B Clip")]
public class HorizonGlowBClip : PlayableAsset, ITimelineClipAsset
{
    [Header("Start State (when clip begins)")]
    public HorizonGlowState startState = HorizonGlowState.Default;

    [Header("End State (when clip ends)")]
    public HorizonGlowState endState   = HorizonGlowState.Default;

    [Tooltip("X 轴 = clip 归一化时间 (0–1)；Y 轴 = 插值进度 (0–1)\n" +
             "线性 = 匀速；S 曲线 = ease-in-out")]
    public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<HorizonGlowBBehaviour>.Create(graph);
        var b = playable.GetBehaviour();
        b.startState = startState;
        b.endState   = endState;
        b.curve      = curve;
        return playable;
    }
}
