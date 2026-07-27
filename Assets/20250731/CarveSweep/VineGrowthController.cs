using System.Collections.Generic;
using UnityEngine;

namespace LiangZhu.ProcMesh
{
    /// <summary>
    /// 驱动逐条藤蔓的错峰生长。Timeline 只需动 growGlobal 这一个值(写入 shader 的 _GrowT);
    /// 每条路径的 local 进度由 shader 用 UV3.x(path id)+ _PathCount 算出。
    /// 路径数直接从渲染网格的 UV3.x 读出(单曲线=1,批处理=N),不依赖任何 authoring 类型。
    /// 用 MaterialPropertyBlock,不实例化材质、也不污染 sharedMaterial。
    /// feather / glow 等静态样式仍由材质面板控制。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    [DefaultExecutionOrder(200)] // 在网格构建组件之后,确保读到最新网格的路径数
    public class VineGrowthController : MonoBehaviour
    {
        public enum StaggerMode { Sequential = 0, Scatter = 1 }

        [Header("生长(Timeline 动 growGlobal → _GrowT)")]
        [Range(0f, 1f)] public float growGlobal = 0f;     // 全局进度,可被 Timeline 驱动
        [Range(0.01f, 1f)] public float growSpan = 0.4f;  // 每条占全局时间轴比例:1=齐长,越小越强顺序
        public StaggerMode mode = StaggerMode.Sequential;

        [Header("路径数来源(留空用本物体的 MeshFilter)")]
        [Tooltip("从网格 UV3.x(path id)读出路径数,适配单条 / 批处理任意来源")]
        public MeshFilter meshSource;

        [Header("预览(无 Timeline 时自动推进)")]
        public bool autoPlay = false;
        public float autoSpeed = 0.25f;                    // 每秒推进的 growGlobal

        static readonly int ID_GrowT = Shader.PropertyToID("_GrowT");
        static readonly int ID_Count = Shader.PropertyToID("_PathCount");
        static readonly int ID_Span  = Shader.PropertyToID("_GrowSpan");
        static readonly int ID_Mode  = Shader.PropertyToID("_GrowMode");

        MeshRenderer _mr;
        MaterialPropertyBlock _mpb;

        // path 数缓存:只在网格实例 / 顶点数变化时重新数 UV3,避免每帧 GetUVs
        Mesh _countedMesh;
        int _countedVerts = -1;
        int _pathCount = 1;
        static readonly List<Vector2> _uv3 = new List<Vector2>();

        void OnEnable()
        {
            _mr = GetComponent<MeshRenderer>();
            Apply();
        }

        void OnValidate() => Apply();

        void Update()
        {
            if (autoPlay && Application.isPlaying)
                growGlobal = Mathf.Repeat(growGlobal + autoSpeed * Time.deltaTime, 1f);
            Apply();
        }

        public void Apply()
        {
            if (_mr == null) _mr = GetComponent<MeshRenderer>();
            if (_mr == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            int count = ResolvePathCount();

            _mr.GetPropertyBlock(_mpb);
            _mpb.SetFloat(ID_GrowT, growGlobal);
            _mpb.SetFloat(ID_Count, Mathf.Max(1, count));
            _mpb.SetFloat(ID_Span,  Mathf.Max(0.01f, growSpan));
            _mpb.SetFloat(ID_Mode,  (float)(int)mode);
            _mr.SetPropertyBlock(_mpb);
        }

        // 从网格 UV3.x 求 max(path id)+1。带缓存,网格没变则直接返回。
        int ResolvePathCount()
        {
            var mf = meshSource != null ? meshSource : GetComponent<MeshFilter>();
            var mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null) { _countedMesh = null; _countedVerts = -1; return 1; }

            if (mesh == _countedMesh && mesh.vertexCount == _countedVerts)
                return _pathCount;

            _countedMesh = mesh;
            _countedVerts = mesh.vertexCount;

            mesh.GetUVs(3, _uv3);           // UV3 未设置时返回空 → count 退化为 1
            int maxId = 0;
            for (int i = 0; i < _uv3.Count; i++)
            {
                int id = (int)_uv3[i].x;
                if (id > maxId) maxId = id;
            }
            _pathCount = maxId + 1;
            return _pathCount;
        }
    }
}
