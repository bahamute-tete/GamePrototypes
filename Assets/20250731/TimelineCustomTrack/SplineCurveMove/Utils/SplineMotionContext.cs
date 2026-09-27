using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Pure, time-addressable pose queries for spline tracks in one graph. Reference dependencies
/// are evaluated recursively, without writing upstream Transforms or seeking the Director.
/// External animation/physics must finish before this graph evaluates.
/// </summary>
internal sealed class SplineMotionContext
{
    private static readonly Dictionary<PlayableGraph, SplineMotionContext> Contexts =
        new Dictionary<PlayableGraph, SplineMotionContext>();
    private readonly List<Entry> entries = new List<Entry>();
    private readonly HashSet<Entry> visiting = new HashSet<Entry>();
    private readonly Dictionary<(Entry, double), Result?> samples = new Dictionary<(Entry, double), Result?>();
    private bool reportedError;
    internal sealed class Entry
    {
        internal Transform target;
        internal PlayableDirector director;
        internal TimelineClip[] clips;
        internal Vector3 initialPosition;
        internal Quaternion initialRotation;
        internal Vector3 initialScale;
        internal readonly Dictionary<Transform, Matrix4x4> frameMatrices = new Dictionary<Transform, Matrix4x4>();
        internal readonly Dictionary<Transform, Quaternion> frameRotations = new Dictionary<Transform, Quaternion>();
        internal void CaptureFrames()
        {
            foreach (var clip in clips)
            {
                var move = clip.asset as SplineCurveMoveClip;
                if (move == null || move.pathSpace == SplinePathSpace.World) continue;
                var frame = move.referenceFrame.Resolve(director);
                if (frame == null && move.Template.UseLocalSpace) frame = target.parent;
                if (frame == null) continue;
                frameMatrices[frame] = frame.localToWorldMatrix;
                frameRotations[frame] = frame.rotation;
            }
        }
    }
    internal struct Result
    {
        internal Vector3 position;
        internal Quaternion rotation;
        internal bool rotate;
    }

    internal static SplineMotionContext Register(PlayableGraph graph, Entry entry)
    {
        if (!Contexts.TryGetValue(graph, out var context))
            Contexts.Add(graph, context = new SplineMotionContext());
        context.entries.Add(entry);
        return context;
    }
    internal void Remove(PlayableGraph graph, Entry entry)
    {
        entries.Remove(entry);
        if (entries.Count == 0) Contexts.Remove(graph);
    }
    internal bool TryEvaluate(Entry entry, double time, out Result result)
    {
        visiting.Clear();
        samples.Clear();
        try { return Evaluate(entry, time, out result); }
        catch (InvalidOperationException e)
        {
            if (!reportedError) Debug.LogError("SplineMove: " + e.Message, entry.target);
            reportedError = true;
            result = default;
            return false;
        }
    }

    private bool Evaluate(Entry entry, double time, out Result result)
    {
        var key = (entry, time);
        if (samples.TryGetValue(key, out var cached))
        {
            result = cached.GetValueOrDefault();
            return cached.HasValue;
        }
        bool valid = EvaluateCore(entry, time, out result);
        samples[key] = valid ? result : (Result?)null;
        return valid;
    }

    private bool EvaluateCore(Entry entry, double time, out Result result)
    {
        if (!visiting.Add(entry)) throw new InvalidOperationException("参考系存在循环依赖，请检查载具和角色绑定。");
        try
        {
            result = default;
            float total = 0, rotationWeight = 0;
            Vector3 baselinePosition = Vector3.zero;
            Quaternion baselineRotation = entry.initialRotation;
            foreach (var clip in entry.clips)
            {
                bool active = time >= clip.start && time < clip.end;
                var endMode = EndMode(clip);
                bool extrapolated = endMode == SplineEndMode.LegacyNearest && clip.IsExtrapolatedTime(time);
                if (!active && !extrapolated) continue;
                float weight = clip.EvaluateMixIn(time) * clip.EvaluateMixOut(time);
                if (extrapolated) weight = 1;
                if (weight <= 0 || !Sample(entry, clip, time, Normalized(clip, time), out var sample)) continue;
                // Missing fade weight stays relative to the same frame, rather than a frozen world point.
                var baseline = Baseline(entry, clip, time);
                baselinePosition += weight * baseline.position;
                baselineRotation = total == 0 ? baseline.rotation : Quaternion.Slerp(baselineRotation, baseline.rotation, weight / (total + weight));
                result.position += weight * sample.position;
                if (sample.rotate)
                {
                    result.rotation = rotationWeight == 0 ? sample.rotation :
                        Quaternion.Slerp(result.rotation, sample.rotation, weight / (rotationWeight + weight));
                    rotationWeight += weight;
                }
                total += weight;
            }
            if (total > 0)
            {
                // Normalize overweight overlaps; fill underweight fades in the selected frame.
                if (total > 1) result.position /= total;
                else if (total < 1) result.position += baselinePosition / total * (1 - total);
                result.rotate = rotationWeight > 0;
                if (result.rotate && rotationWeight < 1)
                    result.rotation = Quaternion.Slerp(baselineRotation, result.rotation, rotationWeight);
                return true;
            }

            TimelineClip selected = null;
            double closest = double.MaxValue;
            foreach (var clip in entry.clips)
            {
                if (EndMode(clip) == SplineEndMode.LegacyNearest)
                {
                    double distance = Math.Min(Math.Abs(time - clip.start), Math.Abs(time - clip.end));
                    if (distance < closest) { selected = clip; closest = distance; }
                }
            }
            // Explicit modes keep the last completed clip through gaps, never snap toward a future clip.
            foreach (var clip in entry.clips)
                if (EndMode(clip) != SplineEndMode.LegacyNearest && time >= clip.end &&
                    (selected == null || clip.end >= selected.end)) selected = clip;
            if (selected == null || EndMode(selected) == SplineEndMode.Release) return false;
            bool atEnd = time >= selected.end || Math.Abs(time - selected.end) < Math.Abs(time - selected.start);
            double sampleTime = EndMode(selected) == SplineEndMode.HoldWorld ? selected.end : time;
            return Sample(entry, selected, sampleTime, atEnd ? 1 : 0, out result);
        }
        finally { visiting.Remove(entry); }
    }

    private static float Normalized(TimelineClip clip, double time)
        => clip.duration <= 1e-6 ? 0 : Mathf.Clamp01((float)((clip.ToLocalTime(time) - clip.clipIn) / clip.timeScale / clip.duration));
    private static SplineEndMode EndMode(TimelineClip clip)
        => clip.asset is SplineTransitionClip transition ? transition.endMode :
            ((SplineCurveMoveClip)clip.asset).endMode;

    private Result Baseline(Entry entry, TimelineClip clip, double time)
    {
        Transform frame = clip.asset is SplineCurveMoveClip move ? ResolveFrame(move, entry) : null;
        if (frame == null) return new Result { position = entry.initialPosition, rotation = entry.initialRotation };
        if (!entry.frameMatrices.TryGetValue(frame, out var initial))
        {
            initial = frame.localToWorldMatrix;
            entry.frameMatrices[frame] = initial;
            entry.frameRotations[frame] = frame.rotation;
        }
        Frame(frame, time, out var current, out var rotation);
        return new Result { position = current.MultiplyPoint3x4(initial.inverse.MultiplyPoint3x4(entry.initialPosition)),
            rotation = rotation * Quaternion.Inverse(entry.frameRotations[frame]) * entry.initialRotation };
    }

    private Transform ResolveFrame(SplineCurveMoveClip clip, Entry entry)
    {
        if (clip.pathSpace == SplinePathSpace.World) return null;
        var frame = clip.referenceFrame.Resolve(entry.director);
        if (clip.pathSpace == SplinePathSpace.ReferenceFrame && frame == null)
            throw new InvalidOperationException("指定参考系未绑定：" + clip.name);
        if (frame == null && clip.Template.UseLocalSpace) frame = entry.target.parent;
        return frame;
    }

    private bool Sample(Entry entry, TimelineClip clip, double time, float u, out Result result, bool fullPath = false)
    {
        result = default;
        if (clip.asset is SplineTransitionClip bridge)
        {
            Vector3 p0, p1, v0, v1;
            Quaternion qa, qb;
            if (bridge.autoMatchAdjacentPaths)
            {
                if (!bridge.TryGetAdjacentPaths(clip, out var previous, out var next, out var error))
                    throw new InvalidOperationException(error);
                Endpoint(entry, previous, time, true, out p0, out qa, out v0);
                Endpoint(entry, next, time, false, out p1, out qb, out v1);
            }
            else
            {
                var from = bridge.departure.Resolve(entry.director);
                var to = bridge.arrival.Resolve(entry.director);
                if (from == null || to == null) throw new InvalidOperationException("手动过渡需要出发与到达两个锚点。");
                Frame(from, time, out var a, out qa);
                Frame(to, time, out var b, out qb);
                p0 = a.MultiplyPoint3x4(Vector3.zero);
                p1 = b.MultiplyPoint3x4(Vector3.zero);
                v0 = qa * Vector3.forward * Mathf.Max(0, bridge.departureSpeed);
                v1 = qb * Vector3.forward * Mathf.Max(0, bridge.arrivalSpeed);
            }
            // Cubic Hermite derivative = relative walking speed * duration at each endpoint.
            // Frame translation/rotation velocity is inherited automatically from the moving anchors.
            var m0 = v0 * (float)clip.duration;
            var m1 = v1 * (float)clip.duration;
            float u2 = u * u, u3 = u2 * u;
            float blend = u3 * (10 + u * (-15 + 6 * u));
            var up = Vector3.Slerp(qa * Vector3.up, qb * Vector3.up, blend);
            result.position = (2*u3-3*u2+1)*p0 + (u3-2*u2+u)*m0 + (-2*u3+3*u2)*p1 + (u3-u2)*m1
                + up * (16 * u2 * (1-u) * (1-u) * bridge.arcHeight);
            var tangent = (6*u2-6*u)*p0 + (3*u2-4*u+1)*m0 + (-6*u2+6*u)*p1 + (3*u2-2*u)*m1
                + up * (32*u*(1-u)*(1-2*u)*bridge.arcHeight);
            result.rotation = tangent.sqrMagnitude > 1e-8f && Vector3.Cross(tangent, up).sqrMagnitude > 1e-8f
                ? Quaternion.LookRotation(tangent, up) : Quaternion.Slerp(qa, qb, blend);
            if (bridge.autoMatchAdjacentPaths)
            {
                // Match any authored roll/offset/axis restriction at the joins as well.
                Quaternion aCorrection = EndpointCorrection(m0, qa);
                Quaternion bCorrection = EndpointCorrection(m1, qb);
                result.rotation *= Quaternion.Slerp(aCorrection, bCorrection, blend);
                if (u <= 0) result.rotation = qa;
                if (u >= 1) result.rotation = qb;
            }
            result.rotate = bridge.applyRotation;
            return true;
        }
        var move = clip.asset as SplineCurveMoveClip;
        if (move == null || move.Template.Spline.ControlPoints.Count < 2) return false;
        var behaviour = move.Template;
        if (fullPath) behaviour.SampleAtArcProgress(u);
        else behaviour.SampleAtNormalized(u);
        result.position = behaviour.SampledPosition;
        result.rotation = behaviour.SampledRotation;
        result.rotate = behaviour.ApplyRotation;
        var frame = ResolveFrame(move, entry);
        if (frame != null)
        {
            Frame(frame, time, out var matrix, out var rotation);
            result.position = matrix.MultiplyPoint3x4(result.position);
            result.rotation = TransformPathRotation(matrix, result.rotation, rotation * result.rotation);
        }
        return true;
    }

    private static Quaternion EndpointCorrection(Vector3 tangent, Quaternion orientation)
    {
        Vector3 up = orientation * Vector3.up;
        if (tangent.sqrMagnitude < 1e-8f || Vector3.Cross(tangent, up).sqrMagnitude < 1e-8f) return Quaternion.identity;
        return Quaternion.Inverse(Quaternion.LookRotation(tangent, up)) * orientation;
    }

    private void Endpoint(Entry entry, TimelineClip clip, double time, bool atEnd,
        out Vector3 position, out Quaternion rotation, out Vector3 relativeWorldVelocity)
    {
        var move = (SplineCurveMoveClip)clip.asset;
        var b = move.Template;
        if (b.Spline.ControlPoints.Count < 2 || clip.duration <= 1e-6)
            throw new InvalidOperationException("相邻路径需要至少两个控制点和有效时长。");
        Sample(entry, clip, time, atEnd ? 1 : 0, out var endpoint);
        position = endpoint.position;
        rotation = endpoint.rotate ? endpoint.rotation : entry.initialRotation;
        // Differentiate the actual arc-length/displacement sampler, including pauses, reversal
        // and clamping. Second-order one-sided difference uses only the neighbouring clip's side.
        const float h = .001f;
        float u = atEnd ? 1 : 0;
        float direction = atEnd ? -1 : 1;
        Vector3 p0 = b.Spline.GetPointByArcLength(b.DisplacementCurve.Evaluate(u));
        Vector3 p1 = b.Spline.GetPointByArcLength(b.DisplacementCurve.Evaluate(u + direction*h));
        Vector3 p2 = b.Spline.GetPointByArcLength(b.DisplacementCurve.Evaluate(u + direction*2*h));
        relativeWorldVelocity = direction * (-3*p0 + 4*p1 - p2) / (2*h*(float)clip.duration);
        var frame = ResolveFrame(move, entry);
        if (frame != null)
        {
            Frame(frame, time, out var matrix, out _);
            relativeWorldVelocity = matrix.MultiplyVector(relativeWorldVelocity);
        }
        // Do NOT add the carrier velocity here: dynamic position(time) already contributes it.
        // At either join dP/dtime = dEndpoint/dtime + this relative velocity.
    }

    // Positions include the full reference matrix. Transform the orientation basis with the
    // same matrix, then orthonormalize it: rotation-only multiplication loses scaled tangents.
    internal static Quaternion TransformPathRotation(Matrix4x4 matrix, Quaternion local, Quaternion fallback)
    {
        Vector3 forward = matrix.MultiplyVector(local * Vector3.forward);
        if (forward.sqrMagnitude < 1e-12f) return fallback;
        forward.Normalize();
        Vector3 up = Vector3.ProjectOnPlane(matrix.MultiplyVector(local * Vector3.up), forward);
        if (up.sqrMagnitude < 1e-12f)
            up = Vector3.ProjectOnPlane(Mathf.Abs(forward.y) < .95f ? Vector3.up : Vector3.right, forward);
        return Quaternion.LookRotation(forward, up.normalized);
    }

    private void Frame(Transform transform, double time, out Matrix4x4 matrix, out Quaternion rotation)
    {
        Entry found = null;
        foreach (var candidate in entries)
            if (candidate.target == transform)
            {
                if (found != null) throw new InvalidOperationException("同一物体只能绑定一个 Spline Move Track。");
                found = candidate;
            }
        if (found != null)
        {
            if (!Evaluate(found, time, out var sample))
            {
                matrix = Matrix4x4.TRS(found.initialPosition, found.initialRotation, found.initialScale);
                rotation = found.initialRotation;
                return;
            }
            rotation = sample.rotate ? sample.rotation : found.initialRotation;
            matrix = Matrix4x4.TRS(sample.position, rotation, found.initialScale);
            return;
        }
        if (transform.parent != null)
        {
            Frame(transform.parent, time, out var parentMatrix, out var parentRotation);
            matrix = parentMatrix * Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale);
            rotation = parentRotation * transform.localRotation;
        }
        else { matrix = transform.localToWorldMatrix; rotation = transform.rotation; }
    }

#if UNITY_EDITOR
    internal static bool Preview(PlayableDirector director, TimelineClip clip, Vector3[] points,
        out SplineScenePreview.Data data)
    {
        data = new SplineScenePreview.Data();
        if (director == null || clip == null || points == null || points.Length < 2)
        { data.message = "请选择场景中 Director 的移动 Clip。"; return false; }
        var track = clip.GetParentTrack() as SplineCurveMoveTrack;
        if (track == null || track.mutedInHierarchy)
        { data.message = "轨道已静音或不是曲线移动轨道。"; return false; }
        var target = director.GetGenericBinding(track) as Transform;
        data.target = target;
        if (target == null) { data.message = "请先为轨道绑定目标 Transform。"; return false; }

        // A private query context shares captured baselines, but never the runtime traversal/cache.
        var preview = new SplineMotionContext();
        bool live = director.playableGraph.IsValid() && Contexts.TryGetValue(director.playableGraph, out _);
        if (live) preview.entries.AddRange(Contexts[director.playableGraph].entries);
        else if (director.playableAsset is TimelineAsset timeline)
        {
            foreach (var output in timeline.GetOutputTracks())
            {
                if (!(output is SplineCurveMoveTrack) || output.mutedInHierarchy) continue;
                var bound = director.GetGenericBinding(output) as Transform;
                if (bound == null) continue;
                var clips = new List<TimelineClip>(output.GetClips()).ToArray();
                var item = new Entry { target = bound, director = director, clips = clips,
                    initialPosition = bound.position, initialRotation = bound.rotation, initialScale = bound.lossyScale };
                item.CaptureFrames();
                preview.entries.Add(item);
            }
        }
        Entry entry = null;
        foreach (var candidate in preview.entries)
            if (candidate.director == director && Array.IndexOf(candidate.clips, clip) >= 0) { entry = candidate; break; }
        if (entry == null) { data.message = "轨道图尚未更新，请拖动 Timeline 后重试。"; return false; }
        try
        {
            // Sample all points at ONE reference-frame time: this is the deck route, not a world trail.
            preview.visiting.Add(entry);
            for (int i = 0; i < points.Length; i++)
            {
                if (!preview.Sample(entry, clip, director.time, i / (float)(points.Length - 1), out var sample, true))
                { data.message = "路径数据不足，请先写入至少两个控制点。"; return false; }
                points[i] = sample.position;
            }
            preview.visiting.Clear();
            preview.samples.Clear();
            if (live && preview.Evaluate(entry, director.time, out var expected))
            {
                data.hasExpected = true;
                data.position = expected.position;
                data.rotation = expected.rotation;
                data.hasRotation = expected.rotate;
            }
            else data.message = live ? "当前时间轨道不写入姿态（未开始或已交接）。" : "播放或拖动 Timeline 后显示当前预期位置。";
            return true;
        }
        catch (InvalidOperationException e) { data.message = e.Message; return false; }
    }
#endif
}

#if UNITY_EDITOR
/// <summary>Read-only Scene preview API. Does not evaluate the Director, move targets or fire events.</summary>
public static class SplineScenePreview
{
    public struct Data
    {
        public Transform target;
        public bool hasExpected, hasRotation;
        public Vector3 position;
        public Quaternion rotation;
        public string message;
    }
    public static bool Sample(PlayableDirector director, TimelineClip clip, Vector3[] points, out Data data)
        => SplineMotionContext.Preview(director, clip, points, out data);
}
#endif
