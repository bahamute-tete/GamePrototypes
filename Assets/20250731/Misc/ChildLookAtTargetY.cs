using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class ChildLookAtTargetY : MonoBehaviour
{
    public enum LocalAxis { Forward, Back, Right, Left, Up, Down }

    [Header("Target")]
    public Transform target;
    public Vector3 targetOffset;

    [Header("Child Normal Axis")]
    [Tooltip("子物体的哪个本地轴代表“法线”方向（即要朝向 target 的方向）")]
    public LocalAxis normalAxis = LocalAxis.Forward;

    [Header("Distance Range")]
    [Tooltip("距离 ≥ outerRange 时保持原样 (t=0)")]
    public float outerRange = 5f;
    [Tooltip("距离 ≤ innerRange 时应用最大旋转 (t=1)")]
    public float innerRange = 1f;
    [Tooltip("使用 SmoothStep 让过渡更平滑")]
    public bool useSmoothStep = true;

    [Header("Rotation Clamp")]
    [Tooltip("绕 Y 轴相对原始朝向的最大旋转角度（度），最终角度还会受 t 缩放")]
    [Range(0f, 180f)]
    public float maxAngle = 60f;

    [Header("Update")]
    [Tooltip("是否每帧自动更新；编辑器下 [ExecuteAlways] 会在场景刷新时触发")]
    public bool updateEveryFrame = true;

    private class Entry
    {
        public Transform xf;
        public Quaternion originalLocalRot;
    }

    private readonly List<Entry> entries = new List<Entry>();

    void OnEnable() => Capture();
    void OnDisable() => Restore();

    /// <summary>
    /// 重新捕获所有直接子物体当前的本地旋转作为“原始朝向”。
    /// 在编辑器里调整子物体后请通过 Inspector 右键菜单调用。
    /// </summary>
    [ContextMenu("Recapture Original Rotations")]
    public void Capture()
    {
        Restore();
        for (int i = 0; i < transform.childCount; i++)
        {
            var c = transform.GetChild(i);
            entries.Add(new Entry { xf = c, originalLocalRot = c.localRotation });
        }
    }

    /// <summary>把所有子物体还原到捕获时的本地旋转。</summary>
    public void Restore()
    {
        foreach (var e in entries)
            if (e.xf != null) e.xf.localRotation = e.originalLocalRot;
        entries.Clear();
    }

    void Update()
    {
        if (updateEveryFrame) UpdateRotations();
    }

    public void UpdateRotations()
    {
        if (target == null || entries.Count == 0) return;

        Vector3 targetPos = target.position + targetOffset;
        float rangeDelta = Mathf.Max(outerRange - innerRange, 1e-4f);
        Vector3 localAxisVec = GetAxisVector(normalAxis);

        foreach (var e in entries)
        {
            if (e.xf == null) continue;

            // 每帧从原始旋转开始算，保证状态无关 → 拖动参数立即正确
            e.xf.localRotation = e.originalLocalRot;

            // 1) 距离 → t
            float dist = Vector3.Distance(e.xf.position, targetPos);
            float t;
            if (dist >= outerRange) t = 0f;
            else if (dist <= innerRange) t = 1f;
            else t = 1f - (dist - innerRange) / rangeDelta;

            if (useSmoothStep) t = Mathf.SmoothStep(0f, 1f, t);
            if (t <= 0f) continue;                       // 范围外保持原样

            // 2) toTarget 在 XZ 平面
            Vector3 toTarget = targetPos - e.xf.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 1e-8f) continue;
            toTarget.Normalize();

            // 3) 子物体当前法线轴在世界空间 XZ 投影
            Vector3 worldNormal = e.xf.TransformDirection(localAxisVec);
            worldNormal.y = 0f;
            if (worldNormal.sqrMagnitude < 1e-8f) continue; // 法线轴接近垂直
            worldNormal.Normalize();

            // 4) 有符号夹角 → clamp → 缩放
            float angle = Vector3.SignedAngle(worldNormal, toTarget, Vector3.up);
            float clamped = Mathf.Clamp(angle, -maxAngle, maxAngle) * t;

            if (Mathf.Abs(clamped) < 1e-4f) continue;

            // 5) 绕世界 Y 叠加旋转
            e.xf.rotation = Quaternion.AngleAxis(clamped, Vector3.up) * e.xf.rotation;
        }
    }

    static Vector3 GetAxisVector(LocalAxis a)
    {
        switch (a)
        {
            case LocalAxis.Forward: return Vector3.forward;
            case LocalAxis.Back: return Vector3.back;
            case LocalAxis.Right: return Vector3.right;
            case LocalAxis.Left: return Vector3.left;
            case LocalAxis.Up: return Vector3.up;
            case LocalAxis.Down: return Vector3.down;
        }
        return Vector3.forward;
    }

    void OnValidate()
    {
        innerRange = Mathf.Max(0f, innerRange);
        outerRange = Mathf.Max(outerRange, innerRange);
    }

    void OnDrawGizmosSelected()
    {
        if (target == null) return;
        Vector3 p = target.position + targetOffset;

        Gizmos.color = new Color(1f, 0.4f, 0f, 1f);
        Gizmos.DrawWireSphere(p, innerRange);
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.6f);
        Gizmos.DrawWireSphere(p, outerRange);

        Gizmos.color = Color.cyan;
        foreach (var e in entries)
        {
            if (e.xf == null) continue;
            Gizmos.DrawLine(e.xf.position, p);

            // 当前法线轴方向可视化
            Gizmos.color = Color.green;
            Vector3 dir = e.xf.TransformDirection(GetAxisVector(normalAxis));
            Gizmos.DrawLine(e.xf.position, e.xf.position + dir * 0.5f);
            Gizmos.color = Color.cyan;
        }
    }
}
