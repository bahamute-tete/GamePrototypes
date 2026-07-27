using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

// ====================================================================
//   Sky B Mixer Behaviour
//
//   行为规则（跟 A Track 镜像）：
//     - 时间轴在第一个 clip 之前 → 输出 clip[0].startState
//     - 时间轴在某个 clip 内      → 按 weight 加权混合 + curve 插值
//     - 时间轴在两个 clip 之间    → 输出「上一个 clip 的 endState」
//     - 时间轴在最后一个 clip 之后 → 输出 clip[N-1].endState
//
//   写入 controller 的 Sky B 字段：
//     skyTintB / skyExposureB / skyRotationB
// ====================================================================

public class SkyBMixerBehaviour : PlayableBehaviour
{
    public struct ClipBound
    {
        public double   startTime;
        public double   endTime;
        public SkyState startState;
        public SkyState endState;
    }

    public List<ClipBound> bounds;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var controller = playerData as MagicWaterController;
        if (controller == null) return;

        int inputCount = playable.GetInputCount();
        Color accumTint     = Color.black;
        float accumExposure = 0f;
        float accumRotation = 0f;
        float totalWeight   = 0f;

        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0f) continue;

            var input = (ScriptPlayable<SkyBBehaviour>)playable.GetInput(i);
            var b = input.GetBehaviour();

            double clipTime = input.GetTime();
            double clipDur  = input.GetDuration();
            float  progress = clipDur > 0 ? (float)(clipTime / clipDur) : 0f;
            progress = Mathf.Clamp01(progress);

            float curveT = b.curve != null ? b.curve.Evaluate(progress) : progress;
            SkyState s = SkyState.Lerp(b.startState, b.endState, curveT);

            accumTint     += s.tint     * weight;
            accumExposure += s.exposure * weight;
            accumRotation += s.rotation * weight;
            totalWeight   += weight;
        }

        SkyState finalState;

        if (totalWeight > 0f)
        {
            float inv = 1f / totalWeight;
            finalState = new SkyState
            {
                tint     = accumTint     * inv,
                exposure = accumExposure * inv,
                rotation = accumRotation * inv,
            };
        }
        else if (bounds != null && bounds.Count > 0)
        {
            double currentTime = playable.GetTime();

            if (currentTime < bounds[0].startTime)
            {
                finalState = bounds[0].startState;
            }
            else
            {
                finalState = bounds[0].endState;
                for (int i = 0; i < bounds.Count; i++)
                {
                    if (currentTime >= bounds[i].endTime)
                        finalState = bounds[i].endState;
                    else
                        break;
                }
            }
        }
        else
        {
            return;
        }

        // ====== 写入 controller 的 Sky B 字段 ======
        controller.skyTintB     = finalState.tint;
        controller.skyExposureB = Mathf.Clamp(finalState.exposure, 0f, 8f);
        controller.skyRotationB = Mathf.Clamp(finalState.rotation, -360f, 360f);
    }
}
