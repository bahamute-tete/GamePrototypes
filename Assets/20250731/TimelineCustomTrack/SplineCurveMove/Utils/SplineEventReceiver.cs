using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 挂在 SplineCurveMoveTrack 绑定的 Transform 所在 GameObject 上。
/// 接收路径事件并派发给配置的 UnityEvent 或代码订阅者。
/// </summary>
[DisallowMultipleComponent]
public class SplineEventReceiver : MonoBehaviour
{
    [System.Serializable]
    public class EventEntry
    {
        public string eventName;
        public UnityEvent onTriggered;
    }

    [Tooltip("事件回调列表。eventName 与 SplinePathEvent.eventName 匹配时触发对应 UnityEvent。" +
             "若需要在回调里访问 parameter，从 LastTriggeredEvent 字段读取。")]
    public List<EventEntry> events = new List<EventEntry>();

    /// <summary>
    /// 上一个被触发的事件数据（含 eventName / parameter / arcLengthRatio）。
    /// UnityEvent 回调里可访问此字段读取参数。
    /// </summary>
    public SplinePathEvent LastTriggeredEvent { get; private set; }

    /// <summary>
    /// 代码订阅版：所有事件都会调用此回调。
    /// </summary>
    public event Action<SplinePathEvent> OnAnyEvent;

    public void Trigger(SplinePathEvent ev)
    {
        if (ev == null) return;
        LastTriggeredEvent = ev;
        OnAnyEvent?.Invoke(ev);
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e != null && e.eventName == ev.eventName)
                e.onTriggered?.Invoke();
        }
    }
}
