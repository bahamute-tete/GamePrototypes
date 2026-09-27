using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEditor.VFX.Block;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
using UnityEngine.VFX.SDF;
using Object = UnityEngine.Object;

namespace UnityEditor.VFX
{
    // Compiled into the VFX editor assembly using an asmref: VFX 14's node API
    // is internal. No package files or package versions are changed.
    [InitializeOnLoad]
    static class CrowdDemoBuilder
    {
        internal const string Root = "Assets/20250731/VAT/CrowdSDF";
        internal const string GraphPath = "Assets/20250731/VAT/VFXGraph/VFX_VAT_Houdini_SDF.vfx";
        const string ScenePath = "Assets/20250731/VAT/VATScene.unity";
        static readonly string RequestPath = Root + "/Editor/request.txt";
        static double nextPoll;
        static CrowdDemoBuilder()
        {
            EditorApplication.update += Poll;
            Application.logMessageReceived += (message,stack,type) => {
                if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)
                    File.AppendAllText(Root+"/EditorDiagnostics.txt",DateTime.Now.ToString("s")+" "+message+"\n"+stack+"\n");
            };
        }
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll=EditorApplication.timeSinceStartup+1;
            CrowdValidation.RuntimeTick();
            if(!File.Exists(RequestPath)) return;
            string command=File.ReadAllText(RequestPath).Trim();
            File.Delete(RequestPath);
            try
            {
                if(command=="refresh") AssetDatabase.Refresh();
                else if(command=="build") Build();
                else if(command=="validate") CrowdValidation.Run();
                else if(command=="capture") Capture();
                else if(command=="runtime") CrowdValidation.StartRuntime();
                else if(command=="repair") RepairOutput();
                else if(command=="configure") ConfigureScene();
                else if(command=="tidy") TidyGraph();
                else if(command=="rebake") Rebake();
                else if(command=="stop") {SessionState.SetBool("VATCrowdRuntimeValidation",false);EditorApplication.isPlaying=false;}
                else if(command=="status") File.WriteAllText(Root+"/RuntimeStatus.txt","playing="+EditorApplication.isPlaying+", paused="+EditorApplication.isPaused+", frames="+Time.frameCount+", time="+Time.time+", test="+SessionState.GetBool("VATCrowdRuntimeValidation",false));
                else throw new ArgumentException("Unknown CrowdSDF request: "+command);
                File.WriteAllText(Root+"/LastOperation.txt",command+" completed "+DateTime.Now.ToString("s"));
            }
            catch(Exception e) { Debug.LogException(e); File.WriteAllText(Root+"/LastOperation.txt",command+" FAILED\n"+e); }
        }
        [MenuItem("Tools/VAT Crowd/Build SDF Demo")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before building.");
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(!scene.IsValid() || !scene.isLoaded) scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            var partial=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="VAT Crowd SDF Demo");
            if(partial!=null)
            {
                if(partial.GetComponents<Component>().Any(c=>c!=null&&c.GetType().Name=="CrowdSDFController"))
                    throw new InvalidOperationException("Demo already exists; use Re-bake SDF or edit its settings instead of rebuilding.");
                Object.DestroyImmediate(partial); // Only our incomplete build, never existing scene objects.
            }
            Directory.CreateDirectory(Root+"/Generated");
            // Preserve the complete current in-memory scene, including pre-existing
            // unsaved edits, before adding the requested demo.
            if(!File.Exists(Root+"/Generated/VATSceneBeforeSDF.unity"))
                EditorSceneManager.SaveScene(scene,Root+"/Generated/VATSceneBeforeSDF.unity",true);
            var original=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<VisualEffect>(true))
                .First(v=>v.gameObject.name=="VFX_VAT_Houdini");
            if(scene.GetRootGameObjects().Any(g=>g.name=="VAT Crowd SDF Demo"))
                throw new InvalidOperationException("Demo already exists; use Re-bake SDF or edit its settings instead of rebuilding.");
            Directory.CreateDirectory(Root+"/Generated"); AssetDatabase.Refresh();
            var root=new GameObject("VAT Crowd SDF Demo"); SceneManager.MoveGameObjectToScene(root,scene);
            var obstacles=new GameObject("Static collision proxies (rebake after editing)"); obstacles.transform.SetParent(root.transform,false);
            Vector3 p0=original.GetVector3("P0"),p1=original.GetVector3("P1"),p2=original.GetVector3("P2"),p3=original.GetVector3("P3");
            var wallMat=Material("Walls",new Color(0.24f,0.32f,0.38f));
            var obstacleMat=Material("Obstacles",new Color(0.65f,0.33f,0.13f));
            Proxy(obstacles.transform,"West wall",PrimitiveType.Cube,new Vector3(-12,1.5f,19),new Vector3(1.5f,3,58),wallMat);
            Proxy(obstacles.transform,"East wall",PrimitiveType.Cube,new Vector3(14,1.5f,19),new Vector3(1.5f,3,58),wallMat);
            Proxy(obstacles.transform,"Start wall",PrimitiveType.Cube,new Vector3(1,1.5f,-10),new Vector3(27.5f,3,1.5f),wallMat);
            Proxy(obstacles.transform,"End wall",PrimitiveType.Cube,new Vector3(1,1.5f,48),new Vector3(27.5f,3,1.5f),wallMat);
            var c1=Bezier(.32f,p0,p1,p2,p3);c1.y=1.5f;
            var c2=Bezier(.63f,p0,p1,p2,p3);c2.y=1.5f;
            var c3=Bezier(.79f,p0,p1,p2,p3)+new Vector3(2,0,0);c3.y=1.5f;
            Proxy(obstacles.transform,"Column A",PrimitiveType.Cylinder,c1,new Vector3(3,1.5f,3),obstacleMat);
            Proxy(obstacles.transform,"Column B",PrimitiveType.Cylinder,c2,new Vector3(3,1.5f,3),obstacleMat);
            Proxy(obstacles.transform,"Offset block",PrimitiveType.Cube,c3,new Vector3(3.2f,3,3.2f),obstacleMat);
            Vector3 center=new Vector3(1,2,19),size=new Vector3(32,8,64);
            Texture3D sdf=Bake(obstacles.transform,center,ref size);
            BuildGraph(sdf,center,size,p0,p1,p2,p3);
            var crowd=new GameObject("VFX_VAT_Houdini_SDF");crowd.transform.SetParent(root.transform,false);
            // Deliberately retain the source object's nonzero transform. World
            // simulation and world-space field/path data prevent double offsets.
            crowd.transform.position=original.transform.position;
            var vfx=crowd.AddComponent<VisualEffect>();vfx.visualEffectAsset=AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath);
            foreach(string n in new[]{"FrameCount","FPS","PlaybackSpeed"}) if(vfx.HasFloat(n)) vfx.SetFloat(n,original.GetFloat(n));
            foreach(string n in new[]{"BoundMin","BoundMax"}) if(vfx.HasVector3(n)) vfx.SetVector3(n,original.GetVector3(n));
            foreach(string n in new[]{"PosTexture","RotTexture","ColorTexture"}) if(vfx.HasTexture(n)) vfx.SetTexture(n,original.GetTexture(n));
            if(vfx.HasMesh("VATMesh")) vfx.SetMesh("VATMesh",original.GetMesh("VATMesh"));
            var controllerType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("VATCrowd.CrowdSDFController")).First(t=>t!=null);
            var controller=root.AddComponent(controllerType);
            var so=new SerializedObject(controller);
            so.FindProperty("effect").objectReferenceValue=vfx;
            so.FindProperty("originalEffect").objectReferenceValue=original;
            so.FindProperty("collisionProxies").objectReferenceValue=obstacles.transform;
            so.FindProperty("distanceField").objectReferenceValue=sdf;
            so.FindProperty("fieldCenter").vector3Value=center;so.FindProperty("fieldSize").vector3Value=size;
            so.FindProperty("p0").vector3Value=p0;so.FindProperty("p1").vector3Value=p1;
            so.FindProperty("p2").vector3Value=p2;so.FindProperty("p3").vector3Value=p3;
            so.ApplyModifiedPropertiesWithoutUndo();
            original.gameObject.SetActive(false);
            foreach(var g in scene.GetRootGameObjects()) if(g.name=="DrawMeshInstance" || g.name=="VFX_VAT")g.SetActive(false);
            var demoCamera=new GameObject("Crowd overview camera").AddComponent<Camera>();
            demoCamera.transform.SetParent(root.transform,false);
            demoCamera.transform.position=new Vector3(30,39,-27);
            demoCamera.transform.LookAt(new Vector3(1,0,18));demoCamera.fieldOfView=55;demoCamera.farClipPlane=200;
            demoCamera.depth=10;demoCamera.clearFlags=CameraClearFlags.Skybox;
            // Preserve other scene cameras. The overview renders last.
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();TidyGraph();vfx.Reinit();
            Selection.activeGameObject=root;
            Debug.Log("VAT Crowd SDF demo built. Select VAT Crowd SDF Demo for settings.");
        }
        static Material Material(string name,Color color)
        {
            string path=Root+"/Generated/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",0.22f);return m;
        }
        static void Proxy(UnityEngine.Transform parent,string name,PrimitiveType primitive,Vector3 position,Vector3 scale,Material material)
        {
            var g=GameObject.CreatePrimitive(primitive);g.name=name;g.transform.SetParent(parent,false);
            g.transform.position=position;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=material;
            Object.DestroyImmediate(g.GetComponent<Collider>());
        }
        internal static Vector3 Bezier(float t,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {float u=1-t;return u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;}
        internal static Texture3D Bake(UnityEngine.Transform proxies,Vector3 center,ref Vector3 size)
        {
            var combines=proxies.GetComponentsInChildren<MeshFilter>().Select(f=>new CombineInstance
                {mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix}).ToArray();
            var mesh=new Mesh {name="Crowd collision proxy"};mesh.CombineMeshes(combines);
            string meshPath=Root+"/Generated/CollisionProxy.asset";
            var oldMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(oldMesh!=null){EditorUtility.CopySerialized(mesh,oldMesh);Object.DestroyImmediate(mesh);mesh=oldMesh;}
            else AssetDatabase.CreateAsset(mesh,meshPath);
            var baker=new MeshToSDFBaker(size,center,128,mesh,3);
            try
            {
                baker.BakeSDF();size=baker.GetActualBoxSize();
                var rt=baker.SdfTexture;
                var request=AsyncGPUReadback.Request(rt,0,TextureFormat.RFloat);request.WaitForCompletion();
                if(request.hasError) throw new InvalidOperationException("SDF GPU readback failed.");
                var texture=new Texture3D(rt.width,rt.height,rt.volumeDepth,TextureFormat.RFloat,false)
                    {name="Crowd Solid SDF 128",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                var pixels=new float[rt.width*rt.height*rt.volumeDepth];
                for(int layer=0;layer<request.layerCount;layer++)
                {
                    var data=request.GetData<float>(layer);
                    Array.Copy(data.ToArray(),0,pixels,layer*data.Length,data.Length);
                }
                texture.SetPixelData(pixels,0);texture.Apply(false,false);
                string path=Root+"/Generated/CrowdSolidSDF.asset";
                var previous=AssetDatabase.LoadAssetAtPath<Texture3D>(path);
                if(previous!=null){EditorUtility.CopySerialized(texture,previous);Object.DestroyImmediate(texture);texture=previous;}
                else AssetDatabase.CreateAsset(texture,path);
                return texture;
            }
            finally
            {
                CrowdBakerDisposal.Dispose(baker);
            }
        }
        static void ClearBlocks(VFXContext c)
        { foreach(var b in c.children.ToArray()) {foreach(var s in b.inputSlots) s.UnlinkAll(true);c.RemoveChild(b);} }
        static VFXParameter Parameter(VFXGraph graph,string name,Type type,object value)
        {
            var p=graph.children.OfType<VFXParameter>().FirstOrDefault(x=>x.exposedName==name);
            if(p==null){p=ScriptableObject.CreateInstance<VFXParameter>();p.Init(type);p.SetSettingValue("m_ExposedName",name);graph.AddChild(p);}
            p.SetSettingValue("m_Exposed",true);p.value=value;return p;
        }
        static void BuildGraph(Texture3D sdf,Vector3 center,Vector3 size,Vector3 p0,Vector3 p1,Vector3 p2,Vector3 p3)
        {
            if(AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath)!=null) throw new InvalidOperationException("SDF graph already exists; refusing to overwrite edits.");
            if(!AssetDatabase.CopyAsset("Assets/20250731/VAT/VFXGraph/VFX_VAT_Houdini.vfx",GraphPath)) throw new IOException("Could not copy source VFX.");
            var resource=VisualEffectResource.GetResourceAtPath(GraphPath);var graph=resource.GetOrCreateGraph();
            var init=graph.children.OfType<VFXBasicInitialize>().Single();
            var update=graph.children.OfType<VFXBasicUpdate>().Single();
            var spawn=graph.children.OfType<VFXBasicSpawner>().Single();
            var output=graph.children.OfType<VFXContext>().First(c=>c.contextType==VFXContextType.Output && c.GetData()==init.GetData());
            foreach(var c in graph.children.OfType<VFXContext>().Where(c=>c.contextType==VFXContextType.Output && c!=output).ToArray()) graph.RemoveChild(c);
            ClearBlocks(init);ClearBlocks(update);ClearBlocks(spawn);ClearBlocks(output);
            var initialize=ScriptableObject.CreateInstance<CrowdInitialize>();init.AddChild(initialize);
            var motion=ScriptableObject.CreateInstance<CrowdUpdate>();update.AddChild(motion);
            var burst=ScriptableObject.CreateInstance<VFXSpawnerBurst>();spawn.AddChild(burst);
            update.SetSettingValue("integration",VFXBasicUpdate.VFXIntegrationMode.None);
            update.SetSettingValue("angularIntegration",VFXBasicUpdate.VFXIntegrationMode.None);
            update.SetSettingValue("ageParticles",false);update.SetSettingValue("reapParticles",false);
            init.GetData().SetSettingValue("capacity",1024u);
            ((VFXDataParticle)init.GetData()).space=VFXCoordinateSpace.World;
            var values=new Dictionary<string,object> {
                {"DistanceField",sdf},{"FieldCenter",center},{"FieldSize",size},
                {"P0",p0},{"P1",p1},{"P2",p2},{"P3",p3},{"GroundY",0f},{"BodyHeight",.9f},
                {"Radius",.4f},{"FrameCount",10f},{"WalkSpeed",2f},{"LookAhead",2f},
                {"AvoidStrength",1.3f},{"TurnRate",6f},{"FPS",30f},{"PlaybackSpeed",.3f},{"ReferenceSpeed",2f}
            };
            int order=0;
            foreach(var kv in values)
            {
                var p=Parameter(graph,kv.Key,kv.Value is Texture3D?typeof(Texture3D):kv.Value.GetType(),kv.Value);
                p.position=new Vector2(-600+(order/10)*260,order%10*120);order++;
                foreach(var b in new VFXBlock[]{initialize,motion})
                {var s=b.inputSlots.FirstOrDefault(x=>x.name==kv.Key);if(s!=null)s.Link(p.outputSlots[0]);}
            }
            burst.inputSlots.First(s=>s.name=="Count").Link(Parameter(graph,"CrowdCount",typeof(float),50f).outputSlots[0]);
            var frame=ScriptableObject.CreateInstance<GetCustomAttribute>();frame.SetSettingValue("attribute","crowdFrame");graph.AddChild(frame);frame.position=new Vector2(600,1250);
            foreach(var slot in output.inputSlots)
            {
                if(slot.name=="_B_autoPlayback"){slot.UnlinkAll(true);slot.value=0f;}
                if(slot.name=="_displayFrame"){slot.UnlinkAll(true);slot.Link(frame.outputSlots[0]);}
                if(slot.name=="_gameTimeAtFirstFrame") {slot.UnlinkAll(true);slot.value=0f;}
            }
            graph.SetExpressionGraphDirty();graph.UpdateSubAssets();resource.WriteAsset();
            AssetDatabase.ImportAsset(GraphPath,ImportAssetOptions.ForceSynchronousImport);
        }
        [MenuItem("Tools/VAT Crowd/Re-bake SDF")]
        public static void Rebake()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before re-baking the SDF.");
            var root=GameObject.Find("VAT Crowd SDF Demo");if(root==null)throw new InvalidOperationException("Open VATScene first.");
            var component=root.GetComponents<Component>().First(c=>c!=null&&c.GetType().Name=="CrowdSDFController");
            var so=new SerializedObject(component);var center=so.FindProperty("fieldCenter").vector3Value;var size=so.FindProperty("fieldSize").vector3Value;
            var sdf=Bake((UnityEngine.Transform)so.FindProperty("collisionProxies").objectReferenceValue,center,ref size);
            so.FindProperty("distanceField").objectReferenceValue=sdf;so.FindProperty("fieldSize").vector3Value=size;so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);
            component.GetType().GetMethod("Restart").Invoke(component,null);
            Debug.Log("VAT Crowd: SDF re-baked from the collision proxies. Save the scene to keep the updated field settings.",root);
        }
        internal static void RepairOutput()
        {
            var resource=VisualEffectResource.GetResourceAtPath(GraphPath);var graph=resource.GetOrCreateGraph();
            foreach(var output in graph.children.OfType<VFXContext>().Where(c=>c.contextType==VFXContextType.Output))
                foreach(var slot in output.inputSlots) if(slot.name=="_B_autoPlayback"){slot.UnlinkAll(true);slot.value=0f;}
            graph.SetExpressionGraphDirty();graph.UpdateSubAssets();resource.WriteAsset();AssetDatabase.ImportAsset(GraphPath);
        }
        internal static void ConfigureScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            var scene=SceneManager.GetSceneByPath(ScenePath);
            foreach(var g in scene.GetRootGameObjects()) if(g.name=="DrawMeshInstance" || g.name=="VFX_VAT")g.SetActive(false);
            RepairOutput();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        [MenuItem("Tools/VAT Crowd/Organize SDF Graph")]
        internal static void TidyGraph()
        {
            var resource=VisualEffectResource.GetResourceAtPath(GraphPath);var graph=resource.GetOrCreateGraph();
            var output=graph.children.OfType<VFXContext>().Single(c=>c.contextType==VFXContextType.Output);
            var bindings=new Dictionary<string,string>{{"_posTexture","PosTexture"},{"_rotTexture","RotTexture"},{"_colTexture","ColorTexture"},
                {"mesh","VATMesh"},{"_boundMin","BoundMin"},{"_boundMax","BoundMax"},{"_frameCount","FrameCount"},{"_houdiniFPS","FPS"},{"_playbackSpeed","PlaybackSpeed"}};
            foreach(var slot in output.inputSlots)
            {
                if(bindings.TryGetValue(slot.name,out string exposed))
                {
                    var parameter=graph.children.OfType<VFXParameter>().Single(p=>p.exposedName==exposed);
                    slot.UnlinkAll(true);slot.Link(parameter.outputSlots[0]);
                }
                if(slot.name=="_PlaybackStartFrame"){slot.UnlinkAll(true);slot.value=1f;}
            }
            foreach(var op in graph.children.OfType<VFXOperator>().Where(o=>!(o is GetCustomAttribute a && a.attribute=="crowdFrame")).ToArray())
            {foreach(var s in op.inputSlots.Concat(op.outputSlots))s.UnlinkAll(true);graph.RemoveChild(op);}
            foreach(var p in graph.children.OfType<VFXParameter>().Where(p=>!p.outputSlots.Any(s=>s.HasLink(true))).ToArray())graph.RemoveChild(p);
            var ui=new SerializedObject(graph.UIInfos);ui.FindProperty("groupInfos").ClearArray();ui.ApplyModifiedPropertiesWithoutUndo();
            int index=0;
            foreach(var p in graph.children.OfType<VFXParameter>()) {p.position=new Vector2(-760+(index/10)*270,(index%10)*150);index++;}
            var spawn=graph.children.OfType<VFXBasicSpawner>().Single();spawn.position=new Vector2(200,0);spawn.label="01 - Spawn crowd";
            var init=graph.children.OfType<VFXBasicInitialize>().Single();init.position=new Vector2(200,300);init.label="02 - Safe start + random phase";
            var update=graph.children.OfType<VFXBasicUpdate>().Single();update.position=new Vector2(200,1000);update.label="03 - Path / SDF slide / VAT phase";
            output.position=new Vector2(200,1950);output.label="04 - Original VAT mesh + manual frame";
            graph.children.OfType<GetCustomAttribute>().Single().position=new Vector2(-150,1850);
            graph.SetExpressionGraphDirty();graph.UpdateSubAssets();resource.WriteAsset();AssetDatabase.ImportAsset(GraphPath);
            // The source graph had texture parameters that were not all connected.
            // Restore scene overrides now that all VAT inputs are explicitly bound.
            var scene=SceneManager.GetSceneByPath(ScenePath);
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<VisualEffect>(true)).ToArray();
            var source=all.Single(v=>v.gameObject.name=="VFX_VAT_Houdini");var target=all.Single(v=>v.gameObject.name=="VFX_VAT_Houdini_SDF");
            foreach(var name in new[]{"PosTexture","RotTexture","ColorTexture"})target.SetTexture(name,source.GetTexture(name));
            target.SetMesh("VATMesh",source.GetMesh("VATMesh"));
            target.Reinit();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        internal static void Capture()
        {
            var camera=GameObject.Find("Crowd overview camera").GetComponent<Camera>();
            var rt=RenderTexture.GetTemporary(1280,900,24);var previous=camera.targetTexture;var active=RenderTexture.active;
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                var texture=new Texture2D(1280,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,900),0,0);texture.Apply();
                File.WriteAllBytes(Root+"/Generated/Preview.png",texture.EncodeToPNG());Object.DestroyImmediate(texture);
            }
            finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);}
        }
    }
}
