// LightClip.cs
using System;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;


[Serializable]
[DisplayName("Light Clip")]
public class LightClip : PlayableAsset, ITimelineClipAsset
{
    [Header("Core")]
    [Min(0f)] public float intensity = 1f;
    [ColorUsage(true, true)] public Color color = Color.white;

    [Header("Light Shape — 勾选才参与混合")]
    public bool affectRange;
    [Min(0f)] public float range = 10f;

    public bool affectSpotAngle;
    [Range(1f, 179f)] public float spotAngle = 30f;

    [Header("Transform (Local Space) — 勾选才参与混合")]
    [Tooltip("点光/聚光灯位置。基于 Light 的 Transform.localPosition。")]
    public bool affectPosition;
    public Vector3 position;

    [Tooltip("平行光/聚光灯朝向。以 Euler 角输入,内部转 Quaternion 后参与 Slerp 混合。")]
    public bool affectRotation;
    public Vector3 rotationEuler;

    [Header("Weight Curve")]
    public AnimationCurve curve = AnimationCurve.Constant(0f, 1f, 1f);

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<LightBehaviour>.Create(graph);
        var b = playable.GetBehaviour();
        b.intensity = intensity;
        b.color = color;
        b.range = range;
        b.spotAngle = spotAngle;
        b.position = position;
        b.rotation = Quaternion.Euler(rotationEuler);
        b.affectRange = affectRange;
        b.affectSpotAngle = affectSpotAngle;
        b.affectPosition = affectPosition;
        b.affectRotation = affectRotation;
        b.curve = curve;
        return playable;
    }
}
