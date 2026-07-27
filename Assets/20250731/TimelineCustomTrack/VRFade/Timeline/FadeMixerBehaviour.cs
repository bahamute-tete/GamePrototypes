using UnityEngine;
using UnityEngine.Playables;

namespace VRFade
{
    /// <summary>
    /// Track 级别的 Mixer。
    /// 策略：
    ///   - 由「权重最大的 Clip」决定 type 与 type-specific 参数（不做异类型混合）
    ///   - alpha 在所有有权重的 Clip 之间加权累加
    ///   - color 仅在 SolidColor / Iris / DepthFade / Flash 之间生效；Desat 忽略
    /// </summary>
    public class FadeMixerBehaviour : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            int inputCount = playable.GetInputCount();
            if (inputCount == 0)
            {
                FadeRuntime.Clear();
                return;
            }

            // ---------- Pass 1: 找权重最大的 Clip 作为「主导 Clip」----------
            int dominantIdx = -1;
            float dominantWeight = 0f;

            for (int i = 0; i < inputCount; i++)
            {
                float w = playable.GetInputWeight(i);
                if (w > dominantWeight)
                {
                    dominantWeight = w;
                    dominantIdx = i;
                }
            }

            if (dominantIdx < 0)
            {
                FadeRuntime.Clear();
                return;
            }

            // ---------- Pass 2: 累加所有 Clip 的加权 alpha ----------
            float totalAlpha = 0f;
            for (int i = 0; i < inputCount; i++)
            {
                float w = playable.GetInputWeight(i);
                if (w <= 0f) continue;

                var p = (ScriptPlayable<FadeBehaviour>)playable.GetInput(i);
                var b = p.GetBehaviour();

                double dur = p.GetDuration();
                double t = p.GetTime();
                float n = dur > 0.0 ? Mathf.Clamp01((float)(t / dur)) : 0f;

                totalAlpha += b.EvaluateAlpha(n) * w;
            }

            // ---------- Pass 3: 用主导 Clip 填 type-specific 参数 ----------
            var domPlayable = (ScriptPlayable<FadeBehaviour>)playable.GetInput(dominantIdx);
            var domBehaviour = domPlayable.GetBehaviour();

            FadeState state = FadeState.Default;
            domBehaviour.WriteTypeSpecific(ref state);
            state.alpha = Mathf.Clamp01(totalAlpha);

            FadeRuntime.SetState(in state);
        }

        public override void OnGraphStop(Playable playable)
        {
            // 故意不重置：LBE 工作流是「Timeline A 淡到黑 → 切场景 → Timeline B 从黑淡入」，
            // 切场景的间隙必须保持纯黑。如需手动清除可调用 FadeRuntime.Clear()。
        }
    }
}
