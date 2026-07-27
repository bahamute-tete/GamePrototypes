using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

// ════════════════════════════════════════════════════════════════════════════════
//  SplineCurveMoveClip 的 Timeline ClipEditor
//
//  职责一【可视化】停顿段（DrawBackground）：
//   - 黄色半透明覆盖停顿时间范围
//   - 端点画 1px 边界线（便于 Timeline 上跟其他轨道对齐）
//   - 中间显示 ⏸ + 时长文字
//
//  职责二【端点拖拽 remap】（OnClipChanged）：
//   用户拖动 Clip 左/右端点改变 duration 时，Timeline 会回调 OnClipChanged（这是
//   拖拽【完成后】的事件回调，不是输入拦截——所以可靠，不存在早年试过的 DrawBackground
//   抢不到鼠标的问题）。我们在这里把"Unity 默认等比缩放整条曲线"纠正为方案 A：
//     · 拖右端 → 只平移曲线最后一个 knot（终点），停顿绝对位置不变、末段变速
//     · 拖左端 → 只平移曲线第一个 knot（起点），停顿绝对位置不变、首段变速
//   并强制 clipIn=0 / timeScale=1，让曲线独占时序。被拖端用 MIN_SEGMENT_FRAMES 帧钳制，
//   不能越过最近的 knot。
//
//  停顿的增删、速度同步等仍在 Inspector 面板完成；那些操作会通过 SyncTrimBaseline
//  同步本类用的基线，避免被误判成端点拖拽。
// ════════════════════════════════════════════════════════════════════════════════

[CustomTimelineEditor(typeof(SplineCurveMoveClip))]
public class SplineCurveMoveClipTimelineEditor : ClipEditor
{
    private static readonly Color FILL_COLOR   = new Color(1.00f, 0.85f, 0.20f, 0.32f);
    private static readonly Color BORDER_COLOR = new Color(1.00f, 0.75f, 0.10f, 0.85f);
    private static readonly Color TEXT_COLOR   = new Color(0.10f, 0.05f, 0.00f, 0.85f);

    private const float MIN_WIDTH_FOR_LABEL = 50f;
    private const float MIN_WIDTH_FOR_ICON  = 12f;

    // 被拖端钳制：末段/首段最少保留这么多帧，避免越过最近的 knot 或速度→∞。
    private const int MIN_SEGMENT_FRAMES = 3;

    // 位移判定阈值（秒）。拖拽吸附到帧，delta 都是 1/frameRate 的整数倍，1e-5 足够区分"没动"。
    private const double TRIM_EPS = 1e-5;

    // ════════════════════════════════════════════════════════════════════════════════
    //  端点拖拽 remap
    // ════════════════════════════════════════════════════════════════════════════════

    /// <summary>新建/克隆 Clip 时，把基线初始化为当前 start/duration（这样首次拖端点就生效）。</summary>
    public override void OnCreate(TimelineClip clip, TrackAsset track, TimelineClip clonedFrom)
    {
        var asset = clip != null ? clip.asset as SplineCurveMoveClip : null;
        if (asset != null) asset.SyncTrimBaseline(clip.start, clip.duration);
    }

    /// <summary>
    /// Clip 被编辑（移动/拖端点/Undo 等）后的回调。只处理"拖端点改 duration"，把 Unity 的
    /// 等比缩放纠正为方案 A（只平移被拖端的 knot），并钳制 + 强制 clipIn=0/timeScale=1。
    /// </summary>
    public override void OnClipChanged(TimelineClip clip)
    {
        if (clip == null) return;
        var asset = clip.asset as SplineCurveMoveClip;
        if (asset == null || asset.Template == null) return;

        double curStart = clip.start;
        double curDur   = clip.duration;

        // Inspector 程序化改时长（插入/删除停顿、速度同步）已自行同步基线 → 跳过 remap
        if (asset.SuppressTrimRemap)
        {
            asset.SyncTrimBaseline(curStart, curDur);
            return;
        }

        // 兜底：刚发生过程序化改时长（同帧或下一两帧），Timeline 此刻补发的 OnClipChanged
        // 不是用户拖端点，跳过 remap，只对齐基线。避免"插入停顿"被二次 remap。
        int progFrame = asset.LastProgrammaticDurationFrame;
        if (progFrame != int.MinValue)
        {
            long elapsed = (long)Time.frameCount - progFrame;
            if (elapsed >= 0 && elapsed <= 2)
            {
                asset.SyncTrimBaseline(curStart, curDur);
                return;
            }
        }

        // 基线未初始化（老 Clip / 刚建立）→ 记录当前值，这一次不 remap
        if (!asset.TrimBaselineInitialized)
        {
            asset.SyncTrimBaseline(curStart, curDur);
            return;
        }

        double baseStart = asset.TrimBaselineStart;
        double baseDur   = asset.TrimBaselineDuration;
        double dStart = curStart - baseStart;
        double dDur   = curDur   - baseDur;

        bool startMoved = System.Math.Abs(dStart) > TRIM_EPS;
        bool durMoved   = System.Math.Abs(dDur)   > TRIM_EPS;

        // 没变化
        if (!startMoved && !durMoved) return;

        // 整体平移（duration 不变、仅 start 变）：曲线本地坐标不动，只更新基线
        if (startMoved && !durMoved)
        {
            asset.SyncTrimBaseline(curStart, curDur);
            return;
        }

        // 判定拖的是哪一端
        CurvePauseQuickEdit.EndpointTrimEdge edge;
        if (!startMoved && durMoved)
        {
            edge = CurvePauseQuickEdit.EndpointTrimEdge.Right;          // start 固定 → 右端
        }
        else if (System.Math.Abs(dStart + dDur) < 1e-4)
        {
            edge = CurvePauseQuickEdit.EndpointTrimEdge.Left;           // 右边缘固定(dStart=-dDur) → 左端
        }
        else
        {
            // 两端同时变（少见，可能吸附/多选）——不猜，重新对齐基线
            asset.SyncTrimBaseline(curStart, curDur);
            return;
        }

        var curve = asset.Template.DisplacementCurve;
        if (curve == null || curve.keys.Length < 2)
        {
            asset.SyncTrimBaseline(curStart, curDur);
            return;
        }
        var oldKeys = curve.keys;
        int n = oldKeys.Length;

        // 每段最小秒数（留几帧）
        var track = clip.GetParentTrack();
        var tl = track != null ? track.timelineAsset : null;
        double frameRate = 60.0;
        if (tl != null) { double fr = tl.editorSettings.frameRate; if (fr >= 1.0) frameRate = fr; }
        double minSeg = MIN_SEGMENT_FRAMES / frameRate;

        bool holdAtEnd = asset.HoldAtEndOnTrim;   // 末端模式：true = 到终点停着等（方案 B）

        // ─── 钳制：被拖端不能越过最近的 knot ───
        double clampedStart = curStart;
        double clampedDur   = curDur;
        bool   clamped      = false;

        if (edge == CurvePauseQuickEdit.EndpointTrimEdge.Right)
        {
            if (!holdAtEnd)
            {
                // 方案 A：字面拖最后一个 knot —— 保护字面末段（倒数第二个 knot），不收拢 hold。
                double secondLastAbs = (double)oldKeys[n - 2].time * baseDur;
                double floorDur = secondLastAbs + minSeg;
                if (clampedDur < floorDur) { clampedDur = floorDur; clamped = true; }
            }
            else
            {
                // 方案 B：收拢已有末端 hold 找回真正的行程终点；延长则补 hold（不钳制），
                // 压缩到行程时间以内才钳制末段。
                int destIdx = CurvePauseQuickEdit.TravelEndIndex(curve);
                if (destIdx < 1) destIdx = n - 1;
                double destAbs = (double)oldKeys[destIdx].time * baseDur;
                bool willHold = clampedDur > destAbs + 1e-6;
                if (!willHold)
                {
                    double predAbs = (destIdx - 1 >= 0) ? (double)oldKeys[destIdx - 1].time * baseDur : 0.0;
                    double floorDur = predAbs + minSeg;
                    if (clampedDur < floorDur) { clampedDur = floorDur; clamped = true; }
                }
            }
        }
        else // Left
        {
            // 首段不能塌到 0：第二个 knot 的新绝对秒 = oldAbs[1] + dDur ≥ minSeg
            double secondAbsOld = (double)oldKeys[1].time * baseDur;
            double rightEdge = baseStart + baseDur;                    // 左拖时右边缘固定
            double floorDur = baseDur + (minSeg - secondAbsOld);       // = oldDur + dDur_min
            if (floorDur < minSeg) floorDur = minSeg;
            if (clampedDur < floorDur)
            {
                clampedDur   = floorDur;
                clampedStart = rightEdge - clampedDur;
                clamped = true;
            }
        }

        // ─── 写回 Clip 几何（钳制 + 强制 clipIn=0 / timeScale=1，曲线独占时序）───
        bool clipInOrScaleDirty = (clip.clipIn != 0.0) || (clip.timeScale != 1.0);
        if (clamped || clipInOrScaleDirty)
        {
            asset.SuppressTrimRemap = true;                            // 防止改几何时再入 OnClipChanged
            try
            {
                if (tl != null) Undo.RegisterCompleteObjectUndo(tl, "Spline 端点拖拽");
                if (clamped)
                {
                    clip.start    = clampedStart;
                    clip.duration = clampedDur;
                }
                clip.clipIn    = 0.0;
                clip.timeScale = 1.0;
                if (tl != null) EditorUtility.SetDirty(tl);
            }
            finally { asset.SuppressTrimRemap = false; }
        }

        // ─── remap 曲线：方案 A 平移被拖端 knot；方案 B 右端延长时末端补 hold ───
        var newCurve = CurvePauseQuickEdit.RemapForEndpointTrim(curve, baseDur, clampedDur, edge, holdAtEnd);

        // ─── 写回 PlayableAsset：曲线 + 基线 + lastBaselineDuration（与漂移横幅对齐，避免重复校正）───
        var so = new SerializedObject(asset);
        var tProp = so.FindProperty("template");
        var cProp = tProp != null ? tProp.FindPropertyRelative("displacementCurve") : null;
        if (cProp != null)
        {
            cProp.animationCurveValue = newCurve;
            var bs = so.FindProperty("trimBaselineStart");
            var bd = so.FindProperty("trimBaselineDuration");
            var lb = so.FindProperty("lastBaselineDuration");
            if (bs != null) bs.doubleValue = clampedStart;
            if (bd != null) bd.doubleValue = clampedDur;
            if (lb != null) lb.doubleValue = clampedDur;
            so.ApplyModifiedProperties();
        }
        else
        {
            asset.SyncTrimBaseline(clampedStart, clampedDur);
        }

        TimelineEditor.Refresh(RefreshReason.ContentsModified);
    }

    public override void DrawBackground(TimelineClip clip, ClipBackgroundRegion region)
    {
        // 只在 Repaint 期间绘制 —— 其它事件类型对纯可视化无意义
        if (Event.current.type != EventType.Repaint) return;
        if (clip == null) return;

        var asset = clip.asset as SplineCurveMoveClip;
        if (asset == null || asset.Template == null) return;

        // 冷启动种子：老 Clip（基线 -1）在首次拖端点前先把基线填上当前 start/duration。
        // DrawBackground 在 Clip 可见时每次 repaint 都跑，拖拽前必已执行；只在未初始化时写一次，
        // 仅改内存值、不 SetDirty（避免 repaint 期间反复弄脏资源）。
        if (!asset.TrimBaselineInitialized)
            asset.SyncTrimBaseline(clip.start, clip.duration);

        var curve = asset.Template.DisplacementCurve;
        if (curve == null || curve.keys.Length < 2) return;

        double clipDuration = clip.duration;
        if (clipDuration <= 1e-6) return;

        var pauses = CurvePauseQuickEdit.DetectPauses(curve, clipDuration);
        if (pauses.Count == 0) return;

        double regionStart = region.startTime;
        double regionEnd   = region.endTime;
        double regionSpan  = regionEnd - regionStart;
        if (regionSpan <= 1e-9) return;

        Rect rect = region.position;
        double pxPerSec = rect.width / regionSpan;

        for (int i = 0; i < pauses.Count; i++)
        {
            var p = pauses[i];
            double pStart = p.startSec;
            double pEnd   = p.startSec + p.durationSec;

            double visStart = System.Math.Max(pStart, regionStart);
            double visEnd   = System.Math.Min(pEnd,   regionEnd);
            if (visEnd - visStart <= 1e-6) continue;

            float vx0 = (float)((visStart - regionStart) * pxPerSec) + rect.x;
            float vx1 = (float)((visEnd   - regionStart) * pxPerSec) + rect.x;

            var pauseRect = new Rect(vx0, rect.y, vx1 - vx0, rect.height);
            EditorGUI.DrawRect(pauseRect, FILL_COLOR);

            // 真实端点位于可见区时画 1px 边界线 —— 帮你在 Timeline 上对齐其他轨道
            float leftX  = (float)((pStart - regionStart) * pxPerSec) + rect.x;
            float rightX = (float)((pEnd   - regionStart) * pxPerSec) + rect.x;
            if (pStart >= regionStart - 1e-6)
                EditorGUI.DrawRect(new Rect(leftX,  rect.y, 1f, rect.height), BORDER_COLOR);
            if (pEnd <= regionEnd + 1e-6)
                EditorGUI.DrawRect(new Rect(rightX - 1f, rect.y, 1f, rect.height), BORDER_COLOR);

            // 中间标签
            if (pauseRect.width >= MIN_WIDTH_FOR_LABEL)
                DrawCenteredLabel(pauseRect, $"⏸ {FormatShort(p.durationSec)}");
            else if (pauseRect.width >= MIN_WIDTH_FOR_ICON)
                DrawCenteredLabel(pauseRect, "⏸");
        }
    }

    private static GUIStyle _centeredLabelStyle;
    private static void DrawCenteredLabel(Rect r, string text)
    {
        if (_centeredLabelStyle == null)
        {
            _centeredLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel);
            _centeredLabelStyle.alignment = TextAnchor.MiddleCenter;
            _centeredLabelStyle.normal.textColor = TEXT_COLOR;
        }
        GUI.Label(r, text, _centeredLabelStyle);
    }

    private static string FormatShort(float sec)
    {
        if (sec < 60f) return $"{sec:F1}s";
        int m = Mathf.FloorToInt(sec / 60f);
        int s = Mathf.FloorToInt(sec - m * 60f);
        return $"{m}:{s:D2}";
    }
}
