using UnityEngine;

namespace SlotSystem
{
    /// <summary>
    /// 由 SlotManager 在目标骨骼下生成的锚点标记组件。
    /// 作用是在重建时按 slotId + owner 精确匹配已有锚点对象,
    /// 复用而非重复生成,并能回收对应 slot 已删除的孤儿锚点。
    /// </summary>
    [DisallowMultipleComponent]
    public class SlotAnchor : MonoBehaviour
    {
        [SerializeField] private SlotManager owner;
        [SerializeField] private string slotId;

        public SlotManager Owner => owner;
        public string SlotId => slotId;

        public void Initialize(SlotManager owner, string slotId)
        {
            this.owner = owner;
            this.slotId = slotId;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            float size = owner != null ? owner.gizmoSize : 0.05f;
            Transform t = transform;
            Vector3 p = t.position;

            // 三轴(transform.right/up/forward 为单位向量,长度不受骨骼缩放影响)
            Gizmos.color = Color.red;   Gizmos.DrawLine(p, p + t.right   * size);
            Gizmos.color = Color.green; Gizmos.DrawLine(p, p + t.up      * size);
            Gizmos.color = Color.blue;  Gizmos.DrawLine(p, p + t.forward * size);

            // 原点小球
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(p, size * 0.25f);

            // slotId 标签
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(p + t.up * size * 1.2f, slotId);
        }
#endif
    }
}
