// ============================================================================
//  RiverPath.cs    (RUNTIME)
//
//  Holds the path control points plus generation settings for two modes:
//    River   — flat ribbon mesh with left/right banks
//    Tunnel  — partial-or-full cylinder (no caps) using the path as axis
//
//  Both modes write UVs in the same convention (V along axial length, U
//  across the cross-section) so the MagicRiverMobile shader works on either.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

public enum RiverPathMode
{
    River,
    Tunnel
}

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RiverPath : MonoBehaviour
{
    [Header("Mode")]
    public RiverPathMode mode = RiverPathMode.River;

    [Header("Path")]
    [Tooltip("Transforms used as control points, in order from start to end. " +
             "Null entries are skipped.")]
    public List<Transform> controlPoints = new List<Transform>();

    [Tooltip("Axial samples per meter of arc length. Auto-clamped so spacing never " +
             "exceeds half the cross-section size (prevents inner-edge fold).")]
    [Range(0.2f, 10f)]
    public float density = 1f;

    [Tooltip("Smooth Catmull-Rom curves through control points. Off = straight segments.")]
    public bool smoothCurves = true;

    [Header("River Settings  (Mode = River)")]
    [Tooltip("River width in world units (meters).")]
    public float width = 4f;

    [Tooltip("Max corner extension at sharp bends, as multiple of half-width.")]
    [Range(1f, 5f)]
    public float mitreLimit = 2f;

    [Header("Tunnel Settings  (Mode = Tunnel)")]
    [Tooltip("Tunnel radius in world units (meters).")]
    public float tunnelRadius = 3f;

    [Tooltip("Fraction of a full circle to span. " +
             "0 = nothing, 0.5 = half-pipe, 1 = full tube (no caps either way).")]
    [Range(0f, 1f)]
    public float tunnelArc = 1f;

    [Tooltip("Rotation of the starting angle, as a fraction of a full circle. " +
             "0 = start at 'right'; 0.25 = start at 'up'; 0.5 = start at 'left'; etc.")]
    [Range(0f, 1f)]
    public float tunnelOffset = 0f;

    [Tooltip("Segments around the circumference.")]
    [Range(3, 64)]
    public int tunnelSegments = 16;

    [Header("Generated  (don't assign manually)")]
    public Mesh generatedMesh;
}
