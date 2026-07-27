using System.Collections.Generic;
using UnityEngine;
using LiangZhu.Geometry;
using LiangZhu.Geometry.Curves;

namespace LiangZhu.ProcMesh
{
    /// <summary>
    /// 读取同物体上 CurveResamplerAuthoring 的重采样结果，扫掠成 mesh 烘到 MeshFilter。
    /// 表面投影为【烘焙模式】：仅右键菜单手动触发，参数变化不会自动重投。
    /// 扫掠本身仍实时（受 liveUpdateInEditor 控制），吃"烘焙曲线(若有)或原始曲线"。
    /// 烘焙结果序列化留存，跨重编译 / 场景保存不丢。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SweepMeshAuthoring : MonoBehaviour
    {
        [Tooltip("留空则取同物体上的组件")]
        public CurveResamplerAuthoring source;

        public SweepConfig config = SweepConfig.DefaultTube;

        [Header("表面投影 (烘焙模式)")]
        [Tooltip("勾选且已烘焙时，sweep 用烘焙曲线；取消可临时显示未投影(不丢烘焙)。投影只由右键菜单触发")]
        public bool projectToSurface = false;
        [Tooltip("目标地形 / 模型的 MeshFilter。导入资源需勾 Read/Write Enabled")]
        public MeshFilter projectionTarget;
        public ProjectionConfig projectionConfig = ProjectionConfig.Default;

        [Tooltip("编辑器里随路点 / sweep 参数变化实时重建网格（不含投影）")]
        public bool liveUpdateInEditor = true;

        [Header("最终 Mesh 烘焙")]
        [Tooltip("Bake Final Mesh Asset 的输出目录，必须在 Assets 下")]
        public string bakedMeshAssetFolder = "Assets/ArtResource/Generic/Generated/SweepMeshes";
        [Tooltip("勾选时 MeshFilter 使用最终烘焙 Mesh Asset，Rebuild 不再改写临时 Mesh")]
        public bool useBakedMeshAsset = false;

        // ---- 烘焙缓存（序列化，跨重编译 / 场景保存留存）----
        [SerializeField, HideInInspector] CurveSample[] _bakedSamples;
        [SerializeField, HideInInspector] Frame[] _bakedFrames;
        [SerializeField, HideInInspector] float _bakedTotalLength;
        [SerializeField, HideInInspector] bool _bakedClosed;
        [SerializeField, HideInInspector] bool _hasBake;
        [SerializeField, HideInInspector] Mesh _bakedMeshAsset;

        public bool HasBake => _hasBake;

        Mesh _mesh;
        MeshFilter _mf;

        // RayMesh / BVH 缓存（仅烘焙时用）
        RayMesh _rayMesh;
        Mesh _cachedTargetMesh;
        int _cachedTargetVertexCount;
        Matrix4x4 _cachedTargetMatrix;
        bool _rayMeshValid;

        // 实际用于 sweep 的 local 曲线（供 GetGrowthTipWorld）
        [System.NonSerialized] ResampleResult _finalCurve;

        void OnEnable()   { EnsureRefs(); Rebuild(); }
        void OnValidate() { EnsureRefs(); Rebuild(); }

#if UNITY_EDITOR
        void Update()
        {
            if (!Application.isPlaying && liveUpdateInEditor) Rebuild();
        }
#endif

        void EnsureRefs()
        {
            if (_mf == null) _mf = GetComponent<MeshFilter>();
            if (source == null) source = GetComponent<CurveResamplerAuthoring>();
            if (useBakedMeshAsset && _bakedMeshAsset != null)
            {
                if (_mf != null && _mf.sharedMesh != _bakedMeshAsset)
                    _mf.sharedMesh = _bakedMeshAsset;
                return;
            }
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "LiangZhu_SweepMesh" };
                _mesh.hideFlags = HideFlags.DontSave;
            }
        }

        /// <summary>仅重建扫掠网格，绝不触发投影。吃烘焙曲线(若 projectToSurface 且已烤)或原始曲线。</summary>
        public void Rebuild()
        {
            EnsureRefs();
            if (useBakedMeshAsset && _bakedMeshAsset != null)
            {
                _mf.sharedMesh = _bakedMeshAsset;
                return;
            }

            if (source == null) return;

            if (!TryResolveSweepCurve(out var curve, out var frames))
            {
                _mesh.Clear();
                _mf.sharedMesh = _mesh;
                _finalCurve = default;
                return;
            }

            _finalCurve = curve;
            SweepBuilder.Build(curve, config, _mesh, frames);
            _mf.sharedMesh = _mesh;
        }

        bool TryResolveSweepCurve(out ResampleResult curve, out Frame[] frames)
        {
            frames = null;
            curve = default;

            if (source == null) return false;

            if (projectToSurface && _hasBake && _bakedSamples != null && _bakedSamples.Length >= 2)
            {
                curve = new ResampleResult
                {
                    samples = _bakedSamples,
                    totalLength = _bakedTotalLength,
                    closed = _bakedClosed
                };
                frames = _bakedFrames;
            }
            else
            {
                curve = source.Result;
            }

            return curve.IsValid;
        }

        /// <summary>右键手动触发：把当前 source 曲线投影到 projectionTarget 表面并缓存（快照）。</summary>
        [ContextMenu("Bake Surface Projection")]
        public void BakeProjection()
        {
            EnsureRefs();
            if (source == null) { Debug.LogWarning("[Sweep] 缺少 source。", this); return; }

            var curve = source.Result;
            if (!curve.IsValid) { Debug.LogWarning("[Sweep] source 曲线无效。", this); return; }
            if (!EnsureRayMesh()) { Debug.LogWarning("[Sweep] projectionTarget 缺失或无可读 Mesh。", this); return; }

            Matrix4x4 l2w = transform.localToWorldMatrix;
            Matrix4x4 w2l = transform.worldToLocalMatrix;

            var worldCurve = SurfaceRayProjector.TransformCurve(curve, l2w);
            var proj = SurfaceRayProjector.Project(worldCurve, _rayMesh, projectionConfig);
            if (!proj.IsValid) { Debug.LogWarning("[Sweep] 投影失败（全部未命中？检查方向 / 最大距离）。", this); return; }

            var localCurve  = SurfaceRayProjector.TransformCurve(proj.curve, w2l);
            var localFrames = SurfaceRayProjector.TransformFrames(proj.frames, w2l);

            _bakedSamples     = localCurve.samples;
            _bakedTotalLength = localCurve.totalLength;
            _bakedClosed      = localCurve.closed;
            _bakedFrames      = localFrames;
            _hasBake          = true;
            projectToSurface  = true; // 烤完默认显示投影结果
            useBakedMeshAsset = false; // 投影数据变了，最终 Mesh 需要重新烘焙

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            Rebuild();
            Debug.Log($"[Sweep] 表面投影已烘焙：{_bakedSamples.Length} 点。", this);
        }

#if UNITY_EDITOR
        /// <summary>右键手动触发：把当前 SweepBuilder 输出固化为可打包的 Mesh Asset，并绑定到 MeshFilter。</summary>
        [ContextMenu("Bake Final Mesh Asset")]
        public void BakeFinalMeshAsset()
        {
            EnsureRefs();

            if (source == null)
            {
                Debug.LogWarning("[Sweep] 缺少 source，无法烘焙最终 Mesh。", this);
                return;
            }

            if (!TryResolveSweepCurve(out var curve, out var frames))
            {
                Debug.LogWarning("[Sweep] 当前曲线无效，无法烘焙最终 Mesh。", this);
                return;
            }

            Mesh targetMesh = _bakedMeshAsset;
            bool createAsset = targetMesh == null || !UnityEditor.AssetDatabase.Contains(targetMesh);
            if (createAsset)
                targetMesh = new Mesh();

            targetMesh.name = MakeSafeAssetName(gameObject.name) + "_SweepMesh";
            targetMesh.hideFlags = HideFlags.None;
            SweepBuilder.Build(curve, config, targetMesh, frames);

            if (!ValidateMeshChannels(targetMesh, out var report))
            {
                Debug.LogError("[Sweep] 最终 Mesh 通道校验失败，已中止保存。\n" + report, this);
                if (createAsset)
                    DestroyImmediate(targetMesh);
                return;
            }

            if (createAsset)
            {
                string folder = NormalizeAssetFolder(bakedMeshAssetFolder);
                EnsureAssetFolder(folder);
                string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath($"{folder}/{targetMesh.name}.asset");
                UnityEditor.AssetDatabase.CreateAsset(targetMesh, path);
            }
            else
            {
                UnityEditor.EditorUtility.SetDirty(targetMesh);
            }

            _bakedMeshAsset = targetMesh;
            useBakedMeshAsset = true;
            liveUpdateInEditor = false;
            _finalCurve = curve;
            _mf.sharedMesh = targetMesh;

            UnityEditor.EditorUtility.SetDirty(targetMesh);
            UnityEditor.EditorUtility.SetDirty(_mf);
            UnityEditor.EditorUtility.SetDirty(this);
            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);

            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log($"[Sweep] 最终 Mesh 已烘焙：{UnityEditor.AssetDatabase.GetAssetPath(targetMesh)}\n{report}", this);
        }

        [UnityEditor.MenuItem("LiangZhu/Carve Sweep/Bake Selected Final Mesh Assets")]
        static void BakeSelectedFinalMeshAssets()
        {
            int count = 0;
            foreach (var go in UnityEditor.Selection.gameObjects)
            {
                var sweep = go.GetComponent<SweepMeshAuthoring>();
                if (sweep == null) continue;

                sweep.BakeFinalMeshAsset();
                count++;
            }

            if (count == 0)
                Debug.LogWarning("[Sweep] 选中的物体上没有 SweepMeshAuthoring。");
        }

        [UnityEditor.MenuItem("LiangZhu/Carve Sweep/Bake Selected Final Mesh Assets", true)]
        static bool ValidateBakeSelectedFinalMeshAssets()
        {
            foreach (var go in UnityEditor.Selection.gameObjects)
                if (go.GetComponent<SweepMeshAuthoring>() != null)
                    return true;
            return false;
        }

        static string NormalizeAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                folder = "Assets/ArtResource/Generic/Generated/SweepMeshes";

            folder = folder.Replace('\\', '/').Trim('/');
            if (!folder.StartsWith("Assets"))
            {
                Debug.LogWarning($"[Sweep] Mesh 输出目录必须在 Assets 下，已改用默认目录。原目录：{folder}");
                folder = "Assets/ArtResource/Generic/Generated/SweepMeshes";
            }
            return folder;
        }

        static void EnsureAssetFolder(string folder)
        {
            folder = NormalizeAssetFolder(folder);
            if (UnityEditor.AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!UnityEditor.AssetDatabase.IsValidFolder(next))
                    UnityEditor.AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static string MakeSafeAssetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = "Sweep";

            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        static bool ValidateMeshChannels(Mesh mesh, out string report)
        {
            int vertexCount = mesh != null ? mesh.vertexCount : 0;
            int triangleIndexCount = mesh != null ? mesh.triangles.Length : 0;
            var uv0 = new List<Vector2>();
            var uv2 = new List<Vector2>();
            var uv3 = new List<Vector2>();
            var tangents = new List<Vector4>();
            var normals = new List<Vector3>();

            if (mesh != null)
            {
                mesh.GetUVs(0, uv0);
                mesh.GetUVs(2, uv2);
                mesh.GetUVs(3, uv3);
                mesh.GetTangents(tangents);
                mesh.GetNormals(normals);
            }

            bool ok = vertexCount > 0
                && normals.Count == vertexCount
                && tangents.Count == vertexCount
                && uv0.Count == vertexCount
                && uv2.Count == vertexCount
                && uv3.Count == vertexCount
                && triangleIndexCount > 0;

            report =
                $"verts={vertexCount}, tris={triangleIndexCount / 3}, " +
                $"normals={normals.Count}, tangents={tangents.Count}, " +
                $"uv0={uv0.Count}, uv2(grow)={uv2.Count}, uv3(path)={uv3.Count}";
            return ok;
        }
#endif

        /// <summary>右键手动触发：清除烘焙，还原为未投影。</summary>
        [ContextMenu("Clear Projection Bake")]
        public void ClearProjectionBake()
        {
            _hasBake = false;
            _bakedSamples = null;
            _bakedFrames = null;
            _bakedTotalLength = 0f;
            _bakedClosed = false;
            useBakedMeshAsset = false;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            Rebuild();
        }

        /// <summary>按需重建 RayMesh：仅当目标 mesh / 顶点数 / transform 变化时重建 BVH（烘焙时调用）。</summary>
        bool EnsureRayMesh()
        {
            if (projectionTarget == null || projectionTarget.sharedMesh == null)
            {
                _rayMesh = null;
                _rayMeshValid = false;
                return false;
            }

            Mesh tm = projectionTarget.sharedMesh;
            int vc = tm.vertexCount;
            Matrix4x4 tmat = projectionTarget.transform.localToWorldMatrix;

            if (!_rayMeshValid || _rayMesh == null
                || tm != _cachedTargetMesh
                || vc != _cachedTargetVertexCount
                || tmat != _cachedTargetMatrix)
            {
                _rayMesh = RayMesh.FromMesh(tm, projectionTarget.transform);
                _cachedTargetMesh = tm;
                _cachedTargetVertexCount = vc;
                _cachedTargetMatrix = tmat;
                _rayMeshValid = true;
            }
            return true;
        }

        /// <summary>生长尖端世界坐标（已投影时贴在表面）。t∈[0,1] 对应 shader 的 _GrowT。</summary>
        public Vector3 GetGrowthTipWorld(float t)
        {
            var c = _finalCurve;
            if (!c.IsValid) return transform.position;

            float d = Mathf.Clamp01(t) * c.totalLength;
            var s = c.samples;
            for (int i = 0; i < s.Length - 1; i++)
            {
                if (d <= s[i + 1].distance)
                {
                    float w = (d - s[i].distance) / Mathf.Max(s[i + 1].distance - s[i].distance, 1e-6f);
                    return transform.TransformPoint(Vector3.Lerp(s[i].position, s[i + 1].position, w));
                }
            }
            return transform.TransformPoint(s[s.Length - 1].position);
        }

        /// <summary>强制下次烘焙重建 BVH（目标 mesh 被原地改动、引用未变时调用）。</summary>
        public void InvalidateRayMesh() => _rayMeshValid = false;

        void OnDestroy()
        {
            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
        }
    }
}
