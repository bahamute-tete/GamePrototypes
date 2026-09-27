using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

public static class DissolveTimelineBatchChecks
{
    [MenuItem("Tools/LiangZhu/Dissolve/Validate Batched Timeline Writes")]
    public static void Run()
    {
        var original=SceneManager.GetActiveScene();bool dirty=original.isDirty;
        var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Batch write checks");SceneManager.MoveGameObjectToScene(root,scene);
        var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.transform.SetParent(root.transform,false);
        var renderer=cube.GetComponent<Renderer>();var material=new Material(Shader.Find("Custom/LiangZhu/Opaque_Dissolve_Lit"));material.SetFloat("_DissolveEnabled",1);renderer.sharedMaterial=material;
        var c=root.AddComponent<DissolveController>();c.mode=DissolveController.DissolveMode.Direction;c.space=DissolveController.DissolveSpace.World;c.worldOrigin=root.transform;c.controlledRenderers.Add(renderer);
        c.planeOffset=-2;c.DissolveEdgeWidth=.03f;c.DissolveEdgeIntensity=.5f;c.ForceRefresh();
        var director=root.AddComponent<PlayableDirector>();director.playOnAwake=false;
        var timeline=ScriptableObject.CreateInstance<TimelineAsset>();
        var log=new StringBuilder();int count=0;
        Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);log.AppendLine("PASS "+name);count++;};
        Func<DissolveParameter,float,float,DissolveTrack> add=(p,a,b)=>{
            var t=timeline.CreateTrack<DissolveTrack>();t.parameter=p;t.parameterConfigured=true;
            var clip=t.CreateClip<DissolveClip>();clip.duration=2;var behaviour=((DissolveClip)clip.asset).template;behaviour.startValue=a;behaviour.endValue=b;
            director.SetGenericBinding(t,c);return t;
        };
        try {
            var plane=add(DissolveParameter.PlaneOffset,-1,3);
            var width=add(DissolveParameter.EdgeWidth,.02f,.2f);
            var intensity=add(DissolveParameter.EdgeIntensity,1,5);
            director.playableAsset=timeline;director.RebuildGraph();
            var block=new MaterialPropertyBlock();
            foreach(double time in new[]{.2,1.8,.6,1.4,.2}){
                int before=c.RendererPropertyWriteCount;director.time=time;director.Evaluate();
                check(c.RendererPropertyWriteCount-before==1,"Three tracks write one property block at "+time+" (actual "+(c.RendererPropertyWriteCount-before)+")");
                renderer.GetPropertyBlock(block);
                check(Mathf.Abs(block.GetFloat("_DissolveAxisCenter")-c.planeOffset)<.001f,"Plane committed before Evaluate returns");
                check(Mathf.Abs(block.GetFloat("_DissolveEdgeWidth")-c.DissolveEdgeWidth)<.001f,"Width committed before Evaluate returns");
                check(Mathf.Abs(block.GetFloat("_DissolveEdgeIntensity")-c.DissolveEdgeIntensity)<.001f,"Intensity committed before Evaluate returns");
                check(renderer.enabled==(time<.75),"Visibility matches committed plane "+time);
            }
            int unchanged=c.RendererPropertyWriteCount;director.Evaluate();check(c.RendererPropertyWriteCount==unchanged,"Unchanged values do not resubmit property block");
            int stopping=c.RendererPropertyWriteCount;director.Stop();check(c.RendererPropertyWriteCount-stopping==1,"Stop restores three parameters in one write");
            check(c.planeOffset==-2&&c.DissolveEdgeWidth==.03f&&c.DissolveEdgeIntensity==.5f&&renderer.enabled,"Stop restores values and visibility");

            // Muted and empty tracks must not leave a manual evaluation pending.
            width.muted=true;var empty=timeline.CreateTrack<DissolveTrack>();empty.parameter=DissolveParameter.Radius;director.SetGenericBinding(empty,c);
            director.RebuildGraph();director.time=1;
            int beforeMuted=c.RendererPropertyWriteCount;director.Evaluate();
            check(c.RendererPropertyWriteCount-beforeMuted==1,"Muted and empty tracks do not block flush");
            renderer.GetPropertyBlock(block);check(Mathf.Abs(block.GetFloat("_DissolveAxisCenter")-1)<.001f,"Muted graph has immediate actual GPU values");
            director.Stop();width.muted=false;

            // Rebuild restores originals before newly created mixers snapshot them.
            director.RebuildGraph();director.time=1.5;director.Evaluate();director.RebuildGraph();
            check(c.planeOffset==-2&&c.DissolveEdgeWidth==.03f&&c.DissolveEdgeIntensity==.5f,"Graph rebuild restores originals");
            director.time=.5;director.Evaluate();renderer.GetPropertyBlock(block);check(Mathf.Abs(block.GetFloat("_DissolveAxisCenter"))<.001f,"New graph immediately writes correctly");
            director.Stop();

            var invalid=add(DissolveParameter.Amount,0,1);director.RebuildGraph();director.time=1;
            int unsupported=c.RendererPropertyWriteCount;director.Evaluate();check(c.RendererPropertyWriteCount-unsupported==1,"Unsupported track does not block other parameters");director.Stop();
            timeline.DeleteTrack(invalid);timeline.DeleteTrack(empty);

            // Rebinding a live track restores its previous parameter in the same commit.
            director.RebuildGraph();director.time=.7;director.Evaluate();width.parameter=DissolveParameter.Radius;
            int switched=c.RendererPropertyWriteCount;director.Evaluate();check(c.RendererPropertyWriteCount-switched==1,"Live parameter change has one consolidated restore/write");check(c.DissolveEdgeWidth==.03f,"Previous parameter restored on change");director.Stop();

            int direct=c.RendererPropertyWriteCount;c.SetParameter(DissolveParameter.PlaneOffset,2);check(c.RendererPropertyWriteCount-direct==1&&!renderer.enabled,"Direct Controller API remains immediate");
            c.SetParameter(DissolveParameter.PlaneOffset,-2);
            timeline.DeleteTrack(width);timeline.DeleteTrack(intensity);
            director.RebuildGraph();director.time=.5;int single=c.RendererPropertyWriteCount;director.Evaluate();check(c.RendererPropertyWriteCount-single==1,"Single track uses same immediate-completion path");director.Stop();
            check(SceneManager.GetActiveScene()==original&&original.isDirty==dirty,"User scene unchanged");
            Directory.CreateDirectory("Temp/DissolveTimelineBatchChecks");File.WriteAllText("Temp/DissolveTimelineBatchChecks/Report.txt",log.ToString());Debug.Log("DISSOLVE_BATCH_CHECKS_PASSED: "+count+" checks");
        }
        finally{director.Stop();Object.DestroyImmediate(root);Object.DestroyImmediate(timeline);Object.DestroyImmediate(material);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
