using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace SlotSystem.EditorTools
{
    [CustomEditor(typeof(SlotManager))]
    public class SlotManagerEditor : Editor
    {
        private SlotManager _mgr;
        private SerializedProperty _animator;
        private SerializedProperty _skeletonRoot;
        private SerializedProperty _gizmoSize;
        private SerializedProperty _slots;

        private int _sceneEditIndex = -1;            // 当前在 Scene 里编辑偏移的 slot
        private AdvancedDropdownState _dropdownState; // 骨骼下拉的持久状态(记住滚动/搜索)

        private void OnEnable()
        {
            _mgr = (SlotManager)target;
            _animator = serializedObject.FindProperty("animator");
            _skeletonRoot = serializedObject.FindProperty("skeletonRoot");
            _gizmoSize = serializedObject.FindProperty("gizmoSize");
            _slots = serializedObject.FindProperty("slots");
            _dropdownState = new AdvancedDropdownState();
        }

        private void OnDisable()
        {
            Tools.hidden = false; // 退出时恢复默认变换工具
        }

        // ---------------- Inspector ----------------

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_animator);
            EditorGUILayout.PropertyField(_skeletonRoot);
            EditorGUILayout.PropertyField(_gizmoSize);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Slots ({_slots.arraySize})", EditorStyles.boldLabel);

            int removeIndex = -1;
            for (int i = 0; i < _slots.arraySize; i++)
                DrawSlot(i, ref removeIndex);

            if (removeIndex >= 0)
            {
                _slots.DeleteArrayElementAtIndex(removeIndex);
                if (_sceneEditIndex == removeIndex) _sceneEditIndex = -1;
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Slot")) AddSlot();
                if (GUILayout.Button("Rebuild Anchors"))
                {
                    serializedObject.ApplyModifiedProperties();
                    _mgr.RebuildAnchors();
                    return;
                }
                if (GUILayout.Button("Rebind Bones"))
                {
                    serializedObject.ApplyModifiedProperties();
                    _mgr.RebindBones();
                    return;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSlot(int i, ref int removeIndex)
        {
            var elem = _slots.GetArrayElementAtIndex(i);
            var slotId = elem.FindPropertyRelative("slotId");
            var bindMode = elem.FindPropertyRelative("bindMode");
            var humanoidBone = elem.FindPropertyRelative("humanoidBone");
            var bonePath = elem.FindPropertyRelative("bonePath");
            var boneTransform = elem.FindPropertyRelative("boneTransform");
            var localPosition = elem.FindPropertyRelative("localPosition");
            var localEulerAngles = elem.FindPropertyRelative("localEulerAngles");
            var localScale = elem.FindPropertyRelative("localScale");
            var occupancy = elem.FindPropertyRelative("occupancy");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(slotId, new GUIContent($"Slot {i}"));
                    bool editing = _sceneEditIndex == i;
                    bool next = GUILayout.Toggle(editing, "Edit", "Button", GUILayout.Width(50f));
                    if (next != editing)
                    {
                        _sceneEditIndex = next ? i : -1;
                        SceneView.RepaintAll();
                    }
                    if (GUILayout.Button("X", GUILayout.Width(22f)))
                        removeIndex = i;
                }

                // 绑定方式切换时清掉旧解析引用,交给新模式的元数据重新解析
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(bindMode);
                if (EditorGUI.EndChangeCheck())
                    boneTransform.objectReferenceValue = null;

                var mode = (SlotBindMode)bindMode.enumValueIndex;
                if (mode == SlotBindMode.HumanoidBone)
                    DrawHumanoidPicker(humanoidBone, boneTransform);
                else
                    DrawBonePathPicker(i, bonePath, boneTransform);

                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(boneTransform, new GUIContent("Resolved Bone"));

                EditorGUILayout.PropertyField(localPosition);
                EditorGUILayout.PropertyField(localEulerAngles);
                EditorGUILayout.PropertyField(localScale);
                EditorGUILayout.PropertyField(occupancy);
            }
        }

        private void DrawHumanoidPicker(SerializedProperty humanoidBone, SerializedProperty boneTransform)
        {
            var anim = _mgr.animator;
            if (anim == null || !anim.isHuman)
            {
                EditorGUILayout.HelpBox("Animator 缺失或非 Humanoid。请改用 BonePath 绑定。", MessageType.Warning);
                EditorGUILayout.PropertyField(humanoidBone);
                return;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(humanoidBone);
            bool changed = EditorGUI.EndChangeCheck();

            var bone = (HumanBodyBones)humanoidBone.enumValueIndex; // HumanBodyBones 顺序即取值,二者一致
            var t = anim.GetBoneTransform(bone);
            if (t == null)
                EditorGUILayout.HelpBox("该骨骼未在 avatar 中映射。", MessageType.Warning);

            // 改动时或解析引用为空时自动回填,保证 Resolved Bone 与所选一致
            if (changed || (boneTransform.objectReferenceValue == null && t != null))
                boneTransform.objectReferenceValue = t;
        }

        private void DrawBonePathPicker(int index, SerializedProperty bonePath, SerializedProperty boneTransform)
        {
            var root = _mgr.skeletonRoot != null ? _mgr.skeletonRoot : _mgr.transform;

            // 带搜索的下拉
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Bone Path");
                string shown = string.IsNullOrEmpty(bonePath.stringValue) ? "(pick bone…)" : bonePath.stringValue;
                var rect = GUILayoutUtility.GetRect(new GUIContent(shown), EditorStyles.popup);
                if (GUI.Button(rect, shown, EditorStyles.popup))
                {
                    var dd = new BonePickerDropdown(_dropdownState, root, picked =>
                    {
                        // 回调在后续事件触发,此时不能用上面那些 SerializedProperty,直接改 def
                        Undo.RecordObject(_mgr, "Pick Slot Bone");
                        var def = _mgr.slots[index];
                        def.boneTransform = picked;
                        def.bonePath = ComputeBonePath(root, picked);
                        EditorUtility.SetDirty(_mgr);
                        Repaint();
                    });
                    dd.Show(rect);
                }
            }

            // 也支持直接拖入(同步,走 SerializedProperty)
            EditorGUI.BeginChangeCheck();
            var dragged = (Transform)EditorGUILayout.ObjectField(
                "or drag bone", boneTransform.objectReferenceValue, typeof(Transform), true);
            if (EditorGUI.EndChangeCheck())
            {
                boneTransform.objectReferenceValue = dragged;
                bonePath.stringValue = ComputeBonePath(root, dragged);
            }
        }

        private void AddSlot()
        {
            int idx = _slots.arraySize;
            _slots.arraySize++;
            var elem = _slots.GetArrayElementAtIndex(idx);
            // 显式初始化,绕开 Inspector 新增元素字段初值不生效的坑
            elem.FindPropertyRelative("slotId").stringValue = "Slot" + idx;
            elem.FindPropertyRelative("bindMode").enumValueIndex = (int)SlotBindMode.BonePath;
            elem.FindPropertyRelative("humanoidBone").enumValueIndex = (int)HumanBodyBones.Hips;
            elem.FindPropertyRelative("bonePath").stringValue = "";
            elem.FindPropertyRelative("boneTransform").objectReferenceValue = null;
            elem.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
            elem.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
            elem.FindPropertyRelative("localScale").vector3Value = Vector3.one;
            elem.FindPropertyRelative("occupancy").enumValueIndex = (int)SlotOccupancy.Single;
        }

        // ---------------- Scene handle ----------------

        private void OnSceneGUI()
        {
            Tools.hidden = _sceneEditIndex >= 0; // 编辑 slot 时隐藏物体本身的变换 gizmo,避免叠加误操作

            if (_sceneEditIndex < 0 || _sceneEditIndex >= _mgr.slots.Count) return;
            var def = _mgr.slots[_sceneEditIndex];
            var bone = _mgr.ResolveBoneQuiet(def);
            if (bone == null) return;

            // 由 骨骼 * 本地偏移 推出世界位姿(不依赖锚点是否已生成)
            Vector3 worldPos = bone.TransformPoint(def.localPosition);
            Quaternion worldRot = bone.rotation * Quaternion.Euler(def.localEulerAngles);

            EditorGUI.BeginChangeCheck();
            Vector3 newPos = worldPos;
            Quaternion newRot = worldRot;

            // 跟随工具栏:W=移动手柄,E=旋转手柄
            if (Tools.current == Tool.Rotate)
                newRot = Handles.RotationHandle(worldRot, worldPos);
            else
                newPos = Handles.PositionHandle(worldPos, worldRot);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_mgr, "Edit Slot Offset");
                // 世界位姿换算回骨骼本地(与 Unity 设置子物体 local 的算法一致)
                def.localPosition = bone.InverseTransformPoint(newPos);
                def.localEulerAngles = (Quaternion.Inverse(bone.rotation) * newRot).eulerAngles;

                // 若锚点已存在,直接更新做实时预览(无需整体重建)
                if (def.anchor != null)
                {
                    def.anchor.localPosition = def.localPosition;
                    def.anchor.localRotation = Quaternion.Euler(def.localEulerAngles);
                }

                EditorUtility.SetDirty(_mgr);
                Repaint();
            }

            Handles.color = Color.white;
            Handles.Label(worldPos, $"{def.slotId}  (W 移动 / E 旋转)");
        }

        // ---------------- utils ----------------

        private static string ComputeBonePath(Transform root, Transform bone)
        {
            if (root == null || bone == null) return "";
            if (bone == root) return ""; // 绑根骨骼时只能靠直接引用,路径回退无法表达
            var parts = new List<string>();
            var cur = bone;
            while (cur != null && cur != root) { parts.Add(cur.name); cur = cur.parent; }
            if (cur != root)
            {
                Debug.LogWarning("[SlotManagerEditor] 选中骨骼不在 skeletonRoot 之下,无法计算 bonePath。");
                return "";
            }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
