#if UNITY_EDITOR
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Timeline;

// 给定一个 Timeline Clip 资产,反向查找其所在轨道的 Track Binding(Volume)。
// 工作前提:对应的 Timeline 正在 Timeline 窗口中处于活动(inspected)状态。
internal static class TimelineBindingUtility
{
    public static bool TryGetBoundVolume(PlayableAsset clipAsset, out Volume volume)
    {
        volume = null;

        var director = TimelineEditor.inspectedDirector;
        if (director == null) return false;

        var timeline = director.playableAsset as TimelineAsset;
        if (timeline == null) return false;

        TrackAsset ownerTrack = null;
        foreach (var track in timeline.GetOutputTracks())
        {
            foreach (var c in track.GetClips())
            {
                if (ReferenceEquals(c.asset, clipAsset))
                {
                    ownerTrack = track;
                    break;
                }
            }
            if (ownerTrack != null) break;
        }

        if (ownerTrack == null) return false;

        volume = director.GetGenericBinding(ownerTrack) as Volume;
        return volume != null && volume.profile != null;
    }
}
#endif
