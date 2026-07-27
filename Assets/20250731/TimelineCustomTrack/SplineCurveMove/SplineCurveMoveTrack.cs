using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.ComponentModel;

[TrackColor(0.3f, 0.8f, 0.3f)]
[TrackClipType(typeof(SplineCurveMoveClip))]
[TrackBindingType(typeof(Transform))]
[DisplayName("Custom/SplineMove/Spline Curve Move Track")]
public class SplineCurveMoveTrack : TrackAsset
{

    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        var director = go.GetComponent<PlayableDirector>();
        if (director != null)
        {
            var boundObject = director.GetGenericBinding(this) as Transform;
            if (boundObject != null)
            {
                name =$"SplineCurve_{boundObject.name}";
            }
        }


        return ScriptPlayable<SplineCurveMoveMixerBehaviour>.Create(graph, inputCount);
    }
}
