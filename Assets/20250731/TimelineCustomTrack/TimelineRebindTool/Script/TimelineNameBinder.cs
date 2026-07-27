// Assets/Scripts/Timeline/TimelineNameBinder.cs
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 把 Timeline 绑定信息以"名字 → 物体"的形式存在组件上,从而脱离 TrackAsset 的对象身份。
/// 复制 .playable 后(轨道实例全变)、或复制本 GameObject 后,点 Apply 即可按名字重绑。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(PlayableDirector))]
[DisallowMultipleComponent]
public class TimelineNameBinder : MonoBehaviour
{
    [Serializable]
    public class TrackBinding
    {
        public string trackName;
        public string trackType;          // 用 Type.FullName 区分同名不同类型的轨道
        public UnityEngine.Object target;
    }

    [Serializable]
    public class ExposedBinding
    {
        public string trackName;
        public int clipIndex;             // 片段在该轨道 GetClips() 里的序号
        public string fieldName;          // ExposedReference 字段名(如 sourceGameObject)
        public UnityEngine.Object target;
    }

    [Tooltip("OnEnable 时自动重绑(Play / 场景加载后兜底)。编辑期一般用 Inspector 的 Apply 即可,默认关以免频繁脏化场景。")]
    public bool applyOnEnable = false;

    [SerializeField] private List<TrackBinding> _trackBindings = new List<TrackBinding>();
    [SerializeField] private List<ExposedBinding> _exposedBindings = new List<ExposedBinding>();

    public IReadOnlyList<TrackBinding> TrackBindings => _trackBindings;
    public IReadOnlyList<ExposedBinding> ExposedBindings => _exposedBindings;

    private PlayableDirector Director => GetComponent<PlayableDirector>();

    private void OnEnable()
    {
        if (applyOnEnable) Apply();
    }

    /// <summary>把当前 Director 的绑定快照进本组件。</summary>
    public void Capture()
    {
        _trackBindings.Clear();
        _exposedBindings.Clear();

        var director = Director;
        if (director == null || !(director.playableAsset is TimelineAsset asset)) return;

        foreach (var track in asset.GetOutputTracks())
        {
            // 1) 轨道 generic binding
            var bound = director.GetGenericBinding(track);
            if (bound != null)
            {
                _trackBindings.Add(new TrackBinding
                {
                    trackName = track.name,
                    trackType = track.GetType().FullName,
                    target = bound
                });
            }

            // 2) 片段上的 ExposedReference(Control 等)
            int clipIndex = 0;
            foreach (var clip in track.GetClips())
            {
                var clipAsset = clip.asset as UnityEngine.Object;
                if (clipAsset != null)
                {
                    foreach (var field in TimelineRebindUtility.GetExposedReferenceFields(clipAsset.GetType()))
                    {
                        var name = TimelineRebindUtility.GetExposedName(clipAsset, field);
                        if (!TimelineRebindUtility.IsValidName(name)) continue;

                        var val = director.GetReferenceValue(name, out bool valid);
                        if (valid && val != null)
                        {
                            _exposedBindings.Add(new ExposedBinding
                            {
                                trackName = track.name,
                                clipIndex = clipIndex,
                                fieldName = field.Name,
                                target = val
                            });
                        }
                    }
                }
                clipIndex++;
            }
        }
    }

    /// <summary>按名字把快照写回当前 Director(asset 可以是复制出来的新资源)。</summary>
    public void Apply()
    {
        var director = Director;
        if (director == null || !(director.playableAsset is TimelineAsset asset)) return;

        var tracks = new List<TrackAsset>(asset.GetOutputTracks());

        // 1) 轨道 binding:名字+类型匹配,同名同类型按顺序消费
        var pool = new List<TrackAsset>(tracks);
        foreach (var tb in _trackBindings)
        {
            if (tb.target == null) continue;
            int idx = pool.FindIndex(t => t.name == tb.trackName && t.GetType().FullName == tb.trackType);
            if (idx < 0) continue;
            director.SetGenericBinding(pool[idx], tb.target);
            pool.RemoveAt(idx);
        }

        // 2) ExposedReference:trackName 找轨道 → clipIndex 找片段 → fieldName 找字段,
        //    读取该字段【当前】的 exposedName(复制后可能已重生成),再写入物体
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        foreach (var eb in _exposedBindings)
        {
            if (eb.target == null) continue;

            var track = FindTrackByName(tracks, eb.trackName);
            if (track == null) continue;

            var clip = GetClipAt(track, eb.clipIndex);
            var clipAsset = clip?.asset as UnityEngine.Object;
            if (clipAsset == null) continue;

            var field = clipAsset.GetType().GetField(eb.fieldName, flags);
            if (field == null) continue;

            var name = TimelineRebindUtility.GetExposedName(clipAsset, field);
            if (!TimelineRebindUtility.IsValidName(name)) continue;

            director.SetReferenceValue(name, eb.target);
        }
    }

    private static TrackAsset FindTrackByName(List<TrackAsset> tracks, string name)
    {
        foreach (var t in tracks) if (t.name == name) return t;
        return null;
    }

    private static TimelineClip GetClipAt(TrackAsset track, int index)
    {
        int i = 0;
        foreach (var c in track.GetClips())
        {
            if (i == index) return c;
            i++;
        }
        return null;
    }
}
