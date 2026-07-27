// ============================================================================
//  MagicTunnelClip.cs
//
//  PlayableAsset for the MagicTunnel custom Track. This is what shows up as
//  a clip you can place on a Track in the Timeline editor. The actual data
//  it holds lives in the embedded `template` (MagicTunnelBehaviour).
//
//  Don't merge this with the other Track files.
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class MagicTunnelClip : PlayableAsset, ITimelineClipAsset
{
    public MagicTunnelBehaviour template = new MagicTunnelBehaviour();

    // Blending: allows two adjacent clips to crossfade their values smoothly.
    // Extrapolation: lets the clip's values "hold" before the first / after
    // the last clip if you set extrapolation to Hold on the Track.
    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        // ScriptPlayable copies `template` into the playable's behaviour, so
        // each clip instance gets its own data at runtime.
        return ScriptPlayable<MagicTunnelBehaviour>.Create(graph, template);
    }
}
