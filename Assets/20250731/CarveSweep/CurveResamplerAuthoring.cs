using System.Collections.Generic;
using UnityEngine;

namespace LiangZhu.ProcMesh
{
    /// <summary>
    /// 路点容器 + 重采样预览。编辑器里实时刷新（路点可拖动 WYSIWYG）；
    /// runtime 因路点静态，仅 OnEnable 烘焙一次。Result 供后续 Sweep 阶段读取。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CurveResamplerAuthoring : MonoBehaviour
    {
        [Header("路点 (Waypoints, 至少 2 个)")]
        public List<Transform> waypoints = new List<Transform>();

        [Header("重采样 (Resample)")]
        public ResampleMode mode = ResampleMode.SubdivisionCurve;
        public ResampleSpec spec = ResampleSpec.ByMaxLength;
        [Min(0.001f)] public float maxSegmentLength = 0.25f; // ByMaxLength 用
        [Min(2)]      public int   targetCount = 64;          // ByCount 用
        public bool closed = false;

        [Header("Catmull-Rom (仅 SubdivisionCurve)")]
        [Tooltip("0=uniform / 0.5=centripetal / 1=chordal，设为与你现有 Catmull-Rom 一致")]
        [Range(0f, 1f)] public float catmullAlpha = 0.5f;
        [Range(2, 64)]  public int   subdivisionSamplesPerSegment = 16;

        [Header("Gizmo 预览")]
        public bool drawWaypoints = true;
        public bool drawResampled = true;
        public bool drawTangents  = false;
        [Min(0f)] public float tangentLength = 0.2f;
        public Color waypointColor = new Color(1f, 0.6f, 0.1f);
        public Color curveColor    = new Color(0.2f, 0.8f, 1f);
        public Color tangentColor  = new Color(0.4f, 1f, 0.4f);

        /// <summary>最新重采样结果（local space）。供 Sweep 阶段消费。</summary>
        public ResampleResult Result => _result;
        ResampleResult _result;

        readonly List<Vector3> _localPts = new List<Vector3>();

        void OnEnable()   => Rebuild();
        void OnValidate() => Rebuild();

#if UNITY_EDITOR
        void Update()
        {
            // 编辑器里路点拖动时实时刷新；runtime 路点不变，故不在这里重建
            if (!Application.isPlaying) Rebuild();
        }
#endif

        /// <summary>重新采集路点并重采样。可被外部主动调用（如运行时改了参数）。</summary>
        public void Rebuild()
        {
            _localPts.Clear();
            _result = default;

            if (waypoints == null) return;
            for (int i = 0; i < waypoints.Count; i++)
            {
                var t = waypoints[i];
                if (t != null) _localPts.Add(transform.InverseTransformPoint(t.position));
            }
            if (_localPts.Count < 2) return;

            float specValue = spec == ResampleSpec.ByCount ? targetCount : maxSegmentLength;
            _result = CurveResampler.Resample(
                _localPts, mode, spec, specValue, closed,
                catmullAlpha, subdivisionSamplesPerSegment);
        }

        void OnDrawGizmos()
        {
            if (drawWaypoints && waypoints != null)
            {
                Gizmos.color = waypointColor;
                for (int i = 0; i < waypoints.Count; i++)
                    if (waypoints[i] != null) Gizmos.DrawSphere(waypoints[i].position, 0.04f);
            }

            if (!_result.IsValid) return;

            var s = _result.samples;
            int n = s.Length;
            Matrix4x4 l2w = transform.localToWorldMatrix;

            if (drawResampled)
            {
                Gizmos.color = curveColor;
                int lastSeg = _result.closed ? n : n - 1;
                for (int i = 0; i < lastSeg; i++)
                {
                    Vector3 a = l2w.MultiplyPoint3x4(s[i].position);
                    Vector3 b = l2w.MultiplyPoint3x4(s[(i + 1) % n].position);
                    Gizmos.DrawLine(a, b);
                }
                for (int i = 0; i < n; i++)
                    Gizmos.DrawSphere(l2w.MultiplyPoint3x4(s[i].position), 0.015f);
            }

            if (drawTangents && tangentLength > 0f)
            {
                Gizmos.color = tangentColor;
                for (int i = 0; i < n; i++)
                {
                    Vector3 p   = l2w.MultiplyPoint3x4(s[i].position);
                    Vector3 dir = l2w.MultiplyVector(s[i].tangent).normalized;
                    Gizmos.DrawLine(p, p + dir * tangentLength);
                }
            }
        }
    }
}
