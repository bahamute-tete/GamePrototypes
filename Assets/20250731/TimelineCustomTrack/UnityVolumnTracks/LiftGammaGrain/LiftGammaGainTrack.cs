using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Timeline;
using System.ComponentModel;

[TrackColor(0.45f, 0.75f, 0.95f)]
[TrackClipType(typeof(LiftGammaGainClip))]
[TrackBindingType(typeof(Volume))]
[DisplayName("Custom/Volume/Lift Gamma Gain Track")]
public class LiftGammaGainTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<LiftGammaGainMixerBehaviour>.Create(graph, inputCount);
    }
}
