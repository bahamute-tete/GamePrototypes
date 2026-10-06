using System;
using System.Text;
using SlotSystem;
using SlotSystem.Timeline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

public static class SlotInteractionChecks
{
    public static string LastReport { get; private set; }

    [MenuItem("Tools/Slot System/Run Interaction Checks")]
    public static void Run()
    {
        int failures = 0, checks = 0;
        var report = new StringBuilder();
        Action<bool, string> check = (ok, label) =>
        {
            checks++; if (!ok) failures++;
            report.AppendLine((ok ? "PASS " : "FAIL ") + label);
        };
        var originalScene = SceneManager.GetActiveScene();
        bool originalDirty = originalScene.isDirty;
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Slot checks");
        SceneManager.MoveGameObjectToScene(root, scene);
        TimelineAsset timeline = null;
        var director = root.AddComponent<PlayableDirector>();
        director.timeUpdateMode = DirectorUpdateMode.Manual;
        try
        {
            var a = MakeManager("Actor A", root.transform);
            var b = MakeManager("Actor B", root.transform);
            var item = Make("Item", root.transform);
            var old = a.Attach("Hand", item.gameObject, AttachMode.Snap, true);
            var current = b.Attach("Hand", item.gameObject);
            check(!old.IsValid && current.IsValid, "transfer invalidates old handle");
            a.Detach(old);
            a.Detach(current);
            check(item != null && current.IsValid && item.parent == b.GetAnchor("Hand"), "old/wrong owner cannot detach or destroy transferred item");
            check(b.Attach("Hand", item.gameObject) == current, "repeated attach is idempotent");
            b.Release(current);
            check(item != null && item.parent == null, "release keeps object alive");
            item.SetParent(root.transform);
            var anchor = a.GetAnchor("Hand");
            a.slots[0].anchor = null;
            check(a.GetAnchor("Hand") == anchor, "recover nonserialized anchor without rebuilding");
            a.slots[0].boneTransform = null;
            a.slots[0].bonePath = "";
            a.RebindBones();
            check(a.slots[0].boneTransform == a.transform, "empty bone path resolves root");

            item.localPosition = Vector3.left * 4;
            item.localScale = Vector3.one * 2;
            var body = item.gameObject.AddComponent<Rigidbody>();
            var destination = Make("Destination", root.transform);
            destination.position = Vector3.right * 10;
            timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            var track = timeline.CreateTrack<SlotInteractionTrack>();
            var clip = track.CreateClip<SlotInteractionClip>(); clip.start = 1; clip.duration = 2;
            var data = (SlotInteractionClip)clip.asset;
            data.source.kind = SlotTargetKind.World;
            data.destination = TransformTarget(destination);
            director.playableAsset = timeline;
            director.SetGenericBinding(track, item);
            Action<double> sample = t => { director.time = t; director.Evaluate(); SlotInteractionUpdate.EvaluateAfterAnimation(); };
            sample(4);
            check(Near(item.position.x, 10) && item.parent == destination, "direct jump past entire clip follows destination");
            destination.position = Vector3.right * 12; sample(4.2);
            check(Near(item.position.x, 12), "completed transfer follows moving destination through gap");
            sample(0);
            check(Near(item.position.x, -4) && item.parent == root.transform && !body.isKinematic && body.useGravity,
                "backward seek before first clip restores hierarchy pose and physics");
            sample(2); check(Near(item.position.x, 6), "midpoint is independent of previous seeks");
            sample(1); check(Near(item.position.x, 0), "start is exact source endpoint");
            sample(2.2); sample(0);
            check(Near(item.position.x, -4), "forward sampling then backward jump restores source state");
            data.gripPosition = Vector3.right * .5f;
            destination.localScale = Vector3.one * 3;
            sample(4);
            check(Near(item.TransformPoint(data.gripPosition).x, 12) && Near(item.lossyScale.x, 2), "grip offset aligns while preserving world size");
            check(body.isKinematic && !body.useGravity, "timeline suspends dynamic physics");
            data.endMode = SlotInteractionEndMode.RestoreOriginal;
            sample(4); check(Near(item.position.x, -4), "explicit restore end mode");
            data.endMode = SlotInteractionEndMode.FollowTarget;
            data.gripPosition = Vector3.zero;
            data.destination.kind = SlotTargetKind.World;
            data.destination.position = Vector3.right * 7;
            sample(4); destination.position = Vector3.right * 30; sample(4);
            check(Near(item.position.x, 7) && item.parent == null, "World target holds fixed pose independent of moving scene");
            director.Stop();
            check(item.parent == root.transform && Near(item.localPosition.x, -4) && Near(item.localScale.x, 2) && !body.isKinematic,
                "stop restores original parent pose scale and physics");

            // A -> B ownership is restored to the exact original handle on exiting preview.
            var original = a.Attach("Hand", item.gameObject, AttachMode.Snap, true);
            data.mode = SlotInteractionMode.Follow;
            data.destination = SlotTarget(b);
            sample(2);
            check(SlotManager.GetAttachment(item.gameObject)?.Owner == b && !original.IsValid, "follow uses manager ownership");
            director.Stop();
            check(original.IsValid && SlotManager.GetAttachment(item.gameObject) == original, "preview restores original gameplay handle");
            a.Release(original);
            item.SetParent(root.transform);
            item.localPosition = Vector3.left * 4;

            var occupied = Make("Occupied", root.transform);
            var occupiedHandle = b.Attach("Hand", occupied.gameObject, AttachMode.Snap, true);
            sample(2);
            check(occupiedHandle.IsValid && SlotManager.GetAttachment(item.gameObject) == null && Near(item.position.x, -4),
                "Single occupancy refuses conflicting item without destroying occupant");
            director.Stop(); b.Release(occupiedHandle);

            // Sequential clips: select last completed through gaps, active clip takes priority.
            data.mode = SlotInteractionMode.Transfer;
            data.destination = TransformTarget(destination);
            destination.position = Vector3.right * 10;
            var follow = track.CreateClip<SlotInteractionClip>(); follow.start = 5; follow.duration = 1;
            var followData = (SlotInteractionClip)follow.asset;
            followData.mode = SlotInteractionMode.Follow;
            followData.destination = SlotTarget(b);
            b.transform.position = Vector3.right * 20;
            sample(7); check(item.parent == b.GetAnchor("Hand") && Near(item.position.x, 20), "jump beyond later follow selects latest completed clip");
            sample(4); check(item.parent == destination && Near(item.position.x, 10), "backward seek to earlier gap selects preceding transfer");
            sample(2); check(item.parent == null && Near(item.position.x, 5), "backward seek into transfer releases later owner");
            sample(5.5); check(item.parent == b.GetAnchor("Hand"), "follow clip attaches immediately");
            director.Stop();
            check(SlotManager.GetAttachment(item.gameObject) == null, "stop clears temporary ownership");

            // Real Spline + Animation tracks evaluated alongside an earlier interaction track.
            timeline.DeleteClip(follow);
            data.destination = SlotTarget(a);
            var hand = Make("AnimatedHand", a.transform);
            a.slots[0].boneTransform = hand;
            a.RebuildAnchors();
            var splineTrack = timeline.CreateTrack<SplineCurveMoveTrack>();
            var splineClip = splineTrack.CreateClip<SplineCurveMoveClip>(); splineClip.duration = 4;
            var splineData = (SplineCurveMoveClip)splineClip.asset;
            splineData.pathSpace = SplinePathSpace.World;
            splineData.Template.ApplyRotation = false;
            splineData.Template.Spline.ControlPoints.Clear();
            splineData.Template.Spline.ControlRotations.Clear();
            splineData.Template.Spline.AddPoint(Vector3.zero);
            splineData.Template.Spline.AddPoint(Vector3.right * 8);
            director.SetGenericBinding(splineTrack, a.transform);
            var animationTrack = timeline.CreateTrack<AnimationTrack>();
            var animation = new AnimationClip();
            animation.SetCurve("AnimatedHand", typeof(Transform), "m_LocalPosition.y", AnimationCurve.Linear(0, 2, 4, 4));
            var animationClip = animationTrack.CreateClip<AnimationPlayableAsset>(); animationClip.duration = 4;
            ((AnimationPlayableAsset)animationClip.asset).clip = animation;
            director.SetGenericBinding(animationTrack, a.gameObject.AddComponent<Animator>());
            sample(2);
            check(Near(a.transform.position.x, 4), "Spline track moves actor root");
            check(Near(hand.localPosition.y, 3), "Animation track updates hand bone");
            check((item.position - a.GetAnchor("Hand").position * .5f).sqrMagnitude < .00001f,
                "transfer samples current spline root and animated hand after graph evaluation");
            sample(3.5);
            check((item.position - a.GetAnchor("Hand").position).sqrMagnitude < .00001f,
                "completed transfer follows animated hand on moving spline actor");
            sample(2); var firstPose = item.position;
            sample(0); sample(3.9); sample(2);
            check((item.position - firstPose).sqrMagnitude < .00001f, "combined spline animation and transfer seek deterministically");
            director.RebuildGraph(); sample(2);
            director.Stop();
            check(item.parent == root.transform && Near(item.localPosition.x, -4), "graph rebuild preserves authored restoration baseline");
            Object.DestroyImmediate(animation);

            timeline.DeleteTrack(animationTrack);
            timeline.DeleteTrack(splineTrack);
            data.mode = SlotInteractionMode.Follow;
            data.destination = SlotTarget(a);
            clip.start = 0; clip.duration = 1;
            var next = track.CreateClip<SlotInteractionClip>(); next.start = 2; next.duration = 1;
            var nextData = (SlotInteractionClip)next.asset;
            nextData.mode = SlotInteractionMode.Follow; nextData.destination = SlotTarget(b);
            var secondItem = Make("Second item", root.transform);
            var secondTrack = timeline.CreateTrack<SlotInteractionTrack>();
            director.SetGenericBinding(secondTrack, secondItem);
            var secondFirst = secondTrack.CreateClip<SlotInteractionClip>(); secondFirst.duration = 1;
            var secondFirstData = (SlotInteractionClip)secondFirst.asset;
            secondFirstData.mode = SlotInteractionMode.Follow; secondFirstData.destination = SlotTarget(b);
            var secondNext = secondTrack.CreateClip<SlotInteractionClip>(); secondNext.start = 2; secondNext.duration = 1;
            var secondNextData = (SlotInteractionClip)secondNext.asset;
            secondNextData.mode = SlotInteractionMode.Follow; secondNextData.destination = SlotTarget(a);
            sample(.5);
            check(item.parent == a.GetAnchor("Hand") && secondItem.parent == b.GetAnchor("Hand"), "two props initially occupy separate hands");
            sample(2.5);
            check(item.parent == b.GetAnchor("Hand") && secondItem.parent == a.GetAnchor("Hand"), "two props swap Single slots in one evaluation");
            sample(.5);
            check(item.parent == a.GetAnchor("Hand") && secondItem.parent == b.GetAnchor("Hand"), "reverse seek swaps both props back deterministically");
            director.Stop();
            check(SlotManager.GetAttachment(item.gameObject) == null && SlotManager.GetAttachment(secondItem.gameObject) == null,
                "multi-item stop releases all temporary ownership");
        }
        catch (Exception e) { failures++; report.AppendLine("EXCEPTION " + e); }
        finally
        {
            director.Stop();
            if (timeline != null)
            {
                foreach (var track in timeline.GetOutputTracks())
                    foreach (var clip in track.GetClips()) Object.DestroyImmediate(clip.asset);
                Object.DestroyImmediate(timeline);
            }
            EditorSceneManager.ClosePreviewScene(scene);
        }
        check(originalScene == SceneManager.GetActiveScene() && originalScene.isDirty == originalDirty, "user scene unchanged");
        LastReport = $"{checks - failures}/{checks} checks passed\n" + report;
        if (failures == 0) Debug.Log(LastReport); else Debug.LogError(LastReport);
    }

    private static bool Near(float a, float b) => Mathf.Abs(a - b) < .001f;
    private static Transform Make(string name, Transform parent)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
    }
    private static SlotManager MakeManager(string name, Transform root)
    {
        var manager = Make(name, root).gameObject.AddComponent<SlotManager>();
        manager.skeletonRoot = manager.transform;
        manager.slots.Add(new SlotDefinition { slotId = "Hand", boneTransform = manager.transform });
        manager.RebuildAnchors(); return manager;
    }
    private static SlotPoseTarget TransformTarget(Transform target) => new SlotPoseTarget
    { kind = SlotTargetKind.Transform, target = new ExposedReference<Transform> { defaultValue = target } };
    private static SlotPoseTarget SlotTarget(SlotManager manager) => new SlotPoseTarget
    { kind = SlotTargetKind.Slot, slotId = "Hand", manager = new ExposedReference<SlotManager> { defaultValue = manager } };
}
