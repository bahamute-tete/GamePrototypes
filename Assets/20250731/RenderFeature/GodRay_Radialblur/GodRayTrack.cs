using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Timeline;

// =============================================================================
// GodRayTrack
// -----------------------------------------------------------------------------
// Timeline track that drives GodRayVolumeComponent parameters on a bound Volume.
//
// Drop one of these into a Timeline asset:
//   Right-click track area → "Rendering/God Ray Track" → drag a Volume onto
//   the binding field → add Clips and edit their parameters.
//
// `using System.ComponentModel` is needed for the [DisplayName] attribute —
// this is the one with a space in it that the Unity-native [DisplayName] also
// has but in a different namespace; sticking with System.ComponentModel keeps
// the runtime assembly references simple.
// =============================================================================

[TrackColor(1f, 0.85f, 0.4f)]                    // golden — matches god ray vibe
[TrackClipType(typeof(GodRayClip))]
[TrackBindingType(typeof(Volume))]
[DisplayName("Custom/Volume/God Ray Track")]
public class GodRayTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<GodRayMixerBehaviour>.Create(graph, inputCount);
    }
}
