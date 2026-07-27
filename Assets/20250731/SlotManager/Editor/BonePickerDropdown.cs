using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace SlotSystem.EditorTools
{
    /// <summary>
    /// 骨骼选择下拉(自带搜索框)。列出 skeletonRoot 下所有 Transform,
    /// 标签用相对路径以便区分重名骨骼;选中后回调返回该 Transform。
    /// </summary>
    public class BonePickerDropdown : AdvancedDropdown
    {
        private readonly List<Transform> _bones = new List<Transform>();
        private readonly List<string> _labels = new List<string>();
        private readonly Action<Transform> _onPick;

        public BonePickerDropdown(AdvancedDropdownState state, Transform root, Action<Transform> onPick)
            : base(state)
        {
            _onPick = onPick;
            minimumSize = new Vector2(260f, 420f);
            if (root != null) Collect(root, root);
        }

        private void Collect(Transform t, Transform root)
        {
            _bones.Add(t);
            _labels.Add(RelativePath(root, t));
            for (int i = 0; i < t.childCount; i++)
                Collect(t.GetChild(i), root);
        }

        private static string RelativePath(Transform root, Transform t)
        {
            if (t == root) return root.name;
            var parts = new List<string>();
            var cur = t;
            while (cur != null && cur != root) { parts.Add(cur.name); cur = cur.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var rootItem = new AdvancedDropdownItem("Bones");
            for (int i = 0; i < _bones.Count; i++)
                rootItem.AddChild(new AdvancedDropdownItem(_labels[i]) { id = i });
            return rootItem;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item.id >= 0 && item.id < _bones.Count)
                _onPick?.Invoke(_bones[item.id]);
        }
    }
}
