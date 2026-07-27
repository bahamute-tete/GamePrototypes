using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace VRFade
{
    /// <summary>
    /// Timeline 自定义轨道：在 Timeline 编辑器右键 > Add Track > VR Fade Track。
    /// 不需要绑定 PlayableDirector 之外的对象，直接驱动 FadeRuntime（全局状态）。
    /// </summary>
    [TrackColor(0.05f, 0.05f, 0.05f)]
    [TrackClipType(typeof(FadeClip))]
    [DisplayName("Custom/VR Fade Track")]
    public class FadeTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<FadeMixerBehaviour>.Create(graph, inputCount);
        }
    }
}
