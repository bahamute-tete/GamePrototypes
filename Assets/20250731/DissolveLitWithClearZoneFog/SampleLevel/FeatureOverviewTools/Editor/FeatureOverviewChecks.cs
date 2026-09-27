using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LiangZhu.Demo.Editor
{
    public static class FeatureOverviewChecks
    {
        [MenuItem("Tools/LiangZhu/Check Saved Overview at 1080p")]
        public static void CheckSavedOverview()
        {
            var d=UnityEngine.Object.FindObjectOfType<FeatureOverviewData>();
            FeatureOverviewRenderer.Validate(d);
            Assert(d.stages.All(s=>!s.activeSelf),"Source stages must remain hidden");
            Assert(d.boardCamera.enabled&&d.boardImage.gameObject.activeInHierarchy&&d.boardImage.texture,"Cached board is not visible");
            var texture=FeatureOverviewRenderer.Render(d.boardCamera,1920,1080);
            try{File.WriteAllBytes("Library/FeatureOverview_Reopen_1080p.png",texture.EncodeToPNG());}
            finally{UnityEngine.Object.DestroyImmediate(texture);}
            Debug.Log("A2C_SAVED_OVERVIEW_PASSED: cached scene visible without Play; 1080p captured");
        }
        [MenuItem("Tools/LiangZhu/Polish A2C Overview Framing")]
        public static void PolishFraming()
        {
            var d=UnityEngine.Object.FindObjectOfType<FeatureOverviewData>();
            d.cameras[0].orthographicSize=2.85f;
            FeatureOverviewRenderer.Refresh(d,-1);EditorSceneManager.SaveScene(d.gameObject.scene);
        }
        static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
        static void Close(float actual,float expected,string message){Assert(Mathf.Abs(actual-expected)<.002f,message+": "+actual+" != "+expected);}
        static float Expected(TimelineClip clip,float time,float original)
        {
            var b=((DissolveClip)clip.asset).template;
            if(time<clip.start)return original;
            if(time>=clip.end&&b.endBehaviour==DissolveEndBehaviour.RestoreOriginal)return original;
            return b.Evaluate((float)((time-clip.start)/clip.duration));
        }
        [MenuItem("Tools/LiangZhu/Validate Feature Overview")]
        public static void Validate()
        {
            var d=UnityEngine.Object.FindObjectOfType<FeatureOverviewData>();
            Assert(d&&d.multiDirector,"Open upgraded demo first");
            FeatureOverviewRenderer.Validate(d);
            var allSnapshots=d.controllers.Select(c=>new FeatureOverviewRenderer.Snapshot(c)).ToArray();
            var refs=d.controllers.Select(c=>c.controlledRenderers.ToArray()).ToArray();
            var enabled=refs.Select(rs=>rs.Select(r=>r.enabled).ToArray()).ToArray();
            var values=d.controllers.Select(c=>new[]{c.amount,c.planeOffset,c.radius,c.DissolveEdgeWidth,c.DissolveEdgeIntensity}).ToArray();
            double basicTime=d.director.time,multiTime=d.multiDirector.time;
            try {
                foreach(var source in new[]{d.director,d.multiDirector}){
                    var tracks=((TimelineAsset)source.playableAsset).GetOutputTracks().OfType<DissolveTrack>().ToArray();
                    Assert(tracks.All(t=>!t.HasConflict(source)),"Conflicting parameter tracks");
                    var originals=tracks.Select(t=>((DissolveController)source.GetGenericBinding(t)).GetParameter(t.parameter)).ToArray();
                    FeatureOverviewRenderer.WithTimeline(source,preview=>{
                        foreach(float time in new[]{4.8f,.6f,3f,1.2f,4.8f,7f,0f}){
                            preview.time=time;preview.Evaluate();
                            for(int i=0;i<tracks.Length;i++){
                                var c=(DissolveController)source.GetGenericBinding(tracks[i]);
                                Close(c.GetParameter(tracks[i].parameter),Expected(tracks[i].GetClips().First(),time,originals[i]),"Seek "+tracks[i].parameter);
                            }
                        }
                        preview.Stop();
                        for(int i=0;i<tracks.Length;i++)Close(((DissolveController)source.GetGenericBinding(tracks[i])).GetParameter(tracks[i].parameter),originals[i],"Stop restore");
                    });
                    var sampleTimes=source==d.director?FeatureOverviewRenderer.BasicTimes:FeatureOverviewRenderer.MultiTimes;
                    for(int sample=0;sample<3;sample++)for(int i=0;i<tracks.Length;i++){
                        float actual=source==d.director?d.timelineAmounts[sample]:i==0?d.multiValues[sample].x:i==1?d.multiValues[sample].y:d.multiValues[sample].z;
                        Close(actual,Expected(tracks[i].GetClips().First(),sampleTimes[sample],originals[i]),"Cached sample");
                    }
                }
                var basicTrack=((TimelineAsset)d.director.playableAsset).GetOutputTracks().OfType<DissolveTrack>().First();
                var clip=basicTrack.GetClips().First();var b=((DissolveClip)clip.asset).template;
                var oldCurve=b.curve;float start=b.startValue,end=b.endValue;double duration=clip.duration;var after=b.endBehaviour;
                try {
                    b.startValue=.15f;b.endValue=.8f;clip.duration=3;b.curve=AnimationCurve.EaseInOut(0,0,1,1);b.endBehaviour=DissolveEndBehaviour.RestoreOriginal;
                    FeatureOverviewRenderer.Refresh(d,4);
                    for(int i=0;i<3;i++)Close(d.timelineAmounts[i],Expected(clip,FeatureOverviewRenderer.BasicTimes[i],values[5][0]),"Edited curve/value/duration");
                }finally{b.curve=oldCurve;b.startValue=start;b.endValue=end;clip.duration=duration;b.endBehaviour=after;}
                // Inject failure while the temporary timeline graph has written values.
                try {FeatureOverviewRenderer.WithTimeline(d.multiDirector,p=>{p.time=3;p.Evaluate();throw new InvalidOperationException("Expected capture failure");});}
                catch(InvalidOperationException e){Assert(e.Message=="Expected capture failure","Unexpected failure");}
                var camera=d.cameras[1];bool fog=RenderSettings.fog;
                try {d.cameras[1]=null;FeatureOverviewRenderer.Refresh(d,1);throw new Exception("Failure injection did not fail");}
                catch(NullReferenceException){}
                finally{d.cameras[1]=camera;}
                Assert(RenderSettings.fog==fog,"Fog not restored");
                for(int i=0;i<d.controllers.Length;i++){
                    var c=d.controllers[i];Assert(c.controlledRenderers.SequenceEqual(refs[i]),"Lost renderer references");
                    Assert(c.controlledRenderers.Select(r=>r.enabled).SequenceEqual(enabled[i]),"Renderer visibility changed");
                    var now=new[]{c.amount,c.planeOffset,c.radius,c.DissolveEdgeWidth,c.DissolveEdgeIntensity};
                    for(int j=0;j<5;j++)Close(now[j],values[i][j],"Parameter restoration");
                }
                Assert(d.director.time==basicTime&&d.multiDirector.time==multiTime,"User timeline time changed");
                FeatureOverviewRenderer.Refresh(d,-1);
                bool labels=d.labels;try{d.labels=false;FeatureOverviewRenderer.Compose(d,true);}finally{d.labels=labels;FeatureOverviewRenderer.Compose(d,true);}
                FeatureOverviewRenderer.Validate(d);EditorSceneManager.SaveScene(d.gameObject.scene);
                Debug.Log("A2C_OVERVIEW_CHECKS_PASSED: actual asset samples; reverse seek; edited values/curve/duration; HoldEnd/RestoreOriginal; Stop; injected failures; references and visibility; repeated refresh; labelled/clean 4K");
            }finally{foreach(var s in allSnapshots)s.Restore();}
        }
    }
}
