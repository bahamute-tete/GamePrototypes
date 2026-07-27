using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Timeline;
using System.ComponentModel;

// Color Adjustments 自定义轨道,绑定到含有 ColorAdjustments 组件的 Volume
[TrackColor(0.95f, 0.55f, 0.20f)]
[TrackClipType(typeof(ColorAdjustmentsClip))]
[TrackBindingType(typeof(Volume))]
[DisplayName("Custom/Volume/Color Adjustments Track")]
public class ColorAdjustmentsTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<ColorAdjustmentsMixerBehaviour>.Create(graph, inputCount);
    }
}
