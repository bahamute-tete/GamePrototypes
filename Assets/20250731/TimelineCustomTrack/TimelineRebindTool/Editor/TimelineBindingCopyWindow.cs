// Assets/Scripts/Timeline/Editor/TimelineBindingCopyWindow.cs
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class TimelineBindingCopyWindow : EditorWindow
{
    private PlayableDirector _source;   // 旧的、绑定正确
    private PlayableDirector _target;   // 新的、用了复制后的 asset
    private bool _copyExposed = true;

    [MenuItem("Tools/Timeline/Copy Bindings Window")]
    private static void Open() => GetWindow<TimelineBindingCopyWindow>("Timeline Bindings");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "把 Source(绑定正确)的轨道绑定按『轨道名+类型』复制到 Target;\n" +
            "可选:同时复制 Control 等片段上的 ExposedReference(按匹配轨道内的片段序号对齐)。",
            MessageType.Info);

        _source = (PlayableDirector)EditorGUILayout.ObjectField("Source (旧)", _source, typeof(PlayableDirector), true);
        _target = (PlayableDirector)EditorGUILayout.ObjectField("Target (新)", _target, typeof(PlayableDirector), true);
        _copyExposed = EditorGUILayout.Toggle("复制 ExposedReference", _copyExposed);

        using (new EditorGUI.DisabledScope(_source == null || _target == null || _source == _target))
            if (GUILayout.Button("Copy Bindings"))
                Copy(_source, _target, _copyExposed);
    }

    private static void Copy(PlayableDirector source, PlayableDirector target, bool copyExposed)
    {
        if (!(source.playableAsset is TimelineAsset srcAsset) ||
            !(target.playableAsset is TimelineAsset dstAsset))
        {
            Debug.LogError("两个 Director 都必须绑定 TimelineAsset。");
            return;
        }

        Undo.RecordObject(target, "Copy Timeline Bindings");

        var srcPool = srcAsset.GetOutputTracks().ToList();
        var dstTracks = dstAsset.GetOutputTracks().ToList();
        var matched = new List<(TrackAsset src, TrackAsset dst)>();
        int genericCount = 0, exposedCount = 0;

        // 1) 轨道 generic binding
        foreach (var dst in dstTracks)
        {
            int idx = srcPool.FindIndex(s => s.name == dst.name && s.GetType() == dst.GetType());
            if (idx < 0) continue;
            var src = srcPool[idx];
            srcPool.RemoveAt(idx);
            matched.Add((src, dst));

            var binding = source.GetGenericBinding(src);
            if (binding != null)
            {
                target.SetGenericBinding(dst, binding);
                genericCount++;
            }
        }

        // 2) ExposedReference:每对匹配轨道内按片段序号对齐,读 src 名取值 → 写 dst 名
        if (copyExposed)
        {
            foreach (var (src, dst) in matched)
            {
                var srcClips = src.GetClips().ToList();
                var dstClips = dst.GetClips().ToList();
                int n = Mathf.Min(srcClips.Count, dstClips.Count);

                for (int i = 0; i < n; i++)
                {
                    var srcA = srcClips[i].asset as UnityEngine.Object;
                    var dstA = dstClips[i].asset as UnityEngine.Object;
                    if (srcA == null || dstA == null || srcA.GetType() != dstA.GetType()) continue;

                    foreach (var field in TimelineRebindUtility.GetExposedReferenceFields(srcA.GetType()))
                    {
                        var srcName = TimelineRebindUtility.GetExposedName(srcA, field);
                        if (!TimelineRebindUtility.IsValidName(srcName)) continue;

                        var val = source.GetReferenceValue(srcName, out bool valid);
                        if (!valid || val == null) continue;

                        var dstName = TimelineRebindUtility.GetExposedName(dstA, field);
                        if (!TimelineRebindUtility.IsValidName(dstName)) continue;

                        target.SetReferenceValue(dstName, val);
                        exposedCount++;
                    }
                }
            }
        }

        EditorUtility.SetDirty(target);
        Debug.Log($"[TimelineBindingCopy] generic {genericCount} 条,exposed {exposedCount} 条 → {target.name}");
    }
}
#endif