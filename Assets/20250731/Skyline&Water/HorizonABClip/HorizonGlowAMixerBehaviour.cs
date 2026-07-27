using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

// ====================================================================
//   Horizon Glow A Mixer Behaviour
//
//   行为规则（与 Sky/HorizonGlow Transition Track 一致）：
//     - 时间轴在第一个 clip 之前 → 输出 clip[0].startState
//     - 时间轴在某个 clip 内      → 按 weight 加权混合 + curve 插值
//     - 时间轴在两个 clip 之间    → 输出「上一个 clip 的 endState」
//     - 时间轴在最后一个 clip 之后 → 输出 clip[N-1].endState
//
//   写入 controller 的 A 组字段（horizonColorA / horizonIntensityA / ...）
// ====================================================================

public class HorizonGlowAMixerBehaviour : PlayableBehaviour
{
    public struct ClipBound
    {
        public double           startTime;
        public double           endTime;
        public HorizonGlowState startState;
        public HorizonGlowState endState;
    }

    public List<ClipBound> bounds;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var controller = playerData as MagicWaterController;
        if (controller == null) return;

        // ====== 1) 有 clip 激活 → 加权混合各字段 ======
        int inputCount = playable.GetInputCount();
        Color accumColor      = Color.black;
        float accumIntensity  = 0f;
        float accumFalloff    = 0f;
        float accumHaloInt    = 0f;
        float accumHaloFall   = 0f;
        float totalWeight     = 0f;

        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0f) continue;

            var input = (ScriptPlayable<HorizonGlowABehaviour>)playable.GetInput(i);
            var b = input.GetBehaviour();

            double clipTime = input.GetTime();
            double clipDur  = input.GetDuration();
            float  progress = clipDur > 0 ? (float)(clipTime / clipDur) : 0f;
            progress = Mathf.Clamp01(progress);

            float curveT = b.curve != null ? b.curve.Evaluate(progress) : progress;
            HorizonGlowState s = HorizonGlowState.Lerp(b.startState, b.endState, curveT);

            accumColor    += s.color         * weight;
            accumIntensity += s.intensity     * weight;
            accumFalloff   += s.falloff       * weight;
            accumHaloInt   += s.haloIntensity * weight;
            accumHaloFall  += s.haloFalloff   * weight;
            totalWeight    += weight;
        }

        HorizonGlowState finalState;

        if (totalWeight > 0f)
        {
            float inv = 1f / totalWeight;
            finalState = new HorizonGlowState
            {
                color         = accumColor * inv,
                intensity     = accumIntensity * inv,
                falloff       = accumFalloff   * inv,
                haloIntensity = accumHaloInt   * inv,
                haloFalloff   = accumHaloFall  * inv,
            };
        }
        // ====== 2) 没有 clip 激活 → 根据时间轴位置 hold 边界值 ======
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

        // ====== 写入 controller 的 A 组字段 ======
        controller.horizonColorA     = finalState.color;
        controller.horizonIntensityA = Mathf.Max(0f, finalState.intensity);
        controller.horizonFalloffA   = Mathf.Clamp(finalState.falloff, 0.1f, 40f);
        controller.haloIntensityA    = Mathf.Max(0f, finalState.haloIntensity);
        controller.haloFalloffA      = Mathf.Clamp(finalState.haloFalloff, 0.1f, 10f);
    }
}
