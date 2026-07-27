using System;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public class MobileBloomBehaviour : PlayableBehaviour
{
    [Header("Intensity")]
    public bool overrideIntensity = true;
    [Range(0f, 4f)] public float intensity = 1f;

    [Header("Threshold")]
    public bool overrideThreshold = false;
    [Min(0f)] public float threshold = 1f;

    [Header("Tint")]
    public bool overrideTint = false;
    [ColorUsage(false, true)] public Color tint = Color.white;

    [Header("Scatter")]
    public bool overrideScatter = false;
    [Range(0f, 1f)] public float scatter = 0.7f;

    [Header("Soft Knee")]
    public bool overrideSoftKnee = false;
    [Range(0f, 1f)] public float softKnee = 0.5f;
}
