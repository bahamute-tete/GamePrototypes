using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RoadMeshGenerator : MonoBehaviour
{
    [Header("Road Settings")]
    public RoadPathManager pathManager;
    [Min(0.01f), Tooltip("道路的世界空间宽度")]
    public float roadWidth = 5f;
    [Range(0f, 2f), Tooltip("沿道路截面向下挤出的厚度，与材质数量无关")]
    public float roadDepth = 0.2f;

    [Header("Material Settings")]
    public bool useMultipleMaterials;
    public Material topMaterial;
    public Material sideMaterial;
    public Material bottomMaterial;

    [Header("Update Settings")]
    public bool autoUpdate;
    [Header("UV Settings")]
    public Vector2 uvRepeat = Vector2.one;
    public bool flipUV;

    public enum SamplingMode { UniformPerSegment, UniformGlobal }
    [Header("Sampling Settings")]
    [Tooltip("网格与路径预览共用采样算法，此处独立控制网格精度")]
    public SamplingMode samplingMode = SamplingMode.UniformGlobal;
    [Range(2, 50)] public int pointsPerSegment = 10;
    [Range(0.1f, 5f)] public float densityFactor = 1f;
    [Range(5, 100)] public int totalCurvePoints = 30;

    [Header("Debug Visualization")]
    public bool showDebugVisuals = true;
    public Color leftCurveColor = Color.red;
    public Color rightCurveColor = Color.blue;
    public float debugPointSize = 0.1f;

    // Preserve ownership over domain reload; duplicated components must not reuse the source mesh.
    [SerializeField, HideInInspector] private Mesh mesh;
    [SerializeField, HideInInspector] private int meshOwnerId;
    private MeshFilter meshFilter;
    private RoadPathManager subscribedPath;
    private readonly List<Vector3> leftCurvePoints = new List<Vector3>();
    private readonly List<Vector3> rightCurvePoints = new List<Vector3>();
    private Matrix4x4 lastMatrix;

    public void GenerateRoadMesh()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= DelayedGenerate;
#endif
        ResolveComponents();
        if (meshFilter == null) return;
        if (pathManager == null || !Finite(roadWidth) || roadWidth <= 0f ||
            !Finite(roadDepth) || !Finite(uvRepeat.x) || !Finite(uvRepeat.y) ||
            Mathf.Abs(transform.localToWorldMatrix.determinant) < 0.000001f)
        {
            ClearRoad();
            return;
        }

        var points = pathManager.SamplePath((RoadPathManager.SamplingMode)samplingMode,
            samplingMode == SamplingMode.UniformGlobal ? totalCurvePoints : pointsPerSegment,
            densityFactor);
        if (points.Length < 2) { ClearRoad(); return; }

        leftCurvePoints.Clear();
        rightCurvePoints.Clear();
        bool extruded = roadDepth > 0f;
        int stride = extruded ? 8 : 2;
        var vertices = new List<Vector3>(points.Length * stride + (extruded ? 8 : 0));
        var uvs = new List<Vector2>(vertices.Capacity);
        var top = new List<int>();
        var sides = new List<int>();
        var bottom = new List<int>();
        float leftU = flipUV ? uvRepeat.x : 0f;
        float rightU = flipUV ? 0f : uvRepeat.x;
        float sideU = Mathf.Max(0f, roadDepth) / roadWidth * uvRepeat.x;

        for (int i = 0; i < points.Length; i++)
        {
            var point = points[i];
            Vector3 right = point.position + point.right * (roadWidth * 0.5f);
            Vector3 left = point.position - point.right * (roadWidth * 0.5f);
            Vector3 down = -point.up * Mathf.Max(0f, roadDepth);
            float v = point.distance / roadWidth * uvRepeat.y;
            rightCurvePoints.Add(transform.InverseTransformPoint(right));
            leftCurvePoints.Add(transform.InverseTransformPoint(left));

            AddVertex(vertices, uvs, right, new Vector2(rightU, v));
            AddVertex(vertices, uvs, left, new Vector2(leftU, v));
            if (extruded)
            {
                // Separate strips allow hard edges and independent UVs at face boundaries.
                AddVertex(vertices, uvs, right + down, new Vector2(rightU, v));
                AddVertex(vertices, uvs, left + down, new Vector2(leftU, v));
                AddVertex(vertices, uvs, right, new Vector2(0f, v));
                AddVertex(vertices, uvs, right + down, new Vector2(sideU, v));
                AddVertex(vertices, uvs, left, new Vector2(0f, v));
                AddVertex(vertices, uvs, left + down, new Vector2(sideU, v));
            }
            if (i == points.Length - 1) continue;
            int start = i * stride;
            AddStrip(top, start, start + 1, start + stride, start + stride + 1, false);
            if (extruded)
            {
                AddStrip(bottom, start + 2, start + 3, start + stride + 2, start + stride + 3, true);
                AddStrip(sides, start + 4, start + 5, start + stride + 4, start + stride + 5, true);
                AddStrip(sides, start + 6, start + 7, start + stride + 6, start + stride + 7, false);
            }
        }
        if (extruded)
        {
            AddCap(vertices, uvs, sides, points[0], false);
            AddCap(vertices, uvs, sides, points[points.Length - 1], true);
        }

        if (mesh != null && meshOwnerId != GetInstanceID()) mesh = null;
#if UNITY_EDITOR
        if (mesh != null && EditorUtility.IsPersistent(mesh)) mesh = null;
#endif
        if (mesh == null)
        {
            mesh = new Mesh { name = "RoadMesh" };
            meshOwnerId = GetInstanceID();
        }
        mesh.Clear();
        mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        if (extruded && useMultipleMaterials)
        {
            mesh.subMeshCount = 3;
            mesh.SetTriangles(top, 0);
            mesh.SetTriangles(sides, 1);
            mesh.SetTriangles(bottom, 2);
        }
        else
        {
            top.AddRange(sides);
            top.AddRange(bottom);
            mesh.subMeshCount = 1;
            mesh.SetTriangles(top, 0);
        }
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;
        // Update an existing collider without introducing a new component or feature.
        MeshCollider collider = GetComponent<MeshCollider>();
        if (collider != null) { collider.sharedMesh = null; collider.sharedMesh = mesh; }
        ApplyMaterials(extruded && useMultipleMaterials);
        lastMatrix = transform.localToWorldMatrix;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(meshFilter);
            EditorUtility.SetDirty(mesh);
        }
#endif
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private void AddVertex(List<Vector3> vertices, List<Vector2> uvs, Vector3 world, Vector2 uv)
    {
        vertices.Add(transform.InverseTransformPoint(world));
        uvs.Add(uv);
    }

    private static void AddStrip(List<int> indices, int a, int b, int c, int d, bool reverse)
    {
        indices.Add(a); indices.Add(reverse ? c : b); indices.Add(reverse ? b : c);
        indices.Add(c); indices.Add(reverse ? d : b); indices.Add(reverse ? b : d);
    }

    private void AddCap(List<Vector3> vertices, List<Vector2> uvs, List<int> indices,
        RoadPathManager.PathPoint point, bool end)
    {
        int start = vertices.Count;
        Vector3 right = point.position + point.right * roadWidth * 0.5f;
        Vector3 left = point.position - point.right * roadWidth * 0.5f;
        Vector3 down = -point.up * roadDepth;
        float depthV = roadDepth / roadWidth * uvRepeat.y;
        AddVertex(vertices, uvs, right, new Vector2(0f, 0f));
        AddVertex(vertices, uvs, left, new Vector2(uvRepeat.x, 0f));
        AddVertex(vertices, uvs, right + down, new Vector2(0f, depthV));
        AddVertex(vertices, uvs, left + down, new Vector2(uvRepeat.x, depthV));
        AddStrip(indices, start, start + 1, start + 2, start + 3, !end);
    }

    private void ApplyMaterials(bool multiple)
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        Material top = topMaterial != null ? topMaterial : renderer.sharedMaterial;
        renderer.sharedMaterials = multiple ? new[]
        {
            top, sideMaterial != null ? sideMaterial : top,
            bottomMaterial != null ? bottomMaterial : top
        } : new[] { top };
    }

    private void ClearRoad()
    {
        leftCurvePoints.Clear();
        rightCurvePoints.Clear();
        if (mesh != null && meshOwnerId == GetInstanceID())
        {
#if UNITY_EDITOR
            if (!EditorUtility.IsPersistent(mesh)) mesh.Clear();
#else
            mesh.Clear();
#endif
        }
        if (meshFilter != null) meshFilter.sharedMesh = null;
        MeshCollider collider = GetComponent<MeshCollider>();
        if (collider != null) collider.sharedMesh = null;
    }

    private void ResolveComponents()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (pathManager == null) pathManager = GetComponent<RoadPathManager>();
        BindPath();
    }

    private void BindPath()
    {
        RoadPathManager next = isActiveAndEnabled ? pathManager : null;
        if (subscribedPath == next) return;
        if (subscribedPath != null) subscribedPath.onPathChanged -= OnPathChanged;
        subscribedPath = next;
        if (subscribedPath != null) subscribedPath.onPathChanged += OnPathChanged;
    }

    private void OnPathChanged() { if (autoUpdate) RequestGenerate(); }

    private void RequestGenerate()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= DelayedGenerate;
        EditorApplication.delayCall += DelayedGenerate;
#else
        if (isActiveAndEnabled) GenerateRoadMesh();
#endif
    }
#if UNITY_EDITOR
    private void DelayedGenerate()
    {
        if (this != null && isActiveAndEnabled && autoUpdate) GenerateRoadMesh();
    }
    private void OnUndoRedo()
    {
        // Geometry is derived data: rebuild it even when automatic preview is disabled.
        if (this != null && isActiveAndEnabled) GenerateRoadMesh();
    }
#endif

    private void OnEnable()
    {
        ResolveComponents();
        lastMatrix = transform.localToWorldMatrix;
#if UNITY_EDITOR
        Undo.undoRedoPerformed += OnUndoRedo;
#endif
        if (autoUpdate) RequestGenerate();
    }

    private void OnDisable()
    {
        if (subscribedPath != null) subscribedPath.onPathChanged -= OnPathChanged;
        subscribedPath = null;
#if UNITY_EDITOR
        EditorApplication.delayCall -= DelayedGenerate;
        Undo.undoRedoPerformed -= OnUndoRedo;
#endif
    }

    private void OnValidate()
    {
        // Do not resolve components or touch meshes during serialization callbacks.
#if UNITY_EDITOR
        EditorApplication.delayCall -= ValidateAndRefresh;
        EditorApplication.delayCall += ValidateAndRefresh;
#endif
    }
#if UNITY_EDITOR
    private void ValidateAndRefresh()
    {
        if (this == null || !isActiveAndEnabled) return;
        ResolveComponents();
        if (autoUpdate) RequestGenerate();
    }
#endif

    private void Update()
    {
        if (subscribedPath != pathManager || lastMatrix != transform.localToWorldMatrix)
        {
            ResolveComponents();
            lastMatrix = transform.localToWorldMatrix;
            if (autoUpdate) RequestGenerate();
        }
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= ValidateAndRefresh;
#endif
        ReleaseMesh();
    }

    private void ReleaseMesh()
    {
        if (mesh == null || meshOwnerId != GetInstanceID()) return;
#if UNITY_EDITOR
        if (EditorUtility.IsPersistent(mesh)) return;
#endif
        // Only the mesh allocated by this instance is owned; never destroy a supplied asset.
        if (meshFilter != null && meshFilter.sharedMesh == mesh) meshFilter.sharedMesh = null;
        MeshCollider collider = GetComponent<MeshCollider>();
        if (collider != null && collider.sharedMesh == mesh) collider.sharedMesh = null;
        if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        mesh = null;
    }

    private void OnDrawGizmos()
    {
        if (!showDebugVisuals) return;
        DrawCurve(leftCurvePoints, leftCurveColor);
        DrawCurve(rightCurvePoints, rightCurveColor);
    }

    private void DrawCurve(List<Vector3> points, Color color)
    {
        Gizmos.color = color;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 world = transform.TransformPoint(points[i]);
            Gizmos.DrawWireSphere(world, debugPointSize * 0.5f);
            if (i > 0) Gizmos.DrawLine(transform.TransformPoint(points[i - 1]), world);
        }
    }
}
