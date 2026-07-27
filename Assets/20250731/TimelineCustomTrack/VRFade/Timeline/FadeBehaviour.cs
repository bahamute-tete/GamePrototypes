using UnityEngine;
using UnityEngine.Playables;

namespace VRFade
{
    /// <summary>
    /// 每个 FadeClip 在 Timeline 运行时对应的 PlayableBehaviour。
    /// 持有该片段的所有参数，由 Mixer 读取并写入 FadeRuntime。
    /// </summary>
    [System.Serializable]
    public class FadeBehaviour : PlayableBehaviour
    {
        // 通用
        public FadeType type = FadeType.SolidColor;
        public Color color = Color.black;
        public float startAlpha = 0f;
        public float endAlpha = 1f;
        public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);

        // Iris
        public Vector2 irisCenter = new Vector2(0.5f, 0.5f);
        public float irisSoftness = 0.05f;
        public bool irisAspectCorrect = true;

        // Desaturate
        public float desaturationAmount = 1f;
        public float brightnessMultiplier = 0.4f;

        // DepthFade
        public float depthNear = 5f;
        public float depthFar = 50f;
        public bool depthInvert = false;

        /// <summary>在 [0,1] 归一化时间上求该片段当前的 alpha。</summary>
        public float EvaluateAlpha(float normalizedTime)
        {
            float t = (curve != null && curve.length > 0)
                ? curve.Evaluate(normalizedTime)
                : normalizedTime;
            return Mathf.LerpUnclamped(startAlpha, endAlpha, t);
        }

        /// <summary>把当前 Behaviour 的 type-specific 参数写到 FadeState 上。</summary>
        public void WriteTypeSpecific(ref FadeState s)
        {
            s.type = type;
            s.color = color;

            switch (type)
            {
                case FadeType.Iris:
                    s.irisCenter = irisCenter;
                    s.irisSoftness = irisSoftness;
                    s.irisAspectCorrect = irisAspectCorrect;
                    break;
                case FadeType.Desaturate:
                    s.desaturationAmount = desaturationAmount;
                    s.brightnessMultiplier = brightnessMultiplier;
                    break;
                case FadeType.DepthFade:
                    s.depthNear = depthNear;
                    s.depthFar = depthFar;
                    s.depthInvert = depthInvert;
                    break;
                // SolidColor / Flash 只用 color，无额外
            }
        }
    }
}
