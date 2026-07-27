using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LiangZhu.ProcMesh.Timeline
{
    [System.Serializable]
    public class GrowthControlClip : PlayableAsset, ITimelineClipAsset
    {
        public GrowthControlBehaviour template = new GrowthControlBehaviour();

        // 支持 clip 间混合与外推
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
            => ScriptPlayable<GrowthControlBehaviour>.Create(graph, template);
    }
}
