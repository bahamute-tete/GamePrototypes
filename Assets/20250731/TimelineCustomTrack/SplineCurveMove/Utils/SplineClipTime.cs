using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>曲线横轴始终覆盖 Clip 本体；Playable.duration 可能包含 Timeline 外推区。</summary>
internal static class SplineClipTime
{
    internal static double Duration(Playable playable, TimelineClip clip)
        => clip != null ? clip.duration : playable.GetDuration();

    internal static float Normalize(Playable playable, TimelineClip clip)
    {
        double duration = Duration(playable, clip);
        double time = playable.GetTime();
        // ToLocalTime 已应用外推及 timeScale/clipIn，这里只撤销后两者。
        if (clip != null)
            time = (time - clip.clipIn) / clip.timeScale;
        if (duration <= 1e-6 || time <= 1e-4) return 0f;
        if (time >= duration - 1e-4) return 1f;
        return Mathf.Clamp01((float)(time / duration));
    }
}
