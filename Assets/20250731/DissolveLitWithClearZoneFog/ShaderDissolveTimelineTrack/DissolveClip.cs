using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum DissolveEndBehaviour { HoldEnd, RestoreOriginal }
[Serializable]
public class DissolveBehaviour : PlayableBehaviour
{
    public float startValue;
    public float endValue = 1f;
    public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);
    public DissolveEndBehaviour endBehaviour;
    public float Evaluate(float t) => Mathf.LerpUnclamped(startValue, endValue, curve == null ? Mathf.Clamp01(t) : curve.Evaluate(Mathf.Clamp01(t)));
}
public class DissolveClip : PlayableAsset, ITimelineClipAsset
{
    public DissolveBehaviour template = new DissolveBehaviour();
    public ClipCaps clipCaps => ClipCaps.Blending;
    public override double duration => 2;
    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) => ScriptPlayable<DissolveBehaviour>.Create(graph, template);
}
