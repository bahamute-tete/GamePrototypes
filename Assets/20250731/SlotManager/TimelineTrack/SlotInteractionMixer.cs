using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SlotSystem.Timeline
{
    /// <summary>One item, one writer. State is selected from absolute time, never playback history.</summary>
    public sealed class SlotInteractionMixer : PlayableBehaviour
    {
        private static readonly Dictionary<Transform, SlotInteractionMixer> Writers = new Dictionary<Transform, SlotInteractionMixer>();
        private PlayableGraph graph;
        private PlayableDirector director;
        private Transform item;
        private TimelineClip[] clips;
        private SlotInteractionTrack track;
        private Transform originalParent;
        private Vector3 originalPosition, originalScale, worldScale;
        private Quaternion originalRotation;
        private SlotAttachment originalAttachment;
        private Rigidbody body;
        private bool originalKinematic, originalGravity;
        private bool configured, driven, warned;

        internal void Configure(PlayableGraph graph, PlayableDirector director, Transform item,
            TimelineClip[] clips, SlotInteractionTrack track)
        {
            if (director == null || item == null) return;
            if (Writers.TryGetValue(item, out var previous) &&
                (!previous.graph.IsValid() || previous.director == director && !previous.graph.Equals(graph)))
                previous.Finish(false);
            if (Writers.ContainsKey(item))
            {
                Debug.LogError("Slot Interaction: 同一道具不能同时绑定多条交互轨道。", item);
                return;
            }
            // Spline tracks move the character root; the interaction track owns the prop.
            if (director.playableAsset is TimelineAsset timeline)
                foreach (var other in timeline.GetOutputTracks())
                    if (!other.mutedInHierarchy && other is SplineCurveMoveTrack && director.GetGenericBinding(other) == item)
                    {
                        Debug.LogError("Slot Interaction: 道具同时绑定了 Spline 轨道，请将 Spline 绑定到角色根节点。", item);
                        return;
                    }
            this.graph = graph; this.director = director; this.item = item; this.clips = clips; this.track = track;
            originalParent = item.parent; originalPosition = item.localPosition;
            originalRotation = item.localRotation; originalScale = item.localScale; worldScale = item.lossyScale;
            originalAttachment = SlotManager.GetAttachment(item.gameObject);
            body = item.GetComponent<Rigidbody>();
            if (body != null) { originalKinematic = body.isKinematic; originalGravity = body.useGravity; }
            Writers.Add(item, this);
            configured = true;
            director.stopped += OnDirectorStopped;
            SlotInteractionUpdate.Register(this);
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
            => SlotInteractionUpdate.EvaluateAfterAnimation();

        private TimelineClip SelectClip(double time)
        {
            TimelineClip selected = null;
            bool active = false;
            foreach (var clip in clips)
            {
                if (time < clip.start) continue;
                bool inside = time < clip.end;
                if (inside || !active && (selected == null || clip.end >= selected.end)) selected = clip;
                active |= inside;
            }
            return selected;
        }

        // Release changing reservations across all tracks before any track claims its next slot.
        // Otherwise directly seeking across two props swapping hands depends on track order.
        internal void PrepareCurrentTime()
        {
            if (!configured || !driven || item == null || director == null || !graph.IsValid()) return;
            var attachment = SlotManager.GetAttachment(item.gameObject);
            if (attachment == null) return;
            var selected = SelectClip(director.time);
            var data = selected != null ? selected.asset as SlotInteractionClip : null;
            bool after = selected != null && director.time >= selected.end;
            bool follow = data != null && (data.mode == SlotInteractionMode.Follow || after) &&
                !(after && data.endMode == SlotInteractionEndMode.RestoreOriginal);
            var target = follow ? data.destination : null;
            if (target != null && target.kind == SlotTargetKind.Slot &&
                target.manager.Resolve(director) == attachment.Owner && target.slotId == attachment.SlotId) return;
            attachment.Owner.Release(attachment);
        }

        public void EvaluateCurrentTime()
        {
            if (!configured || item == null || director == null || !graph.IsValid()) return;
            double time = director.time;
            // In overlaps, the latest start wins. On equal starts, the last clip wins.
            TimelineClip selected = SelectClip(time);
            if (selected == null) { Restore(); return; }
            var data = selected.asset as SlotInteractionClip;
            if (data == null) { Restore(); return; }
            bool after = time >= selected.end;
            if (after && data.endMode == SlotInteractionEndMode.RestoreOriginal) { Restore(); return; }
            if (DependsOnItem(data.destination) || data.mode == SlotInteractionMode.Transfer && DependsOnItem(data.source))
            { Invalid("起点或终点不能位于任何正被交互轨道控制的道具层级中。"); return; }
            if (data.destination == null || !data.destination.TryGetPose(director, out var destination, out var destinationRotation))
            { Invalid("目标挂点无法解析，请检查引用、Slot ID 并 Rebuild Anchors。"); return; }

            float progress = selected.duration > 0 ? Mathf.Clamp01((float)((time - selected.start) / selected.duration)) : 1;
            bool follow = data.mode == SlotInteractionMode.Follow || after || progress >= 1;
            Vector3 position = destination;
            Quaternion rotation = destinationRotation;
            if (!follow)
            {
                if (data.source == null || !data.source.TryGetPose(director, out var source, out var sourceRotation))
                { Invalid("起点无法解析，请检查引用与挂点。"); return; }
                float weight = progress <= 0 ? 0 : progress >= 1 ? 1 :
                    Mathf.Clamp01(data.ease != null && data.ease.length > 0 ? data.ease.Evaluate(progress) : progress);
                position = Vector3.Lerp(source, destination, weight) + Vector3.up * (Mathf.Max(0, data.arcHeight) * Mathf.Sin(Mathf.PI * progress));
                rotation = Quaternion.Slerp(sourceRotation, destinationRotation, weight);
            }
            if (!SetParent(follow ? data.destination : null)) { Invalid("挂点已被占用或目标层级形成循环。"); return; }
            driven = true;
            if (body != null) { body.isKinematic = true; body.useGravity = false; }
            if (track.preserveWorldScale) SetWorldScale();
            else item.localScale = originalScale;
            item.rotation = rotation * Quaternion.Inverse(Quaternion.Euler(data.gripEulerAngles));
            // TransformVector accounts for the current hierarchy scale and rotation of the item.
            item.position = position - item.TransformVector(data.gripPosition);
        }

        private bool SetParent(SlotPoseTarget target)
        {
            Transform parent = null;
            if (target != null && !target.TryResolve(director, out parent, out _, out _)) return false;
            if (parent != null && (parent == item || parent.IsChildOf(item))) return false;
            if (target != null && target.kind == SlotTargetKind.Slot)
            {
                var manager = target.manager.Resolve(director);
                // Refuse occupied Single slots rather than deleting another item's attachment.
                var attachment = manager.Attach(target.slotId, item.gameObject, AttachMode.PreserveWorld, false, false);
                if (attachment == null) return false;
            }
            else
            {
                var attachment = SlotManager.GetAttachment(item.gameObject);
                if (attachment != null) attachment.Owner.Release(attachment);
                if (item.parent != parent) item.SetParent(parent, true);
            }
            return true;
        }

        private void SetWorldScale()
        {
            // Exact for uniform scales. Rotated non-uniform ancestors can introduce unavoidable shear.
            item.localScale = Vector3.one;
            Vector3 inherited = item.lossyScale;
            item.localScale = new Vector3(Divide(worldScale.x, inherited.x), Divide(worldScale.y, inherited.y), Divide(worldScale.z, inherited.z));
        }
        private static float Divide(float value, float scale) => Mathf.Abs(scale) > 1e-6f ? value / scale : 1;

        private void Invalid(string message)
        {
            Restore();
            if (warned) return;
            warned = true;
            Debug.LogWarning("Slot Interaction: " + message, item);
        }

        private void Restore()
        {
            if (!driven || item == null) return;
            var attachment = SlotManager.GetAttachment(item.gameObject);
            if (attachment != null) attachment.Owner.Release(attachment);
            item.SetParent(originalParent, false);
            item.localPosition = originalPosition; item.localRotation = originalRotation; item.localScale = originalScale;
            if (originalAttachment != null && originalAttachment.Owner != null)
                originalAttachment.Owner.RestoreAttachment(originalAttachment);
            if (body != null) { body.isKinematic = originalKinematic; body.useGravity = originalGravity; }
            driven = false;
        }

        private bool DependsOnItem(SlotPoseTarget target)
        {
            if (target == null || !target.TryResolve(director, out var parent, out _, out _) || parent == null) return false;
            foreach (var controlled in Writers.Keys)
                if (controlled != null && (parent == controlled || parent.IsChildOf(controlled))) return true;
            return false;
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            Finish(Application.isPlaying && track != null && track.keepResultOnStop);
        }

        private void OnDirectorStopped(PlayableDirector stopped)
            => Finish(Application.isPlaying && track != null && track.keepResultOnStop);

        internal void RestoreAndUnregister() => Finish(false);

        private void Finish(bool keep)
        {
            if (!configured) return;
            if (!keep) Restore();
            // Kept results retain kinematic physics and attachment; caller explicitly releases when needed.
            if (!ReferenceEquals(item, null) && Writers.TryGetValue(item, out var writer) && writer == this) Writers.Remove(item);
            if (director != null) director.stopped -= OnDirectorStopped;
            configured = false;
            SlotInteractionUpdate.Unregister(this);
        }
    }
}
