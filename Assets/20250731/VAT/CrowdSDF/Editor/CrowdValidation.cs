using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.VFX;

namespace UnityEditor.VFX
{
    static class CrowdValidation
    {
        [StructLayout(LayoutKind.Sequential)]
        struct Agent {public Vector3 position;public float path;public Vector3 velocity;public float phase;public float heading,side,laps,minimum;}
        static readonly string ReportPath=CrowdDemoBuilder.Root+"/Generated/Validation.txt";
        [MenuItem("Tools/VAT Crowd/Validate GPU Movement (120 simulated seconds)")]
        public static void Run()
        {
            var shader=AssetDatabase.LoadAssetAtPath<ComputeShader>(CrowdDemoBuilder.Root+"/Shaders/CrowdValidation.compute");
            var root=GameObject.Find("VAT Crowd SDF Demo");
            var component=root.GetComponents<Component>().First(c=>c!=null&&c.GetType().Name=="CrowdSDFController");
            var so=new SerializedObject(component);var report=new StringBuilder();
            shader.SetVector("_Center",so.FindProperty("fieldCenter").vector3Value);shader.SetVector("_Size",so.FindProperty("fieldSize").vector3Value);
            for(int i=0;i<4;i++)shader.SetVector("_P"+i,so.FindProperty("p"+i).vector3Value);
            shader.SetFloat("_Radius",.4f);shader.SetFloat("_Speed",2);shader.SetInt("_Count",50);
            int init=shader.FindKernel("Initialize"),step=shader.FindKernel("Step");
            var field=(Texture3D)so.FindProperty("distanceField").objectReferenceValue;
            shader.SetTexture(init,"_Field",field);shader.SetTexture(step,"_Field",field);
            Agent[][] results=new Agent[2][];int run=0;
            foreach(int fps in new[]{30,60})
            {
                using(var buffer=new GraphicsBuffer(GraphicsBuffer.Target.Structured,50,Marshal.SizeOf<Agent>()))
                {
                    shader.SetBuffer(init,"_Agents",buffer);shader.SetBuffer(step,"_Agents",buffer);
                    shader.SetFloat("_Dt",1f/fps);shader.Dispatch(init,1,1,1);
                    var timer=System.Diagnostics.Stopwatch.StartNew();
                    for(int frame=0;frame<120*fps;frame++)shader.Dispatch(step,1,1,1);
                    var agents=new Agent[50];buffer.GetData(agents);timer.Stop();results[run++]=agents;
                    int invalid=agents.Count(a=>float.IsNaN(a.position.x)||float.IsInfinity(a.position.x)||Mathf.Abs(a.position.y)>.001f);
                    float clearance=agents.Min(a=>a.minimum);int completed=agents.Count(a=>a.laps>=1);
                    report.AppendLine(fps+" FPS / 120 simulated seconds: minimum clearance="+clearance.ToString("F4")+
                        ", invalid/ground violations="+invalid+", agents completing a lap="+completed+"/50, total laps="+agents.Sum(a=>a.laps)+
                        ", dispatch+readback wall time="+timer.Elapsed.TotalMilliseconds.ToString("F1")+" ms (NOT render frame time)");
                    report.AppendLine("Final path range="+agents.Min(a=>a.path)+".."+agents.Max(a=>a.path));
                    for(int i=0;i<50;i++)if(agents[i].laps<1)report.AppendLine("No lap: "+i+" position="+agents[i].position+" path="+agents[i].path+" velocity="+agents[i].velocity);
                    if(invalid>0 || clearance<.35f || completed<50)report.AppendLine("FAIL: ground, clearance or progress acceptance criterion.");
                }
            }
            report.AppendLine("Both rates must complete every agent's route; positions need not match after repeated respawns.");
            // Deliberately hit walls and a corner with initial inward velocity;
            // these exercise collision projection, not just anticipatory steering.
            var p0=so.FindProperty("p0").vector3Value;var p1=so.FindProperty("p1").vector3Value;
            var p2=so.FindProperty("p2").vector3Value;var p3=so.FindProperty("p3").vector3Value;
            var column=CrowdDemoBuilder.Bezier(.32f,p0,p1,p2,p3);
            var cases=new[]{
                new Agent {position=new Vector3(-10.8f,0,10),velocity=new Vector3(-6,0,0),path=.3f,side=1,minimum=10000},
                new Agent {position=new Vector3(-10.8f,0,46.8f),velocity=new Vector3(-4,0,4),path=.9f,side=-1,minimum=10000},
                new Agent {position=column+new Vector3(0,0,-2.1f),velocity=new Vector3(0,0,6),path=.25f,side=1,minimum=10000},
                new Agent {position=new Vector3(12.8f,0,18),velocity=new Vector3(5,0,3),path=.5f,side=-1,minimum=10000}
            };
            shader.SetInt("_Count",cases.Length);shader.SetFloat("_Speed",6);shader.SetFloat("_Dt",1f/30);
            using(var buffer=new GraphicsBuffer(GraphicsBuffer.Target.Structured,cases.Length,Marshal.SizeOf<Agent>()))
            {
                buffer.SetData(cases);shader.SetBuffer(step,"_Agents",buffer);
                for(int frame=0;frame<300;frame++)shader.Dispatch(step,1,1,1);
                buffer.GetData(cases);
                string[] names={"West wall head-on","Corner diagonal","Column head-on","East wall oblique"};
                for(int i=0;i<cases.Length;i++)report.AppendLine(names[i]+": minimum clearance="+cases[i].minimum.ToString("F4")+
                    ", ground="+cases[i].position.y+((cases[i].minimum<.35f || Mathf.Abs(cases[i].position.y)>.001f)?" FAIL":" PASS"));
                var stopped=new[]{new Agent {position=p0,phase=3.25f,side=1,minimum=10000}};
                shader.SetInt("_Count",1);shader.SetFloat("_Speed",0);buffer.SetData(stopped);
                for(int frame=0;frame<60;frame++)shader.Dispatch(step,1,1,1);
                buffer.GetData(stopped,0,0,1);
                report.AppendLine("Stopped animation: "+(Vector3.Distance(stopped[0].position,p0)<.001f&&Mathf.Abs(stopped[0].phase-3.25f)<.0001f?"PASS":"FAIL"));
            }
            File.WriteAllText(ReportPath,report.ToString());Debug.Log(report);
        }
        static double start;
        static int frames;
        static double frameSum;
        static bool original;
        static bool runtimeRunning;
        static int lastFrame;
        static bool captured;
        public static void StartRuntime()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Runtime validation must start from Edit Mode.");
            SessionState.SetBool("VATCrowdRuntimeValidation",true);
            runtimeRunning=false;RegisterRuntime();EditorApplication.isPaused=false;
            EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod]
        static void RegisterRuntime(){EditorApplication.update-=RuntimeTick;EditorApplication.update+=RuntimeTick;}
        internal static void RuntimeTick()
        {
            if(!SessionState.GetBool("VATCrowdRuntimeValidation",false))return;
            if(!EditorApplication.isPlaying)return;
            if(!runtimeRunning){runtimeRunning=true;start=EditorApplication.timeSinceStartup;frames=0;frameSum=0;original=false;captured=false;lastFrame=-1;Application.runInBackground=true;}
            double elapsed=EditorApplication.timeSinceStartup-start;
            if(frames%120==0)File.WriteAllText(CrowdDemoBuilder.Root+"/RuntimeStatus.txt","elapsed="+elapsed+", frame="+Time.frameCount+", paused="+EditorApplication.isPaused);
            if(elapsed>5 && Time.frameCount!=lastFrame){frames++;frameSum+=Time.unscaledDeltaTime;lastFrame=Time.frameCount;}
            if(!original && elapsed>20 && !captured){CrowdDemoBuilder.Capture();captured=true;}
            if(elapsed<(original?20:120))return;
            File.AppendAllText(ReportPath,"\n"+(original?"Original":"SDF")+" Play Mode: "+elapsed.ToString("F1")+" s, sampled distinct frames="+frames+
                ", mean Unity unscaled frame="+(frames>0?frameSum/frames*1000:0).ToString("F2")+" ms. Includes Editor overhead; not isolated GPU timing.\n");
            if(!original)
            {
                var root=GameObject.Find("VAT Crowd SDF Demo");var c=root.GetComponents<Component>().First(x=>x!=null&&x.GetType().Name=="CrowdSDFController");
                c.GetType().GetField("showOriginal").SetValue(c,true);c.GetType().GetMethod("Apply").Invoke(c,null);
                original=true;start=EditorApplication.timeSinceStartup;frames=0;frameSum=0;
            }
            else{SessionState.SetBool("VATCrowdRuntimeValidation",false);runtimeRunning=false;EditorApplication.isPlaying=false;}
        }
    }
}
