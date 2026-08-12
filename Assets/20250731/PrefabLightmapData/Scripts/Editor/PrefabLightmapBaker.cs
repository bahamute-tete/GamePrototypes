#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LiangZhu.Lighting
{
    /// <summary>
    /// 烘焙后从场景实例抓取 per-renderer lightmap 数据,写入 PrefabLightmapData 组件,
    /// 并 Apply 回 prefab asset,使数据随预设持久化。
    /// </summary>
    [InitializeOnLoad]
    public static class PrefabLightmapBaker
    {
        static bool s_HasFreshBake;
        static string s_FreshBakeSceneSignature;

        static PrefabLightmapBaker()
        {
            Lightmapping.bakeStarted += OnBakeStarted;
            Lightmapping.bakeCompleted += OnBakeCompleted;
        }

        static void OnBakeStarted()
        {
            InvalidateFreshBake();
        }

        static void OnBakeCompleted()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            s_HasFreshBake = true;
            s_FreshBakeSceneSignature = BuildLoadedSceneSignature();
            Debug.Log("[PrefabLightmapBaker] 已记录本次编辑会话刚完成的烘焙，现在可以执行 Bake Prefab Lightmaps。");
        }

        internal static void BakePrefabLightmaps()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[PrefabLightmapBaker] 不能在 Play Mode 中把烘焙数据写回 Prefab。");
                return;
            }

            if (EditorApplication.isCompiling || Lightmapping.isRunning)
            {
                Debug.LogError("[PrefabLightmapBaker] Unity 正在编译或烘焙，请等待完成后再执行。");
                return;
            }

            if (!TryValidateFreshBake(out string validationError))
            {
                Debug.LogError(
                    $"[PrefabLightmapBaker] {validationError} " +
                    "请先执行 Generate Lighting，等待烘焙完成后立即执行本菜单。");
                return;
            }

            // 预览会临时改写 Renderer 索引和全局光图；抓取新烘焙数据前必须先恢复。
            PrefabLightmapPreviewManager.StopAllPreviews();

            var currentLightmaps = LightmapSettings.lightmaps;
            if (currentLightmaps == null || currentLightmaps.Length == 0)
            {
                Debug.LogWarning("[PrefabLightmapBaker] 当前场景没有已烘焙的 lightmap,请先 Generate Lighting。");
                return;
            }

            var datas = Object.FindObjectsByType<PrefabLightmapData>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (datas.Length == 0)
            {
                Debug.LogWarning("[PrefabLightmapBaker] 场景中找不到 PrefabLightmapData,请先在地砖预设根节点挂上该组件。");
                return;
            }

            var groups = new SortedDictionary<string, List<PrefabLightmapData>>();
            int skipped = 0;
            foreach (var data in datas)
            {
                if (data == null || EditorUtility.IsPersistent(data) || !data.gameObject.scene.IsValid())
                {
                    skipped++;
                    continue;
                }

                if (!PrefabUtility.IsPartOfPrefabInstance(data))
                {
                    Debug.LogWarning(
                        $"[PrefabLightmapBaker] 跳过 '{data.name}'：它不是可写回的 Prefab 场景实例。",
                        data);
                    skipped++;
                    continue;
                }

                string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(data);
                if (string.IsNullOrEmpty(assetPath))
                {
                    Debug.LogWarning(
                        $"[PrefabLightmapBaker] 跳过 '{data.name}'：无法取得 Prefab 资源路径。",
                        data);
                    skipped++;
                    continue;
                }

                if (!groups.TryGetValue(assetPath, out var group))
                {
                    group = new List<PrefabLightmapData>();
                    groups.Add(assetPath, group);
                }

                group.Add(data);
            }

            int applied = 0;
            int failed = 0;
            foreach (var pair in groups)
            {
                var data = ChooseBakeSource(pair.Key, pair.Value);
                if (data == null)
                {
                    failed++;
                    continue;
                }

                try
                {
                    if (!GenerateLightmapInfo(data))
                    {
                        failed++;
                        continue;
                    }

                    EditorUtility.SetDirty(data);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(data);
                    PrefabUtility.ApplyObjectOverride(data, pair.Key, InteractionMode.AutomatedAction);
                    applied++;
                }
                catch (System.Exception exception)
                {
                    failed++;
                    Debug.LogException(exception, data);
                }
            }

            if (applied > 0)
                AssetDatabase.SaveAssets();

            if (applied == 0)
            {
                Debug.LogError(
                    $"[PrefabLightmapBaker] 没有任何数据写回 Prefab。候选 {datas.Length} 个，" +
                    $"跳过 {skipped} 个，失败/有歧义 {failed} 组。");
                return;
            }

            if (failed == 0)
            {
                InvalidateFreshBake();
            }
            else
            {
                Debug.LogWarning(
                    "[PrefabLightmapBaker] 有 Prefab 未写回，未消费本次新鲜烘焙标记；" +
                    "修正选择或错误后可直接重试。只会再次处理当前场景中的候选 Prefab。");
            }

            Debug.Log(
                $"[PrefabLightmapBaker] 完成：找到 {datas.Length} 个组件，写回 {applied} 个 Prefab，" +
                $"跳过 {skipped} 个，失败/有歧义 {failed} 组。");
        }

        internal static bool TryGetFreshBakeStatus(out string error)
        {
            return TryValidateFreshBake(out error);
        }

        static bool TryValidateFreshBake(out string error)
        {
            if (!s_HasFreshBake)
            {
                error = "当前编辑会话没有检测到新完成的烘焙，现有 LightmapSettings 可能来自旧数据或场景重载。";
                return false;
            }

            string currentSignature = BuildLoadedSceneSignature();
            if (s_FreshBakeSceneSignature != currentSignature)
            {
                InvalidateFreshBake();
                error = "烘焙完成后，已加载场景集合或活动场景发生了变化，无法确认光图仍属于本次烘焙。";
                return false;
            }

            error = null;
            return true;
        }

        static void InvalidateFreshBake()
        {
            s_HasFreshBake = false;
            s_FreshBakeSceneSignature = null;
        }

        static string BuildLoadedSceneSignature()
        {
            var builder = new StringBuilder();
            var activeScene = SceneManager.GetActiveScene();
            builder.Append("active:")
                .Append(activeScene.handle)
                .Append(':')
                .Append(activeScene.path);

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                builder.Append("|scene:")
                    .Append(scene.handle)
                    .Append(':')
                    .Append(scene.path);
            }

            return builder.ToString();
        }

        static PrefabLightmapData ChooseBakeSource(
            string assetPath,
            List<PrefabLightmapData> candidates)
        {
            if (candidates.Count == 1)
                return candidates[0];

            PrefabLightmapData selected = null;
            int selectedCount = 0;
            foreach (var candidate in candidates)
            {
                if (!IsSelected(candidate)) continue;
                selected = candidate;
                selectedCount++;
            }

            if (selectedCount == 1)
                return selected;

            Debug.LogError(
                $"[PrefabLightmapBaker] Prefab '{assetPath}' 在场景中有 {candidates.Count} 个实例。" +
                "请在 Hierarchy 中只选中其中一个实例作为烘焙源，然后重新执行菜单。",
                candidates[0]);
            return null;
        }

        static bool IsSelected(PrefabLightmapData candidate)
        {
            foreach (var selectedObject in Selection.gameObjects)
            {
                var selectedTransform = selectedObject.transform;
                if (selectedTransform == candidate.transform ||
                    selectedTransform.IsChildOf(candidate.transform) ||
                    candidate.transform.IsChildOf(selectedTransform))
                {
                    return true;
                }
            }

            return false;
        }

        static bool GenerateLightmapInfo(PrefabLightmapData target)
        {
            var rendererInfos = new List<PrefabLightmapData.RendererInfo>();
            var lightmaps = new List<Texture2D>();
            var lightmapsDir = new List<Texture2D>();
            var shadowMasks = new List<Texture2D>();

            var sceneLightmaps = LightmapSettings.lightmaps;
            var renderers = target.GetComponentsInChildren<MeshRenderer>(true);

            foreach (var r in renderers)
            {
                int idx = r.lightmapIndex;
                if (idx < 0 || idx >= sceneLightmaps.Length)
                    continue; // 该 Renderer 未参与烘焙(-1 / 65535)或越界

                var lm = sceneLightmaps[idx];
                if (lm == null || lm.lightmapColor == null)
                    continue;

                int local = FindLightmap(lightmaps, lightmapsDir, shadowMasks, lm);
                if (local < 0)
                {
                    local = lightmaps.Count;
                    lightmaps.Add(lm.lightmapColor);
                    lightmapsDir.Add(lm.lightmapDir);
                    shadowMasks.Add(lm.shadowMask);
                }

                rendererInfos.Add(new PrefabLightmapData.RendererInfo
                {
                    renderer = r,
                    lightmapIndex = local,
                    lightmapScaleOffset = r.lightmapScaleOffset
                });
            }

            if (rendererInfos.Count == 0)
            {
                Debug.LogError(
                    $"[PrefabLightmapBaker] '{target.name}' 没有抓到任何带有效颜色光图的 MeshRenderer，" +
                    "已保留 Prefab 中原有的光照数据。",
                    target);
                return false;
            }

            target.RendererInfos = rendererInfos.ToArray();
            target.Lightmaps = lightmaps.ToArray();
            target.LightmapsDir = lightmapsDir.ToArray();
            target.ShadowMasks = shadowMasks.ToArray();
            target.CapturedLightmapsMode = GetCapturedLightmapsMode(lightmapsDir);

            Debug.Log($"[PrefabLightmapBaker] '{target.name}' 抓到 {rendererInfos.Count} 个 Renderer / {lightmaps.Count} 张 lightmap。");
            return true;
        }

        static LightmapsMode GetCapturedLightmapsMode(List<Texture2D> directions)
        {
            for (int i = 0; i < directions.Count; i++)
            {
                if (directions[i] != null)
                    return LightmapsMode.CombinedDirectional;
            }

            return LightmapsMode.NonDirectional;
        }

        static int FindLightmap(
            List<Texture2D> colors,
            List<Texture2D> directions,
            List<Texture2D> shadowMasks,
            LightmapData candidate)
        {
            for (int i = 0; i < colors.Count; i++)
            {
                if (colors[i] == candidate.lightmapColor &&
                    directions[i] == candidate.lightmapDir &&
                    shadowMasks[i] == candidate.shadowMask)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
#endif
