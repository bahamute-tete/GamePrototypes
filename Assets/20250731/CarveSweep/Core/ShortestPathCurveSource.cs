using System.Collections.Generic;
using UnityEngine;
using LiangZhu.Geometry;

namespace LiangZhu.ProcMesh
{
    /// <summary>
    /// 以 mesh 上的最短路作为曲线来源。求解为【手动模式】:不再每帧 / 改参数就重跑 Dijkstra
    /// (那在初始化和大网格上很容易卡),改由右键菜单 "Rebuild Paths (Solve)" 触发,
    /// 或运行时勾 rebuildOnEnable 一次性求解。求解后 Version 自增,消费方据此重建网格。
    /// 产出的曲线在目标 mesh 的 local space;CurveToWorld 给出到世界的变换。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ShortestPathCurveSource : MonoBehaviour, ICurveSource, ICurveVersion
    {
        [Header("目标 Mesh(图的来源)")]
        public MeshFilter targetMesh;
        [Min(1e-5f)] public float weldEpsilon = 1e-4f;

        [Header("起点 / 终点 anchors(世界坐标)")]
        public List<Transform> startAnchors = new List<Transform>();
        public List<Transform> endAnchors = new List<Transform>();
        [Min(0f)] public float snapMaxDistance = 0.1f;
        public PathPairing pairing = PathPairing.EachStartToEachEnd;

        [Header("重采样(复用 CurveResampler 语义)")]
        public ResampleMode mode = ResampleMode.SubdivisionCurve;
        public ResampleSpec spec = ResampleSpec.ByMaxLength;
        [Min(0.001f)] public float maxSegmentLength = 0.25f; // ByMaxLength 用
        [Min(2)]      public int   targetCount = 64;          // ByCount 用
        [Range(0f, 1f)] public float catmullAlpha = 0.5f;
        [Range(2, 64)]  public int   subdivisionSamplesPerSegment = 16;

        [Header("抽稀 (RDP,平滑前去边尺度锯齿)")]
        [Tooltip("0 = 关闭。简化后折线偏离原最短路不超过此距离(mesh local 单位);" +
                 "建议从平均边长的 0.5~2 倍起调。过大会切角、平滑后偏离原路径")]
        [Min(0f)] public float simplifyEpsilon = 0f;

        [Header("求解(手动)")]
        [Tooltip("勾选则 OnEnable 时求解一次(供 runtime);编辑器里建议保持关闭,用右键菜单手动求解")]
        public bool rebuildOnEnable = false;

        [Header("Gizmo 预览")]
        public bool drawCurves = true;
        public Color curveColor = new Color(0.2f, 0.8f, 1f);

        /// <summary>最新一批曲线(目标 mesh 的 local space)。供 SweepBatch 消费。</summary>
        public IReadOnlyList<ResampleResult> GetCurves() => _curves;

        /// <summary>曲线空间(= 目标 mesh local)到世界的变换。</summary>
        public Matrix4x4 CurveToWorld =>
            targetMesh != null ? targetMesh.transform.localToWorldMatrix : Matrix4x4.identity;

        readonly List<ResampleResult> _curves = new List<ResampleResult>();

        /// <summary>曲线每次真正重算时自增,供消费方做变更检测。</summary>
        public int Version { get; private set; }

        // 图缓存:mesh / weld 不变则复用
        ShortestPathSolver _solver;
        Mesh _builtMesh;
        float _builtWeld;

        void OnEnable()
        {
            if (rebuildOnEnable) Rebuild();
        }

        /// <summary>求解最短路并生成曲线。手动触发(右键菜单 / 代码调用)。</summary>
        [ContextMenu("Rebuild Paths (Solve)")]
        public void Rebuild()
        {
            _curves.Clear();
            if (targetMesh == null || targetMesh.sharedMesh == null) { _solver = null; return; }

            var mesh = targetMesh.sharedMesh;

            // 仅在 mesh / weld 变化时重建图与 Dijkstra scratch
            if (_solver == null || _builtMesh != mesh || !Mathf.Approximately(_builtWeld, weldEpsilon))
            {
                _solver = ShortestPathSolver.FromMesh(mesh, weldEpsilon);
                _builtMesh = mesh;
                _builtWeld = weldEpsilon;
            }

            // anchor 世界坐标 -> 目标 mesh local(图在 local space)
            var w2l = targetMesh.transform.worldToLocalMatrix;
            var req = new ShortestPathRequest { Pairing = pairing, SnapMaxDistance = snapMaxDistance };
            for (int i = 0; i < startAnchors.Count; i++)
                if (startAnchors[i] != null) req.StartPositions.Add(w2l.MultiplyPoint3x4(startAnchors[i].position));
            for (int i = 0; i < endAnchors.Count; i++)
                if (endAnchors[i] != null) req.EndPositions.Add(w2l.MultiplyPoint3x4(endAnchors[i].position));

            if (req.StartPositions.Count == 0 || req.EndPositions.Count == 0) { unchecked { Version++; } return; }

            float specValue = spec == ResampleSpec.ByCount ? targetCount : maxSegmentLength;

            var paths = _solver.Solve(req);
            for (int i = 0; i < paths.Count; i++)
            {
                if (ShortestPathToCurve.TryConvert(paths[i], mode, spec, specValue,
                        catmullAlpha, subdivisionSamplesPerSegment, out var rr, simplifyEpsilon))
                    _curves.Add(rr);
            }

            unchecked { Version++; }

#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>清空已求解的曲线(并通知消费方)。</summary>
        [ContextMenu("Clear Paths")]
        public void Clear()
        {
            _curves.Clear();
            unchecked { Version++; }
        }

        void OnDrawGizmos()
        {
            if (!drawCurves || _curves.Count == 0) return;

            // 曲线在目标 mesh 的 local space,用其 localToWorld 还原显示
            Matrix4x4 l2w = targetMesh != null
                ? targetMesh.transform.localToWorldMatrix
                : Matrix4x4.identity;

            Gizmos.color = curveColor;
            for (int ci = 0; ci < _curves.Count; ci++)
            {
                var s = _curves[ci].samples;
                if (s == null) continue;
                for (int i = 0; i < s.Length - 1; i++)
                    Gizmos.DrawLine(l2w.MultiplyPoint3x4(s[i].position),
                                    l2w.MultiplyPoint3x4(s[i + 1].position));
            }
        }
    }
}
