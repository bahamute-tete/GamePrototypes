using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 步态速度分析工具。
///
/// 解决问题：原地循环走路动画没有 Root Motion，无法直接读取 averageSpeed。
/// 用这个工具：用户提供角色 GameObject + 走路 AnimationClip + 双脚 Transform，
/// 工具在 Editor 里采样动画，通过【脚相对身体根的 Y 振幅找周期，Z 振幅算步长】，
/// 估算出"等效行走速度"（米/秒），用于配置 SplineCurveMoveClip 的时长。
///
/// 支持 Humanoid 自动识别脚和 Generic 手动指定脚。
/// </summary>
public class GaitSpeedAnalyzerWindow : EditorWindow
{
    // ─── 输入 ───
    private GameObject characterGo;
    private AnimationClip walkClip;
    private Transform leftFoot;
    private Transform rightFoot;
    private Transform rootRef;          // hips/pelvis，用于 InverseTransformPoint
    private bool autoDetectedFromAvatar;

    private enum ForwardAxis { PositiveZ, NegativeZ, PositiveX, NegativeX }
    private ForwardAxis forwardAxis = ForwardAxis.PositiveZ;

    private int sampleCount = 240;

    // ─── 输出 ───
    private bool hasResult;
    private float resultSpeed;          // m/s
    private float resultStride;         // m
    private float resultStrideDuration; // s
    private int   resultCycles;         // 检测到的完整步态周期数

    // 预览数据（左脚相对 root 的 y 和 forward 分量）
    private float[] lfY, lfFwd;
    private float[] rfY, rfFwd;
    private List<int> lfMinima, rfMinima;
    private string warning;

    // ─── UI ───
    private Vector2 scroll;

    [MenuItem("Tools/Spline/Gait Speed Analyzer")]
    public static void Open()
    {
        var w = GetWindow<GaitSpeedAnalyzerWindow>("Gait Speed Analyzer");
        w.minSize = new Vector2(380, 540);
        w.Show();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("步态速度分析", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "用于原地循环动画。通过【脚最低点找步态周期】 + 【脚相对身体的前后位移幅度算步长】，\n" +
            "推算等效行走速度（m/s）。把结果填到 SplineCurveMoveClip 即可。",
            MessageType.None);

        EditorGUILayout.Space(4);
        DrawInputs();
        EditorGUILayout.Space(4);
        DrawAnalyzeButton();

        if (!string.IsNullOrEmpty(warning))
        {
            EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        if (hasResult)
        {
            EditorGUILayout.Space(8);
            DrawResults();
            EditorGUILayout.Space(8);
            DrawPreview();
            EditorGUILayout.Space(8);
            DrawApplyButtons();
        }

        EditorGUILayout.EndScrollView();
    }

    // ─────────────────────────────────────────
    //                Inputs UI
    // ─────────────────────────────────────────
    private void DrawInputs()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUI.BeginChangeCheck();
        characterGo = (GameObject)EditorGUILayout.ObjectField("角色 GameObject", characterGo, typeof(GameObject), true);
        walkClip    = (AnimationClip)EditorGUILayout.ObjectField("走路动画", walkClip, typeof(AnimationClip), false);
        if (EditorGUI.EndChangeCheck())
        {
            hasResult = false;
            warning = null;
        }

        // Humanoid 自动检测按钮
        var animator = characterGo != null ? characterGo.GetComponent<Animator>() : null;
        bool isHumanoid = animator != null && animator.isHuman && animator.avatar != null;

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(!isHumanoid);
        if (GUILayout.Button(
                new GUIContent(
                    isHumanoid ? "从 Humanoid Avatar 自动填充双脚" : "（角色非 Humanoid，需手动指定双脚）",
                    "Humanoid Rig 可一键从 Avatar 取出 LeftFoot/RightFoot/Hips Transform"),
                GUILayout.Height(20f)))
        {
            AutoFillFromHumanoid(animator);
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        if (autoDetectedFromAvatar)
        {
            EditorGUILayout.LabelField("✓ 已从 Humanoid Avatar 填充", EditorStyles.miniLabel);
        }

        // 双脚和 root 手动字段
        EditorGUI.BeginChangeCheck();
        leftFoot  = (Transform)EditorGUILayout.ObjectField("左脚 Transform",  leftFoot,  typeof(Transform), true);
        rightFoot = (Transform)EditorGUILayout.ObjectField("右脚 Transform",  rightFoot, typeof(Transform), true);
        rootRef   = (Transform)EditorGUILayout.ObjectField("身体根 Transform", rootRef,  typeof(Transform), true);
        if (EditorGUI.EndChangeCheck()) { hasResult = false; autoDetectedFromAvatar = false; }

        EditorGUILayout.LabelField(
            "身体根用 hips/pelvis（脚相对它的位置）。如果留空，会用角色 GameObject 的 Transform。",
            EditorStyles.miniLabel);

        EditorGUI.BeginChangeCheck();
        forwardAxis = (ForwardAxis)EditorGUILayout.EnumPopup(
            new GUIContent("前进方向（局部）", "角色 forward 朝向。Unity 默认 +Z。"),
            forwardAxis);
        sampleCount = EditorGUILayout.IntSlider("采样点数", sampleCount, 60, 1000);
        if (EditorGUI.EndChangeCheck()) hasResult = false;

        EditorGUILayout.EndVertical();
    }

    private void AutoFillFromHumanoid(Animator animator)
    {
        leftFoot  = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        rootRef   = animator.GetBoneTransform(HumanBodyBones.Hips);
        autoDetectedFromAvatar = leftFoot != null && rightFoot != null && rootRef != null;
        if (!autoDetectedFromAvatar)
            warning = "Humanoid Avatar 缺少 LeftFoot / RightFoot / Hips 骨骼绑定。请检查 Avatar 配置。";
        else
            warning = null;
        hasResult = false;
    }

    private void DrawAnalyzeButton()
    {
        bool canAnalyze = characterGo != null && walkClip != null
                          && leftFoot != null && rightFoot != null;
        EditorGUI.BeginDisabledGroup(!canAnalyze);
        if (GUILayout.Button("▶ 分析", GUILayout.Height(28f)))
        {
            Analyze();
        }
        EditorGUI.EndDisabledGroup();
        if (!canAnalyze)
            EditorGUILayout.LabelField("需要：角色、动画、左脚、右脚", EditorStyles.miniLabel);
    }

    // ─────────────────────────────────────────
    //                Core analysis
    // ─────────────────────────────────────────
    private void Analyze()
    {
        warning = null;
        hasResult = false;

        Transform root = rootRef != null ? rootRef : characterGo.transform;
        int n = sampleCount;
        float dur = walkClip.length;
        float dt = dur / (n - 1);

        lfY   = new float[n]; lfFwd  = new float[n];
        rfY   = new float[n]; rfFwd  = new float[n];

        AnimationMode.StartAnimationMode();
        try
        {
            for (int i = 0; i < n; i++)
            {
                float t = i * dt;
                AnimationMode.SampleAnimationClip(characterGo, walkClip, t);
                Vector3 lfLocal = root.InverseTransformPoint(leftFoot.position);
                Vector3 rfLocal = root.InverseTransformPoint(rightFoot.position);
                lfY[i]  = lfLocal.y;
                rfY[i]  = rfLocal.y;
                lfFwd[i] = ProjectForward(lfLocal);
                rfFwd[i] = ProjectForward(rfLocal);
            }
        }
        finally
        {
            AnimationMode.StopAnimationMode();
        }

        // 在 Y 轨迹上找局部最小值（脚最贴地的时刻）
        // 用 window-based 比较避免噪声 + 限制最小间距过滤同一谷的多个采样点
        int minSpacing = Mathf.Max(3, n / 30); // 至少间隔 N/30 个采样点 ≈ 一秒内最多 30 次最小值
        lfMinima = FindLocalMinima(lfY, 4, minSpacing);
        rfMinima = FindLocalMinima(rfY, 4, minSpacing);

        // ────────────────────────────────────────────────────────────────
        // 周期计算关键点：
        //   一只脚在一个完整步态周期内【只踩地一次】 →
        //   单脚相邻最低点之间的时间差 = 【跨步周期 stride period】（不是 step！）
        //
        // 容易混淆：
        //   - step period   = 左脚踩地→右脚踩地的时间 = stride / 2
        //   - stride period = 左脚踩地→左脚再踩地的时间 = 一个完整 gait cycle
        //
        // 我们要的是 stride period，所以单脚最低点间隔【直接用】，不要 ×2。
        // ────────────────────────────────────────────────────────────────
        double leftStrideDur  = AvgGap(lfMinima) * dt;
        double rightStrideDur = AvgGap(rfMinima) * dt;
        double strideDur = (leftStrideDur > 0 && rightStrideDur > 0)
            ? (leftStrideDur + rightStrideDur) * 0.5
            : System.Math.Max(leftStrideDur, rightStrideDur);

        if (strideDur < 1e-3)
        {
            warning = "无法找到清晰的脚部落地周期。可能动画不是循环走路，或者脚的局部 Y 坐标基本不动。";
            hasResult = false;
            Repaint();
            return;
        }

        // 跨步长 = 脚相对身体的 forward 振幅
        // 取左右脚平均，更稳
        float leftStride  = MaxMinusMin(lfFwd);
        float rightStride = MaxMinusMin(rfFwd);
        float stride = (leftStride + rightStride) * 0.5f;

        if (stride < 1e-3f)
        {
            warning = "脚相对身体的前后位移幅度过小（< 1mm）。请确认前进方向选对了，或动画确实是行走。";
            hasResult = false;
            Repaint();
            return;
        }

        resultStride         = stride;
        resultStrideDuration = (float)strideDur;
        resultSpeed          = stride / (float)strideDur;
        resultCycles         = Mathf.Max(1, (int)System.Math.Round(walkClip.length / strideDur));
        hasResult            = true;

        Repaint();
    }

    /// <summary>把局部坐标投影到用户选择的"前进方向"。</summary>
    private float ProjectForward(Vector3 localPos)
    {
        switch (forwardAxis)
        {
            case ForwardAxis.PositiveZ: return  localPos.z;
            case ForwardAxis.NegativeZ: return -localPos.z;
            case ForwardAxis.PositiveX: return  localPos.x;
            case ForwardAxis.NegativeX: return -localPos.x;
        }
        return localPos.z;
    }

    /// <summary>
    /// 找局部最小值。window = 比较窗口半径；minSpacing = 相邻最小值之间至少的距离（采样点）。
    /// </summary>
    private static List<int> FindLocalMinima(float[] data, int window, int minSpacing)
    {
        var result = new List<int>();
        int lastAccepted = -minSpacing - 1;
        for (int i = window; i < data.Length - window; i++)
        {
            bool isMin = true;
            for (int j = -window; j <= window; j++)
            {
                if (j == 0) continue;
                if (data[i + j] < data[i] - 1e-6f) { isMin = false; break; }
            }
            if (!isMin) continue;
            if (i - lastAccepted < minSpacing) continue;
            result.Add(i);
            lastAccepted = i;
        }
        return result;
    }

    /// <summary>
    /// 计算相邻索引之间的平均间隔（用 index unit，调用方乘 dt 得到时间）。
    /// 用首末点之差 / 段数，比单段差更稳。
    /// </summary>
    private static double AvgGap(List<int> indices)
    {
        if (indices == null || indices.Count < 2) return 0;
        return (double)(indices[indices.Count - 1] - indices[0]) / (indices.Count - 1);
    }

    private static float MaxMinusMin(float[] arr)
    {
        if (arr == null || arr.Length == 0) return 0;
        float min = arr[0], max = arr[0];
        for (int i = 1; i < arr.Length; i++)
        {
            if (arr[i] < min) min = arr[i];
            if (arr[i] > max) max = arr[i];
        }
        return max - min;
    }

    // ─────────────────────────────────────────
    //               Results UI
    // ─────────────────────────────────────────
    private void DrawResults()
    {
        EditorGUILayout.LabelField("分析结果", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"动画总长：    {walkClip.length:F3} s");
        EditorGUILayout.LabelField($"检测到周期：   {resultCycles} 次完整步态");
        EditorGUILayout.LabelField($"跨步周期：    {resultStrideDuration:F3} s");
        EditorGUILayout.LabelField($"跨步长：      {resultStride:F3} m");
        EditorGUILayout.Space(2);
        var style = new GUIStyle(EditorStyles.boldLabel);
        style.normal.textColor = new Color(0.5f, 1f, 0.6f);
        EditorGUILayout.LabelField($"→ 行走速度：  {resultSpeed:F3} m/s", style);
        EditorGUILayout.EndVertical();
    }

    private void DrawPreview()
    {
        EditorGUILayout.LabelField("脚轨迹预览", EditorStyles.miniBoldLabel);

        DrawSeries("左脚 Y（高度）", lfY, lfMinima, new Color(0.4f, 0.9f, 0.5f));
        DrawSeries("左脚 Forward",   lfFwd, null,    new Color(0.4f, 0.7f, 1f));
        DrawSeries("右脚 Y（高度）", rfY, rfMinima, new Color(1f, 0.7f, 0.3f));
        DrawSeries("右脚 Forward",   rfFwd, null,    new Color(1f, 0.5f, 0.8f));
    }

    private void DrawSeries(string label, float[] data, List<int> markers, Color color)
    {
        if (data == null) return;
        EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
        Rect rect = GUILayoutUtility.GetRect(0f, 50f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] < min) min = data[i];
            if (data[i] > max) max = data[i];
        }
        if (max - min < 1e-6f) max = min + 1e-6f;

        var prev = Handles.color;
        Handles.color = color;
        Vector3 p0 = new Vector3(rect.x, rect.yMax - (data[0] - min) / (max - min) * rect.height);
        for (int i = 1; i < data.Length; i++)
        {
            float x = rect.x + (float)i / (data.Length - 1) * rect.width;
            float y = rect.yMax - (data[i] - min) / (max - min) * rect.height;
            Vector3 p1 = new Vector3(x, y);
            Handles.DrawAAPolyLine(2f, p0, p1);
            p0 = p1;
        }
        Handles.color = prev;

        // 标记最小值点（脚最低点 = 落地时刻）
        if (markers != null)
        {
            foreach (int idx in markers)
            {
                float x = rect.x + (float)idx / (data.Length - 1) * rect.width;
                float y = rect.yMax - (data[idx] - min) / (max - min) * rect.height;
                EditorGUI.DrawRect(new Rect(x - 2f, y - 2f, 4f, 4f), Color.red);
            }
        }
    }

    private void DrawApplyButtons()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button($"复制速度到剪贴板（{resultSpeed:F3}）", GUILayout.Height(24f)))
        {
            EditorGUIUtility.systemCopyBuffer = resultSpeed.ToString("F4");
            ShowNotification(new GUIContent($"已复制 {resultSpeed:F4}"));
        }

        // 自动找当前选中的 SplineCurveMoveClip
        SplineCurveMoveClip target = Selection.activeObject as SplineCurveMoveClip;
        EditorGUI.BeginDisabledGroup(target == null);
        if (GUILayout.Button(
                target != null ? "应用到选中的 SplineCurveMoveClip" : "（请在 Project 选中一个 Clip）",
                GUILayout.Height(24f)))
        {
            Undo.RecordObject(target, "Apply Gait Speed");
            target.UseAnimationSpeedSync = true;
            target.AnimationWalkSpeed = resultSpeed;
            EditorUtility.SetDirty(target);
            ShowNotification(new GUIContent($"已应用 {resultSpeed:F3} m/s"));
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
    }
}
