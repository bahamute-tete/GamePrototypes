using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(.1f,.85f,.7f),TrackClipType(typeof(SkylineVfxClip)),TrackBindingType(typeof(SkylineNarrativeVfx))]
public class SkylineVfxTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph,GameObject go,int inputCount)=>ScriptPlayable<SkylineVfxMixer>.Create(graph,inputCount);
}
public class SkylineVfxMixer : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable,FrameData info,object playerData)
    {
        var fx=playerData as SkylineNarrativeVfx;if(fx&&fx.director)fx.Evaluate((float)fx.director.time);
    }
}
