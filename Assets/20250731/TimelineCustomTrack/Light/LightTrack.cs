// LightTrack.cs
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.ComponentModel;


[TrackColor(1.0f, 0.85f, 0.4f)]
[TrackBindingType(typeof(Light))]
[TrackClipType(typeof(LightClip))]
[DisplayName("Custom/Light Track")]
public class LightTrack : TrackAsset
{

    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<LightMixerBehaviour>.Create(graph, inputCount);
    }

    public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
    {
    #if UNITY_EDITOR
        var binding = director.GetGenericBinding(this) as Light;
        if (binding != null)
        {
            var go = binding.gameObject;

            // Light 组件属性
            driver.AddFromName<Light>(go, "m_Color");
            driver.AddFromName<Light>(go, "m_Intensity");
            driver.AddFromName<Light>(go, "m_Range");
            driver.AddFromName<Light>(go, "m_SpotAngle");
            driver.AddFromName<Light>(go, "m_BounceIntensity");

            // Transform 组件属性 —— 让 Timeline 在退出预览时也能正确恢复
            driver.AddFromName<Transform>(go, "m_LocalPosition");
            driver.AddFromName<Transform>(go, "m_LocalRotation");
            driver.AddFromName<Transform>(go, "m_LocalEulerAnglesHint");
        }
    #endif
        base.GatherProperties(director, driver);
    }
}
