using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LiangZhu.Lighting
{
    [CustomEditor(typeof(PrefabLightmapData))]
    [CanEditMultipleObjects]
    public sealed class PrefabLightmapDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            bool anyPreviewing = false;
            foreach (var selectedTarget in targets)
            {
                var data = selectedTarget as PrefabLightmapData;
                if (data != null && PrefabLightmapPreviewManager.IsPreviewing(data))
                {
                    anyPreviewing = true;
                    break;
                }
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Play Mode 会自动恢复 Prefab 光图，不需要使用编辑器预览。",
                    MessageType.Info);
                return;
            }

            if (anyPreviewing)
            {
                if (GUILayout.Button("退出光图预览", GUILayout.Height(28f)))
                {
                    foreach (var selectedTarget in targets)
                    {
                        var data = selectedTarget as PrefabLightmapData;
                        if (data != null)
                            PrefabLightmapPreviewManager.StopPreview(data);
                    }
                }

                EditorGUILayout.HelpBox(
                    "当前只是在编辑器中临时预览。进入 Play Mode、开始烘焙、保存场景或脚本重载前会自动退出。",
                    MessageType.Info);
                return;
            }

            if (GUILayout.Button("预览烘焙光图", GUILayout.Height(28f)))
            {
                serializedObject.ApplyModifiedProperties();
                foreach (var selectedTarget in targets)
                {
                    var data = selectedTarget as PrefabLightmapData;
                    if (data == null) continue;

                    if (!PrefabLightmapPreviewManager.StartPreview(data, out string error))
                        Debug.LogError($"[PrefabLightmapData] 无法预览 '{data.name}'：{error}", data);
                }
            }
        }
    }

    [InitializeOnLoad]
    internal static class PrefabLightmapPreviewManager
    {
        struct RendererState
        {
            public Renderer Renderer;
            public int LightmapIndex;
            public Vector4 LightmapScaleOffset;
        }

        sealed class PreviewState
        {
            public RendererState[] Renderers;
        }

        static readonly Dictionary<PrefabLightmapData, PreviewState> s_Previews =
            new Dictionary<PrefabLightmapData, PreviewState>();

        static PrefabLightmapPreviewManager()
        {
            AssemblyReloadEvents.beforeAssemblyReload += StopAllPreviews;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.quitting += StopAllPreviews;
            EditorApplication.update += CleanupInvalidPreviews;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            Lightmapping.bakeStarted += StopAllPreviews;
        }

        public static bool IsPreviewing(PrefabLightmapData data)
        {
            return data != null && s_Previews.ContainsKey(data);
        }

        public static bool StartPreview(PrefabLightmapData data, out string error)
        {
            if (data == null)
            {
                error = "组件不存在。";
                return false;
            }

            if (Application.isPlaying)
            {
                error = "不能在 Play Mode 中启动编辑器预览。";
                return false;
            }

            if (!data.isActiveAndEnabled ||
                !data.gameObject.scene.IsValid() ||
                !data.gameObject.scene.isLoaded ||
                EditorUtility.IsPersistent(data))
            {
                error = "请在已加载场景中的有效 Prefab 实例上预览。";
                return false;
            }

            if (s_Previews.ContainsKey(data))
            {
                error = null;
                return true;
            }

            var states = CaptureRendererStates(data);
            if (states.Length == 0)
            {
                error = "没有可预览的 Renderer 光照数据，请先完成烘焙并写回 Prefab。";
                return false;
            }

            s_Previews.Add(data, new PreviewState { Renderers = states });
            try
            {
                data.Apply();
                if (!IsApplied(data))
                {
                    StopPreview(data);
                    error = "光图没有成功注册，可能与当前场景的 Directional Mode 冲突。";
                    return false;
                }
            }
            catch (System.Exception exception)
            {
                StopPreview(data);
                Debug.LogException(exception, data);
                error = "应用光图时发生异常，详细信息见 Console。";
                return false;
            }

            SceneView.RepaintAll();
            error = null;
            return true;
        }

        public static void StopPreview(PrefabLightmapData data)
        {
            if (ReferenceEquals(data, null) || !s_Previews.TryGetValue(data, out var state))
                return;

            s_Previews.Remove(data);
            if (data != null)
                data.RemoveEditorPreviewLightmaps();

            RestoreRendererStates(state.Renderers);
            SceneView.RepaintAll();
        }

        public static void StopAllPreviews()
        {
            if (s_Previews.Count == 0) return;

            var owners = new List<PrefabLightmapData>(s_Previews.Keys);
            foreach (var owner in owners)
                StopPreview(owner);
        }

        static RendererState[] CaptureRendererStates(PrefabLightmapData data)
        {
            var states = new List<RendererState>();
            var seen = new HashSet<Renderer>();
            var infos = data.RendererInfos;
            if (infos == null) return states.ToArray();

            foreach (var info in infos)
            {
                var renderer = info.renderer;
                if (renderer == null || !seen.Add(renderer)) continue;

                states.Add(new RendererState
                {
                    Renderer = renderer,
                    LightmapIndex = renderer.lightmapIndex,
                    LightmapScaleOffset = renderer.lightmapScaleOffset,
                });
            }

            return states.ToArray();
        }

        static void RestoreRendererStates(RendererState[] states)
        {
            foreach (var state in states)
            {
                if (state.Renderer == null) continue;
                state.Renderer.lightmapIndex = state.LightmapIndex;
                state.Renderer.lightmapScaleOffset = state.LightmapScaleOffset;
            }
        }

        static bool IsApplied(PrefabLightmapData data)
        {
            var infos = data.RendererInfos;
            var global = LightmapSettings.lightmaps;
            bool foundRenderer = false;

            if (infos == null || global == null) return false;
            foreach (var info in infos)
            {
                if (info.renderer == null) continue;
                if (info.lightmapIndex < 0 ||
                    data.Lightmaps == null ||
                    info.lightmapIndex >= data.Lightmaps.Length)
                {
                    return false;
                }

                int globalIndex = info.renderer.lightmapIndex;
                if (globalIndex < 0 || globalIndex >= global.Length)
                    return false;

                var lightmap = global[globalIndex];
                if (lightmap == null ||
                    lightmap.lightmapColor != GetTexture(data.Lightmaps, info.lightmapIndex) ||
                    lightmap.lightmapDir != GetTexture(data.LightmapsDir, info.lightmapIndex) ||
                    lightmap.shadowMask != GetTexture(data.ShadowMasks, info.lightmapIndex) ||
                    info.renderer.lightmapScaleOffset != info.lightmapScaleOffset)
                {
                    return false;
                }

                foundRenderer = true;
            }

            return foundRenderer;
        }

        static Texture2D GetTexture(Texture2D[] textures, int index)
        {
            return textures != null && index >= 0 && index < textures.Length
                ? textures[index]
                : null;
        }

        static void CleanupInvalidPreviews()
        {
            if (s_Previews.Count == 0) return;

            var invalidOwners = new List<PrefabLightmapData>();
            foreach (var owner in s_Previews.Keys)
            {
                if (owner == null ||
                    !owner.isActiveAndEnabled ||
                    !owner.gameObject.scene.IsValid() ||
                    !owner.gameObject.scene.isLoaded)
                {
                    invalidOwners.Add(owner);
                }
            }

            foreach (var owner in invalidOwners)
                StopPreview(owner);
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                StopAllPreviews();
        }

        static void OnSceneSaving(Scene scene, string path)
        {
            StopAllPreviews();
        }
    }
}
