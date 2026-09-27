using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class RoadPathManager : MonoBehaviour
{
    public event Action onPathChanged;

    [Header("Path Settings")]
    public List<Transform> controlPoints = new List<Transform>();
    [Min(2), Tooltip("全局模式为总点数；每段模式为每段细分数")]
    public int samplingPoints = 20;
    public bool autoUpdate = true;

    [Header("Debug Visualization")]
    public bool showGizmos = true;
    public float gizmoSize = 0.1f;
    public float directionLength = 1f;
    public float controlPointSize = 0.5f;
    public Color pathColor = Color.green;
    public Color tangentColor = Color.blue;
    public Color upDirectionColor = Color.red;
    public Color controlPointColor = Color.yellow;

    public enum SamplingMode { UniformPerSegment, UniformGlobal }

    [Header("Path Sampling Settings")]
    public SamplingMode pathSamplingMode = SamplingMode.UniformGlobal;
    [Range(0.1f, 5f)] public float densityFactor = 1f;

    [Serializable]
    public struct PathPoint
    {
        public Vector3 position;
        public Vector3 tangent;
        public Vector3 up;
        public float distance;
        public Vector3 right => Vector3.Cross(up, tangent).normalized;

        public PathPoint(Vector3 pos, Vector3 tan, Vector3 upDir)
        {
            position = pos;
            tangent = tan;
            up = upDir;
            distance = 0f;
        }
    }

    public struct ControlPointData
    {
        public Vector3 position, tangent, up, bitangent;
    }

    private const float Epsilon = 0.00001f;
    private PathPoint[] currentPathPoints = Array.Empty<PathPoint>();
    private Transform[] lastReferences;
    private Vector3[] lastPositions;
    private Quaternion[] lastRotations;
    private readonly List<Vector3> positions = new List<Vector3>();
    private readonly List<Vector3> ups = new List<Vector3>();
    private bool updating;

    public PathPoint[] GetPathPoints() => currentPathPoints;

    // Read current transforms without rotating or otherwise modifying the author's controls.
    public ControlPointData[] GetControlPointsData()
    {
        if (!ReadControls()) return Array.Empty<ControlPointData>();
        var result = new ControlPointData[positions.Count];
        Vector3 previousUp = ups[0];
        for (int i = 0; i < result.Length; i++)
        {
            PathPoint point = Evaluate(Mathf.Min(i, positions.Count - 2),
                i == positions.Count - 1 ? 1f : 0f, previousUp);
            previousUp = point.up;
            result[i] = new ControlPointData
            {
                position = point.position, tangent = point.tangent,
                up = point.up, bitangent = point.right
            };
        }
        return result;
    }

    public void UpdatePath()
    {
        if (updating) return;
        updating = true;
        try
        {
            currentPathPoints = SamplePath(pathSamplingMode, samplingPoints, densityFactor);
            CaptureControls();
            // Empty/invalid paths must also notify consumers so old geometry is removed.
            onPathChanged?.Invoke();
        }
        finally { updating = false; }
    }

    /// <summary>Shared world-space sampler used by both preview and road geometry.</summary>
    public PathPoint[] SamplePath(SamplingMode mode, int pointCount, float density)
    {
        if (!ReadControls()) return Array.Empty<PathPoint>();
        pointCount = Mathf.Clamp(pointCount, 2, 100000);
        density = Mathf.Clamp(density, 0.1f, 5f);
        var samples = new List<PathPoint>();
        Vector3 previousUp = ups[0];

        for (int segment = 0; segment < positions.Count - 1; segment++)
        {
            int steps = mode == SamplingMode.UniformPerSegment ? pointCount :
                Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(positions[segment],
                    positions[segment + 1]) * density * 10f), 16, 4096);
            for (int j = segment == 0 ? 0 : 1; j <= steps; j++)
            {
                PathPoint sample = Evaluate(segment, j / (float)steps, previousUp);
                previousUp = sample.up;
                if (samples.Count > 0)
                {
                    PathPoint previous = samples[samples.Count - 1];
                    float length = Vector3.Distance(previous.position, sample.position);
                    if (length < Epsilon) continue;
                    sample.distance = previous.distance + length;
                }
                samples.Add(sample);
            }
        }

        if (samples.Count < 2) return Array.Empty<PathPoint>();
        if (mode == SamplingMode.UniformPerSegment) return samples.ToArray();

        var uniform = new PathPoint[pointCount];
        uniform[0] = samples[0];
        uniform[pointCount - 1] = samples[samples.Count - 1];
        float totalLength = uniform[pointCount - 1].distance;
        int index = 0;
        for (int i = 1; i < pointCount - 1; i++)
        {
            float distance = totalLength * i / (pointCount - 1);
            while (index < samples.Count - 2 && samples[index + 1].distance < distance)
                index++;
            PathPoint a = samples[index];
            PathPoint b = samples[index + 1];
            float t = (distance - a.distance) / (b.distance - a.distance);
            Vector3 tangent = Vector3.Lerp(a.tangent, b.tangent, t);
            if (tangent.sqrMagnitude < Epsilon * Epsilon) tangent = b.position - a.position;
            tangent.Normalize();
            uniform[i] = new PathPoint(Vector3.Lerp(a.position, b.position, t), tangent,
                FrameUp(tangent, Vector3.Slerp(a.up, b.up, t), uniform[i - 1].up));
            uniform[i].distance = distance;
        }
        return uniform;
    }

    private bool ReadControls()
    {
        positions.Clear();
        ups.Clear();
        if (controlPoints == null) return false;
        foreach (Transform point in controlPoints)
        {
            if (point == null) { positions.Clear(); ups.Clear(); return false; }
            Vector3 position = point.position;
            if (!Finite(position)) { positions.Clear(); ups.Clear(); return false; }
            // Coincident adjacent controls have no usable segment or tangent.
            if (positions.Count > 0 &&
                (position - positions[positions.Count - 1]).sqrMagnitude < Epsilon * Epsilon)
                continue;
            positions.Add(position);
            ups.Add(point.up);
        }
        return positions.Count >= 2;
    }

    private static bool Finite(Vector3 value) =>
        !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
        !float.IsNaN(value.z) && !float.IsInfinity(value.z);

    private Vector3 Position(int index)
    {
        if (index < 0) return positions[0] * 2f - positions[1];
        if (index >= positions.Count)
            return positions[positions.Count - 1] * 2f - positions[positions.Count - 2];
        return positions[index];
    }

    private PathPoint Evaluate(int segment, float t, Vector3 previousUp)
    {
        Vector3 a = Position(segment - 1), b = Position(segment);
        Vector3 c = Position(segment + 1), d = Position(segment + 2);
        float t2 = t * t, t3 = t2 * t;
        Vector3 position = 0.5f * ((2f * b) + (-a + c) * t +
            (2f * a - 5f * b + 4f * c - d) * t2 + (-a + 3f * b - 3f * c + d) * t3);
        Vector3 tangent = 0.5f * ((-a + c) +
            (2f * a - 5f * b + 4f * c - d) * (2f * t) +
            (-a + 3f * b - 3f * c + d) * (3f * t2));
        if (tangent.sqrMagnitude < Epsilon * Epsilon) tangent = c - b;
        tangent.Normalize();
        // Preserve exact endpoints and interpolate the authored banking reference.
        if (t == 0f) position = b;
        if (t == 1f) position = c;
        Vector3 up = FrameUp(tangent, Vector3.Slerp(ups[segment], ups[segment + 1], t), previousUp);
        return new PathPoint(position, tangent, up);
    }

    private static Vector3 FrameUp(Vector3 tangent, Vector3 preferred, Vector3 previous)
    {
        Vector3 up = Vector3.ProjectOnPlane(preferred, tangent);
        if (up.sqrMagnitude < Epsilon * Epsilon) up = Vector3.ProjectOnPlane(previous, tangent);
        if (up.sqrMagnitude < Epsilon * Epsilon)
            up = Vector3.ProjectOnPlane(Mathf.Abs(tangent.y) < 0.9f ? Vector3.up : Vector3.forward, tangent);
        return up.normalized;
    }

    public void CreateControlPoint()
    {
        if (controlPoints == null) controlPoints = new List<Transform>();
        var pointObject = new GameObject($"ControlPoint_{controlPoints.Count}");
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RegisterCreatedObjectUndo(pointObject, "添加道路控制点");
            UnityEditor.Undo.RecordObject(this, "添加道路控制点");
        }
#endif
        Transform point = pointObject.transform;
        point.SetParent(transform, false);
        Transform last = controlPoints.Count > 0 ? controlPoints[controlPoints.Count - 1] : null;
        ControlPointData[] data = GetControlPointsData();
        Vector3 forward = data.Length > 0 ? data[data.Length - 1].tangent :
            (last != null ? last.forward : transform.forward);
        point.position = last != null ? last.position + forward * 2f : transform.position;
        point.rotation = last != null ? last.rotation : transform.rotation;
        controlPoints.Add(point);
        if (autoUpdate) UpdatePath();
    }

    private bool ControlsChanged()
    {
        int count = controlPoints == null ? 0 : controlPoints.Count;
        if (lastReferences == null || lastReferences.Length != count) return true;
        for (int i = 0; i < count; i++)
        {
            Transform point = controlPoints[i];
            if (lastReferences[i] != point) return true;
            if (point != null && (lastPositions[i] != point.position || lastRotations[i] != point.rotation))
                return true;
        }
        return false;
    }

    private void CaptureControls()
    {
        int count = controlPoints == null ? 0 : controlPoints.Count;
        lastReferences = new Transform[count];
        lastPositions = new Vector3[count];
        lastRotations = new Quaternion[count];
        for (int i = 0; i < count; i++)
        {
            Transform point = controlPoints[i];
            lastReferences[i] = point;
            if (point == null) continue;
            lastPositions[i] = point.position;
            lastRotations[i] = point.rotation;
        }
    }

    private void Update() { if (autoUpdate && ControlsChanged()) UpdatePath(); }
    private void OnEnable()
    {
#if UNITY_EDITOR
        UnityEditor.Undo.undoRedoPerformed += OnUndoRedo;
#endif
        RequestUpdate();
    }
    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= DelayedUpdate;
        UnityEditor.Undo.undoRedoPerformed -= OnUndoRedo;
#endif
    }
    private void OnValidate() { RequestUpdate(); }
    private void RequestUpdate()
    {
#if UNITY_EDITOR
        // Coalesce inspector changes; never create geometry in OnValidate.
        UnityEditor.EditorApplication.delayCall -= DelayedUpdate;
        UnityEditor.EditorApplication.delayCall += DelayedUpdate;
#else
        if (autoUpdate && isActiveAndEnabled) UpdatePath();
#endif
    }
#if UNITY_EDITOR
    private void DelayedUpdate()
    {
        if (this != null && isActiveAndEnabled && autoUpdate) UpdatePath();
    }
    private void OnUndoRedo()
    {
        if (this != null && isActiveAndEnabled) UpdatePath();
    }
#endif

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        Gizmos.color = controlPointColor;
        if (controlPoints != null)
            foreach (Transform point in controlPoints)
                if (point != null) Gizmos.DrawWireSphere(point.position, controlPointSize);
        if (currentPathPoints == null) return;
        for (int i = 0; i < currentPathPoints.Length; i++)
        {
            PathPoint point = currentPathPoints[i];
            Gizmos.color = pathColor;
            if (i > 0) Gizmos.DrawLine(currentPathPoints[i - 1].position, point.position);
            Gizmos.DrawWireSphere(point.position, gizmoSize * 0.5f);
            Gizmos.color = tangentColor;
            Gizmos.DrawRay(point.position, point.tangent * directionLength * 0.5f);
            Gizmos.color = upDirectionColor;
            Gizmos.DrawRay(point.position, point.up * directionLength * 0.5f);
        }
    }
}
