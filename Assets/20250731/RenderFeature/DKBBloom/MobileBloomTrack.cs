using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Timeline;
using System.ComponentModel;

[TrackColor(0.85f, 0.5f, 1.0f)]
[TrackClipType(typeof(MobileBloomClip))]
[TrackBindingType(typeof(Volume))]
[DisplayName("Custom/Volume/Mobile Bloom Track")]
public class MobileBloomTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<MobileBloomMixerBehaviour>.Create(graph, inputCount);
    }
}
