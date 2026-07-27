using System.Collections.Generic;
using UnityEngine;
using LiangZhu.Geometry;

namespace LiangZhu.ProcMesh
{
    /// <summary>
    /// 消费任意 ICurveSource 的"快速、不投影"版:把来源曲线从其自身空间转进本物体 local,
    /// 走 SweepBatch 烘进本物体 MeshFilter。需要表面投影 / framesPerPath 请改用 SweepMeshAuthoring。
    ///
    /// 重要:输出物体不要复用作为图来源的表面物体。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [DefaultExecutionOrder(100)]
    public class SweepBatchAuthoring : MonoBehaviour
    {
        [Header("曲线来源")]
        [Tooltip("拖一个实现 ICurveSource 的组件;留空则在本物体上 GetComponent 查找")]
        public MonoBehaviour curveSource;

        public SweepConfig config = SweepConfig.DefaultTube;

        [Header("输出(留空用本物体的 MeshFilter)")]
        public MeshFilter outputFilter;

        [Header("调试")]
        public bool logOnRebuild = false;

        public PathInfo[] LastPaths { get; private set; } = System.Array.Empty<PathInfo>();

        Mesh _mesh;
        readonly List<ResampleResult> _local = new List<ResampleResult>();

        ICurveSource Source =>
            (curveSource as ICurveSource) ?? GetComponent<ICurveSource>();

        void OnEnable()   => Rebuild();
        void OnValidate() => Rebuild();

#if UNITY_EDITOR
        void Update()
        {
            if (!Application.isPlaying) Rebuild();
        }
#endif

        public void Rebuild()
        {
            var src = Source;
            if (src == null) return;

            var mf = outputFilter != null ? outputFilter : GetComponent<MeshFilter>();
            if (mf == null) return;

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "SweepBatch" };
                _mesh.MarkDynamic();
            }

            // 来源 local → world → 本物体 local
            Matrix4x4 c2l = transform.worldToLocalMatrix * src.CurveToWorld;

            _local.Clear();
            var curves = src.GetCurves();
            if (curves != null)
                for (int i = 0; i < curves.Count; i++)
                    if (curves[i].IsValid)
                        _local.Add(SurfaceRayProjector.TransformCurve(curves[i], c2l));

            LastPaths = SweepBatch.Build(_local, config, _mesh);
            mf.sharedMesh = _mesh;

            if (logOnRebuild)
                Debug.Log($"[SweepBatch] curves={_local.Count} paths={LastPaths.Length} verts={_mesh.vertexCount}");
        }
    }
}
