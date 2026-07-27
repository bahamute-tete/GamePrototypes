using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VRFade
{
    /// <summary>
    /// VR LBE 场景过渡 RenderFeature。
    /// 把它加到 URP Renderer 的 Renderer Features 列表里即可启用。
    /// </summary>
    [DisallowMultipleRendererFeature("VR Fade")]
    public class FadeRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("RenderPass 注入时机。AfterRenderingPostProcessing 是 VR 推荐值（覆盖最终画面）。")]
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

            [Tooltip("覆盖材质。留空则使用默认 shader（含全部 4 种效果路径）。\n" +
                     "做更复杂自定义效果时把材质赋这里即可，无需改代码。")]
            public Material overrideMaterial = null;

            [Header("Editor Preview (仅编辑器)")]
            [Tooltip("勾选后可在 Scene/Game 视图直接看到效果，无需 Play。方便 LBE 流程调试。")]
            public bool editorPreview = false;

            [Tooltip("预览类型。")]
            public FadeType previewType = FadeType.SolidColor;

            [ColorUsage(false, false)]
            public Color previewColor = Color.black;

            [Range(0, 1)]
            public float previewAlpha = 0f;
        }

        public Settings settings = new Settings();

        private FadeRenderPass pass;
        private Material runtimeMaterial;
        private Shader defaultShader;

        private const string DefaultShaderName = "Hidden/VRFade/SolidColor";

        public override void Create()
        {
            EnsureMaterial();

            pass = new FadeRenderPass(runtimeMaterial)
            {
                renderPassEvent = settings.renderPassEvent
            };
        }

        private void EnsureMaterial()
        {
            if (settings.overrideMaterial != null)
            {
                runtimeMaterial = settings.overrideMaterial;
                return;
            }

            if (defaultShader == null)
                defaultShader = Shader.Find(DefaultShaderName);

            if (defaultShader == null)
            {
                Debug.LogError($"[VRFade] 找不到默认 shader '{DefaultShaderName}'。请确认 VRFade.shader 已在工程中。");
                return;
            }

            if (runtimeMaterial == null || runtimeMaterial.shader != defaultShader)
            {
                CoreUtils.Destroy(runtimeMaterial);
                runtimeMaterial = CoreUtils.CreateEngineMaterial(defaultShader);
                runtimeMaterial.name = "VRFade Runtime";
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
#if UNITY_EDITOR
            // 同步 Editor 预览状态
            FadeRuntime.EditorPreviewActive = settings.editorPreview;
            if (settings.editorPreview)
            {
                var ps = FadeState.Default;
                ps.type = settings.previewType;
                ps.color = settings.previewColor;
                ps.alpha = settings.previewAlpha;
                FadeRuntime.EditorPreviewState = ps;
            }
#endif

            // VR 性能关键：alpha=0 时直接 skip 整个 Pass，零开销
            var s = FadeRuntime.GetEffective();
            if (s.alpha <= 0.001f) return;

            // 跳过非主相机（Preview / Reflection 等），避免缩略图被涂黑
            var camType = renderingData.cameraData.cameraType;
            if (camType == CameraType.Preview || camType == CameraType.Reflection) return;

            EnsureMaterial();
            if (runtimeMaterial == null) return;

            pass.renderPassEvent = settings.renderPassEvent;
            pass.SetMaterial(runtimeMaterial);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            // 重要：释放 pass 内的临时 RT，否则切换 URP 设置 / 域重载时会泄漏
            pass?.Dispose();
            pass = null;

            if (settings.overrideMaterial == null)
            {
                CoreUtils.Destroy(runtimeMaterial);
            }
            runtimeMaterial = null;
        }
    }
}
