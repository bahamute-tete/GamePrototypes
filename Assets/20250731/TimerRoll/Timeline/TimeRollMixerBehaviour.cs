// LiangZhu - 时间回溯日历 / Timeline MixerBehaviour
// 不混合:取权重最大的那个 clip,用它的 clip-local 时间和节奏曲线算 p、pDot,推给 Driver。
//   p    = curve(u),  u = clipLocalTime / clipDuration
//   pDot = curve'(u) / clipDuration   —— 解析量,只依赖 u,擦洗/暂停都正确(模糊不靠帧差)
// 无 clip 激活(权重全 0)时不写,保持最后一帧(由你 Deactivate 收尾)。

using System;
using UnityEngine;
using UnityEngine.Playables;

namespace LiangZhu.TimeRoll
{
    public class TimeRollMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var driver = playerData as TimeRollDriver;
            if (driver == null) return;

            // 取权重最大的 clip(不做跨 clip 混合)
            int count = playable.GetInputCount();
            int best = -1;
            float bestW = 0f;
            for (int i = 0; i < count; i++)
            {
                float w = playable.GetInputWeight(i);
                if (w > bestW) { bestW = w; best = i; }
            }
            if (best < 0 || bestW <= 0f) return; // 无激活 clip -> 保持最后一帧

            var input = (ScriptPlayable<TimeRollBehaviour>)playable.GetInput(best);
            var b = input.GetBehaviour();
            if (b == null) return;

            double dur = input.GetDuration();
            double t   = input.GetTime();
            float u = dur > 1e-6 ? Mathf.Clamp01((float)(t / dur)) : 0f;

            float p, pDot;
            AnimationCurve curve = b.curve;
            if (curve != null && curve.length > 0)
            {
                p = Mathf.Clamp01(curve.Evaluate(u));

                const float eps = 1e-3f;
                float uLo = Mathf.Clamp01(u - eps);
                float uHi = Mathf.Clamp01(u + eps);
                float denom = Mathf.Max(uHi - uLo, 1e-6f);
                float slope = (curve.Evaluate(uHi) - curve.Evaluate(uLo)) / denom;
                pDot = (float)(slope / Math.Max(dur, 1e-4));
            }
            else
            {
                p = u;
                pDot = (float)(1.0 / Math.Max(dur, 1e-4));
            }

            var disp = b.overrideDisplay ? b.display : driver.DefaultDisplay;
            driver.PushState(p, pDot, b.cfg, disp);
        }
    }
}
