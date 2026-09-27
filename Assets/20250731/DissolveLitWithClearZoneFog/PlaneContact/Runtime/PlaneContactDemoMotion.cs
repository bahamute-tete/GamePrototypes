using UnityEngine;

/// <summary>Demo only. The contact component itself never moves the character.</summary>
public sealed class PlaneContactDemoMotion : MonoBehaviour
{
    public Vector3 center;
    public Vector3 direction = Vector3.forward;
    public float distance = 1.2f;
    public float period = 6;
    void Update() { transform.position = center + direction.normalized * (Mathf.Sin(Time.time * Mathf.PI * 2 / Mathf.Max(.1f,period)) * distance); }
}
