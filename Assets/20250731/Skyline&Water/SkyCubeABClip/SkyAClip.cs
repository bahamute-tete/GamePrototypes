using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// ====================================================================
//   Sky A Clip
//   定义一段时间内 Sky A 的 tint / exposure / rotation 从 startState 过渡到 endState
//   Curve 控制过渡速率
// ====================================================================

[System.Serializable]
[DisplayName("Sky A Clip")]
public class SkyAClip : PlayableAsset, ITimelineClipAsset
{
    [Header("Start State (when clip begins)")]
    public SkyState startState = SkyState.Default;

    [Header("End State (when clip ends)")]
    public SkyState endState   = SkyState.Default;

    [Tooltip("X 轴 = clip 归一化时间 (0–1)；Y 轴 = 插值进度 (0–1)\n" +
             "线性 = 匀速；S 曲线 = ease-in-out")]
    public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<SkyABehaviour>.Create(graph);
        var b = playable.GetBehaviour();
        b.startState = startState;
        b.endState   = endState;
        b.curve      = curve;
        return playable;
    }
}
