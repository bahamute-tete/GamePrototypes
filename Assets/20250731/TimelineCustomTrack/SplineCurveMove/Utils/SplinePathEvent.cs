using UnityEngine;

/// <summary>
/// 路径事件：在曲线弧长比例 ∈ [0, 1] 处触发的命名事件。
/// 序列化在 SplineCurveMoveClip 上，由 SplineEventReceiver 接收。
/// </summary>
[System.Serializable]
public class SplinePathEvent
{
    [Range(0f, 1f)]
    [Tooltip("事件触发位置：曲线弧长比例（0 = 起点，1 = 终点）。")]
    public float arcLengthRatio = 0.5f;

    [Tooltip("事件名称。SplineEventReceiver 用此匹配回调。")]
    public string eventName = "Event";

    [Tooltip("自定义字符串参数（可选），传给回调。")]
    public string parameter = "";

    [Tooltip("仅在 Editor Gizmo 显示用的颜色。")]
    public Color gizmoColor = new Color(1f, 0.5f, 0f);
}
