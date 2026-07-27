using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace SlotSystem.Timeline
{
    /// <summary>
    /// Handover 轨混合器。每帧按 clip 内相位驱动被移动物体,全程位姿驱动、不 parent:
    ///  - Pickup:源点 → carry,世界位姿混合(可弧线 + 缓动);
    ///  - Carry:每帧采样 carry 挂点世界位姿驱动;
    ///  - Putdown:carry → 目标点,世界位姿混合。
    ///
    /// 为什么不 parent:Timeline 在编辑态 scrub 时,playhead 落在无 clip 的空白区
    /// 通常不再调用本 mixer 的 ProcessFrame,导致"离开 clip"那一刻无法被处理。
    /// 若 carry 期间 parent 到手,就会再也解不下来。改为全程位姿驱动后,不 parent ⇒
    /// 结构上不可能粘手。代价是 carry 可能有一帧滞后(高速时才可见),要消除可用
    /// LateUpdate 采样骨骼的跟随组件。
    ///
    /// 离开边界(若该帧被 tick)按方向收敛到 dest / source;OnPlayableDestroy 还原 authored 位姿。
    /// </summary>
    public class SlotHandoverMixer : PlayableBehaviour
    {
        private const float Epsilon = 1e-4f;

        private enum Mode { Restored, Driven }

        private struct Record
        {
            public Transform originalParent;
            public Vector3 originalLocalPos;
            public Quaternion originalLocalRot;
            public Vector3 originalLocalScale;
            public Mode mode;

            // 边界跟踪:用于离开 clip 时按方向收敛
            public SlotHandoverBehaviour lastBehaviour;
            public double lastTime;
            public double prevTime;
            public double lastDuration;
            public bool wasActive;
        }

        private readonly Dictionary<GameObject, Record> _records = new Dictionary<GameObject, Record>();
        private readonly List<GameObject> _tmp = new List<GameObject>();

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            int count = playable.GetInputCount();

            // 同一物体取权重最高的输入
            var desired = new Dictionary<GameObject, (SlotHandoverBehaviour b, double t, double dur, float w)>();
            for (int i = 0; i < count; i++)
            {
                float w = playable.GetInputWeight(i);
                if (w <= Epsilon) continue;

                var input = (ScriptPlayable<SlotHandoverBehaviour>)playable.GetInput(i);
                var b = input.GetBehaviour();
                if (b == null || b.item == null) continue;

                if (!desired.TryGetValue(b.item, out var cur) || w > cur.w)
                    desired[b.item] = (b, input.GetTime(), input.GetDuration(), w);
            }

            // 激活的物体:更新边界跟踪后驱动
            foreach (var kv in desired)
            {
                var go = kv.Key;
                EnsureCached(go);
                var rec = _records[go];
                rec.prevTime = rec.wasActive ? rec.lastTime : kv.Value.t; // 首个激活帧无方向
                rec.lastTime = kv.Value.t;
                rec.lastDuration = kv.Value.dur;
                rec.lastBehaviour = kv.Value.b;
                rec.wasActive = true;
                _records[go] = rec;

                Apply(go, kv.Value.b, kv.Value.t, kv.Value.dur);
            }

            // 离开边界:上帧激活、本帧未激活 → 收敛到 dest / source(此帧被 tick 时才会执行)
            if (_records.Count > 0)
            {
                _tmp.Clear();
                foreach (var go in _records.Keys) _tmp.Add(go);
                for (int i = 0; i < _tmp.Count; i++)
                {
                    var go = _tmp[i];
                    if (desired.ContainsKey(go)) continue;

                    var rec = _records[go];
                    if (!rec.wasActive) continue; // 已收敛过,保持(永久状态)

                    Settle(go);

                    rec = _records[go]; // Settle 改了 mode,重新读
                    rec.wasActive = false;
                    _records[go] = rec;
                }
            }
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            // 停止预览 / 图重建:还原到 authored 位姿
            _tmp.Clear();
            foreach (var go in _records.Keys) _tmp.Add(go);
            for (int i = 0; i < _tmp.Count; i++) Restore(_tmp[i]);
        }

        // ---------- 按相位驱动 ----------

        private void Apply(GameObject go, SlotHandoverBehaviour b, double time, double duration)
        {
            EnsureCached(go);

            if (!b.useCarry)
            {
                // 退化:整条 clip 直接 源 → 目标
                float s = duration > 0 ? (float)(time / duration) : 1f;
                BlendDriven(go, b.source, b.dest, b.pickupEase, b.arcHeight, s);
                return;
            }

            double pickupEnd = System.Math.Min(b.pickupDuration, duration);
            double putdownStart = System.Math.Max(duration - b.putdownDuration, pickupEnd);

            if (time < pickupEnd)
            {
                float s = pickupEnd > 0 ? (float)(time / pickupEnd) : 1f;
                BlendDriven(go, b.source, b.carry, b.pickupEase, b.arcHeight, s);
            }
            else if (time < putdownStart)
            {
                // Carry:每帧采样 carry 挂点世界位姿驱动(不 parent)
                if (b.carry.TryGetPose(out var pos, out var rot))
                    SetDriven(go, pos, rot);
            }
            else
            {
                double seg = duration - putdownStart;
                float s = seg > 0 ? (float)((time - putdownStart) / seg) : 1f;
                BlendDriven(go, b.carry, b.dest, b.putdownEase, b.arcHeight, s);
            }
        }

        /// <summary>离开 clip 时按方向收敛:往前 → dest,往后 → source。</summary>
        private void Settle(GameObject go)
        {
            var rec = _records[go];
            var b = rec.lastBehaviour;
            if (b == null) return;

            double dir = rec.lastTime - rec.prevTime;
            bool forward = System.Math.Abs(dir) > 1e-6
                ? dir > 0
                : rec.lastTime >= rec.lastDuration * 0.5;

            var target = forward ? b.dest : b.source;
            if (target.TryGetPose(out var pos, out var rot))
                SetDriven(go, pos, rot);
            // 解析不到就保持上一帧位姿;因为全程没 parent,绝不会粘手
        }

        private void BlendDriven(GameObject go, ResolvedPoseTarget a, ResolvedPoseTarget c,
            AnimationCurve ease, float arc, float rawS)
        {
            if (!a.TryGetPose(out var pa, out var ra)) return;
            if (!c.TryGetPose(out var pb, out var rb)) return;

            float s = Eval(ease, rawS);
            Vector3 pos = Vector3.Lerp(pa, pb, s);
            if (arc != 0f) pos += Vector3.up * (arc * Mathf.Sin(Mathf.PI * Mathf.Clamp01(rawS)));
            Quaternion rot = Quaternion.Slerp(ra, rb, s);

            SetDriven(go, pos, rot);
        }

        private void SetDriven(GameObject go, Vector3 pos, Quaternion rot)
        {
            var rec = _records[go];
            var t = go.transform;
            if (rec.mode != Mode.Driven)
            {
                // 回到原父级恢复 authored 缩放,再覆盖世界位姿
                t.SetParent(rec.originalParent, false);
                t.localScale = rec.originalLocalScale;
                rec.mode = Mode.Driven;
                _records[go] = rec;
            }
            t.position = pos;
            t.rotation = rot;
        }

        private void EnsureCached(GameObject go)
        {
            if (_records.ContainsKey(go)) return;
            var t = go.transform;
            _records[go] = new Record
            {
                originalParent = t.parent,
                originalLocalPos = t.localPosition,
                originalLocalRot = t.localRotation,
                originalLocalScale = t.localScale,
                mode = Mode.Restored,
            };
        }

        private void Restore(GameObject go)
        {
            if (go != null && _records.TryGetValue(go, out var rec))
            {
                var t = go.transform;
                t.SetParent(rec.originalParent, false);
                t.localPosition = rec.originalLocalPos;
                t.localRotation = rec.originalLocalRot;
                t.localScale = rec.originalLocalScale;
            }
            _records.Remove(go);
        }

        private static float Eval(AnimationCurve c, float rawS)
        {
            rawS = Mathf.Clamp01(rawS);
            return (c != null && c.length > 0) ? c.Evaluate(rawS) : rawS;
        }
    }
}
