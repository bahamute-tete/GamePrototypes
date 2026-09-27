using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.ComponentModel;

public class DissolveMixerBehaviour : PlayableBehaviour
{
    public TimelineClip[] clips;
    public DissolveTrack sourceTrack;
    public DissolveParameter parameter;
    public DissolveController controller;
    public PlayableDirector director;
    public bool conflict;
    public float original;
    bool written;
    internal DissolveTimelineBatch batch;
    internal void Restore()
    {
        if (written && controller) batch.Write(controller, parameter, original);
        written = false;
    }
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        batch.Begin(info.frameId);
        try { EvaluateFrame(playable); }
        finally { batch.Complete(this); }
    }
    void EvaluateFrame(Playable playable)
    {
        // Track settings remain live even when the graph already exists.
        if (sourceTrack)
        {
            var binding = director ? director.GetGenericBinding(sourceTrack) as DissolveController : controller;
            if (sourceTrack.parameter != parameter || binding != controller)
            {
                Restore();
                controller = binding;
                parameter = sourceTrack.parameter;
                original = controller ? controller.GetParameter(parameter) : 0;
            }
            conflict = sourceTrack.HasConflict(director);
        }
        if (!controller || conflict || !controller.SupportsParameter(parameter))
        {
            Restore();
            return;
        }
        double time = director ? director.time : playable.GetTime();
        float total = 0, value = 0;
        int previous = -1, active = -1;
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            if (clip.start <= time && time < clip.end) active = i;
            if (clip.end <= time && (previous < 0 || clip.end >= clips[previous].end)) previous = i;
            float weight = playable.GetInputWeight(i);
            if (weight <= 0 || time < clip.start || time >= clip.end) continue;
            var b = ((ScriptPlayable<DissolveBehaviour>)playable.GetInput(i)).GetBehaviour();
            value += b.Evaluate((float)((time-clip.start)/clip.duration))*weight;
            total += weight;
        }
        if (total > 0) value /= total;
        else if (active >= 0) value = ((DissolveClip)clips[active].asset).template.Evaluate((float)((time-clips[active].start)/clips[active].duration));
        else if (previous >= 0)
        {
            var b = ((DissolveClip)clips[previous].asset).template;
            value = b.endBehaviour == DissolveEndBehaviour.HoldEnd ? b.Evaluate(1) : original;
        }
        else value = original;
        batch.Write(controller, parameter, value);
        written = true;
    }
    public override void OnPlayableDestroy(Playable playable)
    {
        batch?.Remove(this);
    }
}
[TrackColor(.85f,.45f,.65f), TrackClipType(typeof(DissolveClip)), TrackBindingType(typeof(DissolveController))]
[DisplayName("Custom/Dissolve Track")]
public class DissolveTrack : TrackAsset
{
    public DissolveParameter parameter;
    [HideInInspector] public bool parameterConfigured;
    public bool HasConflict(PlayableDirector director)
    {
        if (!director || !(director.playableAsset is TimelineAsset timeline)) return false;
        var binding = director.GetGenericBinding(this);
        if (!binding) return false;
        foreach (var output in timeline.GetOutputTracks())
            if (output is DissolveTrack other && other != this && !other.mutedInHierarchy && other.parameter == parameter && director.GetGenericBinding(other) == binding) return true;
        return false;
    }
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        var playable = ScriptPlayable<DissolveMixerBehaviour>.Create(graph, inputCount);
        var b = playable.GetBehaviour();
        b.clips = new List<TimelineClip>(GetClips()).ToArray();
        b.sourceTrack = this;
        b.parameter = parameter;
        b.director = go.GetComponent<PlayableDirector>();
        b.controller = b.director ? b.director.GetGenericBinding(this) as DissolveController : null;
        b.original = b.controller ? b.controller.GetParameter(parameter) : 0;
        b.conflict = HasConflict(b.director);
        b.batch = DissolveTimelineBatch.Register(graph, b);
        return playable;
    }
    public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
    {
        var c = director.GetGenericBinding(this) as DissolveController;
        if (c) driver.AddFromName<DissolveController>(c.gameObject, DissolveController.ParameterFieldName(parameter));
        base.GatherProperties(director, driver);
    }
}

