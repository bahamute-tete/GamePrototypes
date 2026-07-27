using UnityEditor;
using UnityEngine;

/// <summary>
/// 曲线快捷编辑工具。提供"插入水平段"、"清除水平段"、"生成转头序列"等快速操作，
/// 避免手动在 Curve Editor 里反复对齐关键帧 value。
/// </summary>
public static class CurvePauseQuickEdit
{
    /// <summary>
    /// 曲线衔接风格——影响停顿与行走段交界处的 tangent mode。
    /// </summary>
    public enum JoinStyle
    {
        /// <summary>
        /// 行走段 = 纯直线（匀速），停顿端点是硬转弯。视觉上是折线。
        /// 优点：节奏精确可预测，便于按"行走时长 = 距离 / 速度"匹配动画。
        /// </summary>
        Linear,

        /// <summary>
        /// 行走段中间用 ClampedAuto（自动平滑），停顿端点内侧仍 Linear（停顿段水平）。
        /// 视觉上进入/离开停顿有 ease in/out。
        /// 优点：观感自然；缺点：行走段速度并非严格匀速，跟动画的精确同步会有微小偏差。
        /// </summary>
        Smooth,
    }

    /// <summary>被拖动的 Clip 端点。</summary>
    public enum EndpointTrimEdge
    {
        /// <summary>右端：start 不变、duration 改变。</summary>
        Right,
        /// <summary>左端：右边缘不变、start 改变。平移曲线【第一个】knot（起点）。</summary>
        Left,
    }

    /// <summary>
    /// 返回"行程终点"knot 的索引：从末尾向前合并所有与【最终值】相等的 knot（即末端 hold 段），
    /// 第一个达到最终值的 knot 即行程终点。没有末端 hold 时返回最后一个 knot。
    /// 用途：方案 B 会在末端补一段水平 hold；再次拖拽时靠本方法把已有 hold 收拢、找回真正的终点，
    /// 保证重复拖拽幂等。
    /// </summary>
    public static int TravelEndIndex(AnimationCurve curve)
    {
        if (curve == null) return -1;
        var keys = curve.keys;
        int n = keys.Length;
        if (n < 2) return n - 1;
        float fv = keys[n - 1].value;
        int idx = n - 1;
        while (idx - 1 >= 1 && Mathf.Approximately(keys[idx - 1].value, fv)) idx--;
        return idx;
    }

    /// <summary>
    /// 【端点拖拽 remap】用户拖动 Timeline Clip 的左/右端点改变 duration 时调用。
    ///
    /// 左端：始终方案 A —— 第一个 knot 落到新左端（绝对 0），其余 knot 绝对秒整体 += dDur
    /// （保持全局位置）→ 停顿绝对位置不动、首段变速。末端模式不影响左端。
    ///
    /// 右端，按 holdAtEnd 分两种末端模式：
    ///   - holdAtEnd = false（方案 A，默认）：行程终点 knot 跟随右边缘到 newDuration，
    ///     其余 knot 绝对秒不变 → 末段被拉长/压缩变速、停顿绝对位置不动。
    ///   - holdAtEnd = true 且在【延长】（newDuration > 行程终点绝对秒）（方案 B，"到终点停着等"）：
    ///     行程终点固定在原绝对秒（保持原末段速度先到达），其后补一段水平 hold 到 newDuration
    ///     → 角色按原速到达终点后【停在终点等待】剩余时间。
    ///     若是压缩到行程时间以内，则退回方案 A（终点内移、不补 hold）。
    ///
    /// 关键点：曲线 key.time 是【归一化 [0,1]】，运行时按 duration 重归一化。
    /// 本方法把每个 key 用 oldDuration 解释成绝对秒后重定位，再用 newDuration 重新归一化。
    /// 调用方负责【先把 duration 钳制】到合法范围（方案 B 延长时不需要钳制末段）。
    /// </summary>
    public static AnimationCurve RemapForEndpointTrim(
        AnimationCurve oldCurve,
        double oldDuration,
        double newDuration,
        EndpointTrimEdge edge,
        bool holdAtEnd)
    {
        if (oldCurve == null || oldCurve.keys.Length < 2 || oldDuration <= 1e-6 || newDuration <= 1e-6)
            return oldCurve != null ? new AnimationCurve(oldCurve.keys) : new AnimationCurve();

        var oldKeys = oldCurve.keys;
        int n = oldKeys.Length;
        double dDur = newDuration - oldDuration;

        // ───────── 左端：始终方案 A，末端模式不影响 ─────────
        if (edge == EndpointTrimEdge.Left)
        {
            var lk = new Keyframe[n];
            for (int i = 0; i < n; i++)
            {
                var k = oldKeys[i];
                double oldAbs = (double)k.time * oldDuration;
                double newAbs = (i == 0) ? 0.0 : oldAbs + dDur;   // 首 knot→0，其余保持全局位置
                k.time = (float)(newAbs / newDuration);
                lk[i] = k;
            }
            var lc = new AnimationCurve(lk);
            for (int i = 0; i < n && i < lc.keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode (lc, i, AnimationUtility.GetKeyLeftTangentMode (oldCurve, i));
                AnimationUtility.SetKeyRightTangentMode(lc, i, AnimationUtility.GetKeyRightTangentMode(oldCurve, i));
            }
            return lc;
        }

        // ───────── 右端 ─────────
        // 方案 A（holdAtEnd = false）：字面意义"拖动画曲线最后一个 knot" —— 末 knot→newDuration，
        // 其余 knot 绝对秒不变。【不】收拢末端 hold：B 模式做出来的 hold 在 A 模式下原样保留
        // （此时末段恰好是那段 hold，会随拖动一起伸缩，但 hold 的存在与行程终点不被重置）。
        if (!holdAtEnd)
        {
            var ak = new Keyframe[n];
            for (int i = 0; i < n; i++)
            {
                var k = oldKeys[i];
                double oldAbs = (double)k.time * oldDuration;
                double newAbs = (i == n - 1) ? newDuration : oldAbs;   // 末 knot 跟随右边缘；其余绝对秒不变
                k.time = (float)(newAbs / newDuration);
                ak[i] = k;
            }
            var ac = new AnimationCurve(ak);
            for (int i = 0; i < n && i < ac.keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode (ac, i, AnimationUtility.GetKeyLeftTangentMode (oldCurve, i));
                AnimationUtility.SetKeyRightTangentMode(ac, i, AnimationUtility.GetKeyRightTangentMode(oldCurve, i));
            }
            return ac;
        }

        // 方案 B（holdAtEnd = true）：收拢已有末端 hold 找回真正的行程终点，再按需补 hold。
        int destIdx = TravelEndIndex(oldCurve);     // 收拢已有末端 hold，找回真正的行程终点
        if (destIdx < 1) destIdx = n - 1;           // 退化保护（整条曲线同值等异常情况）
        float finalValue = oldKeys[n - 1].value;
        double destAbs = (double)oldKeys[destIdx].time * oldDuration;

        bool doHold = newDuration > destAbs + 1e-6;

        if (doHold)
        {
            // 行程终点固定在 destAbs，其后补水平 hold 到 newDuration
            var bk = new Keyframe[destIdx + 2];
            for (int i = 0; i <= destIdx; i++)
            {
                var k = oldKeys[i];
                double oldAbs = (double)k.time * oldDuration;     // [0..destIdx] 绝对秒不变
                k.time = (float)(oldAbs / newDuration);
                bk[i] = k;
            }
            bk[destIdx + 1] = new Keyframe(1f, finalValue);       // hold knot：终点同值，落在 newDuration

            var bc = new AnimationCurve(bk);
            for (int i = 0; i <= destIdx && i < bc.keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(bc, i, AnimationUtility.GetKeyLeftTangentMode(oldCurve, i));
                if (i < destIdx)
                    AnimationUtility.SetKeyRightTangentMode(bc, i, AnimationUtility.GetKeyRightTangentMode(oldCurve, i));
                else
                    AnimationUtility.SetKeyRightTangentMode(bc, i, AnimationUtility.TangentMode.Linear); // 终点→hold 平接
            }
            if (bc.keys.Length >= destIdx + 2)
            {
                AnimationUtility.SetKeyLeftTangentMode (bc, destIdx + 1, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(bc, destIdx + 1, AnimationUtility.TangentMode.Linear);
            }
            return bc;
        }
        else
        {
            // 方案 B 但压缩到行程时间以内：终点移到 newDuration，丢弃已有末端 hold，其余绝对秒不变
            var ck = new Keyframe[destIdx + 1];
            for (int i = 0; i < destIdx; i++)
            {
                var k = oldKeys[i];
                double oldAbs = (double)k.time * oldDuration;
                k.time = (float)(oldAbs / newDuration);
                ck[i] = k;
            }
            var dk = oldKeys[destIdx];
            dk.value = finalValue;
            dk.time  = 1f;                                         // 终点跟随右边缘（norm 1.0）
            ck[destIdx] = dk;

            var cc = new AnimationCurve(ck);
            for (int i = 0; i < destIdx && i < cc.keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode (cc, i, AnimationUtility.GetKeyLeftTangentMode (oldCurve, i));
                AnimationUtility.SetKeyRightTangentMode(cc, i, AnimationUtility.GetKeyRightTangentMode(oldCurve, i));
            }
            if (destIdx < cc.keys.Length)
            {
                AnimationUtility.SetKeyLeftTangentMode (cc, destIdx, AnimationUtility.GetKeyLeftTangentMode (oldCurve, destIdx));
                AnimationUtility.SetKeyRightTangentMode(cc, destIdx, AnimationUtility.GetKeyRightTangentMode(oldCurve, destIdx));
            }
            return cc;
        }
    }

    /// <summary>
    /// 统一的 tangent 设置工具。识别每段是水平段（停顿）还是非水平段（行走），
    /// 然后按 joinStyle 给行走段端点设 Linear 或 ClampedAuto，水平段端点始终 Linear。
    /// </summary>
    public static void ApplyTangents(AnimationCurve curve, JoinStyle joinStyle)
    {
        if (curve == null || curve.keys.Length < 2) return;

        var walkTangent = (joinStyle == JoinStyle.Smooth)
            ? AnimationUtility.TangentMode.ClampedAuto
            : AnimationUtility.TangentMode.Linear;

        // 第一步：所有 tangent 先置 Linear（安全默认值）
        int n = curve.keys.Length;
        for (int i = 0; i < n; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode (curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }

        // 第二步：扫描每段，识别行走段（非水平），把段两端的对应 tangent 改成 walkTangent
        var keys = curve.keys;
        for (int i = 0; i < keys.Length - 1; i++)
        {
            bool isPause = Mathf.Abs(keys[i].value - keys[i + 1].value) < 0.001f;
            if (isPause) continue; // 水平段保持默认 Linear（保证段内严格水平）

            // 行走段：key[i] 的 Right 和 key[i+1] 的 Left 控制这段的形状
            AnimationUtility.SetKeyRightTangentMode(curve, i,     walkTangent);
            AnimationUtility.SetKeyLeftTangentMode (curve, i + 1, walkTangent);
        }
    }
    /// <summary>
    /// 在曲线的 [startTime, startTime+duration] 区间插入一个水平段。
    /// 自动用 startTime 处的现有曲线值作为水平段的 value，保持曲线连续。
    /// 会移除该区间内已有的关键帧避免冲突。
    /// </summary>
    /// <returns>插入是否成功</returns>
    public static bool InsertPause(AnimationCurve curve, float startTime, float duration,
                                   JoinStyle joinStyle = JoinStyle.Smooth)
    {
        if (curve == null || duration <= 0f) return false;
        float endTime = startTime + duration;
        float holdValue = curve.Evaluate(startTime);

        // 移除该范围内已有的关键帧（必须从后往前删，索引才稳定）
        var keys = curve.keys;
        for (int i = keys.Length - 1; i >= 0; i--)
        {
            if (keys[i].time >= startTime && keys[i].time <= endTime)
                curve.RemoveKey(i);
        }

        int idxA = curve.AddKey(startTime, holdValue);
        int idxB = curve.AddKey(endTime,   holdValue);
        if (idxA < 0 || idxB < 0) return false;

        ApplyTangents(curve, joinStyle);
        return true;
    }

    /// <summary>
    /// 移除曲线中所有水平段：对每一对 value 接近的相邻关键帧，删除后一个。
    /// </summary>
    public static void RemoveAllPauses(AnimationCurve curve, float epsilon = 0.001f)
    {
        if (curve == null) return;
        bool found;
        int safety = 0;
        do
        {
            found = false;
            var keys = curve.keys;
            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (Mathf.Abs(keys[i].value - keys[i + 1].value) <= epsilon)
                {
                    curve.RemoveKey(i + 1);
                    found = true;
                    break;
                }
            }
        } while (found && ++safety < 1000);
    }

    /// <summary>
    /// 【保持速度版】在 [pauseStartSec, pauseStartSec + pauseDurationSec] 区间插入水平段。
    /// 算法核心：把所有现有关键帧按【绝对时间】（旧归一化 × 旧时长）重映射到新归一化空间，
    /// 而不是按归一化时间重映射。这样：
    ///   - 停顿前的关键帧绝对时间不变 → 那段速度不变
    ///   - 停顿后的关键帧绝对时间整体后移 pauseDurationSec → 那段速度也不变
    ///   - 中间多出一段水平的"停顿"
    ///
    /// 配合调用方把 TimelineClip.duration 改成 newDuration = oldDuration + pauseDurationSec，
    /// 物体真正的视觉行为就是"暂停 N 秒后以原速度继续"。
    /// </summary>
    /// <returns>新生成的曲线（关键帧时间在 [0,1]，对应新归一化）</returns>
    public static AnimationCurve InsertPausePreservingSpeed(
        AnimationCurve oldCurve,
        double oldDuration,
        double pauseStartSec,
        double pauseDurationSec,
        out double newDuration,
        JoinStyle joinStyle = JoinStyle.Smooth)
    {
        newDuration = oldDuration + pauseDurationSec;

        if (oldCurve == null || oldDuration <= 1e-6 || pauseDurationSec <= 0)
            return oldCurve != null ? new AnimationCurve(oldCurve.keys) : new AnimationCurve();

        pauseStartSec = System.Math.Max(0.0, System.Math.Min(pauseStartSec, oldDuration));

        // 1. 在旧归一化空间下取停顿位置的 value，作为水平段的 value
        float pauseOldNorm = (float)(pauseStartSec / oldDuration);
        float pauseValue   = oldCurve.Evaluate(pauseOldNorm);

        var newCurve = new AnimationCurve();

        // 2. 重映射所有原关键帧到新归一化空间
        //    规则：旧绝对时间 t_abs = oldKey.time * oldDuration
        //          t_abs <= pauseStartSec → 新绝对时间 = t_abs（不变）
        //          t_abs >  pauseStartSec → 新绝对时间 = t_abs + pauseDurationSec
        //          新归一化 = 新绝对 / newDuration
        const double EPS = 1e-5;
        var oldKeys = oldCurve.keys;
        for (int i = 0; i < oldKeys.Length; i++)
        {
            var k = oldKeys[i];
            double oldAbs = k.time * oldDuration;
            double newAbs = (oldAbs <= pauseStartSec + EPS) ? oldAbs : oldAbs + pauseDurationSec;
            // 跳过会与即将插入的停顿起点重合的旧关键帧（避免 AddKey 失败 / value 冲突）
            // 终点位置在旧空间里不存在（是新插入的），所以无需检查
            if (System.Math.Abs(oldAbs - pauseStartSec) < EPS) continue;
            k.time = (float)(newAbs / newDuration);
            newCurve.AddKey(k);
        }

        // 3. 插入停顿两端的关键帧
        float pauseStartNewNorm = (float)(pauseStartSec / newDuration);
        float pauseEndNewNorm   = (float)((pauseStartSec + pauseDurationSec) / newDuration);
        newCurve.AddKey(pauseStartNewNorm, pauseValue);
        newCurve.AddKey(pauseEndNewNorm,   pauseValue);

        // 4. 用统一的 tangent 工具处理整条曲线
        ApplyTangents(newCurve, joinStyle);

        return newCurve;
    }

    /// <summary>
    /// 生成 "0 → 转过去 → 保持 → 转回来 → 0" 的 Yaw 模板曲线（典型场景：转头看向、停留、转回）。
    /// 五个时间段：起始静止、转动 1、保持、转动 2、终止静止。
    /// 所有时间都是 Clip 内归一化 [0,1]。
    /// </summary>
    public static AnimationCurve BuildLookSequence(
        float startTime, float turnDuration, float holdDuration, float targetAngle)
    {
        startTime    = Mathf.Clamp01(startTime);
        turnDuration = Mathf.Max(0.001f, turnDuration);
        holdDuration = Mathf.Max(0f, holdDuration);

        float t0 = startTime;
        float t1 = Mathf.Min(t0 + turnDuration, 1f);
        float t2 = Mathf.Min(t1 + holdDuration, 1f);
        float t3 = Mathf.Min(t2 + turnDuration, 1f);

        var c = new AnimationCurve();
        c.AddKey(0f, 0f);
        if (t0 > 0.001f) c.AddKey(t0, 0f);
        c.AddKey(t1, targetAngle);
        if (t2 > t1 + 0.001f) c.AddKey(t2, targetAngle);
        c.AddKey(t3, 0f);
        if (t3 < 0.999f) c.AddKey(1f, 0f);

        // 全部 ClampedAuto，平滑过渡；水平段两端的内侧 tangent 改 Linear 避免过冲
        var keys = c.keys;
        for (int i = 0; i < keys.Length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode (c, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        // 识别水平段，修正 tangent
        keys = c.keys;
        for (int i = 0; i < keys.Length - 1; i++)
        {
            if (Mathf.Abs(keys[i].value - keys[i + 1].value) < 0.001f)
            {
                AnimationUtility.SetKeyRightTangentMode(c, i,     AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyLeftTangentMode (c, i + 1, AnimationUtility.TangentMode.Linear);
            }
        }
        return c;
    }

    /// <summary>
    /// 按目标行走时长（splineLength / walkSpeed）重缩放 displacementCurve：
    ///   - 水平段（停顿）：绝对时长不变
    ///   - 非水平段（行走）：按比例缩放，使行走段总时长 = targetWalkDuration
    /// 新 Clip 时长 = targetWalkDuration + 原停顿总时长
    ///
    /// 用于"角色实际有动画速度，路径长度已知，要求 Clip 时长精确匹配动画速度"的场景。
    /// </summary>
    /// <param name="oldCurve">原 displacementCurve（关键帧 time ∈ [0,1]）</param>
    /// <param name="oldDuration">原 Clip 时长（秒）</param>
    /// <param name="targetWalkDuration">期望的行走段总时长（秒），通常 = splineLength / walkSpeed</param>
    /// <param name="newDuration">输出：缩放后的新 Clip 时长（秒）</param>
    /// <returns>新曲线。如果原曲线没有行走段（全是停顿），返回原曲线副本，newDuration = oldDuration</returns>
    public static AnimationCurve RescaleWalkPreservingPauses(
        AnimationCurve oldCurve,
        double oldDuration,
        double targetWalkDuration,
        out double newDuration)
    {
        if (oldCurve == null || oldCurve.keys.Length < 2)
        {
            newDuration = System.Math.Max(0.001, targetWalkDuration);
            var c = new AnimationCurve();
            c.AddKey(0f, 0f);
            c.AddKey(1f, 1f);
            return c;
        }

        var oldKeys = oldCurve.keys;
        const float PAUSE_EPS = 0.001f;

        // 1. 累加 walk / pause 的旧绝对时长
        double walkAbs = 0;
        double pauseAbs = 0;
        for (int i = 0; i < oldKeys.Length - 1; i++)
        {
            double segDur = (oldKeys[i + 1].time - oldKeys[i].time) * oldDuration;
            bool isPause = Mathf.Abs(oldKeys[i].value - oldKeys[i + 1].value) < PAUSE_EPS;
            if (isPause) pauseAbs += segDur;
            else         walkAbs  += segDur;
        }

        if (walkAbs < 1e-6)
        {
            // 全是停顿段，无法按行走速度缩放
            newDuration = oldDuration;
            return new AnimationCurve(oldKeys);
        }

        double walkScale = targetWalkDuration / walkAbs;
        newDuration = targetWalkDuration + pauseAbs;

        // 2. 重算每个 key 的新绝对时间
        var newAbsTimes = new double[oldKeys.Length];
        newAbsTimes[0] = 0;
        for (int i = 0; i < oldKeys.Length - 1; i++)
        {
            double segDur = (oldKeys[i + 1].time - oldKeys[i].time) * oldDuration;
            bool isPause = Mathf.Abs(oldKeys[i].value - oldKeys[i + 1].value) < PAUSE_EPS;
            double newSegDur = isPause ? segDur : segDur * walkScale;
            newAbsTimes[i + 1] = newAbsTimes[i] + newSegDur;
        }

        // 3. 构建新曲线（time 重新映射到 [0,1]）
        var newCurve = new AnimationCurve();
        for (int i = 0; i < oldKeys.Length; i++)
        {
            var k = oldKeys[i];
            k.time = (float)(newAbsTimes[i] / newDuration);
            newCurve.AddKey(k);
        }

        // 4. 拷贝 tangent mode（直接复制 in/out tangent value 在 AddKey 时已经保留，
        //    但 tangent "Mode"（Linear/Auto/Constant）是 AnimationUtility 单独存的）
        for (int i = 0; i < oldKeys.Length && i < newCurve.keys.Length; i++)
        {
            var lm = AnimationUtility.GetKeyLeftTangentMode (oldCurve, i);
            var rm = AnimationUtility.GetKeyRightTangentMode(oldCurve, i);
            AnimationUtility.SetKeyLeftTangentMode (newCurve, i, lm);
            AnimationUtility.SetKeyRightTangentMode(newCurve, i, rm);
        }
        return newCurve;
    }

    /// <summary>
    /// 单个停顿段的描述。startSec / durationSec 是相对 Clip 起点的【绝对秒数】。
    /// progressValue 是曲线在停顿期间的 value（即在路径上的进度）。
    /// </summary>
    public struct PauseSegment
    {
        public float startSec;
        public float durationSec;
        public float progressValue;
        public float EndSec => startSec + durationSec;
    }

    /// <summary>
    /// 扫描曲线，识别所有水平段（停顿），返回其绝对时间区间和 value。
    /// </summary>
    public static System.Collections.Generic.List<PauseSegment> DetectPauses(
        AnimationCurve curve, double clipDuration)
    {
        var result = new System.Collections.Generic.List<PauseSegment>();
        if (curve == null || curve.keys.Length < 2 || clipDuration <= 1e-6) return result;

        var keys = curve.keys;
        const float PAUSE_EPS = 0.001f;
        for (int i = 0; i < keys.Length - 1; i++)
        {
            if (Mathf.Abs(keys[i].value - keys[i + 1].value) < PAUSE_EPS)
            {
                float startSec = (float)(keys[i].time     * clipDuration);
                float endSec   = (float)(keys[i + 1].time * clipDuration);
                if (endSec - startSec < 1e-4f) continue; // 过滤无意义的零宽段
                result.Add(new PauseSegment
                {
                    startSec       = startSec,
                    durationSec    = endSec - startSec,
                    progressValue  = keys[i].value
                });
            }
        }
        return result;
    }

    /// <summary>
    /// 【基于原始无停顿曲线】重建 displacementCurve。
    ///
    /// 语义：抹掉旧曲线，回到纯匀速行走 (0,0)→(T,1)，然后把用户指定的停顿区间嵌入为水平段。
    /// 每个停顿的 progressValue 由系统自动计算 = (该停顿之前的累计行走秒数) / (总行走秒数)。
    /// 注意：传入的 PauseSegment.progressValue 字段会被【忽略并重算】。
    ///
    /// 自动处理：
    ///   - 截断：超出 [0, clipDuration] 的部分被裁掉，clamp 后宽度 ≤ 0 的整段被丢弃
    ///   - 重叠合并：按 start 排序后，相邻区间 start ≤ 上一个 end 即合并
    ///     例：[18, 26] 和 [20, 25] 合并为 [18, 26]；[18, 20] 和 [20, 25] 合并为 [18, 25]
    ///   - 停顿占满 Clip（walkTotal = 0）：返回水平为 0 的曲线
    /// </summary>
    public static AnimationCurve RebuildFromScratch(
        double clipDuration,
        System.Collections.Generic.List<PauseSegment> userPauses,
        JoinStyle joinStyle = JoinStyle.Linear)
    {
        var c = new AnimationCurve();

        if (clipDuration <= 1e-6)
        {
            c.AddKey(0f, 0f);
            c.AddKey(1f, 1f);
            ApplyTangents(c, joinStyle);
            return c;
        }

        // 1. 截断到 [0, clipDuration]，过滤宽度 ≤ 0 的
        var truncated = new System.Collections.Generic.List<(float start, float end)>();
        if (userPauses != null)
        {
            for (int i = 0; i < userPauses.Count; i++)
            {
                var p = userPauses[i];
                float start = Mathf.Clamp((float)p.startSec, 0f, (float)clipDuration);
                float end   = Mathf.Clamp((float)(p.startSec + p.durationSec), 0f, (float)clipDuration);
                if (end - start > 1e-4f) truncated.Add((start, end));
            }
        }

        // 2. 按 start 排序
        truncated.Sort((a, b) => a.start.CompareTo(b.start));

        // 3. 合并重叠
        var merged = new System.Collections.Generic.List<(float start, float end)>();
        for (int i = 0; i < truncated.Count; i++)
        {
            var p = truncated[i];
            if (merged.Count > 0 && p.start <= merged[merged.Count - 1].end + 1e-4f)
            {
                var last = merged[merged.Count - 1];
                merged[merged.Count - 1] = (last.start, Mathf.Max(last.end, p.end));
            }
            else
            {
                merged.Add(p);
            }
        }

        // 4. 算行走总时长
        double totalPauseDur = 0;
        for (int i = 0; i < merged.Count; i++) totalPauseDur += merged[i].end - merged[i].start;
        double walkTotal = clipDuration - totalPauseDur;

        // 5. 构建曲线
        c.AddKey(0f, 0f);

        if (walkTotal <= 1e-6)
        {
            // 停顿占满 Clip
            c.AddKey(1f, 0f);
            ApplyTangents(c, joinStyle);
            return c;
        }

        double walkedSec = 0;
        for (int i = 0; i < merged.Count; i++)
        {
            var p = merged[i];
            double prevEnd = (i > 0) ? merged[i - 1].end : 0;
            walkedSec += p.start - prevEnd;
            float v = (float)(walkedSec / walkTotal);
            c.AddKey((float)(p.start / clipDuration), v);
            c.AddKey((float)(p.end   / clipDuration), v);
        }

        c.AddKey(1f, 1f);
        ApplyTangents(c, joinStyle);
        return c;
    }

    /// <summary>
    /// 把 RebuildFromScratch 的"截断 + 合并重叠"逻辑暴露出来，方便 UI 预览合并后的结果。
    /// 返回：(合并后停顿列表, 总停顿秒数, 行走总秒数)
    /// </summary>
    public static System.Collections.Generic.List<PauseSegment> NormalizePauses(
        double clipDuration,
        System.Collections.Generic.List<PauseSegment> userPauses,
        out double totalPauseSec,
        out double walkTotalSec)
    {
        var result = new System.Collections.Generic.List<PauseSegment>();
        totalPauseSec = 0;
        walkTotalSec = clipDuration;

        if (clipDuration <= 1e-6 || userPauses == null) return result;

        var truncated = new System.Collections.Generic.List<(float start, float end)>();
        for (int i = 0; i < userPauses.Count; i++)
        {
            var p = userPauses[i];
            float start = Mathf.Clamp((float)p.startSec, 0f, (float)clipDuration);
            float end   = Mathf.Clamp((float)(p.startSec + p.durationSec), 0f, (float)clipDuration);
            if (end - start > 1e-4f) truncated.Add((start, end));
        }
        truncated.Sort((a, b) => a.start.CompareTo(b.start));

        for (int i = 0; i < truncated.Count; i++)
        {
            var p = truncated[i];
            if (result.Count > 0 && p.start <= result[result.Count - 1].EndSec + 1e-4f)
            {
                var last = result[result.Count - 1];
                last.durationSec = Mathf.Max(last.EndSec, p.end) - last.startSec;
                result[result.Count - 1] = last;
            }
            else
            {
                result.Add(new PauseSegment
                {
                    startSec = p.start,
                    durationSec = p.end - p.start,
                    progressValue = 0f, // 重新计算
                });
            }
        }

        for (int i = 0; i < result.Count; i++) totalPauseSec += result[i].durationSec;
        walkTotalSec = clipDuration - totalPauseSec;
        return result;
    }

    /// <summary>
    /// 【普通模式 / 水平平移模式】重建曲线，保留每个停顿当前的 progressValue。
    /// 跟 RebuildFromScratch 的核心区别：
    ///   - RebuildFromScratch: 重算 progressValue = 累计行走时间 / 总行走时间
    ///     → 结果是所有行走段速度统一一致（沿曲线方向匀速）
    ///   - RebuildPreservingProgress: 使用 PauseSegment.progressValue 字段的现值
    ///     → 停顿在时间轴上水平平移，path 进度位置不变
    ///       但停顿前后行走段的时间长度会变，斜率（速度）也会变
    ///
    /// 适用场景：用户想"在原 path 进度位置上，把这段停顿挪到不同时刻"，
    /// 接受由此带来的局部速度变化。
    ///
    /// 调用方负责保证传入的停顿列表无重叠 & 在 [0, clipDuration] 内（本方法不做截断/合并）。
    /// </summary>
    public static AnimationCurve RebuildPreservingProgress(
        double clipDuration,
        System.Collections.Generic.List<PauseSegment> orderedPauses,
        JoinStyle joinStyle = JoinStyle.Linear)
    {
        var c = new AnimationCurve();

        if (clipDuration <= 1e-6)
        {
            c.AddKey(0f, 0f);
            c.AddKey(1f, 1f);
            ApplyTangents(c, joinStyle);
            return c;
        }

        c.AddKey(0f, 0f);

        if (orderedPauses != null)
        {
            // 按 startSec 排序（防御性）
            var sorted = new System.Collections.Generic.List<PauseSegment>(orderedPauses);
            sorted.Sort((a, b) => a.startSec.CompareTo(b.startSec));

            for (int i = 0; i < sorted.Count; i++)
            {
                var p = sorted[i];
                float t0 = Mathf.Clamp01((float)(p.startSec / clipDuration));
                float t1 = Mathf.Clamp01((float)((p.startSec + p.durationSec) / clipDuration));
                if (t1 - t0 < 1e-6f) continue;
                c.AddKey(t0, p.progressValue);
                c.AddKey(t1, p.progressValue);
            }
        }

        c.AddKey(1f, 1f);
        ApplyTangents(c, joinStyle);
        return c;
    }

    /// <summary>
    /// 验证停顿列表是否合法。返回 null 表示合法，否则返回错误描述。
    /// 注意：RebuildFromScratch 内部已经会自动处理重叠/截断，此方法仅用于 UI 提前预警。
    /// </summary>
    public static string ValidatePauses(
        System.Collections.Generic.List<PauseSegment> pauses, double clipDuration)
    {
        if (pauses == null || pauses.Count == 0) return null;
        var sorted = new System.Collections.Generic.List<PauseSegment>(pauses);
        sorted.Sort((a, b) => a.startSec.CompareTo(b.startSec));
        for (int i = 0; i < sorted.Count; i++)
        {
            var p = sorted[i];
            if (p.durationSec <= 1e-4f)
                return $"停顿 #{i + 1} 时长必须 > 0";
        }
        return null;
    }
}
