using UnityEditor;
using UnityEngine;

/// <summary>
/// 共用绘制工具：在 CurveField / 分析栏 下方画秒数刻度时间轴；在任意 Rect 上画 Timeline playhead 红线。
///
/// 设计原则：AnimationCurve 的关键帧时间始终是【归一化的 [0,1]】，
/// 但 UI 显示和用户输入【都按秒】。这一层完成"显示坐标系（秒）"↔"数据坐标系（归一化）"的翻译。
/// </summary>
public static class TimedCurveDrawer
{
    /// <summary>
    /// 在 rect 上画一条 Timeline playhead 竖线（仅当 playhead 在 Clip 范围内）。
    /// 调用方负责保证 rect 的横轴坐标系与曲线的 [0,1] 归一化时间轴一致。
    /// </summary>
    public static void DrawPlayhead(Rect rect, TimelineClipContext.Info info, Color color)
    {
        if (!info.playheadInClip) return;
        float x = rect.x + info.normalizedPlayhead * rect.width;
        EditorGUI.DrawRect(new Rect(x - 0.5f, rect.y, 1.5f, rect.height), color);

        // 小三角顶部标记，让 playhead 在曲线编辑器复杂背景上更显眼
        var tri = new Vector3[]
        {
            new Vector3(x - 4f, rect.y),
            new Vector3(x + 4f, rect.y),
            new Vector3(x,      rect.y + 5f),
        };
        Color prev = Handles.color;
        Handles.color = color;
        Handles.DrawAAConvexPolygon(tri);
        Handles.color = prev;
    }

    /// <summary>
    /// 在 rect 上画秒数刻度标尺。step 由 ChooseNiceStep 自动选择。
    /// </summary>
    public static void DrawTimeAxis(Rect rect, double durationSec)
    {
        EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f));

        if (durationSec <= 0) return;

        double step = ChooseNiceStep(durationSec, 8);
        var labelStyle = new GUIStyle(EditorStyles.miniLabel);
        labelStyle.normal.textColor = new Color(0.78f, 0.78f, 0.78f);
        labelStyle.alignment = TextAnchor.UpperCenter;

        Color tickColor = new Color(0.6f, 0.6f, 0.6f);

        for (double t = 0; t <= durationSec + 1e-6; t += step)
        {
            float x = rect.x + (float)(t / durationSec) * rect.width;
            EditorGUI.DrawRect(new Rect(x, rect.y, 1f, 5f), tickColor);
            Rect lr = new Rect(x - 30f, rect.y + 5f, 60f, rect.height - 5f);
            GUI.Label(lr, FormatTime(t), labelStyle);
        }

        // 末尾刻度标注（如果 ChooseNiceStep 落不到末尾）
        double lastTick = System.Math.Floor(durationSec / step) * step;
        if (durationSec - lastTick > step * 0.15)
        {
            float x = rect.xMax - 1f;
            EditorGUI.DrawRect(new Rect(x, rect.y, 1f, 5f), tickColor);
            Rect lr = new Rect(x - 50f, rect.y + 5f, 50f, rect.height - 5f);
            var endStyle = new GUIStyle(labelStyle);
            endStyle.alignment = TextAnchor.UpperRight;
            GUI.Label(lr, FormatTime(durationSec), endStyle);
        }

        // 底边线
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0f, 0f, 0f, 0.5f));
    }

    /// <summary>
    /// 选"漂亮"的刻度步长，使总刻度数 ≈ targetTickCount。
    /// 用 {1, 2, 5} × 10ⁿ 系列，避免刻度数字像 0.317 这种丑陋值。
    /// 例：range=1200s, target=8 → rough=150 → 取 200。
    /// 例：range=12s,   target=8 → rough=1.5 → 取 2。
    /// </summary>
    public static double ChooseNiceStep(double range, int targetTickCount)
    {
        if (range <= 0 || targetTickCount <= 0) return 1.0;
        double rough = range / targetTickCount;
        double mag = System.Math.Pow(10, System.Math.Floor(System.Math.Log10(rough)));
        double normalized = rough / mag;
        double nice;
        if      (normalized < 1.5) nice = 1;
        else if (normalized < 3.5) nice = 2;
        else if (normalized < 7.5) nice = 5;
        else                       nice = 10;
        return nice * mag;
    }

    /// <summary>
    /// 时间格式化：
    /// - &lt;1s    → "240ms"
    /// - &lt;60s   → "12.50s"
    /// - &lt;3600s → "2:30"
    /// - 否则     → "1:02:30"
    /// </summary>
    public static string FormatTime(double t)
    {
        if (t < 0) t = 0;
        if (t < 1)  return $"{t * 1000:F0}ms";
        if (t < 60) return $"{t:F2}s";
        int totalSec = (int)System.Math.Round(t);
        int h = totalSec / 3600;
        int m = (totalSec % 3600) / 60;
        int s = totalSec % 60;
        if (h > 0) return $"{h}:{m:D2}:{s:D2}";
        return $"{m}:{s:D2}";
    }
}
