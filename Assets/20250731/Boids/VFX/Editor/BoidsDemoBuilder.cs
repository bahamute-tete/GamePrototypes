using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.VFX.Block;
using UnityEngine;
using UnityEngine.VFX;

namespace UnityEditor.VFX
{
    [InitializeOnLoad]
    static class BoidsDemoBuilder
    {
        const string Root = "Assets/20250731/Boids/VFX";
        const string GraphPath = Root + "/BoidsDemo.vfx";
        static BoidsDemoBuilder() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string request = Root + "/Editor/build.request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            try { Build(); Validate(); }
            catch (Exception e) { File.WriteAllText(Root + "/Verification.txt", e.ToString()); Debug.LogException(e); }
        }

        [MenuItem("Tools/Boids VFX/Create Demo Assets")]
        public static void Build()
        {
            if (File.Exists(GraphPath)) throw new InvalidOperationException("BoidsDemo.vfx already exists. Existing edits will not be overwritten.");
            string template = VisualEffectGraphPackageInfo.assetPackagePath + "/Editor/Templates/SimpleParticleSystem.vfx";
            if (!AssetDatabase.CopyAsset(template, GraphPath)) throw new IOException("Could not copy VFX template.");
            var resource = VisualEffectResource.GetResourceAtPath(GraphPath);
            var graph = resource.GetOrCreateGraph();
            var init = graph.children.OfType<VFXBasicInitialize>().Single();
            var update = graph.children.OfType<VFXBasicUpdate>().Single();
            var spawn = graph.children.OfType<VFXBasicSpawner>().Single();
            foreach (var context in new VFXContext[] { spawn, update })
                foreach (var b in context.children.ToArray()) context.RemoveChild(b);
            var state = ScriptableObject.CreateInstance<VFXParameter>();
            state.Init(typeof(GraphicsBuffer)); state.SetSettingValue("m_ExposedName", "BoidsState");
            state.SetSettingValue("m_Exposed", true); graph.AddChild(state); state.position = new Vector2(-400, 300);
            var count = ScriptableObject.CreateInstance<VFXParameter>();
            count.Init(typeof(uint)); count.SetSettingValue("m_ExposedName", "BoidsCount");
            count.SetSettingValue("m_Exposed", true); count.value = 256u; graph.AddChild(count); count.position = new Vector2(-400, 0);
            var burst = ScriptableObject.CreateInstance<VFXSpawnerBurst>(); spawn.AddChild(burst);
            burst.inputSlots.First(s => s.name == "Count").Link(count.outputSlots[0]);
            foreach (var context in new VFXContext[] { init, update })
            {
                var block = ScriptableObject.CreateInstance<BoidsApplyState>();
                context.AddChild(block);
                block.inputSlots[0].Link(state.outputSlots[0]);
            }
            update.SetSettingValue("integration", VFXBasicUpdate.VFXIntegrationMode.None);
            update.SetSettingValue("angularIntegration", VFXBasicUpdate.VFXIntegrationMode.None);
            update.SetSettingValue("ageParticles", false);
            update.SetSettingValue("reapParticles", false);
            init.GetData().SetSettingValue("capacity", 2048u);
            ((VFXDataParticle)init.GetData()).space = VFXCoordinateSpace.World;
            var bounds = init.inputSlots.FirstOrDefault(s => s.name == "bounds");
            if (bounds != null) { bounds.UnlinkAll(true); bounds.value = new AABox { center = Vector3.zero, size = new Vector3(60, 20, 60) }; }
            graph.SetExpressionGraphDirty(); graph.UpdateSubAssets(); resource.WriteAsset();
            AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceSynchronousImport);
            var go = new GameObject("Boids VFX Demo");
            try
            {
                go.AddComponent<VisualEffect>().visualEffectAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(GraphPath);
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("BoidsVFX.BoidsVFXSimulation")).First(t => t != null);
                go.AddComponent(type);
                PrefabUtility.SaveAsPrefabAsset(go, Root + "/BoidsDemo.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            Debug.Log("Boids VFX demo assets created. Drag BoidsDemo.prefab into a scene, then enter Play Mode.");
        }

        [MenuItem("Tools/Boids VFX/Validate GPU Simulation")]
        public static void Validate()
        {
            var source = AssetDatabase.LoadAssetAtPath<ComputeShader>(Root + "/Resources/BoidsSimulation.compute");
            var shader = UnityEngine.Object.Instantiate(source);
            var report = new StringBuilder();
            try
            {
                int kernel = shader.FindKernel("Simulate");
                shader.SetFloat("_DeltaTime", 1f / 60);
                shader.SetFloat("_NeighbourRadius", 3); shader.SetFloat("_SeparationRadius", 1);
                shader.SetFloat("_MaxSpeed", 3); shader.SetFloat("_MaxAcceleration", 8);
                shader.SetFloat("_BoundsRadius", 12); shader.SetVector("_Center", Vector3.zero);
                shader.SetFloat("_BoundaryWeight", 8);
                shader.SetFloat("_Alignment", 0); shader.SetFloat("_Cohesion", 0); shader.SetFloat("_Separation", 5);
                var pair = new[] { new Vector4(-0.1f,0,0,0), Vector4.zero, new Vector4(0.1f,0,0,0), Vector4.zero };
                var result = Run(shader, kernel, pair, 1);
                Require(result[1].x < 0 && result[3].x > 0, "Separation pushes a close pair apart", report);
                pair[0] = pair[2] = Vector4.zero;
                result = Run(shader, kernel, pair, 1);
                Require(((Vector3)result[1]).sqrMagnitude > 0 && ((Vector3)(result[1] + result[3])).sqrMagnitude < 1e-6f, "Coincident pair receives opposite finite velocities", report);
                shader.SetFloat("_Separation", 0); shader.SetFloat("_Cohesion", 1);
                pair[0] = new Vector4(-1,0,0,0); pair[2] = new Vector4(1,0,0,0);
                result = Run(shader, kernel, pair, 1);
                Require(result[1].x > 0 && result[3].x < 0, "Cohesion steers toward neighbours", report);
                shader.SetFloat("_Cohesion", 0); shader.SetFloat("_Alignment", 1);
                pair[1] = new Vector4(0,0,1,0);
                result = Run(shader, kernel, pair, 1);
                Require(result[3].z > 0, "Alignment transfers neighbour heading", report);
                shader.SetFloat("_Cohesion", .6f); shader.SetFloat("_Separation", 5);
                var agents = new Vector4[65 * 2];
                for (int i = 0; i < 65; i++)
                {
                    float a = i * 2.399963f;
                    agents[2*i] = new Vector4(Mathf.Cos(a)*10,0,Mathf.Sin(a)*10,0);
                    agents[2*i+1] = new Vector4(Mathf.Cos(a)*3,0,Mathf.Sin(a)*3,0);
                }
                result = Run(shader, kernel, agents, 600);
                for (int i = 0; i < 65; i++)
                {
                    var p = result[2*i]; var v = result[2*i+1];
                    if (float.IsNaN(p.sqrMagnitude + v.sqrMagnitude) || float.IsInfinity(p.sqrMagnitude + v.sqrMagnitude)
                        || Mathf.Abs(p.y) > .0001f || ((Vector3)p).magnitude > 12.001f || ((Vector3)v).magnitude > 3.001f)
                        throw new Exception("Finite state / ground / bounds / speed invariant failed for agent " + i);
                }
                report.AppendLine("PASS: 65 agents (partial thread group), 600 GPU steps: finite positions, ground plane, arena bounds, speed limit.");
                report.AppendLine("Graph imported and prefab generated separately. This report does not measure rendering performance or guarantee collision-free motion.");
                File.WriteAllText(Root + "/Verification.txt", report.ToString());
                Debug.Log(report.ToString());
            }
            finally { UnityEngine.Object.DestroyImmediate(shader); }
        }

        static void Require(bool condition, string label, StringBuilder report)
        { if (!condition) throw new Exception(label); report.AppendLine("PASS: " + label); }

        static Vector4[] Run(ComputeShader shader, int kernel, Vector4[] state, int steps)
        {
            using (var a = new GraphicsBuffer(GraphicsBuffer.Target.Structured, state.Length, 16))
            using (var b = new GraphicsBuffer(GraphicsBuffer.Target.Structured, state.Length, 16))
            {
                a.SetData(state); b.SetData(state);
                var read = a; var write = b;
                shader.SetInt("_Count", state.Length / 2);
                for (int n = 0; n < steps; n++)
                {
                    shader.SetBuffer(kernel, "_Read", read); shader.SetBuffer(kernel, "_Write", write);
                    shader.Dispatch(kernel, (state.Length / 2 + 63) / 64, 1, 1);
                    var old = read; read = write; write = old;
                }
                var result = new Vector4[state.Length]; read.GetData(result); return result;
            }
        }
    }
}
