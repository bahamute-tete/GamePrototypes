using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// 从 PlayableAsset 反查它所属的 TimelineClip，获取实际时长 / 起始时间 / 当前 playhead 位置。
///
/// 用途：让 CustomEditor 能把"归一化曲线时间"翻译成"秒数 UI"，并与 Timeline 播放头联动。
///
/// 查找策略：
///   1. 优先从 TimelineEditor.selectedClips 找（用户选中 Clip 时这里必然包含）。
///   2. 兜底：遍历 TimelineEditor.inspectedAsset 的所有 Track 的所有 Clip。
///   3. 都找不到 → Info.valid = false（降级为归一化模式）。
/// </summary>
public static class TimelineClipContext
{
    public struct Info
    {
        public bool         valid;              // 是否成功找到对应 TimelineClip
        public TimelineClip clip;               // 反查到的 TimelineClip 引用（用于修改 duration 等）
        public TimelineAsset timeline;          // Clip 所属的 TimelineAsset（用于 SetDirty / Refresh）
        public double       duration;           // Clip 时长（秒）
        public double       clipStart;          // Clip 在 Timeline 上的起始时间（秒）
        public bool         hasDirector;        // inspectedDirector 是否非空
        public double       currentTime;        // director.time（Timeline 全局时间，秒）
        public double       clipLocalTime;      // currentTime - clipStart，可能 <0 或 >duration
        public bool         playheadInClip;     // playhead 是否在本 Clip 时间范围内
        public float        normalizedPlayhead; // [0,1]，clipLocalTime / duration（Clamp01）
    }

    public static Info Resolve(PlayableAsset asset)
    {
        var info = new Info();
        if (asset == null) return info;

        TimelineClip foundClip = null;
        TimelineAsset foundTimeline = null;

        // 1. 从 selectedClips 找（最快路径）
        var selected = TimelineEditor.selectedClips;
        if (selected != null)
        {
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] != null && selected[i].asset == asset)
                {
                    foundClip = selected[i];
                    break;
                }
            }
        }

        // 2. 兜底：遍历 inspectedAsset 所有 Clip
        if (foundClip == null)
        {
            foundTimeline = TimelineEditor.inspectedAsset;
            if (foundTimeline != null)
            {
                foreach (var track in foundTimeline.GetOutputTracks())
                {
                    foreach (var c in track.GetClips())
                    {
                        if (c.asset == asset) { foundClip = c; break; }
                    }
                    if (foundClip != null) break;
                }
            }
        }
        else
        {
            // selected 路径找到了，也补一下 timeline 引用
            foundTimeline = TimelineEditor.inspectedAsset;
        }

        if (foundClip == null) return info;

        info.valid     = true;
        info.clip      = foundClip;
        info.timeline  = foundTimeline;
        info.duration  = foundClip.duration;
        info.clipStart = foundClip.start;

        var dir = TimelineEditor.inspectedDirector;
        if (dir != null)
        {
            info.hasDirector    = true;
            info.currentTime    = dir.time;
            info.clipLocalTime  = dir.time - foundClip.start;
            info.playheadInClip = (info.clipLocalTime >= 0.0 && info.clipLocalTime <= foundClip.duration);
            info.normalizedPlayhead = (foundClip.duration > 1e-6)
                ? Mathf.Clamp01((float)(info.clipLocalTime / foundClip.duration))
                : 0f;
        }
        return info;
    }
}
