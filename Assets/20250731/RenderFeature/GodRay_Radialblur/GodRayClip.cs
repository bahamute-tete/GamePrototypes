using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// =============================================================================
// GodRayClip
// -----------------------------------------------------------------------------
// PlayableAsset that lives on the Timeline track. The serialized `template` is
// what artists edit in the clip Inspector; CreatePlayable hands a copy of it
// to the runtime as a ScriptPlayable<GodRayBehaviour>.
//
// ClipCaps.Blending enables crossfade between overlapping clips. Extrapolation
// lets clips repeat / hold past their bounds (matches Unity's VolumeTrack).
// =============================================================================

[Serializable]
public class GodRayClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip("Animatable god ray parameters for this clip.")]
    public GodRayBehaviour template = new GodRayBehaviour();

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        // ScriptPlayable.Create with a template makes a copy; the asset
        // itself isn't mutated at runtime.
        return ScriptPlayable<GodRayBehaviour>.Create(graph, template);
    }
}
