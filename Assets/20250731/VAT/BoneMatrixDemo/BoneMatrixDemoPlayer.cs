using UnityEngine;

namespace VATLearning
{
    // CPU 只更新播放参数；不更新顶点、不计算运行时骨骼矩阵。
    public sealed class BoneMatrixDemoPlayer : MonoBehaviour
    {
        public MeshRenderer animatedRenderer;
        public int frameCount = 120;
        public float framesPerSecond = 60f;
        public bool playing = true;
        public bool interpolate = true;
        public bool applySkinning = true;
        [Range(0f, 119f)] public float frame;

        private MaterialPropertyBlock properties;
        private static readonly int FrameId = Shader.PropertyToID("_Frame");
        private static readonly int InterpolateId = Shader.PropertyToID("_Interpolate");
        private static readonly int SkinningId = Shader.PropertyToID("_ApplySkinning");

        private void OnEnable() => ApplyParameters();

        private void Update()
        {
            if (playing)
                frame = Mathf.Repeat(frame + Time.deltaTime * framesPerSecond, Mathf.Max(1, frameCount));
            ApplyParameters();
        }

        private void ApplyParameters()
        {
            if (animatedRenderer == null) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            animatedRenderer.GetPropertyBlock(properties);
            properties.SetFloat(FrameId, frame);
            properties.SetFloat(InterpolateId, interpolate ? 1f : 0f);
            properties.SetFloat(SkinningId, applySkinning ? 1f : 0f);
            animatedRenderer.SetPropertyBlock(properties);
        }

        private void OnGUI()
        {
            // 使用英文 UI，避免默认 IMGUI 字体缺少中文字形；中文步骤见 README。
            float width = Mathf.Min(440f, Screen.width - 24f);
            GUILayout.BeginArea(new Rect(12, 12, width, 190), GUI.skin.box);
            GUILayout.Label("BONE MATRIX TEXTURE / Two-bone ribbon");
            GUILayout.Label("LEFT: bind pose     RIGHT: GPU skinning");
            GUILayout.Label("Blue = bone 0     Orange = bone 1     Gradient = blend");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(playing ? "Pause" : "Play")) playing = !playing;
            if (GUILayout.Button("Next frame"))
            {
                playing = false;
                frame = Mathf.Repeat(Mathf.Floor(frame) + 1f, Mathf.Max(1, frameCount));
            }
            if (GUILayout.Button("Frame 0")) { playing = false; frame = 0; }
            GUILayout.EndHorizontal();
            interpolate = GUILayout.Toggle(interpolate, "Interpolate adjacent baked frames");
            applySkinning = GUILayout.Toggle(applySkinning, "Apply bone matrices");
            // 自动播放仍需经过 119.x → 0；滑条末端的截断不能把播放误判为用户拖动。
            float sliderFrame = Mathf.Min(frame, Mathf.Max(0, frameCount - 1));
            float scrubbed = GUILayout.HorizontalSlider(sliderFrame, 0f, Mathf.Max(0, frameCount - 1));
            if (!Mathf.Approximately(scrubbed, sliderFrame)) { playing = false; frame = scrubbed; }
            int f0 = Mathf.FloorToInt(frame);
            GUILayout.Label($"Frame {f0} -> {(f0 + 1) % Mathf.Max(1, frameCount)}    blend {(interpolate ? frame - f0 : 0f):F2}");
            GUILayout.EndArea();
            ApplyParameters();
        }
    }
}
