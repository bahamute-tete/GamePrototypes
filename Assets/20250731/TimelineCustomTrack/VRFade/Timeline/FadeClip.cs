using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace VRFade
{
    /// <summary>
    /// Timeline 上的过渡片段。通过 type 枚举选择效果，OnValidate 自动应用对应默认值。
    /// </summary>
    [System.Serializable]
    [DisplayName("VR Fade Clip")]
    public class FadeClip : PlayableAsset, ITimelineClipAsset
    {
        // ============== 类型选择 ==============
        [Tooltip("过渡效果类型。切换类型时会自动应用该类型的推荐默认值，可在下方继续调整。")]
        public FadeType type = FadeType.SolidColor;

        // 内部记录上次的类型，用于检测变化
        [HideInInspector] public FadeType _lastType = FadeType.SolidColor;

        // ============== 通用参数 ==============
        [Tooltip("过渡颜色。Desaturate 类型不使用此项。")]
        [ColorUsage(false, false)]
        public Color color = Color.black;

        [Tooltip("片段开始时的强度。\n  · 淡出: 0\n  · 淡入: 1\n  · 保持: 1")]
        [Range(0, 1)] public float startAlpha = 0f;

        [Tooltip("片段结束时的强度。\n  · 淡出: 1\n  · 淡入: 0\n  · 保持: 1")]
        [Range(0, 1)] public float endAlpha = 1f;

        [Tooltip("过渡曲线。VR 推荐 EaseInOut，避免线性带来的速度突变。")]
        public AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        // ============== Iris 参数 ==============
        [Tooltip("虹膜中心 (UV 空间)。VR 中 (0.5, 0.5) = 摄像机正前方，通常不用改。")]
        public Vector2 irisCenter = new Vector2(0.5f, 0.5f);

        [Range(0.001f, 0.5f)]
        [Tooltip("虹膜边缘软度。0.05 是温和过渡，0.001 是硬切。")]
        public float irisSoftness = 0.05f;

        [Tooltip("宽高比校正：开启后虹膜在屏幕上是正圆。")]
        public bool irisAspectCorrect = true;

        // ============== Desaturate 参数 ==============
        [Range(0, 1)]
        [Tooltip("alpha=1 时的目标饱和度衰减量。1=完全灰度，0.5=半饱和。")]
        public float desaturationAmount = 1f;

        [Range(0, 1)]
        [Tooltip("alpha=1 时的亮度乘数。0.4 = 完全淡入时压暗到 40% 亮度。")]
        public float brightnessMultiplier = 0.4f;

        // ============== DepthFade 参数 ==============
        [Min(0.1f)]
        [Tooltip("近距离阈值：该距离开始淡入。")]
        public float depthNear = 5f;

        [Min(0.1f)]
        [Tooltip("远距离阈值：该距离完全淡入。")]
        public float depthFar = 50f;

        [Tooltip("反转：勾选后近处先淡入（默认是远处先淡入）。")]
        public bool depthInvert = false;

        // ============== Timeline ClipCaps ==============
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

        // ============== Default 应用 ==============
        private void OnValidate()
        {
            if (type != _lastType)
            {
                ApplyTypeDefaults(type);
                _lastType = type;
            }
        }

        private void Reset()
        {
            type = FadeType.SolidColor;
            _lastType = FadeType.SolidColor;
            color = Color.black;
            startAlpha = 0f;
            endAlpha = 1f;
            curve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            ApplyTypeDefaults(type);
        }

        /// <summary>切换 type 时调用：重置该类型的专属参数到推荐值。</summary>
        public void ApplyTypeDefaults(FadeType t)
        {
            switch (t)
            {
                case FadeType.SolidColor:
                    // 无额外参数；保留 color/curve/alpha 用户当前值
                    break;

                case FadeType.Iris:
                    irisCenter = new Vector2(0.5f, 0.5f);
                    irisSoftness = 0.05f;
                    irisAspectCorrect = true;
                    break;

                case FadeType.Desaturate:
                    desaturationAmount = 1f;
                    brightnessMultiplier = 0.4f;
                    break;

                case FadeType.DepthFade:
                    depthNear = 5f;
                    depthFar = 50f;
                    depthInvert = false;
                    if (color == Color.white) color = Color.black;
                    break;

                case FadeType.Flash:
                    // Flash 是「白 + 尖峰」预设，会覆盖 color 和 curve
                    color = Color.white;
                    startAlpha = 0f;
                    endAlpha = 1f;
                    curve = MakeFlashCurve();
                    break;
            }
        }

        private static AnimationCurve MakeFlashCurve()
        {
            // 快速上升到峰值，再较慢回落到 0：典型闪光曲线
            var c = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.12f, 1f),
                new Keyframe(1f, 0f)
            );
            // 让中间峰更尖一些
            for (int i = 0; i < c.length; i++)
            {
                AnimationCurveExtension.SetSmoothTangent(c, i);
            }
            return c;
        }

        // ============== Playable 创建 ==============
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<FadeBehaviour>.Create(graph);
            var b = playable.GetBehaviour();

            b.type = type;
            b.color = color;
            b.startAlpha = startAlpha;
            b.endAlpha = endAlpha;
            b.curve = (curve != null && curve.length > 0) ? curve : AnimationCurve.Linear(0, 0, 1, 1);

            b.irisCenter = irisCenter;
            b.irisSoftness = irisSoftness;
            b.irisAspectCorrect = irisAspectCorrect;

            b.desaturationAmount = desaturationAmount;
            b.brightnessMultiplier = brightnessMultiplier;

            b.depthNear = depthNear;
            b.depthFar = depthFar;
            b.depthInvert = depthInvert;

            return playable;
        }
    }

    /// <summary>
    /// AnimationCurve 辅助：在运行时为 Keyframe 生成平滑切线（无需 AnimationUtility）。
    /// </summary>
    internal static class AnimationCurveExtension
    {
        public static void SetSmoothTangent(AnimationCurve c, int index)
        {
            if (index < 0 || index >= c.length) return;

            var key = c[index];
            float inT = 0f, outT = 0f;

            if (index > 0 && index < c.length - 1)
            {
                var prev = c[index - 1];
                var next = c[index + 1];
                float dt = next.time - prev.time;
                if (dt > 0.0001f)
                {
                    float slope = (next.value - prev.value) / dt;
                    inT = slope;
                    outT = slope;
                }
            }

            key.inTangent = inT;
            key.outTangent = outT;
            c.MoveKey(index, key);
        }
    }
}
