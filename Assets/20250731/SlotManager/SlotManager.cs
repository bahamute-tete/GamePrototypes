using System.Collections.Generic;
using UnityEngine;

namespace SlotSystem
{
    /// <summary>
    /// 挂在角色根上,统一管理该角色的所有挂点。
    ///
    /// 物理子物体方案:为每个 slot 在目标骨骼下生成一个锚点空物体,挂载物 parent 到锚点,
    /// 蒙皮更新时随骨骼免费跟随,无每帧 CPU 开销。
    ///
    /// 同时支持 Humanoid(HumanBodyBones)与 Generic(bonePath)两种绑定。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class SlotManager : MonoBehaviour
    {
        [Tooltip("Humanoid 骨骼解析所需。留空时自动从子物体获取")]
        public Animator animator;

        [Tooltip("Generic 路径解析的根。留空时取 SkinnedMeshRenderer.rootBone,再退回自身")]
        public Transform skeletonRoot;

        public List<SlotDefinition> slots = new List<SlotDefinition>();

        [Header("Gizmo")]
        [Tooltip("Scene 视图中挂点三轴 gizmo 的长度(世界单位)")]
        public float gizmoSize = 0.05f;

        private readonly Dictionary<string, SlotDefinition> _byId = new Dictionary<string, SlotDefinition>();
        private readonly Dictionary<string, List<SlotAttachment>> _attachments = new Dictionary<string, List<SlotAttachment>>();

        // ---------- 生命周期 ----------

        private void Reset()
        {
            AutoAssignReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
            // 运行态自动重建;编辑态用右键菜单 Rebuild Anchors 手动触发,避免编辑器回调时机问题
            if (Application.isPlaying) RebuildAnchors();
            else BuildLookup();
        }

        private void OnValidate()
        {
            // 兜底:Inspector 新增列表元素时字段初值不生效,localScale 会是 (0,0,0)。
            // 这里把全零缩放纠正回 (1,1,1),让 Inspector 直接显示正确值。
            foreach (var def in slots)
                if (def != null && def.localScale == Vector3.zero)
                    def.localScale = Vector3.one;
        }

        // ---------- 引用自动绑定 ----------

        private void EnsureReferences()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (skeletonRoot == null) AutoAssignReferences();
        }

        private void AutoAssignReferences()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (skeletonRoot == null)
            {
                var smr = GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null && smr.rootBone != null) skeletonRoot = smr.rootBone;
                else if (animator != null) skeletonRoot = animator.transform;
                else skeletonRoot = transform;
            }
        }

        // ---------- 骨骼解析 ----------

        private Transform ResolveBone(SlotDefinition def, bool log = true)
        {
            // 主路径:已有有效引用直接用
            if (def.boneTransform != null) return def.boneTransform;

            Transform t = null;
            if (def.bindMode == SlotBindMode.HumanoidBone)
            {
                if (animator != null && animator.isHuman)
                    t = animator.GetBoneTransform(def.humanoidBone);
                else if (log)
                    Debug.LogWarning($"[SlotManager] slot '{def.slotId}' 用 HumanoidBone,但 Animator 缺失或非 Humanoid", this);
            }
            else
            {
                var root = skeletonRoot != null ? skeletonRoot : transform;
                if (!string.IsNullOrEmpty(def.bonePath))
                    t = root.Find(def.bonePath);
            }

            def.boneTransform = t; // 回写缓存
            return t;
        }

        /// <summary>编辑器用:静默解析骨骼(不打警告,供 Scene handle 每帧调用)。</summary>
        public Transform ResolveBoneQuiet(SlotDefinition def) => ResolveBone(def, false);

        /// <summary>清空已缓存引用并按元数据重新解析(模型重导入 / 换骨骼后调用)。</summary>
        [ContextMenu("Rebind Bones (clear cached refs)")]
        public void RebindBones()
        {
            foreach (var def in slots) def.boneTransform = null;
            RebuildAnchors();
        }

        // ---------- 锚点重建 ----------

        [ContextMenu("Rebuild Anchors")]
        public void RebuildAnchors()
        {
            EnsureReferences();
            BuildLookup();

            // 收集现有锚点(含未激活),只认本 manager 拥有的
            var existing = new List<SlotAnchor>();
            foreach (var a in GetComponentsInChildren<SlotAnchor>(true))
                if (a.Owner == this) existing.Add(a);

            var used = new HashSet<SlotAnchor>();

            foreach (var def in _byId.Values)
            {
                var bone = ResolveBone(def);
                if (bone == null)
                {
                    Debug.LogWarning($"[SlotManager] slot '{def.slotId}' 无法解析骨骼,跳过", this);
                    continue;
                }

                // 按 slotId 复用,没有则新建
                SlotAnchor anchor = existing.Find(a => a.SlotId == def.slotId && !used.Contains(a));
                if (anchor == null)
                {
                    var go = new GameObject($"[Slot] {def.slotId}");
                    anchor = go.AddComponent<SlotAnchor>();
                    anchor.Initialize(this, def.slotId);
                }
                used.Add(anchor);

                var at = anchor.transform;
                if (at.parent != bone) at.SetParent(bone, false); // 绑定变化时重新挂到正确骨骼
                at.localPosition = def.localPosition;
                at.localRotation = Quaternion.Euler(def.localEulerAngles);
                // 兜底:零缩放退回 1,避免锚点及其挂载物被缩成 0
                at.localScale = def.localScale == Vector3.zero ? Vector3.one : def.localScale;

                def.anchor = at;
            }

            // 回收孤儿锚点(对应 slot 已删除);含挂载物的保留不删,避免误删授权内容
            foreach (var a in existing)
            {
                if (used.Contains(a)) continue;
                if (a.transform.childCount > 0)
                {
                    Debug.LogWarning($"[SlotManager] 孤儿锚点 '{a.SlotId}' 含挂载物,保留不删除", a);
                    continue;
                }
                SafeDestroy(a.gameObject);
            }
        }

        private void BuildLookup()
        {
            _byId.Clear();
            foreach (var def in slots)
            {
                if (string.IsNullOrEmpty(def.slotId)) continue;
                if (_byId.ContainsKey(def.slotId))
                {
                    Debug.LogWarning($"[SlotManager] 重复 slotId '{def.slotId}',忽略后者", this);
                    continue;
                }
                _byId.Add(def.slotId, def);
            }
        }

        // ---------- 查询 ----------

        public bool HasSlot(string slotId) => _byId.ContainsKey(slotId);

        public SlotDefinition GetDefinition(string slotId)
            => _byId.TryGetValue(slotId, out var d) ? d : null;

        public Transform GetAnchor(string slotId)
        {
            var d = GetDefinition(slotId);
            return d != null ? d.anchor : null;
        }

        /// <summary>
        /// 由 骨骼 + slot 偏移 直接算出挂点世界位姿,不依赖锚点 GameObject 是否已生成。
        /// 供 Handover 等需要每帧采样挂点位姿(过渡混合)的场景使用。
        /// </summary>
        public bool TryGetSlotPose(string slotId, out Vector3 position, out Quaternion rotation)
        {
            var def = GetDefinition(slotId);
            if (def != null)
            {
                var bone = ResolveBoneQuiet(def);
                if (bone != null)
                {
                    position = bone.TransformPoint(def.localPosition);
                    rotation = bone.rotation * Quaternion.Euler(def.localEulerAngles);
                    return true;
                }
            }
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }

        /// <summary>
        /// 取挂点对应的骨骼及其本地偏移,供 carry 直接 parent 到骨骼(不依赖锚点物体)。
        /// </summary>
        public bool TryGetSlotBone(string slotId, out Transform bone,
            out Vector3 localPosition, out Quaternion localRotation)
        {
            var def = GetDefinition(slotId);
            if (def != null)
            {
                var b = ResolveBoneQuiet(def);
                if (b != null)
                {
                    bone = b;
                    localPosition = def.localPosition;
                    localRotation = Quaternion.Euler(def.localEulerAngles);
                    return true;
                }
            }
            bone = null;
            localPosition = Vector3.zero;
            localRotation = Quaternion.identity;
            return false;
        }

        public IEnumerable<string> SlotIds => _byId.Keys;

        // ---------- 挂载 ----------

        public SlotAttachment Attach(string slotId, GameObject go,
            AttachMode mode = AttachMode.Snap, bool destroyOnDetach = false)
        {
            if (go == null) return null;

            var anchor = GetAnchor(slotId);
            if (anchor == null)
            {
                Debug.LogWarning($"[SlotManager] Attach 失败,无锚点 '{slotId}'(运行态需先 RebuildAnchors)", this);
                return null;
            }

            var def = GetDefinition(slotId);
            if (def.occupancy == SlotOccupancy.Single) DetachAll(slotId);

            if (mode == AttachMode.Snap)
            {
                go.transform.SetParent(anchor, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                // 注意:不动 localScale,见下方"骨骼缩放"说明
            }
            else // PreserveWorld
            {
                go.transform.SetParent(anchor, true);
            }

            var att = new SlotAttachment
            {
                SlotId = slotId,
                Attached = go,
                Anchor = anchor,
                DestroyOnDetach = destroyOnDetach,
            };

            if (!_attachments.TryGetValue(slotId, out var list))
            {
                list = new List<SlotAttachment>();
                _attachments[slotId] = list;
            }
            list.Add(att);
            return att;
        }

        public void Detach(SlotAttachment att)
        {
            if (att == null || att._detached) return;
            att._detached = true;

            if (_attachments.TryGetValue(att.SlotId, out var list))
                list.Remove(att);

            if (att.Attached != null)
            {
                if (att.DestroyOnDetach) SafeDestroy(att.Attached);
                else att.Attached.transform.SetParent(null, true);
            }
        }

        public void DetachAll(string slotId)
        {
            if (!_attachments.TryGetValue(slotId, out var list)) return;
            var copy = list.ToArray(); // 复制避免遍历中修改
            foreach (var att in copy) Detach(att);
            list.Clear();
        }

        // ---------- 工具 ----------

        private static void SafeDestroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
