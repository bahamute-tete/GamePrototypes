using System.Collections.Generic;
using UnityEngine;

namespace SpiralPlacer
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Layout/Spiral Placer")]
    public class SpiralPlacer : MonoBehaviour
    {
        [Tooltip("螺旋线参数（在父物体的局部空间中计算）")]
        public SpiralParams spiral = new SpiralParams();

        [Header("Children")]
        [Tooltip("是否包括 inactive 的子物体")]
        public bool includeInactive = true;

        [Tooltip("反转排列方向：勾选后第一个子物体放在螺旋末端")]
        public bool reverseOrder = false;

        [Header("Behaviour")]
        [Tooltip("修改参数时自动刷新位置；关闭后需手动点 Apply")]
        public bool autoApply = true;

        // ──────────────────────────────────────────────────────────────────────
        public List<Transform> CollectChildren()
        {
            var list = new List<Transform>(transform.childCount);
            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i);
                if (!includeInactive && !c.gameObject.activeSelf) continue;
                list.Add(c);
            }
            return list;
        }

        /// <summary>
        /// 把子物体放到螺旋线上。不记录 Undo —— Inspector 按钮会单独包一层 Undo。
        /// </summary>
        public void Apply()
        {
            var children = CollectChildren();
            if (children.Count == 0) return;

            Vector3[] pts = SpiralMath.Sample(spiral, children.Count);

            for (int i = 0; i < children.Count; i++)
            {
                int targetIndex = reverseOrder ? (children.Count - 1 - i) : i;
                children[i].localPosition = pts[targetIndex];
            }
        }

        // ── Auto-apply on parameter change ────────────────────────────────────
        private void OnValidate()
        {
            if (!autoApply) return;

#if UNITY_EDITOR
            // OnValidate 在反序列化期间触发，直接改 transform 会被警告；
            // 用 delayCall 推迟一帧
            UnityEditor.EditorApplication.delayCall += DelayedApply;
#endif
        }

#if UNITY_EDITOR
        private void DelayedApply()
        {
            UnityEditor.EditorApplication.delayCall -= DelayedApply;
            if (this == null) return;          // 组件已被销毁
            if (!autoApply) return;
            Apply();
        }
#endif

        // 子物体增删时也刷新一次（仅 Editor）
#if UNITY_EDITOR
        private int _lastChildCount = -1;
        private void Update()
        {
            if (Application.isPlaying) return;
            if (!autoApply)            return;
            if (transform.childCount == _lastChildCount) return;
            _lastChildCount = transform.childCount;
            Apply();
        }
#endif
    }
}
