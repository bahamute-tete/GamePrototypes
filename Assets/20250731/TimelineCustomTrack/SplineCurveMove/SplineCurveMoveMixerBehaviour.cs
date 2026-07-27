using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// SplineCurveMoveTrack 的混合器。
///
/// 工作流程：
///   1. 探测当前帧是否有任何激活 Clip 设置了 ResolvedReferenceFrame（refFrame）。
///      - 有 → 进入【世界空间混合模式】：每个 sample 各自经 refFrame.TransformPoint 变换
///        到世界空间，加权混合后写入 target.position（世界）。
///      - 无 → 进入【legacy 模式】：sample 原封不动累加，按第一个激活 Clip 的 useLocalSpace
///        标志决定写到 target.localPosition 或 target.position。完全保留原有行为。
///   2. 跨参考系混合（例如世界路径 Clip 与载具局部路径 Clip 重叠时）天然平滑：
///      二者都先变换到世界空间，再按 weight 插值——速度连续，无跳变。
///
/// 注意 Track Evaluate 顺序：若使用 refFrame，承载 refFrame 的 Track（如载具 SplineTrack）
/// 必须排在本 Track 的【上方】，确保本帧执行到此 Mixer 时 refFrame.position/rotation 已经
/// 是本帧的新值。Unity Timeline 自上而下顺序 Evaluate，这点用户需要在搭轨时注意。
/// </summary>
public class SplineCurveMoveMixerBehaviour : PlayableBehaviour
{
    private Transform _cachedTarget;
    private Vector3 _cachedDefaultPos;
    private Quaternion _cachedDefaultRot;
    private bool _cachedInited;

    public override void OnPlayableCreate(Playable playable) { _cachedInited = false; }
    public override void OnGraphStop(Playable playable) { _cachedInited = false; }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var target = playerData as Transform;
        if (target == null) return;

        int inputCount = playable.GetInputCount();
        if (inputCount == 0) return;

        if (!_cachedInited || _cachedTarget != target)
        {
            _cachedTarget = target;
            _cachedDefaultPos = target.position;
            _cachedDefaultRot = target.rotation;
            _cachedInited = true;
        }

        // -------- Pass 1：探测本帧是否进入世界空间混合模式 --------
        // 只要有一个激活且采样有效的 Clip 设置了 refFrame，整帧就进入 world-space 模式。
        // 这样设计的原因：跨参考系混合在数学上必须先各自变换到世界空间，再做加权插值。
        // 即便只有一个 Clip 用了 refFrame，混合也要在世界空间执行——否则未设 refFrame 的
        // legacy Clip 与设了 refFrame 的 Clip 在重叠区会因坐标系不同而错位。
        bool anyRefFrame = false;
        for (int i = 0; i < inputCount; i++)
        {
            float w = playable.GetInputWeight(i);
            if (w <= 0f) continue;
            var ip = (ScriptPlayable<SplineCurveMoveBehaviour>)playable.GetInput(i);
            var b = ip.GetBehaviour();
            if (b == null || !b.HasValidSample) continue;
            if (b.ResolvedReferenceFrame != null) { anyRefFrame = true; break; }
        }

        // -------- Pass 2：加权混合 --------
        float totalWeight = 0f;
        Vector3 blendedPos = Vector3.zero;
        Quaternion blendedRot = Quaternion.identity;
        bool firstRotationApplied = false;
        bool useLocalSpaceLegacy = false;   // 仅在 !anyRefFrame 时有意义
        bool anyApplyRotation = false;

        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0f) continue;

            var inputPlayable = (ScriptPlayable<SplineCurveMoveBehaviour>)playable.GetInput(i);
            var behaviour = inputPlayable.GetBehaviour();
            if (behaviour == null || !behaviour.HasValidSample) continue;

            // 计算本 sample 用于加权混合的最终坐标。
            // - anyRefFrame=true：所有 sample 转到世界空间。
            //     设置了 refFrame 的 Clip 用 refFrame.TransformPoint；
            //     未设的 Clip 视其 SampledPosition 已在世界空间（refFrame=null 的常规情况）。
            // - anyRefFrame=false：sample 保持原状（legacy 路径）。
            Vector3 sampledPos;
            Quaternion sampledRot;
            if (anyRefFrame)
            {
                Transform rf = behaviour.ResolvedReferenceFrame;
                if (rf != null)
                {
                    sampledPos = rf.TransformPoint(behaviour.SampledPosition);
                    sampledRot = rf.rotation * behaviour.SampledRotation;
                }
                else
                {
                    // refFrame 模式下未设 refFrame 的 Clip：假定其数据已在世界空间。
                    // 若该 Clip 同时设了 useLocalSpace=true（数据本应在 target.parent 空间），
                    // 这里会发生坐标错位——但此种 useLocalSpace 与 refFrame 混用的场景罕见，
                    // 推荐用户在乘载具场景统一使用 refFrame 字段。
                    sampledPos = behaviour.SampledPosition;
                    sampledRot = behaviour.SampledRotation;
                }
            }
            else
            {
                sampledPos = behaviour.SampledPosition;
                sampledRot = behaviour.SampledRotation;

                // legacy 路径下，记录首个激活 Clip 的 useLocalSpace 标志作为整帧输出模式
                if (totalWeight <= 0f)
                    useLocalSpaceLegacy = behaviour.UseLocalSpace;
            }

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

        // 部分权重时与缓存的默认 pose 混合。
        // _cachedDefaultPos/Rot 是首次绑定 target 时的 target.position/rotation（世界值）。
        // 在 refFrame 世界混合模式下，blendedPos 已是世界，与世界默认 pose 混合天然正确。
        // 在 legacy useLocalSpace 模式下，blendedPos 是 target-parent 局部值，与世界默认 pose
        // 混合在语义上不严格——这是原有代码的行为，本次未改动以保持向后兼容。
        if (totalWeight < 1f)
        {
            float remain = 1f - totalWeight;
            blendedPos = blendedPos + _cachedDefaultPos * remain;
            if (anyApplyRotation)
                blendedRot = Quaternion.Slerp(_cachedDefaultRot, blendedRot, totalWeight);
        }

        // 写入 Transform
        if (anyRefFrame)
        {
            // refFrame 模式：始终写世界空间
            target.position = blendedPos;
            if (anyApplyRotation) target.rotation = blendedRot;
        }
        else if (useLocalSpaceLegacy)
        {
            target.localPosition = blendedPos;
            if (anyApplyRotation) target.localRotation = blendedRot;
        }
        else
        {
            target.position = blendedPos;
            if (anyApplyRotation) target.rotation = blendedRot;
        }

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
        bool bestUseLocal = false;
        Transform bestRefFrame = null;
        double smallestDist = double.MaxValue;

        for (int i = 0; i < inputCount; i++)
        {
            var inputPlayable = (ScriptPlayable<SplineCurveMoveBehaviour>)playable.GetInput(i);
            var b = inputPlayable.GetBehaviour();
            if (b == null) continue;
            if (b.Spline == null || b.Spline.ControlPoints.Count < 2) continue;

            double t = inputPlayable.GetTime();
            double dur = inputPlayable.GetDuration();
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
                    bestUseLocal = b.UseLocalSpace;
                    bestRefFrame = b.ResolvedReferenceFrame;
                }
            }
            else
            {
                if (distToEnd < smallestDist)
                {
                    smallestDist = distToEnd;
                    bestBehaviour = b;
                    bestAtEnd = true;
                    bestUseLocal = b.UseLocalSpace;
                    bestRefFrame = b.ResolvedReferenceFrame;
                }
            }
        }

        if (bestBehaviour == null) return;

        bestBehaviour.SampleAtNormalized(bestAtEnd ? 1f : 0f);
        if (!bestBehaviour.HasValidSample) return;

        if (bestRefFrame != null)
        {
            // refFrame 模式：变换到世界后写入
            Vector3 wp = bestRefFrame.TransformPoint(bestBehaviour.SampledPosition);
            Quaternion wr = bestRefFrame.rotation * bestBehaviour.SampledRotation;
            target.position = wp;
            if (bestBehaviour.ApplyRotation) target.rotation = wr;
        }
        else if (bestUseLocal)
        {
            target.localPosition = bestBehaviour.SampledPosition;
            if (bestBehaviour.ApplyRotation)
                target.localRotation = bestBehaviour.SampledRotation;
        }
        else
        {
            target.position = bestBehaviour.SampledPosition;
            if (bestBehaviour.ApplyRotation)
                target.rotation = bestBehaviour.SampledRotation;
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
