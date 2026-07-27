using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

// ====================================================================
//   Sky Transition Mixer Behaviour
//
//   行为规则（基于 Track 传入的 clip 边界）：
//     - 时间轴在第一个 clip 之前 → 输出 clip[0].startBlend
//     - 时间轴在某个 clip 内      → 按 weight 加权混合 clip 值（curve 控制）
//     - 时间轴在两个 clip 之间    → 输出「上一个 clip 的 endBlend」（hold last)
//     - 时间轴在最后一个 clip 之后 → 输出 clip[N-1].endBlend
//
//   也就是说：clip 前后的「时间轴拖到 clip 范围之外」都会自动保持在
//   clip 自己定义的端点值上，不会跳回 controller 自身的值。
// ====================================================================

public class SkyTransitionMixerBehaviour : PlayableBehaviour
{
    public struct ClipBound
    {
        public double startTime;
        public double endTime;
        public float  startBlend;
        public float  endBlend;
    }

    public List<ClipBound> bounds;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        var controller = playerData as MagicWaterController;
        if (controller == null) return;

        // ====== 1) 先看是否有 clip 激活（权重 > 0），有就用加权混合 ======
        int inputCount = playable.GetInputCount();
        float accumulated = 0f;
        float totalWeight = 0f;

        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0f) continue;

            var input = (ScriptPlayable<SkyTransitionBehaviour>)playable.GetInput(i);
            var b = input.GetBehaviour();

            double clipTime = input.GetTime();
            double clipDur  = input.GetDuration();
            float  progress = clipDur > 0 ? (float)(clipTime / clipDur) : 0f;
            progress = Mathf.Clamp01(progress);

            float curveT = b.curve != null ? b.curve.Evaluate(progress) : progress;
            float clipValue = Mathf.Lerp(b.startBlend, b.endBlend, curveT);

            accumulated += clipValue * weight;
            totalWeight += weight;
        }

        if (totalWeight > 0f)
        {
            // Clip 激活：归一化权重，输出 clip 的当前值
            controller.skyBlend = Mathf.Clamp01(accumulated / totalWeight);
            return;
        }

        // ====== 2) 没有 clip 激活，根据时间轴位置决定输出哪个 hold 值 ======
        if (bounds == null || bounds.Count == 0) return;

        double currentTime = playable.GetTime();

        // 时间轴在第一个 clip 之前 → hold startBlend
        if (currentTime < bounds[0].startTime)
        {
            controller.skyBlend = Mathf.Clamp01(bounds[0].startBlend);
            return;
        }

        // 时间轴在某个 clip 之后（gap 或末尾）→ hold「最近经过的 clip」的 endBlend
        float holdValue = bounds[0].endBlend;
        for (int i = 0; i < bounds.Count; i++)
        {
            if (currentTime >= bounds[i].endTime)
            {
                holdValue = bounds[i].endBlend;  // 不断更新到「时间轴已经越过的最近一个 clip」
            }
            else
            {
                break;  // 后面的 clip 都还没经过，可以停了
            }
        }
        controller.skyBlend = Mathf.Clamp01(holdValue);
    }
}
