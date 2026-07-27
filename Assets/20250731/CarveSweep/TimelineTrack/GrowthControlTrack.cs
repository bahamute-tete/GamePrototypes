using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.ComponentModel;

namespace LiangZhu.ProcMesh.Timeline
{
    /// <summary>
    /// 绑定到 sweep mesh 的 Renderer，驱动其 _GrowT 做 Carve 式生长。
    /// 注意：Track / Clip / Behaviour / Mixer 分文件，Unity 反射发现才正常。
    /// </summary>
    [TrackColor(0.3f, 0.85f, 1f)]
    [TrackClipType(typeof(GrowthControlClip))]
    [TrackBindingType(typeof(Renderer))]
    [DisplayName("Custom/Curve/Growth Control Track")]
    public class GrowthControlTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
            => ScriptPlayable<GrowthControlMixer>.Create(graph, inputCount);

        // _GrowT 经 MPB 驱动，非序列化字段，编辑器预览的还原由 Mixer 的
        // 首帧缓存 + OnPlayableDestroy 还原负责（IPropertyCollector 无法 checkpoint MPB 材质属性）。
        public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
        {
            base.GatherProperties(director, driver);
        }
    }
}
