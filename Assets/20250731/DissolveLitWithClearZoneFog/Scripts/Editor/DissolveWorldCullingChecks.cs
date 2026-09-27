using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

public static class DissolveWorldCullingChecks
{
    [MenuItem("Tools/LiangZhu/Dissolve/Validate World Renderer Culling")]
    public static void Run()
    {
        var scene=EditorSceneManager.NewPreviewScene();
        var root=new GameObject("World culling isolated checks");SceneManager.MoveGameObjectToScene(root,scene);
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(root.transform,false);
        var r=go.GetComponent<Renderer>();
        var mat=new Material(Shader.Find("Custom/LiangZhu/Opaque_Dissolve_Lit"));mat.SetFloat("_DissolveEnabled",1);mat.SetFloat("_DissolveCoverageWidth",.02f);r.sharedMaterial=mat;
        var c=root.AddComponent<DissolveController>();c.controlledRenderers.Add(r);c.mode=DissolveController.DissolveMode.Direction;c.space=DissolveController.DissolveSpace.World;c.axisDirection=Vector3.up;
        var origin=new GameObject("Origin").transform;origin.SetParent(root.transform,false);c.worldOrigin=origin;
        var report=new StringBuilder();int count=0;
        Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);report.AppendLine("PASS "+name);count++;};
        Action tick=()=>typeof(DissolveController).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,null);
        TimelineAsset timeline=null;Material off=null;Texture2D hdr=null;
        try {
            c.planeOffset=0;c.ForceRefresh();check(r.enabled,"Intersecting plane stays visible");
            c.planeOffset=.55f;c.ForceRefresh();check(r.enabled,"AA padding prevents boundary popping");
            c.planeOffset=1;c.ForceRefresh();check(!r.enabled,"World Direction fully hidden");
            c.planeOffset=0;c.ForceRefresh();check(r.enabled,"Reverse seek restores Direction");
            c.directionReverse=true;c.planeOffset=-1;c.ForceRefresh();check(!r.enabled,"Reversed Direction hides correct side");
            c.planeOffset=1;c.ForceRefresh();check(r.enabled,"Reversed Direction preserves opposite side");
            c.directionReverse=false;c.planeOffset=1;c.ForceRefresh();
            go.transform.position=Vector3.up*2;tick();check(r.enabled,"Moving disabled renderer out restores without ForceRefresh");
            go.transform.position=Vector3.zero;tick();check(!r.enabled,"Moving renderer back hides");
            origin.position=Vector3.down*2;tick();check(r.enabled,"Moving Origin restores");origin.position=Vector3.zero;
            c.worldOrigin=null;tick();check(r.enabled,"Missing Origin stays visible");c.worldOrigin=origin;
            c.edgeNoiseStrength=.6f;c.ForceRefresh();check(r.enabled,"Noise margin keeps potential visible islands");
            c.planeOffset=2;c.ForceRefresh();check(!r.enabled,"Noise fully beyond margin hides");
            hdr=new Texture2D(2,2,TextureFormat.RGBAFloat,false);c.noiseTexture=hdr;c.ForceRefresh();check(r.enabled,"Unbounded HDR noise conservatively retained");c.noiseTexture=null;c.edgeNoiseStrength=0;
            c.planeOffset=.8f;mat.SetFloat("_DissolveCoverageWidth",1);c.ForceRefresh();check(r.enabled,"Material Coverage Width respected");mat.SetFloat("_DissolveCoverageWidth",.02f);
            c.planeOffset=1;c.ForceRefresh();check(!r.enabled,"Coverage width change reevaluated");
            off=new Material(mat);off.SetFloat("_DissolveEnabled",0);r.sharedMaterials=new[]{mat,off};tick();check(r.enabled,"Mixed non-dissolving material remains visible");r.sharedMaterials=new[]{mat};
            var block=new MaterialPropertyBlock();block.SetFloat("_DissolveEnabled",0);r.SetPropertyBlock(block,0);tick();check(r.enabled,"Per-submesh override retained");r.SetPropertyBlock(null,0);
            c.autoToggleRenderer=false;tick();check(r.enabled,"Disabling automatic toggle restores");
            r.enabled=false;c.autoToggleRenderer=true;c.ForceRefresh();c.planeOffset=-2;c.ForceRefresh();check(!r.enabled,"Originally disabled renderer remains disabled");
            c.autoToggleRenderer=false;c.ForceRefresh();r.enabled=true;c.autoToggleRenderer=true;
            c.planeOffset=2;c.ForceRefresh();check(!r.enabled,"Hidden before controller disable");c.enabled=false;check(r.enabled,"Controller disable restores original visibility");c.enabled=true;
            c.controlledRenderers.Clear();c.ForceRefresh();check(r.enabled,"Removing target restores visibility");c.controlledRenderers.Add(r);
            c.mode=DissolveController.DissolveMode.Radial;c.radius=.7f;c.ForceRefresh();check(r.enabled,"Sphere has visible corners");
            c.radius=1.2f;c.ForceRefresh();check(!r.enabled,"Radial whole Bounds inside sphere hides");
            c.radius=.5f;c.ForceRefresh();check(r.enabled,"Radial shrinking restores");
            c.radialReverse=true;c.radius=1;c.ForceRefresh();check(r.enabled,"Reverse sphere retains center");
            go.transform.position=Vector3.right*3;tick();check(!r.enabled,"Reverse sphere whole Bounds outside hides");
            go.transform.position=Vector3.right*1.2f;tick();check(r.enabled,"Reverse sphere intersection retained");
            go.transform.position=Vector3.zero;
            c.mode=DissolveController.DissolveMode.Direction;c.directionReverse=false;c.planeOffset=10;c.ForceRefresh();check(!r.enabled,"World hidden before mode switch");
            c.space=DissolveController.DissolveSpace.Local;c.amount=.2f;c.ForceRefresh();check(r.enabled,"Switch to Local restores");c.amount=1;c.ForceRefresh();check(!r.enabled,"Existing Amount terminal hide preserved");
            c.amount=0;c.ForceRefresh();check(r.enabled,"Existing Amount rewind preserved");
            c.space=DissolveController.DissolveSpace.World;c.planeOffset=0;c.ForceRefresh();
            var director=root.AddComponent<PlayableDirector>();director.playOnAwake=false;timeline=ScriptableObject.CreateInstance<TimelineAsset>();
            var track=timeline.CreateTrack<DissolveTrack>();track.parameter=DissolveParameter.PlaneOffset;track.parameterConfigured=true;
            var clip=track.CreateClip<DissolveClip>();clip.duration=2;var b=((DissolveClip)clip.asset).template;b.startValue=-1;b.endValue=2;b.curve=AnimationCurve.Linear(0,0,1,1);
            director.playableAsset=timeline;director.SetGenericBinding(track,c);director.RebuildGraph();
            foreach(double time in new[]{1.9,.1,1.9,.1}){director.time=time;director.Evaluate();check(r.enabled==(time<1),"Actual Timeline forward/backward "+time);}
            director.time=1.9;director.Evaluate();director.Stop();check(r.enabled&&Mathf.Abs(c.planeOffset)<.001f,"Timeline Stop restores original value and visibility");
            c.planeOffset=2;go.transform.localRotation=Quaternion.Euler(0,0,45);go.transform.localScale=new Vector3(1,5,1);c.ForceRefresh();check(r.enabled,"Rotated scaled Bounds prevent premature culling");
            c.planeOffset=4;c.ForceRefresh();check(!r.enabled,"Rotated scaled whole Bounds can hide");
            Directory.CreateDirectory("Temp/DissolveWorldCullingChecks");File.WriteAllText("Temp/DissolveWorldCullingChecks/Report.txt",report.ToString());
            Debug.Log("WORLD_CULLING_CHECKS_PASSED: "+count+" checks; report Temp/DissolveWorldCullingChecks/Report.txt");
        }finally{Object.DestroyImmediate(root);if(timeline)Object.DestroyImmediate(timeline);Object.DestroyImmediate(mat);if(off)Object.DestroyImmediate(off);if(hdr)Object.DestroyImmediate(hdr);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
