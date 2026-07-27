using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace SlotSystem.Timeline
{
    /// <summary>
    /// 挂载轨的混合器。每帧调和:
    ///  - clip 激活(权重 > Epsilon)→ 把目标物体挂到对应 slot 锚点;
    ///  - 离开所有 clip → 还原到首次挂载前缓存的父子关系与本地位姿。
    /// Timeline 不会自动还原被自定义轨搬动的场景物体,所以缓存 + 还原由这里负责
    /// (首次挂载时缓存,scrub 出去时还原,OnPlayableDestroy 时全部还原)。
    /// </summary>
    public class SlotAttachMixer : PlayableBehaviour
    {
        private const float Epsilon = 1e-4f;

        private struct Record
        {
            public Transform parent;
            public Vector3 localPos;
            public Quaternion localRot;
            public Vector3 localScale;
            public string slotId; // 当前挂在哪个 slot;null 表示已缓存但尚未挂载
        }

        private readonly Dictionary<GameObject, Record> _records = new Dictionary<GameObject, Record>();
        private readonly List<GameObject> _tmp = new List<GameObject>();

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var manager = playerData as SlotManager;
            int count = playable.GetInputCount();

            // 1) 收集本帧应挂载的物体(同一物体取权重最高的输入)
            var desired = new Dictionary<GameObject, (string slot, AttachMode mode, float w)>();
            for (int i = 0; i < count; i++)
            {
                float w = playable.GetInputWeight(i);
                if (w <= Epsilon) continue;

                var input = (ScriptPlayable<SlotAttachBehaviour>)playable.GetInput(i);
                var b = input.GetBehaviour();
                if (b == null || b.resolvedTarget == null || string.IsNullOrEmpty(b.slotId)) continue;

                var go = b.resolvedTarget;
                if (!desired.TryGetValue(go, out var cur) || w > cur.w)
                    desired[go] = (b.slotId, b.attachMode, w);
            }

            // 2) 应用挂载(没绑 SlotManager 则只能跳过挂载)
            if (manager != null)
            {
                foreach (var kv in desired)
                {
                    var go = kv.Key;
                    var slot = kv.Value.slot;
                    var anchor = manager.GetAnchor(slot);
                    if (anchor == null) continue; // 锚点未生成,需先 Rebuild Anchors

                    if (!_records.TryGetValue(go, out var rec))
                    {
                        // 首次挂载:缓存原始父子关系与本地位姿
                        var tr = go.transform;
                        rec = new Record
                        {
                            parent = tr.parent,
                            localPos = tr.localPosition,
                            localRot = tr.localRotation,
                            localScale = tr.localScale,
                            slotId = null,
                        };
                        _records[go] = rec;
                    }

                    if (rec.slotId != slot) // 首次或换了 slot 才重挂
                    {
                        AttachUnder(go.transform, anchor, kv.Value.mode);
                        rec.slotId = slot;
                        _records[go] = rec;
                    }
                }
            }

            // 3) 不再需要挂载的物体:还原
            if (_records.Count > 0)
            {
                _tmp.Clear();
                foreach (var go in _records.Keys) _tmp.Add(go);
                for (int i = 0; i < _tmp.Count; i++)
                {
                    var go = _tmp[i];
                    if (desired.ContainsKey(go)) continue;
                    Restore(go, _records[go]);
                    _records.Remove(go);
                }
            }
        }

        public override void OnPlayableDestroy(Playable playable)
        {
            // Timeline 停止 / 重建图时,全部还原
            foreach (var kv in _records)
                Restore(kv.Key, kv.Value);
            _records.Clear();
        }

        private static void AttachUnder(Transform t, Transform anchor, AttachMode mode)
        {
            if (mode == AttachMode.Snap)
            {
                t.SetParent(anchor, false);
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                // 不动 localScale,行为与 SlotManager.Attach 一致(注意骨骼缩放仍会继承)
            }
            else // PreserveWorld
            {
                t.SetParent(anchor, true);
            }
        }

        private static void Restore(GameObject go, Record rec)
        {
            if (go == null) return;
            var t = go.transform;
            t.SetParent(rec.parent, false);
            t.localPosition = rec.localPos;
            t.localRotation = rec.localRot;
            t.localScale = rec.localScale;
        }
    }
}
