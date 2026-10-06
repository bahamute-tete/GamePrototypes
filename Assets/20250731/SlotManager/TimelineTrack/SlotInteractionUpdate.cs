using System.Collections.Generic;
using UnityEngine;

namespace SlotSystem.Timeline
{
    // Final pose pass after Timeline, animation and spline movement. Also services paused previews.
    [ExecuteAlways, DefaultExecutionOrder(32000)]
    public sealed class SlotInteractionUpdate : MonoBehaviour
    {
        private static SlotInteractionUpdate instance;
        private static readonly List<SlotInteractionMixer> Mixers = new List<SlotInteractionMixer>();

        internal static void Register(SlotInteractionMixer mixer)
        {
            if (!Mixers.Contains(mixer)) Mixers.Add(mixer);
            if (instance != null) return;
            var go = new GameObject("Slot Interaction Preview Driver") { hideFlags = HideFlags.HideAndDontSave };
            instance = go.AddComponent<SlotInteractionUpdate>();
            if (Application.isPlaying) DontDestroyOnLoad(go);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EvaluateAfterAnimation;
            UnityEditor.EditorApplication.update += EvaluateAfterAnimation;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
        }

        internal static void Unregister(SlotInteractionMixer mixer)
        {
            Mixers.Remove(mixer);
            if (Mixers.Count != 0) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EvaluateAfterAnimation;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
#endif
            if (instance != null)
            {
                var go = instance.gameObject;
                instance = null;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            }
        }

        private void LateUpdate() => EvaluateAfterAnimation();

        private static void Shutdown()
        {
            while (Mixers.Count > 0) Mixers[Mixers.Count - 1].RestoreAndUnregister();
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            Shutdown();
        }

#if UNITY_EDITOR
        private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingEditMode ||
                state == UnityEditor.PlayModeStateChange.ExitingPlayMode) Shutdown();
        }
#endif

        // Call after director.Evaluate() when manually evaluating animated bones in the same method.
        public static void EvaluateAfterAnimation()
        {
            for (int i = 0; i < Mixers.Count; i++) Mixers[i].PrepareCurrentTime();
            for (int i = 0; i < Mixers.Count; i++) Mixers[i].EvaluateCurrentTime();
        }
    }
}
