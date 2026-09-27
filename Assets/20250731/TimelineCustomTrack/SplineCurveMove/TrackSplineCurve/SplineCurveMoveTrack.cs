using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.ComponentModel;
using System.Linq;

[TrackColor(0.3f, 0.8f, 0.3f)]
[TrackClipType(typeof(SplineCurveMoveClip))]
[TrackClipType(typeof(SplineTransitionClip))]
[TrackBindingType(typeof(Transform))]
[DisplayName("Custom/SplineMove/Spline Curve Move Track")]
public class SplineCurveMoveTrack : TrackAsset
{
    protected override Playable CreatePlayable(PlayableGraph graph, GameObject go, TimelineClip clip)
    {
        var playable = base.CreatePlayable(graph, go, clip);
        if (playable.IsValid())
            ((ScriptPlayable<SplineCurveMoveBehaviour>)playable).GetBehaviour().TimelineClip = clip;
        return playable;
    }

    public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
    {
        var target = director.GetGenericBinding(this) as Transform;
        if (target != null)
        {
            driver.AddFromName<Transform>(target.gameObject, "m_LocalPosition.x");
            driver.AddFromName<Transform>(target.gameObject, "m_LocalPosition.y");
            driver.AddFromName<Transform>(target.gameObject, "m_LocalPosition.z");
            LookOverrideTrack.GatherRotationProperties(target, driver);
        }
        base.GatherProperties(director, driver);
    }

    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        var director = go.GetComponent<PlayableDirector>();
        var mixer = ScriptPlayable<SplineCurveMoveMixerBehaviour>.Create(graph, inputCount);
        var target = director != null ? director.GetGenericBinding(this) as Transform : null;
        if (target != null)
            mixer.GetBehaviour().Configure(graph, director, target, GetClips().ToArray());
        return mixer;
    }
}
