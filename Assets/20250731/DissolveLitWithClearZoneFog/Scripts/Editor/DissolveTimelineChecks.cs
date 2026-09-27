using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
public static class DissolveTimelineChecks
{
    [MenuItem("Tools/LiangZhu/Dissolve/Validate Timeline Parameters")]
    public static void Run()
    {
        var report=new StringBuilder(); int failures=0;
        Action<bool,string> check=(ok,s)=>{report.AppendLine((ok?"PASS ":"FAIL ")+s);if(!ok)failures++;};
        var originalScene=SceneManager.GetActiveScene(); bool dirty=originalScene.isDirty;
        var scene=EditorSceneManager.NewPreviewScene();
        var root=new GameObject("Timeline Parameter Tests"); SceneManager.MoveGameObjectToScene(root,scene);
        var c=root.AddComponent<DissolveController>(); var director=root.AddComponent<PlayableDirector>();
        TimelineAsset timeline=null;
        try
        {
            foreach(DissolveParameter p in Enum.GetValues(typeof(DissolveParameter)))
            {
                c.space=p==DissolveParameter.Amount?DissolveController.DissolveSpace.Local:DissolveController.DissolveSpace.World;
                c.mode=p==DissolveParameter.Radius?DissolveController.DissolveMode.Radial:DissolveController.DissolveMode.Direction;
                c.worldOrigin=root.transform;
                float baseline=p==DissolveParameter.Amount?.3f:2;
                c.SetParameter(p,baseline);
                timeline=ScriptableObject.CreateInstance<TimelineAsset>();
                var track=timeline.CreateTrack<DissolveTrack>(); track.parameter=p;track.parameterConfigured=true;
                var clip=track.CreateClip<DissolveClip>();clip.start=1;clip.duration=2;
                var b=((DissolveClip)clip.asset).template;b.startValue=p==DissolveParameter.PlaneOffset?-4:0;b.endValue=p==DissolveParameter.Amount?1:8;
                director.playableAsset=timeline;director.SetGenericBinding(track,c);
                Action<double,float,string> sample=(time,value,label)=>{director.time=time;director.Evaluate();check(Mathf.Abs(c.GetParameter(p)-value)<.001f,p+" "+label);};
                sample(0,baseline,"before first clip preserves original");
                sample(2,(b.startValue+b.endValue)/2,"midpoint actual units");
                sample(3.5,b.endValue,"hold after end");
                sample(1,b.startValue,"seek backwards to start");
                sample(0,baseline,"seek backwards before start");
                b.endBehaviour=DissolveEndBehaviour.RestoreOriginal;
                sample(3.5,baseline,"restore original after end");
                sample(2,(b.startValue+b.endValue)/2,"seek inside again");
                director.Stop();check(Mathf.Abs(c.GetParameter(p)-baseline)<.001f,p+" stop restores original");
                UnityEngine.Object.DestroyImmediate(timeline);timeline=null;
            }
            timeline=ScriptableObject.CreateInstance<TimelineAsset>();
            var changing=timeline.CreateTrack<DissolveTrack>();changing.parameter=DissolveParameter.EdgeWidth;
            var existing=changing.CreateClip<DissolveClip>();existing.duration=2;
            var existingAsset=(DissolveClip)existing.asset;existingAsset.template.endValue=.8f;
            c.DissolveEdgeWidth=.12f;c.DissolveEdgeIntensity=3;
            director.playableAsset=timeline;director.SetGenericBinding(changing,c);director.time=1;director.Evaluate();
            check(Mathf.Abs(c.DissolveEdgeWidth-.4f)<.001f,"existing clip initially drives EdgeWidth");
            changing.parameter=DissolveParameter.EdgeIntensity;director.Evaluate();
            check(Mathf.Abs(c.DissolveEdgeIntensity-.4f)<.001f,"same graph and clip immediately drive changed parameter");
            check(Mathf.Abs(c.DissolveEdgeWidth-.12f)<.001f,"switch restores previous parameter");
            check(existing.asset==existingAsset&&existingAsset.template.endValue==.8f&&existing.duration==2,"switch preserves clip identity values and duration");
            changing.parameter=DissolveParameter.EdgeWidth;director.Evaluate();
            check(Mathf.Abs(c.DissolveEdgeWidth-.4f)<.001f&&Mathf.Abs(c.DissolveEdgeIntensity-3)<.001f,"switch back restores and rebinds");
            director.Stop();check(Mathf.Abs(c.DissolveEdgeWidth-.12f)<.001f,"stop after parameter switch restores correct original");
            UnityEngine.Object.DestroyImmediate(timeline);timeline=null;
            c.space=DissolveController.DissolveSpace.World;c.mode=DissolveController.DissolveMode.Direction;c.planeOffset=2;c.DissolveEdgeIntensity=3;c.amount=.37f;
            timeline=ScriptableObject.CreateInstance<TimelineAsset>();
            var plane=timeline.CreateTrack<DissolveTrack>();plane.parameter=DissolveParameter.PlaneOffset;
            var a=plane.CreateClip<DissolveClip>();a.duration=2;((DissolveClip)a.asset).template.endValue=4;
            var bclip=plane.CreateClip<DissolveClip>();bclip.start=1;bclip.duration=2;((DissolveClip)bclip.asset).template.startValue=10;((DissolveClip)bclip.asset).template.endValue=14;
            var edge=timeline.CreateTrack<DissolveTrack>();edge.parameter=DissolveParameter.EdgeIntensity;var e=edge.CreateClip<DissolveClip>();e.duration=4;((DissolveClip)e.asset).template.endValue=8;
            var invalid=timeline.CreateTrack<DissolveTrack>();invalid.parameter=DissolveParameter.Amount;invalid.CreateClip<DissolveClip>().duration=4;
            director.playableAsset=timeline;director.SetGenericBinding(plane,c);director.SetGenericBinding(edge,c);director.SetGenericBinding(invalid,c);
            director.time=1.5;director.Evaluate();
            check(Mathf.Abs(c.planeOffset-7)<.01f,"overlap blends two clip values");check(Mathf.Abs(c.DissolveEdgeIntensity-3)<.01f,"different parameter tracks coexist");check(Mathf.Abs(c.amount-.37f)<.001f,"incompatible Amount ignored in World Direction");
            director.time=2.5;director.Evaluate();check(Mathf.Abs(c.DissolveEdgeIntensity-5)<.01f,"edge track continues independently");
            director.Stop();
            var duplicate=timeline.CreateTrack<DissolveTrack>();duplicate.parameter=DissolveParameter.PlaneOffset;duplicate.CreateClip<DissolveClip>();director.SetGenericBinding(duplicate,c);
            check(plane.HasConflict(director)&&duplicate.HasConflict(director),"duplicate parameters detected");
            director.time=1;director.Evaluate();check(Mathf.Abs(c.planeOffset-2)<.001f,"conflicting tracks do not write");director.Stop();
            c.space=DissolveController.DissolveSpace.Local;check(c.RecommendedParameter==DissolveParameter.Amount,"Local recommends Amount");
            c.space=DissolveController.DissolveSpace.World;check(c.RecommendedParameter==DissolveParameter.PlaneOffset,"World Direction recommends PlaneOffset");
            c.mode=DissolveController.DissolveMode.Radial;check(c.RecommendedParameter==DissolveParameter.Radius,"World Radial recommends Radius");
            c.mode=DissolveController.DissolveMode.Noise;check(c.RecommendedParameter==DissolveParameter.Amount,"World Noise recommends Amount");
        }
        catch(Exception ex){check(false,ex.ToString());}
        finally
        {
            director.Stop();director.playableAsset=null;
            if(timeline)UnityEngine.Object.DestroyImmediate(timeline);
            EditorSceneManager.ClosePreviewScene(scene);
            check(SceneManager.GetActiveScene()==originalScene&&originalScene.isDirty==dirty,"user scene preserved");
            report.AppendLine("FAILURES "+failures);Directory.CreateDirectory("Temp/DissolveTimelineChecks");File.WriteAllText("Temp/DissolveTimelineChecks/Report.txt",report.ToString());
            Debug.Log("[DissolveTimelineChecks] failures="+failures+"\n"+report);
        }
    }
}

