using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VRFade
{
    /// <summary>
    /// 实际执行淡入淡出的 Pass。
    ///
    /// URP 14 关键约束：Blitter.BlitCameraTexture 不允许 source == destination。
    /// 因此使用「double-blit + 临时 RT」：
    ///   Pass A: cameraColor → tempRT     (无 material，纯复制)
    ///   Pass B: tempRT      → cameraColor (经过 material，应用淡入淡出)
    /// shader 通过 _BlitTexture 采样 tempRT，所以 sample 与 write 不冲突。
    ///
    /// 通过 shader keyword 切换 4 种效果路径（SolidColor/Flash 共用一条）。
    /// </summary>
    public class FadeRenderPass : ScriptableRenderPass, IDisposable
    {
        // ============== Shader Property IDs ==============
        private static readonly int FadeColorID            = Shader.PropertyToID("_FadeColor");
        private static readonly int FadeAlphaID            = Shader.PropertyToID("_FadeAlpha");
        // Iris
        private static readonly int IrisCenterID           = Shader.PropertyToID("_IrisCenter");
        private static readonly int IrisSoftnessID         = Shader.PropertyToID("_IrisSoftness");
        private static readonly int IrisAspectCorrectID    = Shader.PropertyToID("_IrisAspectCorrect");
        // Desaturate
        private static readonly int DesatAmountID          = Shader.PropertyToID("_DesaturationAmount");
        private static readonly int BrightnessMultID       = Shader.PropertyToID("_BrightnessMultiplier");
        // DepthFade
        private static readonly int DepthNearID            = Shader.PropertyToID("_DepthNear");
        private static readonly int DepthFarID             = Shader.PropertyToID("_DepthFar");
        private static readonly int DepthInvertID          = Shader.PropertyToID("_DepthInvert");

        // ============== Shader Keywords ==============
        private const string KW_IRIS  = "_FADE_IRIS";
        private const string KW_DESAT = "_FADE_DESAT";
        private const string KW_DEPTH = "_FADE_DEPTH";

        private const string ProfilerTag = "VR Fade";
        private static readonly ProfilingSampler s_ProfilingSampler = new ProfilingSampler(ProfilerTag);
        private const string TempRTName = "_VRFadeTemp";

        private Material material;
        private RTHandle m_TempRT;

        public FadeRenderPass(Material material)
        {
            this.material = material;
            profilingSampler = s_ProfilingSampler;

            // DepthFade 需要 _CameraDepthTexture。即使 URP Asset 没勾 Depth Texture，
            // 这行也能让 URP 在该 Pass 之前生成深度纹理。
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public void SetMaterial(Material material)
        {
            this.material = material;
        }

        // ============== 临时 RT 分配（URP 14 推荐时机）==============
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;   // 不需要 depth buffer
            desc.msaaSamples = 1;       // 临时 RT 不开 MSAA，URP 会在 Blit 时 resolve

            // ReAllocateIfNeeded 内部判断尺寸是否变化，没变化不会重新分配
            RenderingUtils.ReAllocateIfNeeded(
                ref m_TempRT,
                desc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: TempRTName);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || m_TempRT == null) return;

            var s = FadeRuntime.GetEffective();
            if (s.alpha <= 0.001f) return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, s_ProfilingSampler))
            {
                ApplyState(material, s);

                var cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;

                // Step 1: cameraColor → m_TempRT （纯复制，无 material）
                Blitter.BlitCameraTexture(cmd, cameraColor, m_TempRT);

                // Step 2: m_TempRT → cameraColor （应用 material；shader 从 _BlitTexture 采样 tempRT）
                Blitter.BlitCameraTexture(cmd, m_TempRT, cameraColor, material, 0);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // ============== IDisposable: 由 RenderFeature 在 Dispose 时调用 ==============
        public void Dispose()
        {
            m_TempRT?.Release();
            m_TempRT = null;
        }

        /// <summary>
        /// 把 FadeState 推到材质：先切 keyword（决定走哪条 shader 分支），再设 uniform。
        /// </summary>
        private static void ApplyState(Material mat, in FadeState s)
        {
            // ---------- Keyword 切换 ----------
            // 默认（无 keyword）= SolidColor 路径，Flash 也走这条
            mat.DisableKeyword(KW_IRIS);
            mat.DisableKeyword(KW_DESAT);
            mat.DisableKeyword(KW_DEPTH);

            switch (s.type)
            {
                case FadeType.Iris:       mat.EnableKeyword(KW_IRIS);  break;
                case FadeType.Desaturate: mat.EnableKeyword(KW_DESAT); break;
                case FadeType.DepthFade:  mat.EnableKeyword(KW_DEPTH); break;
                // SolidColor / Flash 不开任何 keyword
            }

            // ---------- 通用 uniform ----------
            mat.SetColor(FadeColorID, s.color);
            mat.SetFloat(FadeAlphaID, s.alpha);

            // ---------- Iris ----------
            mat.SetVector(IrisCenterID, new Vector4(s.irisCenter.x, s.irisCenter.y, 0, 0));
            mat.SetFloat(IrisSoftnessID, Mathf.Max(s.irisSoftness, 0.0001f));
            mat.SetFloat(IrisAspectCorrectID, s.irisAspectCorrect ? 1f : 0f);

            // ---------- Desaturate ----------
            mat.SetFloat(DesatAmountID, s.desaturationAmount);
            mat.SetFloat(BrightnessMultID, s.brightnessMultiplier);

            // ---------- DepthFade ----------
            mat.SetFloat(DepthNearID, s.depthNear);
            mat.SetFloat(DepthFarID, Mathf.Max(s.depthFar, s.depthNear + 0.01f));
            mat.SetFloat(DepthInvertID, s.depthInvert ? 1f : 0f);
        }
    }
}
