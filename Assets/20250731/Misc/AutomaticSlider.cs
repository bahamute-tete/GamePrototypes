using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class AutomaticSlider : MonoBehaviour
{
    // 持续时间 duration —— 原脚本漏了 [SerializeField]，Inspector 改不动，这里补上
    [SerializeField, Min(0.0001f)] float duration = 1f;

    [SerializeField] bool autoReverse = false, smoothStep = false;

    // 序列化 value，方便在 Inspector 里直接拖动预览 (scrub) / 查看当前进度
    [SerializeField, Range(0f, 1f)] float value;

    public bool Reversed { get; set; }

    public bool AutoReversed
    {
        get => autoReverse;
        set => autoReverse = value;
    }

    // smoothstep: 3t² - 2t³，两端缓入缓出 (ease-in-out)，浮动看起来更自然
    float SmoothValue => 3f * value * value - 2f * value * value * value;

    public float Duration { get => duration; set => duration = value; }

    [System.Serializable]
    public class OnValueChangedEvent : UnityEvent<float> { }

    [SerializeField] OnValueChangedEvent onValueChanged = default;

    // —— 编辑器驱动 editor-mode driving ————————————————————————————————
    // 根源 root cause：FixedUpdate / Update 在非运行时不会被稳定调用。
    // 编辑器下用 EditorApplication.update + 真实时间差 (real-time delta) 来推进。

#if UNITY_EDITOR
    double lastEditorTime;
#endif

    void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            lastEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= EditorTick; // 防止程序集重载后重复注册 double-subscribe
            EditorApplication.update += EditorTick;
        }
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
#endif
    }

    void Update()
    {
        // 运行时走正常 Update（按帧推进，比原来的 FixedUpdate 更平滑）
        if (Application.isPlaying)
        {
            Tick(Time.deltaTime);
        }
    }

#if UNITY_EDITOR
    void EditorTick()
    {
        if (Application.isPlaying) return;
        if (this == null) { EditorApplication.update -= EditorTick; return; }

        double now = EditorApplication.timeSinceStartup;
        float dt = (float)(now - lastEditorTime);
        lastEditorTime = now;

        // 编辑器空闲 / 失焦回来时 dt 可能很大，钳制一下避免一帧跳太远
        if (dt <= 0f) return;
        if (dt > 0.1f) dt = 0.1f;

        Tick(dt);
        SceneView.RepaintAll(); // 强制重绘 Scene 视图，动画才会连续
    }
#endif

    // 核心推进逻辑 core stepping —— 运行时 / 编辑器共用，逻辑与原脚本一致
    void Tick(float dt)
    {
        float delta = dt / Mathf.Max(duration, 0.0001f);
        if (Reversed)
        {
            value -= delta;
            if (value <= 0f)
            {
                if (autoReverse)
                {
                    value = Mathf.Min(1f, -value);
                    Reversed = false;
                }
                else
                {
                    value = 0f;
                    enabled = false;
                }
            }
        }
        else
        {
            value += delta;
            if (value >= 1f)
            {
                if (autoReverse)
                {
                    value = Mathf.Max(0f, 2f - value);
                    Reversed = true;
                }
                else
                {
                    value = 1f;
                    enabled = false;
                }
            }
        }
        onValueChanged.Invoke(smoothStep ? SmoothValue : value);
    }
}
