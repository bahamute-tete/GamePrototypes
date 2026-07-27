using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpiralPlacer
{
    public class SpiralPlacerWindow : EditorWindow
    {
        // ── Spiral params ─────────────────────────────────────────────────────
        private SpiralParams _params = new SpiralParams();

        // ── Target objects ────────────────────────────────────────────────────
        private List<Transform> _targets = new List<Transform>();
        private Vector2 _scrollTargets;

        // ── UI state ──────────────────────────────────────────────────────────
        private Vector2 _scroll;
        private bool    _showParams    = true;
        private bool    _showTargets   = true;
        private bool    _previewActive = true;

        // ── Preview cache ─────────────────────────────────────────────────────
        private Vector3[] _previewLine;
        private Vector3[] _previewPoints;
        private bool      _dirty = true;

        // ── Drag-drop temp ────────────────────────────────────────────────────
        private readonly List<Transform> _dragBuffer = new List<Transform>();

        // ─────────────────────────────────────────────────────────────────────
        [MenuItem("Tools/Spiral Placer")]
        public static void Open()
        {
            var w = GetWindow<SpiralPlacerWindow>("Spiral Placer");
            w.minSize = new Vector2(320, 480);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Rebuild();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  GUI
        // ══════════════════════════════════════════════════════════════════════
        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();
            GUILayout.Space(6);
            DrawSpiralParams();
            GUILayout.Space(6);
            DrawTargetList();
            GUILayout.Space(6);
            DrawActions();

            EditorGUILayout.EndScrollView();

            // Repaint Scene on any change
            if (GUI.changed)
            {
                _dirty = true;
                SceneView.RepaintAll();
            }
        }

        // ── Header ────────────────────────────────────────────────────────────
        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Spiral Placer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("把选中的 Transform 沿螺旋线重新定位", EditorStyles.miniLabel);

            bool prev = _previewActive;
            _previewActive = EditorGUILayout.Toggle("Scene Preview", _previewActive);
            if (_previewActive != prev) SceneView.RepaintAll();
        }

        // ── Spiral params ─────────────────────────────────────────────────────
        private void DrawSpiralParams()
        {
            _showParams = EditorGUILayout.BeginFoldoutHeaderGroup(_showParams, "Spiral Parameters");
            if (_showParams)
            {
                EditorGUI.indentLevel++;

                // Shape
                EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);
                FloatField("Revolutions",  ref _params.revolutions, 0.25f, 100f);
                IntField  ("Divisions",    ref _params.divisions,   1, 360);

                EditorGUILayout.Space(4);

                // Radius
                EditorGUILayout.LabelField("Radius", EditorStyles.boldLabel);
                FloatField("Start Radius", ref _params.startRadius, 0f, 1000f);
                FloatField("End Radius",   ref _params.endRadius,   0f, 1000f);
                _params.radiusMode = (SpiralRadiusMode)EditorGUILayout.EnumPopup("Radius Mode", _params.radiusMode);

                EditorGUILayout.Space(4);

                // Height
                EditorGUILayout.LabelField("Height (3D)", EditorStyles.boldLabel);
                FloatField("Height", ref _params.height, -1000f, 1000f);

                EditorGUILayout.Space(4);

                // Angle
                EditorGUILayout.LabelField("Angle", EditorStyles.boldLabel);
                FloatField("Start Angle", ref _params.startAngleDeg, -360f, 360f);

                EditorGUILayout.Space(4);

                // Orientation & Center
                EditorGUILayout.LabelField("Orientation & Position", EditorStyles.boldLabel);
                _params.orientation = (SpiralOrientation)EditorGUILayout.EnumPopup("Orientation", _params.orientation);
                _params.center      = EditorGUILayout.Vector3Field("Center", _params.center);

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ── Target list ───────────────────────────────────────────────────────
        private void DrawTargetList()
        {
            _showTargets = EditorGUILayout.BeginFoldoutHeaderGroup(_showTargets, $"Target Transforms  [{_targets.Count}]");
            if (_showTargets)
            {
                EditorGUI.indentLevel++;

                // Buttons row
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+ Add Selected", EditorStyles.miniButton))
                    AddSelected();
                if (GUILayout.Button("Clear", EditorStyles.miniButton))
                { _targets.Clear(); _dirty = true; }
                EditorGUILayout.EndHorizontal();

                // Drag-drop zone
                DrawDragDropZone();

                // List
                _scrollTargets = EditorGUILayout.BeginScrollView(_scrollTargets,
                    GUILayout.MaxHeight(Mathf.Clamp(_targets.Count * 20f + 10f, 40f, 200f)));

                for (int i = 0; i < _targets.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    var t = (Transform)EditorGUILayout.ObjectField(_targets[i], typeof(Transform), true);
                    if (t != _targets[i]) { _targets[i] = t; _dirty = true; }

                    if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20)))
                    {
                        _targets.RemoveAt(i);
                        _dirty = true;
                        GUILayout.EndHorizontal();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawDragDropZone()
        {
            Rect dropRect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drop GameObjects here", EditorStyles.helpBox);

            Event e = Event.current;
            if (dropRect.Contains(e.mousePosition))
            {
                if (e.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    e.Use();
                }
                else if (e.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (Object obj in DragAndDrop.objectReferences)
                    {
                        if (obj is GameObject go)
                        {
                            if (!_targets.Contains(go.transform))
                            { _targets.Add(go.transform); _dirty = true; }
                        }
                    }
                    e.Use();
                }
            }
        }

        // ── Actions ───────────────────────────────────────────────────────────
        private void DrawActions()
        {
            bool canApply = _targets.Count > 0;
            EditorGUI.BeginDisabledGroup(!canApply);

            if (GUILayout.Button("Apply to Transforms", GUILayout.Height(32)))
                Apply();

            EditorGUI.EndDisabledGroup();

            if (!canApply)
                EditorGUILayout.HelpBox("请先在 Target Transforms 中添加对象。", MessageType.Info);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Logic
        // ══════════════════════════════════════════════════════════════════════
        private void AddSelected()
        {
            foreach (var go in Selection.gameObjects)
            {
                if (!_targets.Contains(go.transform))
                {
                    _targets.Add(go.transform);
                    _dirty = true;
                }
            }
        }

        private void Apply()
        {
            if (_targets.Count == 0) return;

            Undo.SetCurrentGroupName("Spiral Place Transforms");
            int group = Undo.GetCurrentGroup();

            Vector3[] pts = SpiralMath.Sample(_params, _targets.Count);

            for (int i = 0; i < _targets.Count; i++)
            {
                if (_targets[i] == null) continue;
                Undo.RecordObject(_targets[i], "Move Transform");
                _targets[i].position = pts[i];
            }

            Undo.CollapseUndoOperations(group);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Preview
        // ══════════════════════════════════════════════════════════════════════
        private void Rebuild()
        {
            _previewLine   = SpiralMath.PreviewLine(_params, 128);
            _previewPoints = _targets.Count > 0
                ? SpiralMath.Sample(_params, _targets.Count)
                : System.Array.Empty<Vector3>();
            _dirty = false;
        }

        private void OnSceneGUI(SceneView sv)
        {
            if (!_previewActive) return;
            if (_dirty) Rebuild();

            Handles.color = new Color(0.3f, 0.9f, 1f, 0.6f);

            // Draw spiral line
            if (_previewLine != null && _previewLine.Length > 1)
                Handles.DrawPolyLine(_previewLine);

            // Draw point markers
            if (_previewPoints != null)
            {
                Handles.color = new Color(1f, 0.8f, 0.2f, 0.9f);
                float size = HandleUtility.GetHandleSize(_params.center) * 0.06f;
                foreach (var pt in _previewPoints)
                    Handles.DotHandleCap(0, pt, Quaternion.identity, size, EventType.Repaint);
            }

            // Center handle (drag to move)
            EditorGUI.BeginChangeCheck();
            Vector3 newCenter = Handles.PositionHandle(_params.center, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                _params.center = newCenter;
                _dirty = true;
                Repaint();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════════════════
        private void FloatField(string label, ref float val, float min, float max)
        {
            float v = EditorGUILayout.FloatField(label, val);
            if (v != val) { val = Mathf.Clamp(v, min, max); _dirty = true; }
        }

        private void IntField(string label, ref int val, int min, int max)
        {
            int v = EditorGUILayout.IntField(label, val);
            if (v != val) { val = Mathf.Clamp(v, min, max); _dirty = true; }
        }
    }
}
