using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 每个 Clip 按自己的参考系（referenceFrame / target.parent / 世界）转为世界姿态后混合。
/// 同一 Graph 的曲线参考系由 SplineMotionContext 按依赖查询，不依赖轨道排列。
/// 外部动画/物理参考系仍须在此 Graph 前完成更新。
/// </summary>
public class SplineCurveMoveMixerBehaviour : PlayableBehaviour
{
    private SplineMotionContext _context;
    private SplineMotionContext.Entry _entry;
    internal void Configure(PlayableGraph graph, PlayableDirector director, Transform target, TimelineClip[] clips)
    {
        _entry = new SplineMotionContext.Entry { director = director, target = target, clips = clips,
            initialPosition = target.position, initialRotation = target.rotation, initialScale = target.lossyScale };
        _entry.CaptureFrames();
        _context = SplineMotionContext.Register(graph, _entry);
    }
    public override void OnPlayableDestroy(Playable playable)
    {
        if (_context != null) _context.Remove(playable.GetGraph(), _entry);
        _context = null;
    }
    private Transform _cachedTarget;
    private Vector3 _cachedDefaultPos;
    private Quaternion _cachedDefaultRot;
    private bool _cachedInited;

    public override void OnPlayableCreate(Playable playable) { _cachedInited = false; }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var target = playerData as Transform;
        if (target == null) return;

        if (_context != null)
        {
            if (_context.TryEvaluate(_entry, _entry.director.time, out var pose))
            {
                target.position = pose.position;
                if (pose.rotate) target.rotation = pose.rotation;
            }
            DispatchEvents(playable, target);
            return;
        }

        int inputCount = playable.GetInputCount();
        if (inputCount == 0) return;

        if (!_cachedInited || _cachedTarget != target)
        {
            _cachedTarget = target;
            _cachedDefaultPos = target.position;
            _cachedDefaultRot = target.rotation;
            _cachedInited = true;
        }

        // 所有输入在世界空间加权混合。
        float totalWeight = 0f;
        Vector3 blendedPos = Vector3.zero;
        Quaternion blendedRot = Quaternion.identity;
        bool firstRotationApplied = false;

        bool anyApplyRotation = false;

        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0f) continue;

            var inputPlayable = (ScriptPlayable<SplineCurveMoveBehaviour>)playable.GetInput(i);
            var behaviour = inputPlayable.GetBehaviour();
            if (behaviour == null || !behaviour.HasValidSample) continue;

            GetWorldSample(behaviour, target, out Vector3 sampledPos, out Quaternion sampledRot);

            blendedPos += sampledPos * weight;

            if (behaviour.ApplyRotation)
            {
                if (!firstRotationApplied)
                {
                    blendedRot = sampledRot;
                    firstRotationApplied = true;
                }
                else
                {
                    float t = weight / (totalWeight + weight);
                    blendedRot = Quaternion.Slerp(blendedRot, sampledRot, t);
                }
                anyApplyRotation = true;
            }

            totalWeight += weight;
        }

        if (totalWeight <= 0f)
        {
            // 所有 input 权重为 0：滑块在 Track 上 Clip 之外。
            // 主动 snap 到最近 Clip 边界，避免物体停在"weight 刚归零前的最后近似位置"。
            SnapToBoundary(playable, target);
            DispatchEvents(playable, target);
            return;
        }

        // 默认姿态与每个输入均为世界坐标，淡入淡出不再混用局部值。
        if (totalWeight < 1f)
        {
            float remain = 1f - totalWeight;
            blendedPos = blendedPos + _cachedDefaultPos * remain;
            if (anyApplyRotation)
                blendedRot = Quaternion.Slerp(_cachedDefaultRot, blendedRot, totalWeight);
        }

        target.position = blendedPos;
        if (anyApplyRotation) target.rotation = blendedRot;

        DispatchEvents(playable, target);
    }

    /// <summary>
    /// 当所有 input weight 为 0（滑块在 Clip 区域之外）时，
    /// 找到 time 最接近 Clip 起点或终点的 input，主动让它在精确边界位置（s=0 或 s=1）采样，
    /// 并把采样结果写入 Transform。
    ///
    /// 避免滑块快速越过 Clip 后物体停在"weight 归零前的近似位置"导致的不精确。
    ///
    /// Phase 1：如果最近 Clip 设置了 refFrame，采样结果先用 refFrame 变换到世界后再写入。
    /// </summary>
    private void SnapToBoundary(Playable playable, Transform target)
    {
        int inputCount = playable.GetInputCount();
        SplineCurveMoveBehaviour bestBehaviour = null;
        bool bestAtEnd = false;

        double smallestDist = double.MaxValue;

        for (int i = 0; i < inputCount; i++)
        {
            var inputPlayable = (ScriptPlayable<SplineCurveMoveBehaviour>)playable.GetInput(i);
            var b = inputPlayable.GetBehaviour();
            if (b == null) continue;
            if (b.Spline == null || b.Spline.ControlPoints.Count < 2) continue;

            double t = inputPlayable.GetTime();
            double dur = SplineClipTime.Duration(inputPlayable, b.TimelineClip);
            if (dur <= 1e-6) continue;

            // 取 time 到 Clip 起点 / 终点的较小距离作为该 input 的"边界接近度"。
            double distToStart = System.Math.Abs(t);
            double distToEnd = System.Math.Abs(t - dur);

            if (distToStart <= distToEnd)
            {
                if (distToStart < smallestDist)
                {
                    smallestDist = distToStart;
                    bestBehaviour = b;
                    bestAtEnd = false;

                }
            }
            else
            {
                if (distToEnd < smallestDist)
                {
                    smallestDist = distToEnd;
                    bestBehaviour = b;
                    bestAtEnd = true;

                }
            }
        }

        if (bestBehaviour == null) return;

        bestBehaviour.SampleAtNormalized(bestAtEnd ? 1f : 0f);
        if (!bestBehaviour.HasValidSample) return;

        GetWorldSample(bestBehaviour, target, out Vector3 position, out Quaternion rotation);
        target.position = position;
        if (bestBehaviour.ApplyRotation) target.rotation = rotation;
    }

    private static void GetWorldSample(SplineCurveMoveBehaviour behaviour, Transform target,
        out Vector3 position, out Quaternion rotation)
    {
        var frame = behaviour.ResolvedReferenceFrame;
        if (frame == null && behaviour.UseLocalSpace) frame = target.parent;
        position = behaviour.SampledPosition;
        rotation = behaviour.SampledRotation;
        if (frame != null)
        {
            position = frame.TransformPoint(position);
            rotation = SplineMotionContext.TransformPathRotation(frame.localToWorldMatrix, rotation, frame.rotation * rotation);
        }
    }

    /// <summary>
    /// 阶段 4：在写完 Transform 后，遍历所有 input 的 TriggeredEventsThisFrame，
    /// 派发给 Track Binding 上的 SplineEventReceiver。
    /// </summary>
    private void DispatchEvents(Playable playable, Transform target)
    {
        SplineEventReceiver receiver = target.GetComponent<SplineEventReceiver>();
        if (receiver == null) return;

        int inputCount = playable.GetInputCount();
        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0f) continue;

            var inputPlayable = (ScriptPlayable<SplineCurveMoveBehaviour>)playable.GetInput(i);
            var behaviour = inputPlayable.GetBehaviour();
            if (behaviour == null) continue;

            var triggered = behaviour.TriggeredEventsThisFrame;
            if (triggered == null || triggered.Count == 0) continue;

            for (int k = 0; k < triggered.Count; k++)
                receiver.Trigger(triggered[k]);
        }
    }
}
