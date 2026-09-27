using System.Collections.Generic;
using UnityEngine.Playables;

// One coordinator per playable graph, not per Unity frame. Repeated Evaluate()
// calls in a single frame must each reach the renderers before returning.
internal sealed class DissolveTimelineBatch
{
    static readonly Dictionary<PlayableGraph, DissolveTimelineBatch> Batches = new Dictionary<PlayableGraph, DissolveTimelineBatch>();
    readonly PlayableGraph graph;
    readonly PlayableDirector director;
    readonly HashSet<DissolveMixerBehaviour> mixers = new HashSet<DissolveMixerBehaviour>();
    readonly HashSet<DissolveMixerBehaviour> completed = new HashSet<DissolveMixerBehaviour>();
    readonly HashSet<DissolveController> pending = new HashSet<DissolveController>();
    ulong frame;
    bool hasFrame;

    DissolveTimelineBatch(PlayableGraph graph, PlayableDirector director)
    {
        this.graph = graph; this.director = director;
        if (director) director.stopped += OnStopped;
    }
    internal static DissolveTimelineBatch Register(PlayableGraph graph, DissolveMixerBehaviour mixer)
    {
        if (!Batches.TryGetValue(graph, out var batch))
        {
            batch = new DissolveTimelineBatch(graph, mixer.director);
            Batches.Add(graph, batch);
        }
        batch.mixers.Add(mixer);
        return batch;
    }
    internal void Begin(ulong frameId)
    {
        if (hasFrame && frame == frameId) return;
        // Defensive drain if an externally modified graph skipped an output.
        Flush();
        frame = frameId; hasFrame = true; completed.Clear();
    }
    internal void Write(DissolveController controller, DissolveParameter parameter, float value)
    {
        controller.SetTimelineParameter(parameter, value);
        pending.Add(controller);
    }
    internal void Complete(DissolveMixerBehaviour mixer)
    {
        completed.Add(mixer);
        if (completed.Count == mixers.Count) Flush();
    }
    void Flush()
    {
        try { foreach (var controller in pending) if (controller) controller.FlushTimelineParameters(); }
        finally { pending.Clear(); }
    }
    void OnStopped(PlayableDirector stopped)
    {
        // Restore all parameters before the one visibility/property update.
        foreach (var mixer in mixers) mixer.Restore();
        Flush();
    }
    internal void Remove(DissolveMixerBehaviour mixer)
    {
        mixer.Restore();
        mixers.Remove(mixer); completed.Remove(mixer);
        if (mixers.Count != 0) return;
        try { Flush(); }
        finally
        {
            if (director) director.stopped -= OnStopped;
            Batches.Remove(graph);
        }
    }
}
