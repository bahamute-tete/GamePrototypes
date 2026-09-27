using System;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public class FogBehaviour : PlayableBehaviour
{
    [Tooltip("雾的目标颜色")]
    public Color fogColor = Color.white;
    [Min(0f), Tooltip("线性雾开始出现的距离（米）")]
    public float fogStartDistance = 0f;
    [Min(0.01f), Tooltip("线性雾完全遮蔽的距离（米），必须大于开始距离")]
    public float fogEndDistance = 300f;
    [Min(0f), Tooltip("指数雾密度；数值越大，雾越浓")]
    public float fogDensity = 0.01f;

    public void Validate()
    {
        fogStartDistance = Mathf.Max(0f, fogStartDistance);
        fogEndDistance = Mathf.Max(fogStartDistance + 0.01f, fogEndDistance);
        fogDensity = Mathf.Max(0f, fogDensity);
    }

    public void ReadScene()
    {
        fogColor = RenderSettings.fogColor;
        fogStartDistance = RenderSettings.fogStartDistance;
        fogEndDistance = RenderSettings.fogEndDistance;
        fogDensity = RenderSettings.fogDensity;
        Validate();
    }
}
