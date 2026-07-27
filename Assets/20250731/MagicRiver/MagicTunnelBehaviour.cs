// ============================================================================
//  MagicTunnelBehaviour.cs
//
//  Per-clip data carrier for MagicTunnelTrack.
//
//  Each property exposed by the Track has a paired "override" toggle. If the
//  toggle is off, this clip leaves that property alone (other clips or the
//  material's default win for it). If on, the clip's value participates in
//  the Track Mixer's weighted blend.
//
//  Don't merge this with the other Track files. Unity Timeline finds Tracks
//  by reflecting over TrackAsset subclasses, and Unity's ScriptableObject
//  serializer wants each TrackAsset / PlayableAsset class in its own .cs.
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public class MagicTunnelBehaviour : PlayableBehaviour
{
    [Header("Flow")]
    public bool  overrideFlowSpeed = true;
    [Tooltip("Held value while this clip is active. Crossfades to the next clip when they overlap.")]
    public float flowSpeed = 0.4f;

    public bool  overrideTurbulenceSpeed = false;
    public float turbulenceSpeed = 1.0f;

    [Header("Color")]
    public bool  overrideColorA = false;
    [ColorUsage(true, true)]
    public Color colorA = new Color(0f, 1f, 0.984f, 1f);

    public bool  overrideColorB = false;
    [ColorUsage(true, true)]
    public Color colorB = new Color(0.733f, 0.961f, 0.239f, 1f);

    public bool  overrideBrightness = false;
    [Range(0f, 20f)]
    public float brightness = 4.0f;

    [Header("Opacity")]
    public bool  overrideAlphaScale = false;
    [Range(0f, 2f)]
    public float alphaScale = 1.0f;

    public bool  overrideAllFade = false;
    [Tooltip("0 = fully visible, 1 = fully faded out. Animate this between adjacent clips to make the tunnel dissolve in / out.")]
    [Range(0f, 1f)]
    public float allFade = 0.0f;
}
