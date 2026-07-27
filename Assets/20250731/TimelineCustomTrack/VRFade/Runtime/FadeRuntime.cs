using UnityEngine;

namespace VRFade
{
    /// <summary>
    /// 过渡效果类型。新增类型时需在此枚举、shader keyword、FadeRenderPass、ApplyTypeDefaults 同步处理。
    /// </summary>
    public enum FadeType
    {
        /// <summary>纯色（黑场 / 白场 / 任意纯色）</summary>
        SolidColor = 0,
        /// <summary>圆形虹膜遮罩（Iris / Tunnel Vignette），最 VR 友好</summary>
        Iris = 1,
        /// <summary>色彩降饱和 + 压暗，最温和的过渡</summary>
        Desaturate = 2,
        /// <summary>深度感应黑场（远近不同步淡入）</summary>
        DepthFade = 3,
        /// <summary>闪白（SolidColor 路径 + 白色 + 尖峰曲线 预设）</summary>
        Flash = 4,
    }

    /// <summary>
    /// 完整的过渡状态。Mixer 写入；FadeRenderPass 读取并配置 shader keyword + uniform。
    /// </summary>
    public struct FadeState
    {
        public FadeType type;
        public Color color;
        public float alpha;

        // Iris
        public Vector2 irisCenter;
        public float irisSoftness;
        public bool irisAspectCorrect;

        // Desaturate
        public float desaturationAmount;   // 0=原色, 1=完全灰度
        public float brightnessMultiplier; // 完全降饱和时的亮度乘数 (压暗)

        // DepthFade
        public float depthNear;            // 该距离开始淡入
        public float depthFar;             // 该距离完全覆盖
        public bool depthInvert;           // false=远先黑, true=近先黑

        public static FadeState Default => new FadeState
        {
            type = FadeType.SolidColor,
            color = Color.black,
            alpha = 0f,
            irisCenter = new Vector2(0.5f, 0.5f),
            irisSoftness = 0.05f,
            irisAspectCorrect = true,
            desaturationAmount = 1f,
            brightnessMultiplier = 0.4f,
            depthNear = 5f,
            depthFar = 50f,
            depthInvert = false,
        };
    }

    /// <summary>
    /// 全局过渡状态。Timeline / 脚本 写入；FadeRenderFeature 读取。
    /// static 设计使得状态在场景切换时不会丢失，符合 LBE 跨场景黑场工作流。
    /// </summary>
    public static class FadeRuntime
    {
        public static FadeState State = FadeState.Default;

        // Editor 预览覆盖
        internal static bool EditorPreviewActive = false;
        internal static FadeState EditorPreviewState = FadeState.Default;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            State = FadeState.Default;
            EditorPreviewActive = false;
        }

        /// <summary>Mixer 调用：写入完整状态。</summary>
        public static void SetState(in FadeState s)
        {
            State = s;
            State.alpha = Mathf.Clamp01(State.alpha);
        }

        /// <summary>简化调用：仅设置颜色和强度（用于纯色淡入淡出脚本调用）。</summary>
        public static void SetSolid(Color color, float alpha)
        {
            State.type = FadeType.SolidColor;
            State.color = color;
            State.alpha = Mathf.Clamp01(alpha);
        }

        /// <summary>清除（淡入回正常画面）。Timeline 不会自动调用。</summary>
        public static void Clear()
        {
            State.alpha = 0f;
        }

        /// <summary>取实际生效状态（考虑 Editor 预览）。</summary>
        public static FadeState GetEffective()
        {
#if UNITY_EDITOR
            if (EditorPreviewActive && !Application.isPlaying)
                return EditorPreviewState;
#endif
            return State;
        }
    }
}
