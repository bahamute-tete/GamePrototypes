using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using System.ComponentModel;

[TrackColor(0.7f, 0.7f, 0.9f)]
[TrackClipType(typeof(FogClip))]
[TrackBindingType(typeof(GameObject))]
[DisplayName("Custom/Fog/Fog Track")]
public class FogTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        // 创建混合器
        return ScriptPlayable<FogMixer>.Create(graph, inputCount);
    }

    // 确保相同类型的雾效果可以混合
    public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
    {
        base.GatherProperties(director, driver);
        
        driver.AddFromName((GameObject)null, "UnityEngine.RenderSettings.fog");
        driver.AddFromName((GameObject)null, "UnityEngine.RenderSettings.fogMode");
        driver.AddFromName((GameObject)null, "UnityEngine.RenderSettings.fogColor");
        driver.AddFromName((GameObject)null, "UnityEngine.RenderSettings.fogStartDistance");
        driver.AddFromName((GameObject)null, "UnityEngine.RenderSettings.fogEndDistance");
        driver.AddFromName((GameObject)null, "UnityEngine.RenderSettings.fogDensity");    
    }
}