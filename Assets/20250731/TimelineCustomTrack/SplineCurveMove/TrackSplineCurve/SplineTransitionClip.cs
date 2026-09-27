using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>A time-addressable bridge between two independently moving anchors.</summary>
[System.Serializable]
public class SplineTransitionClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip("自动读取相邻移动 Clip 的端点位置、相对速度和朝向。过渡两端必须紧邻且无淡入淡出。")]
    public bool autoMatchAdjacentPaths = true;
    [Tooltip("留空时自动寻找同轨道上紧邻的前一移动 Clip；指定后按引用校验。")]
    public SplineCurveMoveClip previousPath;
    [Tooltip("留空时自动寻找同轨道上紧邻的后一移动 Clip；指定后按引用校验。")]
    public SplineCurveMoveClip nextPath;
    [Tooltip("出发点；其 +Z 方向为离开方向。建议放在前一条路径终点。")]
    public ExposedReference<Transform> departure;
    [Tooltip("到达点；其 +Z 方向为进入方向。建议放在后一条路径起点。")]
    public ExposedReference<Transform> arrival;
    [Min(0)] public float departureSpeed = 1;
    [Min(0)] public float arrivalSpeed = 1;
    [Tooltip("过渡中段额外抬高，端点位置和速度不受影响。")]
    public float arcHeight;
    public bool applyRotation = true;
    public SplineEndMode endMode = SplineEndMode.FollowReference;
    public ClipCaps clipCaps => ClipCaps.Blending;

    public bool TryGetAdjacentPaths(TimelineClip self, out TimelineClip previous, out TimelineClip next, out string error)
    {
        previous = next = null;
        error = null;
        const double tolerance = .0001;
        if (self == null || self.GetParentTrack() == null)
        { error = "请从 Timeline 中选择过渡 Clip。"; return false; }
        foreach (var candidate in self.GetParentTrack().GetClips())
        {
            if (!(candidate.asset is SplineCurveMoveClip)) continue;
            if (System.Math.Abs(candidate.end - self.start) < tolerance && (previousPath == null || candidate.asset == previousPath))
            {
                if (previous != null) { error = "出发端有多个相邻移动 Clip，请明确指定前一条路径。"; return false; }
                previous = candidate;
            }
            if (System.Math.Abs(candidate.start - self.end) < tolerance && (nextPath == null || candidate.asset == nextPath))
            {
                if (next != null) { error = "到达端有多个相邻移动 Clip，请明确指定后一条路径。"; return false; }
                next = candidate;
            }
        }
        if (previous == null || next == null)
        { error = "自动衔接需要同轨道上的两段移动 Clip 与过渡首尾紧邻；请检查间隙、重叠和路径引用。"; return false; }
        foreach (var candidate in self.GetParentTrack().GetClips())
            if (candidate != self && candidate != previous && candidate != next &&
                candidate.end > self.start + tolerance && candidate.start < self.end - tolerance)
            { error = "过渡范围内还有重叠 Clip，无法保证端点速度连续。请移除该重叠。"; return false; }
        if (previous.EvaluateMixOut(previous.end) < .9999f || next.EvaluateMixIn(next.start) < .9999f ||
            self.EvaluateMixIn(self.start) < .9999f || self.EvaluateMixOut(self.end) < .9999f)
        { error = "请取消前段末尾、过渡两端和后段开头的淡入淡出；自动过渡自身负责平滑衔接。"; return false; }
        return true;
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<SplineCurveMoveBehaviour>.Create(graph);
        var behaviour = playable.GetBehaviour();
        behaviour.ApplyRotation = applyRotation;
        return playable;
    }
}

public enum SplineEndMode
{
    [InspectorName("兼容旧版：最近边界")] LegacyNearest = 0,
    [InspectorName("保持参考系内终点")] FollowReference = 1,
    [InspectorName("固定结束时世界姿态")] HoldWorld = 2,
    [InspectorName("交给其他系统")] Release = 3
}

public enum SplinePathSpace
{
    [InspectorName("兼容旧版空间设置")] Legacy = 0,
    [InspectorName("世界空间")] World = 1,
    [InspectorName("指定参考系")] ReferenceFrame = 2
}
