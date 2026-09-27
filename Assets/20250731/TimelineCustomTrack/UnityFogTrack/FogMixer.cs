using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public class FogMixer : PlayableBehaviour
{
    // RenderSettings is global to the active scene. Only one mixer may own it at a time.
    private static FogMixer owner;
    private bool warnedAboutConflict;
    private FogSnapshot original;
    private Scene capturedScene;
    public FogTrack Track { get; set; }
    public static bool IsControllingFog => owner != null;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        Color color = Color.clear;
        float start = 0f, end = 0f, density = 0f, total = 0f;
        for (int i = 0; i < playable.GetInputCount(); i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= 0f) continue;
            var input = (ScriptPlayable<FogBehaviour>)playable.GetInput(i);
            var value = input.GetBehaviour();
            color += value.fogColor * weight;
            start += Mathf.Max(0f, value.fogStartDistance) * weight;
            end += Mathf.Max(value.fogStartDistance + 0.01f, value.fogEndDistance) * weight;
            density += Mathf.Max(0f, value.fogDensity) * weight;
            total += weight;
        }

        if (total <= 0f)
        {
            Release();
            return;
        }

        if (owner != null && owner.capturedScene != SceneManager.GetActiveScene()) owner.Release();
        if (owner != null && owner != this)
        {
            if (!warnedAboutConflict)
            {
                Debug.LogWarning("多条 Fog Track 正在同时控制全局雾。当前轨道暂不写入，请错开片段或只保留一条 Fog Track。", Track);
                warnedAboutConflict = true;
            }
            return;
        }

        if (owner == null)
        {
            // Capture before the first write, including manual Evaluate while not playing.
            original = FogSnapshot.Capture();
            capturedScene = SceneManager.GetActiveScene();
            owner = this;
        }

        FogMode mode = Track != null ? Track.ResolveMode(original.mode) : original.mode;
        FogSnapshot target = new FogSnapshot
        {
            enabled = true, mode = mode, color = color / total,
            start = start / total, end = end / total, density = density / total
        };
        Blend(original, target, Mathf.Clamp01(total)).Apply();
    }

    // A pause stops graph playback too. Keep the evaluated frame until the graph is destroyed.
    public override void OnPlayableDestroy(Playable playable) => Release();

    public static void RestoreActivePreview()
    {
        if (owner != null) owner.Release();
    }

    private void Release()
    {
        if (owner != this) return;
        owner = null;
        if (!capturedScene.IsValid() || !capturedScene.isLoaded) return;
        Scene active = SceneManager.GetActiveScene();
        // Restore the captured scene, never accidentally write its settings into a new scene.
        if (active == capturedScene)
        {
            original.Apply();
            return;
        }
        if (!SceneManager.SetActiveScene(capturedScene)) return;
        try { original.Apply(); }
        finally
        {
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        }
    }

    public static FogSnapshot Blend(FogSnapshot baseline, FogSnapshot target, float weight)
    {
        weight = Mathf.Clamp01(weight);
        if (weight <= 0f) return baseline;
        if (weight >= 1f) return target;
        var result = target;
        if (baseline.enabled)
        {
            result.color = Color.Lerp(baseline.color, target.color, weight);
            result.start = Mathf.Lerp(baseline.start, target.start, weight);
            result.end = Mathf.Max(result.start + 0.01f, Mathf.Lerp(baseline.end, target.end, weight));
            result.density = Mathf.Lerp(baseline.density, target.density, weight);
        }
        else
        {
            // Do not blend with the irrelevant color/density of disabled scene fog.
            // Built-in linear fog has no intensity: expanding its range approaches no fog
            // at every finite distance as weight approaches zero.
            result.end = target.start + Mathf.Max(0.01f, target.end - target.start) / weight;
            result.density = target.density * (target.mode == FogMode.ExponentialSquared ? Mathf.Sqrt(weight) : weight);
        }
        return result;
    }

    public struct FogSnapshot
    {
        public bool enabled;
        public Color color;
        public FogMode mode;
        public float start, end, density;

        public static FogSnapshot Capture() => new FogSnapshot
        {
            enabled = RenderSettings.fog, color = RenderSettings.fogColor,
            mode = RenderSettings.fogMode, start = RenderSettings.fogStartDistance,
            end = RenderSettings.fogEndDistance, density = RenderSettings.fogDensity
        };

        public void Apply()
        {
            RenderSettings.fogColor = color;
            RenderSettings.fogMode = mode;
            RenderSettings.fogStartDistance = start;
            RenderSettings.fogEndDistance = end;
            RenderSettings.fogDensity = density;
            RenderSettings.fog = enabled;
        }
    }
}
