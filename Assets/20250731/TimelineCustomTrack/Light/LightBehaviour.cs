// LightBehaviour.cs
using UnityEngine;
using UnityEngine.Playables;


public class LightBehaviour : PlayableBehaviour
{
    // Light 属性
    public Color color = Color.white;
    public float intensity = 1f;
    public float range = 10f;
    public float spotAngle = 30f;

    // Transform 属性 (local space)
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;

    // 参与混合的开关
    public bool affectRange;
    public bool affectSpotAngle;
    public bool affectPosition;
    public bool affectRotation;

    public AnimationCurve curve;
}

