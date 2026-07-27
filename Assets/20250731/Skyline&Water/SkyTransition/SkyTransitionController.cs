using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class SkyTransitionController : MonoBehaviour
{
    [Header("Sky Blend")]
    [Range(0, 1)] public float blend = 0f;

    static readonly int ID_SkyBlend = Shader.PropertyToID("_SkyBlend");

    void OnEnable() { Push(); }
    void OnValidate() { if (isActiveAndEnabled) Push(); }
    void Update() { Push(); }   // 让 Timeline / 动画系统能驱动

    void Push()
    {
        Shader.SetGlobalFloat(ID_SkyBlend, blend);
    }
}
