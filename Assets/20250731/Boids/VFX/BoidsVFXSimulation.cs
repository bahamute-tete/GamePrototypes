using UnityEngine;
using UnityEngine.VFX;

namespace BoidsVFX
{
    [DisallowMultipleComponent, RequireComponent(typeof(VisualEffect))]
    [AddComponentMenu("VFX/Boids VFX Simulation")]
    public sealed class BoidsVFXSimulation : MonoBehaviour
    {
        [Header("GPU simulation (XZ ground plane, world space)")]
        [SerializeField] ComputeShader simulation;
        [Range(1, 2048)] public int agentCount = 256;
        [Min(0.1f)] public float arenaRadius = 12;
        public int seed = 1234;
        [Header("Boids rules")]
        [Min(0.01f)] public float neighbourRadius = 3;
        [Min(0.01f)] public float separationRadius = 0.8f;
        [Min(0)] public float alignment = 1;
        [Min(0)] public float cohesion = 0.6f;
        [Min(0)] public float separation = 5;
        [Min(0)] public float boundaryWeight = 8;
        [Min(0.01f)] public float maxSpeed = 3;
        [Min(0.01f)] public float maxAcceleration = 8;

        VisualEffect effect;
        ComputeShader shader;
        GraphicsBuffer read, write;
        int kernel, allocatedCount;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            effect = GetComponent<VisualEffect>();
            if (!SystemInfo.supportsComputeShaders || effect.visualEffectAsset == null)
            {
                Debug.LogError("Boids needs compute shader support and a Visual Effect Asset.", this);
                enabled = false;
                return;
            }
            var source = simulation != null ? simulation : Resources.Load<ComputeShader>("BoidsSimulation");
            if (source == null || !effect.HasGraphicsBuffer("BoidsState") || !effect.HasUInt("BoidsCount"))
            {
                Debug.LogError("Assign BoidsSimulation.compute and expose BoidsState (GraphicsBuffer), BoidsCount (uint) in the graph.", this);
                enabled = false;
                return;
            }
            // Each component owns its compute parameter state and buffers.
            shader = Instantiate(source);
            kernel = shader.FindKernel("Simulate");
            ResetSimulation();
        }

        [ContextMenu("Reset Boids Simulation")]
        public void ResetSimulation()
        {
            if (!Application.isPlaying || shader == null) return;
            effect.Stop();
            ReleaseBuffers();
            allocatedCount = Mathf.Clamp(agentCount, 1, 2048);
            read = new GraphicsBuffer(GraphicsBuffer.Target.Structured, allocatedCount * 2, 16);
            write = new GraphicsBuffer(GraphicsBuffer.Target.Structured, allocatedCount * 2, 16);
            var initial = new Vector4[allocatedCount * 2];
            var random = new System.Random(seed);
            for (int i = 0; i < allocatedCount; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                float radius = Mathf.Sqrt((float)random.NextDouble()) * Mathf.Max(0.1f, arenaRadius) * 0.8f;
                Vector3 p = transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                float yaw = (float)random.NextDouble() * Mathf.PI * 2;
                Vector3 v = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw)) * Mathf.Max(0.01f, maxSpeed) * 0.6f;
                initial[2 * i] = new Vector4(p.x, p.y, p.z, yaw);
                initial[2 * i + 1] = new Vector4(v.x, 0, v.z, 0);
            }
            read.SetData(initial);
            write.SetData(initial);
            effect.SetUInt("BoidsCount", (uint)allocatedCount);
            effect.SetGraphicsBuffer("BoidsState", read);
            effect.Reinit(); // Reset particle IDs before a single OnPlay burst.
        }

        void Update()
        {
            if (read == null || shader == null || effect.pause) return;
            if (allocatedCount != Mathf.Clamp(agentCount, 1, 2048)) ResetSimulation();
            float dt = Mathf.Min(Time.deltaTime * Mathf.Max(0, effect.playRate), 0.05f);
            if (dt <= 0) return;
            shader.SetInt("_Count", allocatedCount);
            shader.SetFloat("_DeltaTime", dt);
            shader.SetFloat("_NeighbourRadius", Mathf.Max(0.01f, neighbourRadius));
            shader.SetFloat("_SeparationRadius", Mathf.Max(0.01f, separationRadius));
            shader.SetFloat("_Alignment", Mathf.Max(0, alignment));
            shader.SetFloat("_Cohesion", Mathf.Max(0, cohesion));
            shader.SetFloat("_Separation", Mathf.Max(0, separation));
            shader.SetFloat("_MaxSpeed", Mathf.Max(0.01f, maxSpeed));
            shader.SetFloat("_MaxAcceleration", Mathf.Max(0.01f, maxAcceleration));
            shader.SetFloat("_BoundsRadius", Mathf.Max(0.1f, arenaRadius));
            shader.SetFloat("_BoundaryWeight", Mathf.Max(0, boundaryWeight));
            shader.SetVector("_Center", transform.position);
            shader.SetBuffer(kernel, "_Read", read);
            shader.SetBuffer(kernel, "_Write", write);
            shader.Dispatch(kernel, (allocatedCount + 63) / 64, 1, 1);
            var old = read; read = write; write = old;
            effect.SetGraphicsBuffer("BoidsState", read);
        }

        void OnDisable()
        {
            if (effect != null)
            {
                effect.Stop();
                if (effect.HasGraphicsBuffer("BoidsState")) effect.SetGraphicsBuffer("BoidsState", null);
            }
            ReleaseBuffers();
            if (shader != null) Destroy(shader);
            shader = null;
        }

        void ReleaseBuffers()
        {
            read?.Dispose(); write?.Dispose();
            read = write = null;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, arenaRadius));
        }
    }
}
