// =============================================================================
//  DissolveClip.cs
//  文件名必须 == 类名 (DissolveClip),否则 Unity 找不到 MonoScript,
//  会导致 Timeline 的 CurvesProxy 抛 "Invalid type"。
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class DissolveBehaviour : PlayableBehaviour
{
    public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
}

// PlayableAsset (= ScriptableObject 派生),必须独占文件且文件名 == 类名
public class DissolveClip : PlayableAsset, ITimelineClipAsset
{
    public DissolveBehaviour template = new DissolveBehaviour();

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation | ClipCaps.Looping;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<DissolveBehaviour>.Create(graph, template);
    }
}
