using UnityEngine;

public class GizmoShow : MonoBehaviour
{
    public Color gizmoColor = Color.yellow;

    // Preserve the existing Box and Sphere serialized enum values.
    public enum GizmosShape { Box, Sphere, Plane }
    public GizmosShape shape = GizmosShape.Box;

    [Tooltip("显示局部 +Z 正方向箭头。箭头跟随位置和旋转，不受自身或父物体缩放影响。")]
    public bool showPositiveDirection = true;

    [Min(0f), Tooltip("箭头从物体原点到尖端的长度，单位为世界米。")]
    public float normalLength = 1.0f;

    [Min(0f), Tooltip("箭头头部的长度与宽度，单位为世界米。")]
    public float arrowHeadSize = 0.1f;

    private void OnDrawGizmos()
    {
        Color previousColor = Gizmos.color;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        try
        {
            Gizmos.color = gizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;

            switch (shape)
            {
                case GizmosShape.Box:
                    Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                    break;
                case GizmosShape.Sphere:
                    Gizmos.DrawWireSphere(Vector3.zero, 1f);
                    break;
                case GizmosShape.Plane:
                    // Unit plane in local XY; its positive normal is local +Z.
                    Vector3 a = new Vector3(-0.5f, -0.5f, 0f);
                    Vector3 b = new Vector3(0.5f, -0.5f, 0f);
                    Vector3 c = new Vector3(0.5f, 0.5f, 0f);
                    Vector3 d = new Vector3(-0.5f, 0.5f, 0f);
                    Gizmos.DrawLine(a, b);
                    Gizmos.DrawLine(b, c);
                    Gizmos.DrawLine(c, d);
                    Gizmos.DrawLine(d, a);
                    break;
            }

            if (!showPositiveDirection || normalLength <= 0f) return;

            // Rebuild the matrix from position and rotation only. This removes
            // both local and inherited scale, including nonuniform/negative scale.
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Vector3 tip = Vector3.forward * Mathf.Max(0f, normalLength);
            float headLength = Mathf.Min(Mathf.Max(0f, arrowHeadSize), normalLength);
            float halfWidth = headLength * 0.5f;
            Vector3 headBase = tip - Vector3.forward * headLength;
            Gizmos.DrawLine(Vector3.zero, tip);
            Gizmos.DrawLine(tip, headBase + Vector3.up * halfWidth);
            Gizmos.DrawLine(tip, headBase - Vector3.up * halfWidth);
            Gizmos.DrawLine(tip, headBase + Vector3.right * halfWidth);
            Gizmos.DrawLine(tip, headBase - Vector3.right * halfWidth);
        }
        finally
        {
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
