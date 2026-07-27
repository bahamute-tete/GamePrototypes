using System;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public class FogBehaviour : PlayableBehaviour
{
    [Header("雾颜色")]
    public Color fogColor = Color.white;
    
    [Header("线性雾参数")]
    public float fogStartDistance = 0;
    public float fogEndDistance = 300;
    
    [Header("指数雾参数")]
    public float fogDensity = 0.01f;
}
