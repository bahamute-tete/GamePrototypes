using UnityEditor;
using UnityEngine;

namespace SpiralPlacer
{
    [CustomEditor(typeof(SpiralPlacer))]
    public class SpiralPlacerEditor : Editor
    {
        private SerializedProperty _spiral;
        private SerializedProperty _includeInactive;
        private SerializedProperty _reverseOrder;
        private SerializedProperty _autoApply;

        private bool _showPreview = true;

        private void OnEnable()
        {
            _spiral          = serializedObject.FindProperty("spiral");
            _includeInactive = serializedObject.FindProperty("includeInactive");
            _reverseOrder    = serializedObject.FindProperty("reverseOrder");
            _autoApply       = serializedObject.FindProperty("autoApply");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Inspector
        // ══════════════════════════════════════════════════════════════════════
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var placer = (SpiralPlacer)target;

            EditorGUILayout.LabelField("Spiral Placer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("子物体将按 sibling index 沿螺旋线排布", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            // ── Child info ────────────────────────────────────────────────────
            int total      = placer.transform.childCount;
            int affected   = placer.CollectChildren().Count;
            EditorGUILayout.HelpBox($"子物体: {affected} / {total}  (受影响 / 总数)",
                affected > 0 ? MessageType.None : MessageType.Warning);

            EditorGUILayout.Space(4);

            // ── Spiral params ─────────────────────────────────────────────────
            EditorGUILayout.PropertyField(_spiral, true);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_includeInactive);
            EditorGUILayout.PropertyField(_reverseOrder);
            EditorGUILayout.PropertyField(_autoApply);

            EditorGUILayout.Space(8);
            _showPreview = EditorGUILayout.Toggle("Scene Gizmo", _showPreview);

            EditorGUILayout.Space(8);

            // ── Buttons ───────────────────────────────────────────────────────
            using (new EditorGUI.DisabledScope(affected == 0))
            {
                if (GUILayout.Button("Apply Now", GUILayout.Height(28)))
                    ApplyWithUndo(placer);
            }

            using (new EditorGUI.DisabledScope(total == 0))
            {
                if (GUILayout.Button("Reverse Children Order", EditorStyles.miniButton))
                    ReverseChildren(placer);
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Operations
        // ══════════════════════════════════════════════════════════════════════
        private static void ApplyWithUndo(SpiralPlacer placer)
        {
            var children = placer.CollectChildren();
            if (children.Count == 0) return;

            Undo.SetCurrentGroupName("Spiral Place Children");
            int group = Undo.GetCurrentGroup();

            foreach (var c in children)
                Undo.RecordObject(c, "Move Child");

            placer.Apply();
            Undo.CollapseUndoOperations(group);
        }

        private static void ReverseChildren(SpiralPlacer placer)
        {
            Undo.RegisterFullObjectHierarchyUndo(placer.gameObject, "Reverse Children Order");
            int n = placer.transform.childCount;
            for (int i = n - 1; i >= 0; i--)
                placer.transform.GetChild(i).SetSiblingIndex(n - 1 - i);
            if (placer.autoApply) placer.Apply();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Scene View
        // ══════════════════════════════════════════════════════════════════════
        private void OnSceneGUI()
        {
            if (!_showPreview) return;

            var placer = (SpiralPlacer)target;
            var p      = placer.spiral;

            // 在父物体局部空间绘制
            using (new Handles.DrawingScope(placer.transform.localToWorldMatrix))
            {
                // 螺旋曲线
                Handles.color = new Color(0.3f, 0.9f, 1f, 0.75f);
                Vector3[] line = SpiralMath.PreviewLine(p, 128);
                if (line.Length > 1) Handles.DrawAAPolyLine(2f, line);

                // 子物体落点
                int count = placer.CollectChildren().Count;
                if (count > 0)
                {
                    Handles.color = new Color(1f, 0.8f, 0.2f, 0.9f);
                    Vector3[] pts = SpiralMath.Sample(p, count);
                    // 用局部位置计算 size 的世界基准点
                    Vector3 worldRef = placer.transform.TransformPoint(p.center);
                    float baseSize   = HandleUtility.GetHandleSize(worldRef) * 0.06f
                                       / Mathf.Max(placer.transform.lossyScale.x, 0.0001f);
                    foreach (var pt in pts)
                        Handles.DotHandleCap(0, pt, Quaternion.identity, baseSize, EventType.Repaint);

                    // 起点用绿色，终点用红色（识别方向）
                    Handles.color = Color.green;
                    Handles.SphereHandleCap(0, pts[0], Quaternion.identity, baseSize * 2f, EventType.Repaint);
                    Handles.color = Color.red;
                    Handles.SphereHandleCap(0, pts[pts.Length - 1], Quaternion.identity, baseSize * 2f, EventType.Repaint);
                }
            }

            // Center 拖拽手柄（世界空间）
            EditorGUI.BeginChangeCheck();
            Vector3 worldCenter    = placer.transform.TransformPoint(p.center);
            Vector3 newWorldCenter = Handles.PositionHandle(worldCenter, placer.transform.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(placer, "Move Spiral Center");
                p.center = placer.transform.InverseTransformPoint(newWorldCenter);
                if (placer.autoApply) placer.Apply();
            }
        }
    }
}
