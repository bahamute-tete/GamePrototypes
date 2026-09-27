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

/// <summary>Isolated preview-scene checks. Does not save or modify the user's scene.</summary>
public static class DissolveSpatialChecks
{
    static readonly StringBuilder Report = new StringBuilder();
    static int failures;
    static Camera camera;
    static Renderer renderer;
    static DissolveController controller;
    static Color[] Pixels;
    static void Check(bool ok, string message) { Report.AppendLine((ok ? "PASS " : "FAIL ") + message); if (!ok) failures++; }
    static bool Visible(int x, int y)
    {
        Color p = Pixels[y * 256 + x];
        return Mathf.Abs(p.r-1) + Mathf.Abs(p.g) + Mathf.Abs(p.b-1) > .18f;
    }
    static void Render(string file = null)
    {
        var rt = RenderTexture.GetTemporary(256,256,24,RenderTextureFormat.ARGB32);
        var tex = new Texture2D(256,256,TextureFormat.RGB24,false);
        var old = RenderTexture.active; var oldTarget = camera.targetTexture;
        try
        {
            camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
            tex.ReadPixels(new Rect(0,0,256,256),0,0); tex.Apply(); Pixels=tex.GetPixels();
            if(file!=null) File.WriteAllBytes("Temp/DissolveSpatialChecks/"+file+".png",tex.EncodeToPNG());
        }
        finally { camera.targetTexture=oldTarget; RenderTexture.active=old; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(tex); }
    }

    [MenuItem("Tools/LiangZhu/Dissolve/Validate Spatial Controls")]
    public static void Run()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Run in Edit Mode.");
        Report.Clear(); failures=0; Directory.CreateDirectory("Temp/DissolveSpatialChecks");
        var original=SceneManager.GetActiveScene(); bool dirty=original.isDirty;
        var preview=EditorSceneManager.NewPreviewScene();
        var root=new GameObject("Dissolve isolated check"); SceneManager.MoveGameObjectToScene(root,preview);
        var quad=GameObject.CreatePrimitive(PrimitiveType.Quad); quad.transform.SetParent(root.transform,false); quad.transform.localScale=new Vector3(2,2,1);
        renderer=quad.GetComponent<Renderer>();
        controller=quad.AddComponent<DissolveController>(); controller.controlledRenderers.Add(renderer);
        var anchor=new GameObject("World Origin").transform; anchor.SetParent(root.transform,false);
        var camGo=new GameObject("Test Camera"); camGo.transform.SetParent(root.transform,false);
        camera=camGo.AddComponent<Camera>(); camera.scene=preview; camera.transform.position=new Vector3(0,0,-5);
        camera.orthographic=true; camera.orthographicSize=1.5f; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.magenta; camera.allowHDR=false; camera.enabled=false;
        var light=new GameObject("Test Light").AddComponent<Light>(); light.transform.SetParent(root.transform,false); light.type=LightType.Directional; light.intensity=2;
        Material material=null; TimelineAsset timeline=null;
        try
        {
            foreach(string name in new[]{"Opaque_Dissolve_Lit","Transparent_Dissolve_Lit"})
            {
                var shader=Shader.Find("Custom/LiangZhu/"+name);
                Check(shader!=null,"Shader found: "+name); if(shader==null)continue;
                material=new Material(shader); renderer.sharedMaterial=material;
                material.SetColor(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color",Color.white); material.SetFloat("_Cull",0); material.SetFloat("_FogEnable",0); material.SetFloat("_DissolveEnabled",1);
                material.SetFloat("_DissolveEdgeIntensity",0); material.SetFloat("_DissolveCoverageWidth",0);
                if(material.HasProperty("_FresnelIntensity"))material.SetFloat("_FresnelIntensity",0);
                if(material.HasProperty("_RefractionEnable"))material.SetFloat("_RefractionEnable",0);
                controller.DissolveEdgeIntensity=0; controller.worldOrigin=anchor; controller.space=DissolveController.DissolveSpace.World;
                controller.mode=DissolveController.DissolveMode.Direction; controller.axisDirection=Vector3.right; controller.directionReverse=false;
                controller.edgeNoiseStrength=0; controller.amount=0; controller.autoToggleRenderer=true; anchor.position=Vector3.zero;
                controller.ForceRefresh(); Render(name+"_WorldPlane");
                Check(!Visible(64,128)&&Visible(192,128),name+" world plane clips at Origin with Amount=0");
                controller.SetParameter(DissolveParameter.PlaneOffset,1.2f); Render(); Check(!Visible(192,128),name+" plane offset moves clipping plane");
                controller.directionReverse=true;controller.ForceRefresh();Render();Check(Visible(192,128),name+" reverse keeps offset plane location");
                controller.directionReverse=false;controller.SetParameter(DissolveParameter.PlaneOffset,-1.2f);Render();Check(Visible(64,128),name+" negative plane offset");
                controller.SetParameter(DissolveParameter.PlaneOffset,0);
                controller.SetAmount(1); Render(); Check(!Visible(64,128)&&Visible(192,128)&&renderer.enabled,name+" world plane ignores Amount=1 and never hides whole Renderer");
                anchor.position=new Vector3(1.2f,0,0); controller.SendMessage("LateUpdate"); Render(); Check(!Visible(192,128),name+" moving Origin updates without rebake");
                anchor.position=Vector3.zero; controller.directionReverse=true; controller.ForceRefresh(); Render(); Check(Visible(64,128)&&!Visible(192,128),name+" plane side reversal");
                controller.directionReverse=false; controller.mode=DissolveController.DissolveMode.Radial; controller.radius=.45f; controller.radialReverse=false; controller.ForceRefresh(); Render(name+"_WorldSphere");
                Check(!Visible(128,128)&&Visible(192,128),name+" world radius removes inside sphere");
                controller.radialReverse=true; controller.ForceRefresh(); Render(); Check(Visible(128,128)&&!Visible(192,128),name+" sphere reversal");
                controller.worldOrigin=null; controller.ForceRefresh(); Render(); Check(Visible(128,128)&&Visible(192,128),name+" missing Origin safely leaves geometry visible");
                controller.worldOrigin=anchor; controller.radialReverse=false; controller.autoToggleRenderer=false;
                foreach(var space in new[]{DissolveController.DissolveSpace.Local,DissolveController.DissolveSpace.World})
                foreach(var mode in new[]{DissolveController.DissolveMode.Direction,DissolveController.DissolveMode.Radial,DissolveController.DissolveMode.Noise})
                {
                    if(space==DissolveController.DissolveSpace.World&&mode!=DissolveController.DissolveMode.Noise)continue;
                    controller.space=space; controller.mode=mode; controller.amount=0; controller.ForceRefresh(); Render(); Check(Visible(128,128),name+" "+space+" "+mode+" Amount=0 visible");
                    controller.SetAmount(1); Render(); Check(!Visible(128,128),name+" "+space+" "+mode+" Amount=1 hidden");
                }
                controller.space=DissolveController.DissolveSpace.Local; controller.mode=DissolveController.DissolveMode.Direction; controller.amount=.5f; controller.ForceRefresh(); Render(name+"_LocalPlane");
                Check(!Visible(64,128)&&Visible(192,128),name+" local normalized half dissolve");
                 controller.DissolveEdgeWidth=.25f; controller.DissolveEdgeColor=Color.cyan*2; controller.ForceRefresh();
                var block=new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                Check(Mathf.Approximately(block.GetFloat("_DissolveEdgeWidth"),.25f)&&block.GetColor("_DissolveEdgeColor")==Color.cyan*2,name+" controller edge width / HDR color uploaded");
                controller.space=DissolveController.DissolveSpace.World; controller.mode=DissolveController.DissolveMode.Direction;
                controller.amount=0; controller.DissolveEdgeColor=Color.green; controller.ForceRefresh();
                material.SetColor(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color",Color.black); controller.DissolveEdgeIntensity=1; controller.ForceRefresh();
                Render(name+"_EdgeColor");
                Color edge=Pixels[128*256+136],far=Pixels[128*256+185];
                Check(edge.g>edge.r+.1f&&edge.g>edge.b+.1f&&edge.g>far.g+.1f,name+" edge color and finite world width actually render");
                controller.SetParameter(DissolveParameter.EdgeIntensity,0);Render();Check(Pixels[128*256+136].g<edge.g-.1f,name+" zero controller intensity removes glow");
                controller.SetParameter(DissolveParameter.EdgeIntensity,1);
                controller.DissolveEdgeWidth=0;controller.ForceRefresh();Render();
                Check(Pixels[128*256+136].g<edge.g-.1f,name+" zero edge width removes glow");
                // Ask Unity to compile every pass, including shadow/depth variants of the opaque shader.
                var compile=typeof(ShaderUtil).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)
                    .FirstOrDefault(m=>m.Name=="CompilePass"&&m.GetParameters().Length==3&&m.GetParameters()[0].ParameterType==typeof(Material));
                if(compile!=null) for(int p=0;p<material.passCount;p++)compile.Invoke(null,new object[]{material,p,true});
                var messages=ShaderUtil.GetShaderMessages(shader);
                foreach(var m in messages.Where(m=>m.severity.ToString()=="Error"))Report.AppendLine("SHADER ERROR "+m.message);
                Check(!messages.Any(m=>m.severity.ToString()=="Error"),name+" shader passes compile");
                Object.DestroyImmediate(material); material=null;
            }
            // Verify switching control schemes restores a Renderer hidden by normalized amount.
            material=new Material(Shader.Find("Custom/LiangZhu/Opaque_Dissolve_Lit")); renderer.sharedMaterial=material;
            controller.autoToggleRenderer=true; controller.space=DissolveController.DissolveSpace.Local; controller.mode=DissolveController.DissolveMode.Direction; controller.amount=1; controller.ForceRefresh();
            Check(!renderer.enabled,"Local final-state hiding");
            controller.space=DissolveController.DissolveSpace.World; controller.ForceRefresh(); Check(renderer.enabled,"Switching to spatial mode restores controlled visibility");
            timeline=ScriptableObject.CreateInstance<TimelineAsset>(); var track=timeline.CreateTrack<DissolveTrack>(); var clip=track.CreateClip<DissolveClip>(); clip.duration=2;
            var director=root.AddComponent<PlayableDirector>(); director.playableAsset=timeline; director.SetGenericBinding(track,controller);
            controller.space=DissolveController.DissolveSpace.Local; director.time=1; director.Evaluate(); Check(Mathf.Abs(controller.amount-.5f)<.001f,"Timeline drives Local Amount");
            controller.space=DissolveController.DissolveSpace.World; controller.amount=.37f; director.time=1.5; director.Evaluate(); Check(Mathf.Abs(controller.amount-.37f)<.001f,"Timeline Amount leaves world geometry alone");
            director.playableAsset=null; director.RebuildGraph();
        }
        catch(Exception ex){Check(false,ex.ToString());}
        finally
        {
            if(material)Object.DestroyImmediate(material);
            if(timeline){foreach(var track in timeline.GetRootTracks().ToArray())timeline.DeleteTrack(track);Object.DestroyImmediate(timeline);}
            EditorSceneManager.ClosePreviewScene(preview);
            Check(SceneManager.GetActiveScene()==original&&original.isDirty==dirty,"User scene and dirty state preserved");
            Report.AppendLine("FAILURES "+failures); File.WriteAllText("Temp/DissolveSpatialChecks/Report.txt",Report.ToString());
        }
        Debug.Log("DISSOLVE_SPATIAL_CHECK_FAILURES="+failures);
    }
}



