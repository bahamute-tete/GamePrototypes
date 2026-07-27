// ============================================================================
//  RiverPathEditor.cs    (EDITOR ONLY — place in any "Editor" folder)
//
//  - RiverPathEditor:        Custom inspector. Generate button switches its
//                            label / behavior based on RiverPath.mode.
//
//  - RiverPathSceneDrawer:   Global SceneView callback ([InitializeOnLoad]).
//                            Draws helpers whenever the RiverPath GameObject
//                            OR any of its control-point Transforms is
//                            selected. Branches between river preview
//                            (center + 2 banks) and tunnel preview
//                            (center + ring outlines at intervals).
//
//  - RiverMeshGenerator:     Mesh builders for both modes.
//                            * Generate         — river ribbon
//                            * GenerateTunnel   — partial/full cylinder
//                            UV convention is shared (V along axial length,
//                            U across the cross-section).
// ============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ----------------------------------------------------------------------------
//  1. Inspector
// ----------------------------------------------------------------------------
[CustomEditor(typeof(RiverPath))]
public class RiverPathEditor : Editor
{
    public override void OnInspectorGUI()
    {
        RiverPath path = (RiverPath)target;

        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Usage", EditorStyles.boldLabel);

        if (path.mode == RiverPathMode.River)
        {
            EditorGUILayout.HelpBox(
                "River mode: 'width' and 'mitreLimit' control geometry. " +
                "Tunnel settings are ignored.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Tunnel mode: 'tunnelRadius', 'tunnelArc' (0..1 = 0..360°), " +
                "'tunnelOffset' (0..1 = 0..360° rotation of starting angle), " +
                "and 'tunnelSegments' control geometry. River settings are ignored.\n\n" +
                "No caps are produced (open at both ends).",
                MessageType.Info);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);

        int validCount = RiverPathSceneDrawer.CountValid(path);
        using (new EditorGUI.DisabledScope(validCount < 2))
        {
            string label = path.mode == RiverPathMode.River
                ? "Generate River Mesh"
                : "Generate Tunnel Mesh";

            if (GUILayout.Button(label, GUILayout.Height(32)))
            {
                if (path.mode == RiverPathMode.River) GenerateRiver(path);
                else                                   GenerateTunnel(path);
            }
        }
        if (validCount < 2)
        {
            EditorGUILayout.HelpBox(
                $"Need at least 2 non-null Transforms in the list (currently {validCount}).",
                MessageType.Warning);
        }

        if (path.generatedMesh != null)
        {
            EditorGUILayout.LabelField(
                $"Current mesh: {path.generatedMesh.vertexCount} verts, " +
                $"{path.generatedMesh.triangles.Length / 3} tris",
                EditorStyles.miniLabel);

            if (GUILayout.Button("Save Mesh As Asset"))
            {
                SaveMeshAsAsset(path);
            }
        }
    }

    private static void GenerateRiver(RiverPath path)
    {
        Mesh m = RiverMeshGenerator.Generate(path);
        AssignGenerated(path, m);
    }

    private static void GenerateTunnel(RiverPath path)
    {
        Mesh m = RiverMeshGenerator.GenerateTunnel(path);
        AssignGenerated(path, m);
    }

    private static void AssignGenerated(RiverPath path, Mesh mesh)
    {
        if (mesh == null) { Debug.LogError("[RiverPath] Mesh generation failed."); return; }

        if (path.generatedMesh != null && !AssetDatabase.Contains(path.generatedMesh))
            Object.DestroyImmediate(path.generatedMesh);

        path.generatedMesh = mesh;
        path.GetComponent<MeshFilter>().sharedMesh = mesh;

        EditorUtility.SetDirty(path);
        EditorUtility.SetDirty(path.GetComponent<MeshFilter>());
    }

    private static void SaveMeshAsAsset(RiverPath path)
    {
        if (path.generatedMesh == null)
        {
            Debug.LogWarning("[RiverPath] No mesh to save — Generate first.");
            return;
        }

        string suffix = path.mode == RiverPathMode.River ? "_RiverMesh" : "_TunnelMesh";
        string filePath = EditorUtility.SaveFilePanelInProject(
            "Save Mesh",
            $"{path.gameObject.name}{suffix}",
            "asset",
            "Save the mesh as a project asset.");

        if (string.IsNullOrEmpty(filePath)) return;

        Mesh copy = Object.Instantiate(path.generatedMesh);
        copy.name = System.IO.Path.GetFileNameWithoutExtension(filePath);

        AssetDatabase.CreateAsset(copy, filePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        path.generatedMesh = copy;
        path.GetComponent<MeshFilter>().sharedMesh = copy;
    }
}

// ----------------------------------------------------------------------------
//  2. Global SceneView drawer — visible whenever a RiverPath or any of its
//     control-point Transforms is selected.
// ----------------------------------------------------------------------------
[InitializeOnLoad]
public static class RiverPathSceneDrawer
{
    private static readonly HashSet<RiverPath> _toDraw = new HashSet<RiverPath>();

    static RiverPathSceneDrawer()
    {
        SceneView.duringSceneGui -= OnSceneGui;
        SceneView.duringSceneGui += OnSceneGui;
    }

    private static void OnSceneGui(SceneView sv)
    {
        _toDraw.Clear();

        Transform[] selection = Selection.transforms;
        if (selection == null || selection.Length == 0) return;

        foreach (var tr in selection)
        {
            if (tr == null) continue;
            var p = tr.GetComponent<RiverPath>();
            if (p != null) _toDraw.Add(p);
        }

#if UNITY_2022_2_OR_NEWER
        RiverPath[] allPaths = Object.FindObjectsByType<RiverPath>(FindObjectsSortMode.None);
#else
        RiverPath[] allPaths = Object.FindObjectsOfType<RiverPath>();
#endif

        foreach (var path in allPaths)
        {
            if (path == null) continue;
            if (_toDraw.Contains(path)) continue;

            for (int i = 0; i < path.controlPoints.Count; i++)
            {
                var cp = path.controlPoints[i];
                if (cp == null) continue;
                if (ContainsTransform(selection, cp))
                {
                    _toDraw.Add(path);
                    break;
                }
            }
        }

        foreach (var path in _toDraw)
        {
            DrawHelpers(path);
        }
    }

    private static bool ContainsTransform(Transform[] arr, Transform t)
    {
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] == t) return true;
        return false;
    }

    private static void DrawHelpers(RiverPath path)
    {
        // P0/P1/... labels
        for (int i = 0; i < path.controlPoints.Count; i++)
        {
            var cp = path.controlPoints[i];
            if (cp == null) continue;
            Handles.color = Color.white;
            Handles.Label(cp.position + Vector3.up * 0.6f, $"P{i}");
        }

        if (CountValid(path) < 2) return;

        if (path.mode == RiverPathMode.River) DrawRiverPreview(path);
        else                                   DrawTunnelPreview(path);
    }

    private static void DrawRiverPreview(RiverPath path)
    {
        RiverMeshGenerator.Sample(path,
            out List<Vector3> center,
            out List<Vector3> left,
            out List<Vector3> right,
            worldSpace: true);

        if (center.Count < 2) return;

        Handles.color = new Color(0.2f, 0.8f, 1f, 1f);
        Handles.DrawAAPolyLine(3f, center.ToArray());

        Handles.color = new Color(0.4f, 0.7f, 1f, 0.7f);
        Handles.DrawAAPolyLine(1.5f, left.ToArray());
        Handles.DrawAAPolyLine(1.5f, right.ToArray());

        DrawLengthLabel(center);
    }

    private static void DrawTunnelPreview(RiverPath path)
    {
        List<Vector3> centerWorld = RiverMeshGenerator.SampleCenter(path, worldSpace: true);
        if (centerWorld.Count < 2) return;

        // axial center line
        Handles.color = new Color(0.2f, 0.8f, 1f, 1f);
        Handles.DrawAAPolyLine(3f, centerWorld.ToArray());

        // rings: first, last, and 6 in between (8 total max)
        int n = centerWorld.Count;
        int ringCount = Mathf.Min(8, n);
        for (int k = 0; k < ringCount; k++)
        {
            int i = Mathf.RoundToInt((float)k * (n - 1) / Mathf.Max(1, ringCount - 1));
            float alpha = (k == 0 || k == ringCount - 1) ? 0.9f : 0.5f;
            DrawTunnelRing(centerWorld, i, path, alpha);
        }

        DrawLengthLabel(centerWorld, extraLine:
            $"radius {path.tunnelRadius:F2}m  •  arc {path.tunnelArc * 360f:F0}°  •  offset {path.tunnelOffset * 360f:F0}°");
    }

    private static void DrawTunnelRing(List<Vector3> centerWorld, int i, RiverPath path, float alpha)
    {
        Vector3 forward;
        if (i == 0)                          forward = centerWorld[1]            - centerWorld[0];
        else if (i == centerWorld.Count - 1) forward = centerWorld[i]            - centerWorld[i - 1];
        else                                 forward = centerWorld[i + 1]        - centerWorld[i - 1];
        forward.Normalize();

        Vector3 right, up;
        BuildFrame(forward, out right, out up);

        float startAngle = path.tunnelOffset * Mathf.PI * 2f;
        float endAngle   = (path.tunnelOffset + path.tunnelArc) * Mathf.PI * 2f;
        int segs = Mathf.Max(2, path.tunnelSegments);

        Vector3[] pts = new Vector3[segs + 1];
        for (int j = 0; j <= segs; j++)
        {
            float t = (float)j / segs;
            float a = startAngle + t * (endAngle - startAngle);
            Vector3 radial = right * Mathf.Cos(a) + up * Mathf.Sin(a);
            pts[j] = centerWorld[i] + radial * path.tunnelRadius;
        }

        Handles.color = new Color(0.4f, 0.85f, 1f, alpha);
        Handles.DrawAAPolyLine(1.5f, pts);
    }

    private static void DrawLengthLabel(List<Vector3> center, string extraLine = null)
    {
        Vector3 mid = center[center.Count / 2];
        float total = 0;
        for (int i = 1; i < center.Count; i++) total += Vector3.Distance(center[i - 1], center[i]);
        Handles.color = Color.white;
        string label = $"~{total:F1} m";
        if (!string.IsNullOrEmpty(extraLine)) label += "\n" + extraLine;
        Handles.Label(mid + Vector3.up * 1.0f, label);
    }

    // Right/Up frame from a forward direction. World up is the reference so
    // that horizontal-ish tunnels keep "up" at the top.
    public static void BuildFrame(Vector3 forward, out Vector3 right, out Vector3 up)
    {
        if (Mathf.Abs(forward.y) > 0.99f)
        {
            // Forward nearly vertical: pick a horizontal seed for right.
            right = Vector3.Cross(forward, Vector3.right).normalized;
            if (right.sqrMagnitude < 1e-4f) right = Vector3.Cross(forward, Vector3.forward).normalized;
        }
        else
        {
            right = Vector3.Cross(Vector3.up, forward).normalized;
        }
        up = Vector3.Cross(forward, right).normalized;
    }

    public static int CountValid(RiverPath path)
    {
        int c = 0;
        foreach (var t in path.controlPoints) if (t != null) c++;
        return c;
    }
}

// ----------------------------------------------------------------------------
//  3. Mesh generators
// ----------------------------------------------------------------------------
public static class RiverMeshGenerator
{
    // =======================================================================
    //  River ribbon — same as before
    // =======================================================================
    public static Mesh Generate(RiverPath path)
    {
        Sample(path,
            out List<Vector3> center,
            out List<Vector3> left,
            out List<Vector3> right,
            worldSpace: false);

        int n = center.Count;
        if (n < 2) return null;

        float[] arc = new float[n];
        arc[0] = 0;
        for (int i = 1; i < n; i++)
            arc[i] = arc[i - 1] + Vector3.Distance(center[i - 1], center[i]);
        float totalArc = arc[n - 1];
        if (totalArc < 1e-4f) return null;

        Vector3[] vertices = new Vector3[n * 2];
        Vector2[] uvs      = new Vector2[n * 2];
        Vector3[] normals  = new Vector3[n * 2];
        Vector4[] tangents = new Vector4[n * 2];

        Transform tr = path.transform;
        Vector3 normalLocal = tr.InverseTransformDirection(Vector3.up).normalized;

        for (int i = 0; i < n; i++)
        {
            vertices[i * 2 + 0] = left[i];
            vertices[i * 2 + 1] = right[i];

            float v = arc[i] / totalArc;
            uvs[i * 2 + 0] = new Vector2(0f, v);
            uvs[i * 2 + 1] = new Vector2(1f, v);

            normals[i * 2 + 0] = normalLocal;
            normals[i * 2 + 1] = normalLocal;

            Vector3 tDir = (right[i] - left[i]).normalized;
            Vector4 tVal = new Vector4(tDir.x, tDir.y, tDir.z, -1f);
            tangents[i * 2 + 0] = tVal;
            tangents[i * 2 + 1] = tVal;
        }

        int[] tris = new int[(n - 1) * 6];
        for (int i = 0; i < n - 1; i++)
        {
            int l0 = i * 2 + 0;
            int r0 = i * 2 + 1;
            int l1 = (i + 1) * 2 + 0;
            int r1 = (i + 1) * 2 + 1;

            tris[i * 6 + 0] = l0;
            tris[i * 6 + 1] = l1;
            tris[i * 6 + 2] = r0;

            tris[i * 6 + 3] = l1;
            tris[i * 6 + 4] = r1;
            tris[i * 6 + 5] = r0;
        }

        Mesh mesh = new Mesh();
        mesh.name = $"{path.gameObject.name}_RiverMesh";
        if (vertices.Length > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices  = vertices;
        mesh.uv        = uvs;
        mesh.normals   = normals;
        mesh.tangents  = tangents;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        Debug.Log($"[RiverPath] River mesh: {n} samples, {tris.Length / 3} tris, arc {totalArc:F2}m");
        return mesh;
    }

    // =======================================================================
    //  Tunnel — partial/full cylinder, no caps
    // =======================================================================
    public static Mesh GenerateTunnel(RiverPath path)
    {
        if (path.tunnelArc < 1e-3f)
        {
            Debug.LogWarning("[RiverPath] tunnelArc is 0 — nothing to generate.");
            return null;
        }

        List<Vector3> centerWorld = SampleCenter(path, worldSpace: true);
        int n = centerWorld.Count;
        if (n < 2) return null;

        // Axial arc lengths for UV.v
        float[] axialArc = new float[n];
        axialArc[0] = 0;
        for (int i = 1; i < n; i++)
            axialArc[i] = axialArc[i - 1] + Vector3.Distance(centerWorld[i - 1], centerWorld[i]);
        float totalAxial = axialArc[n - 1];
        if (totalAxial < 1e-4f) return null;

        // Per-sample forward direction
        Vector3[] forwards = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 t;
            if (i == 0)                   t = centerWorld[1]     - centerWorld[0];
            else if (i == n - 1)          t = centerWorld[n - 1] - centerWorld[n - 2];
            else                          t = centerWorld[i + 1] - centerWorld[i - 1];
            forwards[i] = t.normalized;
        }

        // Tunnel cross-section parameters
        float startAngle = path.tunnelOffset * Mathf.PI * 2f;
        float endAngle   = (path.tunnelOffset + path.tunnelArc) * Mathf.PI * 2f;
        int circSeg      = Mathf.Max(2, path.tunnelSegments);
        int ringVerts    = circSeg + 1;              // open ring — endpoints distinct, U covers full [0,1]
        int totalVerts   = n * ringVerts;

        Vector3[] vertices = new Vector3[totalVerts];
        Vector2[] uvs      = new Vector2[totalVerts];
        Vector3[] normals  = new Vector3[totalVerts];
        Vector4[] tangents = new Vector4[totalVerts];

        Transform pathTr = path.transform;

        for (int i = 0; i < n; i++)
        {
            Vector3 forward = forwards[i];
            Vector3 right, up;
            RiverPathSceneDrawer.BuildFrame(forward, out right, out up);

            float v = axialArc[i] / totalAxial;

            for (int j = 0; j < ringVerts; j++)
            {
                float t  = (float)j / circSeg;
                float a  = startAngle + t * (endAngle - startAngle);
                float cs = Mathf.Cos(a);
                float sn = Mathf.Sin(a);

                Vector3 radialOut    = right * cs + up * sn;
                Vector3 ringTangent  = -right * sn + up * cs;       // +U direction (d/da of radialOut)

                Vector3 vertWorld    = centerWorld[i] + radialOut * path.tunnelRadius;
                Vector3 normalWorld  = -radialOut;                   // inward — viewed from inside the tube

                int idx = i * ringVerts + j;
                vertices[idx] = pathTr.InverseTransformPoint(vertWorld);
                uvs[idx]      = new Vector2(t, v);

                normals[idx]  = pathTr.InverseTransformDirection(normalWorld).normalized;

                Vector3 tLoc  = pathTr.InverseTransformDirection(ringTangent).normalized;
                tangents[idx] = new Vector4(tLoc.x, tLoc.y, tLoc.z, -1f);
            }
        }

        // Triangles. Winding chosen so the front face's normal points inward,
        // matching the per-vertex normal we wrote above.
        //   a = (i, j),   b = (i+1, j),   c = (i, j+1),   d = (i+1, j+1)
        int quads = (n - 1) * circSeg;
        int[] tris = new int[quads * 6];
        int k = 0;
        for (int i = 0; i < n - 1; i++)
        {
            for (int j = 0; j < circSeg; j++)
            {
                int a = i       * ringVerts + j;
                int b = (i + 1) * ringVerts + j;
                int c = i       * ringVerts + (j + 1);
                int d = (i + 1) * ringVerts + (j + 1);

                tris[k++] = a; tris[k++] = b; tris[k++] = c;
                tris[k++] = c; tris[k++] = b; tris[k++] = d;
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = $"{path.gameObject.name}_TunnelMesh";
        if (totalVerts > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices  = vertices;
        mesh.uv        = uvs;
        mesh.normals   = normals;
        mesh.tangents  = tangents;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        Debug.Log($"[RiverPath] Tunnel mesh: {n} rings × {ringVerts} verts = {totalVerts} verts, " +
                  $"{tris.Length / 3} tris, axial {totalAxial:F2}m, " +
                  $"radius {path.tunnelRadius:F2}m, arc {path.tunnelArc * 360f:F0}°.");
        return mesh;
    }

    // =======================================================================
    //  Sampling
    // =======================================================================

    // River-specific: center + 2 banks
    public static void Sample(RiverPath path,
                              out List<Vector3> center,
                              out List<Vector3> left,
                              out List<Vector3> right,
                              bool worldSpace)
    {
        center = SampleCenterWorld(path);
        left   = new List<Vector3>();
        right  = new List<Vector3>();

        if (center.Count < 2) return;

        ComputeEdges(center, path.width * 0.5f, path.mitreLimit, ref left, ref right);

        if (!worldSpace)
        {
            Transform tr = path.transform;
            for (int i = 0; i < center.Count; i++) center[i] = tr.InverseTransformPoint(center[i]);
            for (int i = 0; i < left.Count;   i++) left[i]   = tr.InverseTransformPoint(left[i]);
            for (int i = 0; i < right.Count;  i++) right[i]  = tr.InverseTransformPoint(right[i]);
        }
    }

    // Public single-output sample used by tunnel mesh + tunnel preview
    public static List<Vector3> SampleCenter(RiverPath path, bool worldSpace)
    {
        var center = SampleCenterWorld(path);
        if (!worldSpace)
        {
            Transform tr = path.transform;
            for (int i = 0; i < center.Count; i++) center[i] = tr.InverseTransformPoint(center[i]);
        }
        return center;
    }

    private static List<Vector3> SampleCenterWorld(RiverPath path)
    {
        var pts = new List<Vector3>();
        foreach (var t in path.controlPoints)
            if (t != null) pts.Add(t.position);

        var result = new List<Vector3>();
        if (pts.Count < 2) return result;

        // Dedup near-coincident neighbors
        for (int i = pts.Count - 1; i > 0; i--)
        {
            if (Vector3.Distance(pts[i], pts[i - 1]) < 1e-4f)
                pts.RemoveAt(i);
        }
        if (pts.Count < 2) return result;

        var dense = new List<Vector3>();
        var arc   = new List<float>();
        float a = 0;
        void Push(Vector3 p)
        {
            if (dense.Count > 0) a += Vector3.Distance(dense[dense.Count - 1], p);
            dense.Add(p);
            arc.Add(a);
        }

        if (path.smoothCurves && pts.Count >= 2)
        {
            Vector3 ghost0 = pts[0] + (pts[0] - pts[1]);
            Vector3 ghostN = pts[pts.Count - 1] + (pts[pts.Count - 1] - pts[pts.Count - 2]);

            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 p0 = (i == 0)                  ? ghost0 : pts[i - 1];
                Vector3 p1 = pts[i];
                Vector3 p2 = pts[i + 1];
                Vector3 p3 = (i == pts.Count - 2)      ? ghostN : pts[i + 2];

                const int sub = 32;
                for (int j = 0; j < sub; j++)
                {
                    float t = (float)j / sub;
                    Push(CatmullRom(p0, p1, p2, p3, t));
                }
            }
            Push(pts[pts.Count - 1]);
        }
        else
        {
            Push(pts[0]);
            for (int i = 1; i < pts.Count; i++)
            {
                const int sub = 16;
                for (int j = 1; j <= sub; j++)
                {
                    float t = (float)j / sub;
                    Push(Vector3.Lerp(pts[i - 1], pts[i], t));
                }
            }
        }

        float totalArc = a;
        if (totalArc < 1e-4f) { result.Add(pts[0]); return result; }

        // Adaptive uniform-arc resampling.
        // Spacing cap depends on mode — both cap at "half cross-section".
        float crossSectionHalf = (path.mode == RiverPathMode.River)
            ? path.width * 0.5f
            : path.tunnelRadius;
        float requestedSpacing = 1f / Mathf.Max(path.density, 0.01f);
        float spacing          = Mathf.Min(requestedSpacing, crossSectionHalf);
        int sampleCount        = Mathf.Max(2, Mathf.CeilToInt(totalArc / Mathf.Max(spacing, 1e-3f)) + 1);

        for (int i = 0; i < sampleCount; i++)
        {
            float targetArc = (float)i / (sampleCount - 1) * totalArc;
            result.Add(LerpAlongArc(dense, arc, targetArc));
        }

        return result;
    }

    // =======================================================================
    //  River edges (mitre joint)
    // =======================================================================
    private static void ComputeEdges(List<Vector3> center, float halfWidth, float mitreLimit,
                                      ref List<Vector3> left, ref List<Vector3> right)
    {
        left.Clear();
        right.Clear();
        int n = center.Count;

        int sharpBendCount = 0;

        for (int i = 0; i < n; i++)
        {
            Vector3 prevT, nextT;
            if (i == 0)                { prevT = nextT = center[1]      - center[0]; }
            else if (i == n - 1)       { prevT = nextT = center[n - 1]  - center[n - 2]; }
            else
            {
                prevT = center[i]     - center[i - 1];
                nextT = center[i + 1] - center[i];
            }

            prevT.y = 0; prevT.Normalize();
            nextT.y = 0; nextT.Normalize();

            Vector3 prevN = new Vector3(-prevT.z, 0, prevT.x);
            Vector3 nextN = new Vector3(-nextT.z, 0, nextT.x);

            Vector3 mitreN = prevN + nextN;
            if (mitreN.sqrMagnitude < 1e-6f) mitreN = prevN;
            else                              mitreN.Normalize();

            float dot = Vector3.Dot(mitreN, prevN);
            float mitreLen;
            if (dot < 0.05f)
            {
                sharpBendCount++;
                mitreLen = halfWidth * mitreLimit;
            }
            else
            {
                mitreLen = halfWidth / dot;
                float maxLen = halfWidth * mitreLimit;
                if (mitreLen > maxLen)
                {
                    if (mitreLen > maxLen * 1.5f) sharpBendCount++;
                    mitreLen = maxLen;
                }
            }

            left .Add(center[i] + mitreN * mitreLen);
            right.Add(center[i] - mitreN * mitreLen);
        }

        if (sharpBendCount > 0)
        {
            Debug.LogWarning(
                $"[RiverPath] {sharpBendCount} sharp bend(s) detected — mitre extension clamped. " +
                "Add more control point Transforms near the corner, or reduce river width.");
        }
    }

    // =======================================================================
    //  Math
    // =======================================================================
    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    private static Vector3 LerpAlongArc(List<Vector3> dense, List<float> arc, float targetArc)
    {
        if (targetArc <= arc[0]) return dense[0];
        int last = arc.Count - 1;
        if (targetArc >= arc[last]) return dense[last];

        int lo = 0, hi = last;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) >> 1;
            if (arc[mid] <= targetArc) lo = mid;
            else                       hi = mid;
        }

        float segLen = arc[hi] - arc[lo];
        if (segLen < 1e-6f) return dense[lo];
        float t = (targetArc - arc[lo]) / segLen;
        return Vector3.Lerp(dense[lo], dense[hi], t);
    }
}
