using UnityEditor;
using UnityEngine;

/// <summary>
/// 曲线分析绘制工具。在 EditorGUI.CurveField 的下方画一条窄"时间轴分析栏"，
/// 高亮显示曲线中的【水平段】（停顿/hold 段），并标注每个关键帧的位置。
///
/// 用途：
///   - SplineCurveMoveClip 的 displacementCurve：水平段 = 角色停顿
///   - LookOverrideClip 的 yawCurve / pitchCurve / rollCurve：水平段 = 朝向锁定
///
/// 判定规则：相邻两个关键帧的 value 差小于 holdEpsilon，视为水平段。
/// 这个判定不看 tangent ——只看端点值，因为只要端点值相同，无论 tangent 如何，
/// 段中间数值上 ≈ 端点值（除非 tangent 极端拉扯过冲，但那种情况你自己也能看出来）。
/// </summary>
public static class CurvePauseAnalysisDrawer
{
    public class Options
    {
        public Color holdColor    = new Color(1f, 0.4f, 0.2f, 0.45f);
        public Color keyColor     = new Color(1f, 1f, 1f, 0.85f);
        public Color gridColor    = new Color(1f, 1f, 1f, 0.08f);
        public Color bgColor      = new Color(0.18f, 0.18f, 0.18f, 1f);
        public Color borderColor  = new Color(0f, 0f, 0f, 0.5f);
        public float holdEpsilon  = 0.001f;
        public bool  showDuration = true;
        public bool  showKeyDots  = true;
    }

    private static readonly Options DefaultOptions = new Options();

    public static void Draw(Rect rect, AnimationCurve curve, Options opts = null)
    {
        if (opts == null) opts = DefaultOptions;

        // 背景
        EditorGUI.DrawRect(rect, opts.bgColor);

        // 网格 - 横向 10 等分
        for (int i = 1; i < 10; i++)
        {
            float x = rect.x + rect.width * i / 10f;
            EditorGUI.DrawRect(new Rect(x, rect.y, 1f, rect.height), opts.gridColor);
        }

        // 边框
        Color prevHandlesColor = Handles.color;
        Handles.color = opts.borderColor;
        Handles.DrawLine(new Vector3(rect.x,    rect.y),    new Vector3(rect.xMax, rect.y));
        Handles.DrawLine(new Vector3(rect.x,    rect.yMax), new Vector3(rect.xMax, rect.yMax));
        Handles.DrawLine(new Vector3(rect.x,    rect.y),    new Vector3(rect.x,    rect.yMax));
        Handles.DrawLine(new Vector3(rect.xMax, rect.y),    new Vector3(rect.xMax, rect.yMax));
        Handles.color = prevHandlesColor;

        if (curve == null) return;
        var keys = curve.keys;
        if (keys == null || keys.Length < 2) return;

        // 时间范围按曲线的实际范围取。对 Clip 内归一化曲线，理论上是 [0,1]，但用户可能拖出 [0,1] 之外，
        // 用实际范围保证可视化不被裁掉。
        float tMin = keys[0].time;
        float tMax = keys[keys.Length - 1].time;
        float span = Mathf.Max(1e-6f, tMax - tMin);

        // 1. 扫描水平段并填充
        var labelStyle = new GUIStyle(EditorStyles.miniBoldLabel);
        labelStyle.normal.textColor = Color.white;
        labelStyle.alignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < keys.Length - 1; i++)
        {
            var a = keys[i];
            var b = keys[i + 1];
            if (Mathf.Abs(a.value - b.value) > opts.holdEpsilon) continue;

            float xa = rect.x + (a.time - tMin) / span * rect.width;
            float xb = rect.x + (b.time - tMin) / span * rect.width;
            if (xb - xa < 1f) continue; // 太窄不画

            Rect holdRect = new Rect(xa, rect.y, xb - xa, rect.height);
            EditorGUI.DrawRect(holdRect, opts.holdColor);

            if (opts.showDuration && (xb - xa) > 30f)
            {
                float duration = b.time - a.time;
                GUI.Label(holdRect, duration.ToString("F2"), labelStyle);
            }
        }

        // 2. 关键帧点
        if (opts.showKeyDots)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                float x = rect.x + (keys[i].time - tMin) / span * rect.width;
                EditorGUI.DrawRect(new Rect(x - 1f, rect.y, 2f, rect.height), opts.keyColor);
            }
        }
    }
}
