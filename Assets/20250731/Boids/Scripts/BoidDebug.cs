using UnityEngine;

public class BoidDebug : MonoBehaviour
{
    public float radius = 5f;
    public Color radiusColor = new Color(0, 1, 0, 0.3f);

    private void OnDrawGizmos()
    {
        Gizmos.color = radiusColor;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
