using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Timeline;

internal static class FogEditorUtility
{
    public static FogTrack FindTrack(FogClip clip)
    {
        string path = AssetDatabase.GetAssetPath(clip);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<FogTrack>()
            .FirstOrDefault(t => t.GetClips().Any(c => c.asset == clip));
    }

    public static string ModeName(FogMode mode)
    {
        return mode == FogMode.Linear ? "线性" : mode == FogMode.Exponential ? "指数" : "指数平方";
    }

    public static void Refresh()
    {
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
        SceneView.RepaintAll();
    }
}

[CustomEditor(typeof(FogTrack)), CanEditMultipleObjects]
public class FogTrackInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("控制当前活动场景的全局雾，无需绑定对象。\n暂停保留效果；片段空档、停止或退出预览恢复原始设置。", MessageType.Info);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mode"), new GUIContent("雾模式"));
        if (serializedObject.ApplyModifiedProperties()) FogEditorUtility.Refresh();
        var track = (FogTrack)target;
        if (track.mode != FogTrackMode.Scene)
            EditorGUILayout.HelpBox("整条轨道使用同一种模式。若场景原始雾模式不同，进入片段时会切换算法，建议与场景保持一致以获得平滑过渡。", MessageType.Info);
        var timeline = track.timelineAsset;
        if (timeline != null && timeline.GetOutputTracks().OfType<FogTrack>().Count(t => !t.mutedInHierarchy) > 1)
            EditorGUILayout.HelpBox("此 Timeline 有多条未静音的 Fog Track。请避免片段同时生效，否则先取得控制权的轨道生效，其他轨道会提示冲突。", MessageType.Warning);
    }
}

[CustomEditor(typeof(FogClip)), CanEditMultipleObjects]
public class FogClipInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var clip = (FogClip)target;
        var track = FogEditorUtility.FindTrack(clip);
        FogMode mode = track != null ? track.ResolveMode(RenderSettings.fogMode) : RenderSettings.fogMode;
        bool mixedModes = targets.Cast<FogClip>().Any(c =>
        {
            var t = FogEditorUtility.FindTrack(c);
            return (t != null ? t.ResolveMode(RenderSettings.fogMode) : RenderSettings.fogMode) != mode;
        });
        EditorGUILayout.LabelField("当前雾模式", mixedModes ? "多种模式（显示全部参数）" : FogEditorUtility.ModeName(mode));
        EditorGUILayout.HelpBox("雾模式在轨道上设置。拖动片段两端的淡入淡出手柄可控制过渡；交叠片段会自动混合。", MessageType.Info);
        var value = serializedObject.FindProperty("template");
        EditorGUILayout.PropertyField(value.FindPropertyRelative("fogColor"), new GUIContent("雾颜色"));
        if (mixedModes || mode == FogMode.Linear)
        {
            EditorGUILayout.PropertyField(value.FindPropertyRelative("fogStartDistance"), new GUIContent("开始距离（米）"));
            EditorGUILayout.PropertyField(value.FindPropertyRelative("fogEndDistance"), new GUIContent("完全遮蔽距离（米）"));
        }
        if (mixedModes || mode != FogMode.Linear)
            EditorGUILayout.PropertyField(value.FindPropertyRelative("fogDensity"), new GUIContent("雾密度"));
        if (serializedObject.ApplyModifiedProperties()) FogEditorUtility.Refresh();

        bool preview = FogMixer.IsControllingFog || AnimationMode.InAnimationMode() || Application.isPlaying;
        if (preview)
            EditorGUILayout.HelpBox("当前处于预览或播放中：读取按钮会捕获此刻画面中的雾参数。若要读取场景原始设置，请先退出预览。", MessageType.Warning);
        if (GUILayout.Button("读取当前场景雾"))
        {
            Undo.RecordObjects(targets, "读取当前场景雾");
            foreach (FogClip item in targets)
            {
                item.template.ReadScene();
                EditorUtility.SetDirty(item);
            }
            serializedObject.Update();
            FogEditorUtility.Refresh();
        }
        EditorGUILayout.HelpBox("读取颜色、距离和密度，不改变整条轨道的模式。场景雾关闭时仍读取其参数；片段生效时会启用雾。", MessageType.None);
    }
}

[CustomTimelineEditor(typeof(FogClip))]
public class FogTimelineClipEditor : ClipEditor
{
    public override void OnCreate(TimelineClip clip, TrackAsset track, TimelineClip clonedFrom)
    {
        base.OnCreate(clip, track, clonedFrom);
        // Duplicating a clip must preserve the source settings.
        if (clonedFrom != null) return;
        var asset = (FogClip)clip.asset;
        if (!FogMixer.IsControllingFog && !AnimationMode.InAnimationMode() && !Application.isPlaying)
        {
            Undo.RecordObject(asset, "初始化场景雾参数");
            asset.template.ReadScene();
            EditorUtility.SetDirty(asset);
        }
        clip.displayName = "雾";
    }

    public override ClipDrawOptions GetClipOptions(TimelineClip clip)
    {
        var options = base.GetClipOptions(clip);
        var value = ((FogClip)clip.asset).template;
        options.displayClipName = false;
        options.highlightColor = new Color(value.fogColor.r, value.fogColor.g, value.fogColor.b, 1f);
        options.tooltip = Summary(clip) + "\n拖动两端淡入淡出，交叠片段自动混合";
        return options;
    }

    public override void DrawBackground(TimelineClip clip, ClipBackgroundRegion region)
    {
        var value = ((FogClip)clip.asset).template;
        var rect = region.position;
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f), new Color(value.fogColor.r, value.fogColor.g, value.fogColor.b, 1f));
        string label = rect.width > 140f ? clip.displayName + " · " + Summary(clip) : clip.displayName;
        GUI.Label(new Rect(rect.x + 4f, rect.y + (rect.height - 14f) * 0.5f, Mathf.Max(0f, rect.width - 8f), 14f), label, EditorStyles.whiteMiniLabel);
    }

    private static string Summary(TimelineClip clip)
    {
        var value = ((FogClip)clip.asset).template;
        var track = clip.GetParentTrack() as FogTrack;
        var mode = track != null ? track.ResolveMode(RenderSettings.fogMode) : RenderSettings.fogMode;
        return mode == FogMode.Linear
            ? $"线性 · {value.fogStartDistance:0.##}–{value.fogEndDistance:0.##} m"
            : $"{FogEditorUtility.ModeName(mode)} · 密度 {value.fogDensity:0.####}";
    }
}

[InitializeOnLoad]
internal static class FogPreviewCleanup
{
    private static bool wasPreviewing;
    private static bool resumeAfterSave;

    static FogPreviewCleanup()
    {
        AssemblyReloadEvents.beforeAssemblyReload += FogMixer.RestoreActivePreview;
        EditorApplication.quitting += FogMixer.RestoreActivePreview;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                FogMixer.RestoreActivePreview();
        };
        Undo.undoRedoPerformed += FogEditorUtility.Refresh;
        // RenderSettings is not a bound Component: don't serialize a preview into the scene.
        EditorSceneManager.sceneSaving += (scene, path) =>
        {
            if (EditorApplication.isPlaying || !FogMixer.IsControllingFog) return;
            resumeAfterSave = true;
            FogMixer.RestoreActivePreview();
        };
        EditorSceneManager.sceneSaved += scene =>
        {
            if (!resumeAfterSave) return;
            resumeAfterSave = false;
            TimelineEditor.Refresh(RefreshReason.SceneNeedsUpdate);
        };
        EditorApplication.update += () =>
        {
            bool previewing = AnimationMode.InAnimationMode();
            if (!EditorApplication.isPlaying && wasPreviewing && !previewing)
                FogMixer.RestoreActivePreview();
            wasPreviewing = previewing;
        };
    }
}
