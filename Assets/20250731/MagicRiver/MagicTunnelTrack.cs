// ============================================================================
//  MagicTunnelTrack.cs
//
//  Custom Timeline Track for the MagicTunnel material.
//
//  Once this file compiles, you'll see a new entry under the Timeline's
//  "Add Track" menu: "Magic Tunnel Track". Add it, drag your MagicTunnel
//  GameObject (or its MeshRenderer) into the Track's binding slot, then
//  right-click the Track and "Add Magic Tunnel Clip".
//
//  The three reflection-driven attributes below are the contract with
//  Unity Timeline — none of them work if MagicTunnelTrack lives in the
//  same .cs as MagicTunnelMixerBehaviour / MagicTunnelClip, so keep them
//  in separate files.
// ============================================================================

using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.ComponentModel;

[TrackColor(0.55f, 0.35f, 0.85f)]            // Purple — easy to spot among other tracks
[TrackClipType(typeof(MagicTunnelClip))]
[TrackBindingType(typeof(Renderer))]
[DisplayName("Custom/Magic Tunnel Track")]
public class MagicTunnelTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<MagicTunnelMixerBehaviour>.Create(graph, inputCount);
    }
}
