using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

static class DissolveTimelineUI
{
    public static DissolveController Binding(DissolveTrack t) => TimelineEditor.inspectedDirector ? TimelineEditor.inspectedDirector.GetGenericBinding(t) as DissolveController : null;
    public static void Configure(DissolveTrack t)
    {
        var c=Binding(t);
        if(t.parameterConfigured || !c) return;
        Undo.RecordObject(t,"Configure dissolve parameter"); t.parameter=c.RecommendedParameter; t.parameterConfigured=true; EditorUtility.SetDirty(t);
    }
    public static void ParameterChanged(DissolveTrack t, DissolveParameter previous)
    {
        // Rename only generated labels. Authored values and custom names stay intact.
        foreach (var clip in t.GetClips())
            if (clip.displayName == previous.ToString()) clip.displayName = t.parameter.ToString();
        EditorUtility.SetDirty(t);
        TimelineEditor.Refresh(RefreshReason.ContentsModified);
    }
    public static string Warning(DissolveTrack t)
    {
        var c=Binding(t);
        if(!c)return "请绑定 DissolveController。";
        if(t.HasConflict(TimelineEditor.inspectedDirector))return "同一控制器的同一参数存在多条轨道：冲突轨道暂停写入。";
        if(!c.SupportsParameter(t.parameter))return "当前模式不支持此参数。建议："+c.RecommendedParameter;
        if(c.UsesWorldGeometry&&!c.worldOrigin)return "世界几何模式需要 Origin。";
        return null;
    }
    public static string Units(DissolveTrack t)
    {
        var c=Binding(t);
        switch(t.parameter)
        {
            case DissolveParameter.Amount:return "Amount：0 完整，1 消失；Local模式下使用， Noise模式无论是否Local都需要使用这个参数溶解";
            case DissolveParameter.PlaneOffset:return "Plane Offset：相对于 Origin 沿 Direction 的距离（米），可为负数。";
            case DissolveParameter.Radius:return "Radius：球半径（米），最小为 0。";
            case DissolveParameter.EdgeWidth:return "Edge Width："+(c&&c.mode==DissolveController.DissolveMode.Noise?"噪声阈值宽度":c&&c.space==DissolveController.DissolveSpace.Local?"局部长度":"世界米")+"，0 关闭边缘。";
            default:return "Edge Intensity：边缘颜色的强度倍数，最小为 0。";
        }
    }


   
}
[CustomTimelineEditor(typeof(DissolveTrack))]
public class DissolveTrackTimelineEditor : TrackEditor
{
    public override void OnCreate(TrackAsset track,TrackAsset copiedFrom) { if(!copiedFrom)DissolveTimelineUI.Configure((DissolveTrack)track); }
    public override void OnTrackChanged(TrackAsset track) { DissolveTimelineUI.Configure((DissolveTrack)track); }
    public override TrackDrawOptions GetTrackOptions(TrackAsset track,Object binding)
    {
        var options=base.GetTrackOptions(track,binding); options.errorText=DissolveTimelineUI.Warning((DissolveTrack)track); return options;
    }
}
[CustomEditor(typeof(DissolveTrack))]
public class DissolveTrackInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var t=(DissolveTrack)target; DissolveTimelineUI.Configure(t); serializedObject.Update();
        EditorGUI.BeginChangeCheck(); EditorGUILayout.PropertyField(serializedObject.FindProperty("parameter"),new GUIContent("驱动参数 / Parameter"));
        if(EditorGUI.EndChangeCheck())serializedObject.FindProperty("parameterConfigured").boolValue=true;
        var previous = t.parameter;
        if (serializedObject.ApplyModifiedProperties()) DissolveTimelineUI.ParameterChanged(t, previous);
        EditorGUILayout.HelpBox("此轨道控制 Dissolve 参数的动画。根据参数类型，绑定控制的溶解控制器里对应的参数", MessageType.Info);
        EditorGUILayout.HelpBox(DissolveTimelineUI.Units(t),MessageType.Info);
        var c=DissolveTimelineUI.Binding(t);
       // if(c&&GUILayout.Button("使用当前模式推荐参数："+c.RecommendedParameter)) { Undo.RecordObject(t,"Recommend parameter"); previous=t.parameter;t.parameter=c.RecommendedParameter;t.parameterConfigured=true;DissolveTimelineUI.ParameterChanged(t,previous); }
        var warning=DissolveTimelineUI.Warning(t); if(!string.IsNullOrEmpty(warning))EditorGUILayout.HelpBox(warning,MessageType.Warning);
    }
}
[CustomTimelineEditor(typeof(DissolveClip))]
public class DissolveClipTimelineEditor : ClipEditor
{
    public override void OnCreate(TimelineClip clip,TrackAsset track,TimelineClip clonedFrom)
    {
        if(clonedFrom!=null)return;
        var t=(DissolveTrack)track; DissolveTimelineUI.Configure(t);
        var b=((DissolveClip)clip.asset).template;
        b.startValue=t.parameter==DissolveParameter.PlaneOffset?-3:0;
        b.endValue=t.parameter==DissolveParameter.PlaneOffset?3:t.parameter==DissolveParameter.Radius?5:t.parameter==DissolveParameter.EdgeWidth?.1f:t.parameter==DissolveParameter.EdgeIntensity?3:1;
        clip.displayName=t.parameter.ToString();
    }
}
[CustomEditor(typeof(DissolveClip)),CanEditMultipleObjects]
public class DissolveClipInspector : Editor
{
    public override void OnInspectorGUI()
    {
        if(TimelineEditor.inspectedAsset)
            foreach(var output in TimelineEditor.inspectedAsset.GetOutputTracks())
                if(output is DissolveTrack t) foreach(var clip in t.GetClips()) if(clip.asset==target)EditorGUILayout.HelpBox(DissolveTimelineUI.Units(t),MessageType.Info);
        serializedObject.Update();
        var b=serializedObject.FindProperty("template");
        EditorGUILayout.PropertyField(b.FindPropertyRelative("startValue"),new GUIContent("Start Value / 起始值"));
        EditorGUILayout.PropertyField(b.FindPropertyRelative("endValue"),new GUIContent("End Value / 结束值"));
        EditorGUILayout.PropertyField(b.FindPropertyRelative("curve"),new GUIContent("Progress Curve / 时间曲线"));
        EditorGUILayout.PropertyField(b.FindPropertyRelative("endBehaviour"),new GUIContent("After Clip / 结束行为"));
        EditorGUILayout.HelpBox("曲线横轴为片段时间 0–1，纵轴为起止值之间的插值比例。Hold End 在空隙中保持末值；Restore Original 恢复轨道开始预览/播放前的值。停止 Timeline 后恢复原值。",MessageType.None);
        serializedObject.ApplyModifiedProperties();
    }
}

