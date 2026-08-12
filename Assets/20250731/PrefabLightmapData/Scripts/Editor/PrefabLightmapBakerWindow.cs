using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LiangZhu.Lighting
{
    public sealed class PrefabLightmapBakerWindow : EditorWindow
    {
        struct SceneSummary
        {
            public int ComponentCount;
            public int ValidInstanceCount;
            public int NonPrefabCount;
            public int AssetGroupCount;
            public int DuplicateGroupCount;
            public int AmbiguousGroupCount;
        }

        Vector2 m_ScrollPosition;
        bool m_ConfirmedSop;

        [MenuItem("LiangZhu/Lighting/Bake Prefab Lightmaps")]
        public static void ShowWindow()
        {
            var window = GetWindow<PrefabLightmapBakerWindow>();
            window.titleContent = new GUIContent("Prefab Lightmap Baker");
            window.minSize = new Vector2(600f, 620f);
            window.Show();
        }

        void OnEnable()
        {
            EditorApplication.update += Repaint;
        }

        void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Prefab 烘焙光图写回", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "此面板不会执行场景灯光烘焙，只会把刚完成的场景烘焙数据安全写入对应 Prefab。",
                EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(8f);
            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);
            DrawSop();
            EditorGUILayout.Space(10f);

            var summary = BuildSceneSummary();
            bool editorReady = !EditorApplication.isPlayingOrWillChangePlaymode &&
                               !EditorApplication.isCompiling &&
                               !Lightmapping.isRunning;
            bool hasFreshBake = PrefabLightmapBaker.TryGetFreshBakeStatus(out string freshBakeError);
            int lightmapCount = LightmapSettings.lightmaps != null
                ? LightmapSettings.lightmaps.Length
                : 0;

            DrawStatus(editorReady, hasFreshBake, freshBakeError, lightmapCount, summary);
            EditorGUILayout.Space(10f);
            DrawTools();
            EditorGUILayout.EndScrollView();

            //EditorGUILayout.Space(8f);
            //m_ConfirmedSop = EditorGUILayout.ToggleLeft(
            //    "我已阅读并确认上述 SOP，且烘焙完成后没有移动对象、切换或重载场景。",
            //    m_ConfirmedSop);

            bool canBake = editorReady &&
                           hasFreshBake &&
                           lightmapCount > 0 &&
                           summary.ComponentCount > 0 &&
                           summary.ValidInstanceCount > 0 &&
                           summary.NonPrefabCount == 0 &&
                           summary.AmbiguousGroupCount == 0;

            using (new EditorGUI.DisabledScope(!canBake))
            {
                if (GUILayout.Button("写入 Prefab 光图数据", GUILayout.Height(38f)))
                {
                    PrefabLightmapBaker.BakePrefabLightmaps();
                    m_ConfirmedSop = false;
                    GUIUtility.ExitGUI();
                }
            }

            if (!canBake)
            {
                EditorGUILayout.LabelField(
                    GetBlockingReason(
                        editorReady,
                        hasFreshBake,
                        lightmapCount,
                        summary,
                        m_ConfirmedSop),
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(8f);
        }

        static void DrawSop()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("标准操作流程（SOP）", EditorStyles.boldLabel);
            DrawStep(1, "资源准备", "先将目标物体做成Prefab。同一个 Prefab 的多个实例只能共享一套数据。");
            DrawStep(2, "组件检查", "每个目标 Prefab 根节点挂载 PrefabLightmapData，并确保子 Renderer 参与烘焙。");
            DrawStep(3, "场景烘焙", "在 Unity Lighting 窗口执行 Generate Lighting，并等待烘焙完全结束。");
            DrawStep(4, "保持现场", "烘焙完成后不要移动对象、切换/重载场景或修改光照设置。");
            DrawStep(5, "多实例处理", "如果同一 Prefab 在场景中有多个实例，只能在 Hierarchy 中选中一个作为写回源；若都要保存不同坐标，请先制作独立 Prefab。");
            DrawStep(6, "写回数据", "确认下方状态全部就绪，点击“写入 Prefab 光图数据”。写入一次后无法再次写入，除非重新烘焙。");
            EditorGUILayout.EndVertical();
        }

        static void DrawStep(int number, string title, string description)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(number.ToString(), EditorStyles.miniButton, GUILayout.Width(24f), GUILayout.Height(22f));
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4f);
        }

        static void DrawStatus(
            bool editorReady,
            bool hasFreshBake,
            string freshBakeError,
            int lightmapCount,
            SceneSummary summary)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("执行前状态", EditorStyles.boldLabel);

            DrawStatusRow(
                "Unity 状态",
                editorReady,
                editorReady ? "就绪" : "正在 Play Mode、编译或烘焙，请等待完成");
            DrawStatusRow(
                "本次新烘焙",
                hasFreshBake,
                hasFreshBake ? "已检测到，可以写回" : freshBakeError);
            DrawStatusRow(
                "全局光图",
                lightmapCount > 0,
                lightmapCount > 0 ? $"检测到 {lightmapCount} 张" : "没有光图，请先 Generate Lighting");
            DrawStatusRow(
                "PrefabLightmapData",
                summary.ComponentCount > 0,
                $"场景组件 {summary.ComponentCount} 个，可写 Prefab 实例 {summary.ValidInstanceCount} 个，Prefab 资源 {summary.AssetGroupCount} 个");
            DrawStatusRow(
                "非 Prefab 对象",
                summary.NonPrefabCount == 0,
                summary.NonPrefabCount == 0
                    ? "没有不可写对象"
                    : $"发现 {summary.NonPrefabCount} 个普通场景对象；请先将它们制作成独立 Prefab");
            DrawStatusRow(
                "同源多实例",
                summary.AmbiguousGroupCount == 0,
                summary.DuplicateGroupCount == 0
                    ? "没有重复 Prefab 实例"
                    : summary.AmbiguousGroupCount == 0
                        ? $"{summary.DuplicateGroupCount} 个 Prefab 存在多实例，且每组已选中一个写回源"
                        : $"有 {summary.AmbiguousGroupCount} 组尚未唯一选择写回源");

            EditorGUILayout.EndVertical();
        }

        static void DrawStatusRow(string label, bool success, string detail)
        {
            EditorGUILayout.BeginHorizontal();
            var oldColor = GUI.color;
            GUI.color = success
                ? new Color(0.45f, 0.9f, 0.5f)
                : new Color(1f, 0.65f, 0.35f);
            GUILayout.Label(success ? "●" : "●", GUILayout.Width(16f));
            GUI.color = oldColor;
            GUILayout.Label(label, EditorStyles.boldLabel, GUILayout.Width(120f));
            GUILayout.Label(detail ?? string.Empty, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2f);
        }

        static void DrawTools()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("打开 Lighting 窗口（Generate Lighting）"))
                EditorApplication.ExecuteMenuItem("Window/Rendering/Lighting");

            //if (GUILayout.Button("退出所有光图预览"))
            //    PrefabLightmapPreviewManager.StopAllPreviews();
            EditorGUILayout.EndHorizontal();
        }

        static SceneSummary BuildSceneSummary()
        {
            var summary = new SceneSummary();
            var groups = new Dictionary<string, List<PrefabLightmapData>>();
            var datas = Object.FindObjectsByType<PrefabLightmapData>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (var data in datas)
            {
                if (data == null || EditorUtility.IsPersistent(data) || !data.gameObject.scene.IsValid())
                    continue;

                summary.ComponentCount++;
                if (!PrefabUtility.IsPartOfPrefabInstance(data))
                {
                    summary.NonPrefabCount++;
                    continue;
                }

                string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(data);
                if (string.IsNullOrEmpty(assetPath))
                {
                    summary.NonPrefabCount++;
                    continue;
                }

                summary.ValidInstanceCount++;
                if (!groups.TryGetValue(assetPath, out var group))
                {
                    group = new List<PrefabLightmapData>();
                    groups.Add(assetPath, group);
                }

                group.Add(data);
            }

            summary.AssetGroupCount = groups.Count;
            foreach (var group in groups.Values)
            {
                if (group.Count <= 1) continue;
                summary.DuplicateGroupCount++;
                if (CountSelectedSources(group) != 1)
                    summary.AmbiguousGroupCount++;
            }

            return summary;
        }

        static int CountSelectedSources(List<PrefabLightmapData> candidates)
        {
            int count = 0;
            foreach (var candidate in candidates)
            {
                foreach (var selectedObject in Selection.gameObjects)
                {
                    var selectedTransform = selectedObject.transform;
                    if (selectedTransform == candidate.transform ||
                        selectedTransform.IsChildOf(candidate.transform) ||
                        candidate.transform.IsChildOf(selectedTransform))
                    {
                        count++;
                        break;
                    }
                }
            }

            return count;
        }

        static string GetBlockingReason(
            bool editorReady,
            bool hasFreshBake,
            int lightmapCount,
            SceneSummary summary,
            bool confirmedSop)
        {
            if (!editorReady) return "等待 Unity 完成当前操作。";
            if (!hasFreshBake) return "请先在 Lighting 窗口执行 Generate Lighting。";
            if (lightmapCount == 0) return "当前没有可写入的光图。";
            if (summary.ComponentCount == 0) return "场景中没有 PrefabLightmapData。";
            if (summary.ValidInstanceCount == 0) return "没有可写回的 Prefab 实例。";
            if (summary.NonPrefabCount > 0) return "请先处理状态面板列出的普通场景对象。";
            if (summary.AmbiguousGroupCount > 0) return "请为每组同源多实例在 Hierarchy 中只选择一个写回源。";
            if (!confirmedSop) return "请阅读 SOP 并勾选确认项。";
            return string.Empty;
        }
    }
}
