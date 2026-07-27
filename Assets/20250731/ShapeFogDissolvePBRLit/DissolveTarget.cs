// =============================================================================
//  DissolveTarget.cs
//  挂在每个使用 Custom/SceneLitFoggedDissolve 的 Renderer 上,
//  通过 MaterialPropertyBlock 推送本物体的 dissolve "形状" 配置:
//    - 模式 (Noise / Axis / Radial)
//    - Axis 方向、Radial 中心
//
//  amount 由 DissolveController (它的 controlledRenderers 列表里的物体共享一个 amount)
// =============================================================================

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public class DissolveTarget : MonoBehaviour
{
    public enum DissolveMode
    {
        Noise  = 0,    // 有机噪声(默认)
        Axis   = 1,    // 沿局部空间某条轴线性扫过
        Radial = 2,    // 距离局部中心点的径向
    }

    // ---- Inspector ----------------------------------------------------------
    [Header("Mode")]
    public DissolveMode mode = DissolveMode.Noise;

    [Header("Axis Mode")]
    [Tooltip("局部空间方向。沿此方向投影最大的点最后消失。\n+Y = 自下而上, -Y = 自上而下, +Z = 由后向前, etc.")]
    public Vector3 axisDirection = Vector3.up;

    [Header("Radial Mode")]
    [Tooltip("局部空间中心点。默认 (0,0,0) = mesh origin")]
    public Vector3 radialCenter = Vector3.zero;

    [Tooltip("勾选后从外向内消失(球状物体推荐勾上)")]
    public bool radialReverse = false;

    // ---- Shader Property IDs ------------------------------------------------
    static readonly int ID_Mode          = Shader.PropertyToID("_DissolveMode");
    static readonly int ID_Axis          = Shader.PropertyToID("_DissolveAxis");
    static readonly int ID_AxisCenter    = Shader.PropertyToID("_DissolveAxisCenter");
    static readonly int ID_Radial        = Shader.PropertyToID("_DissolveRadial");
    static readonly int ID_RadialReverse = Shader.PropertyToID("_DissolveRadialReverse");

    // ---- Runtime ------------------------------------------------------------
    Renderer _rend;
    MaterialPropertyBlock _mpb;

    void OnEnable()  { Apply(); }
    void OnValidate(){ Apply(); }

    public void Apply()
    {
        if (_rend == null) _rend = GetComponent<Renderer>();
        if (_rend == null) return;
        if (_mpb == null)  _mpb  = new MaterialPropertyBlock();

        // 关键:GetPropertyBlock 先读现有 MPB,这样 DissolveController 写的
        // _DissolveAmount 不会被覆盖
        _rend.GetPropertyBlock(_mpb);

        _mpb.SetFloat(ID_Mode, (float)mode);

        // 根据 mesh 局部 bounds 计算 Axis / Radial 数据
        Bounds bounds = ComputeLocalBounds();

        // Axis: 把 bounds 沿归一化轴投影,得到半长 + 中心投影
        Vector3 axisNorm = axisDirection.sqrMagnitude > 1e-4f
            ? axisDirection.normalized
            : Vector3.up;
        Vector3 ext = bounds.extents;
        float halfExtent = Mathf.Abs(ext.x * axisNorm.x)
                         + Mathf.Abs(ext.y * axisNorm.y)
                         + Mathf.Abs(ext.z * axisNorm.z);
        float axisCenter = Vector3.Dot(bounds.center, axisNorm);

        _mpb.SetVector(ID_Axis, new Vector4(axisNorm.x, axisNorm.y, axisNorm.z, halfExtent));
        _mpb.SetFloat (ID_AxisCenter, axisCenter);

        // Radial: 到 8 个角中最远的距离 = 完整覆盖整个 mesh
        float maxDist = ComputeMaxRadialDistance(bounds, radialCenter);
        _mpb.SetVector(ID_Radial, new Vector4(radialCenter.x, radialCenter.y, radialCenter.z, maxDist));
        _mpb.SetFloat (ID_RadialReverse, radialReverse ? 1f : 0f);

        _rend.SetPropertyBlock(_mpb);
    }

    Bounds ComputeLocalBounds()
    {
        var mf  = GetComponent<MeshFilter>();
        if (mf  != null && mf.sharedMesh  != null) return mf.sharedMesh.bounds;
        var smr = GetComponent<SkinnedMeshRenderer>();
        if (smr != null && smr.sharedMesh != null) return smr.sharedMesh.bounds;
        return new Bounds(Vector3.zero, Vector3.one);
    }

    static float ComputeMaxRadialDistance(Bounds b, Vector3 center)
    {
        Vector3 mn = b.min, mx = b.max;
        float d = 0f;
        for (int xi = 0; xi < 2; xi++)
        for (int yi = 0; yi < 2; yi++)
        for (int zi = 0; zi < 2; zi++)
        {
            Vector3 c = new Vector3(xi == 0 ? mn.x : mx.x,
                                     yi == 0 ? mn.y : mx.y,
                                     zi == 0 ? mn.z : mx.z);
            d = Mathf.Max(d, (c - center).magnitude);
        }
        return Mathf.Max(d, 0.001f);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Bounds b = ComputeLocalBounds();
        float gizmoSize = Mathf.Max(b.size.magnitude * 0.02f, 0.01f);

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color  = new Color(0.4f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireCube(b.center, b.size);

        if (mode == DissolveMode.Axis)
        {
            Vector3 axis = axisDirection.sqrMagnitude > 1e-4f ? axisDirection.normalized : Vector3.up;
            Vector3 ext  = b.extents;
            float half   = Mathf.Abs(ext.x * axis.x) + Mathf.Abs(ext.y * axis.y) + Mathf.Abs(ext.z * axis.z);

            Vector3 first = b.center - axis * half;    // 先消失端
            Vector3 last  = b.center + axis * half;    // 后消失端

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(first, gizmoSize);
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(last, gizmoSize);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(first, last);
        }
        else if (mode == DissolveMode.Radial)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(radialCenter, gizmoSize);
        }
    }
#endif
}

// =============================================================================
// Custom Editor: 模式专属预设按钮
// =============================================================================
#if UNITY_EDITOR
[CustomEditor(typeof(DissolveTarget))]
public class DissolveTargetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var t = (DissolveTarget)target;

        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
        {
            t.Apply();
            EditorUtility.SetDirty(t);
        }

        EditorGUILayout.Space(6);

        if (t.mode == DissolveTarget.DissolveMode.Axis)
        {
            EditorGUILayout.LabelField("Axis Presets", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("↑ Up (+Y)"))    SetAxis(t, Vector3.up);
            if (GUILayout.Button("↓ Down (-Y)"))  SetAxis(t, Vector3.down);
            if (GUILayout.Button("→ Right (+X)")) SetAxis(t, Vector3.right);
            if (GUILayout.Button("← Left (-X)"))  SetAxis(t, Vector3.left);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Forward (+Z)")) SetAxis(t, Vector3.forward);
            if (GUILayout.Button("Back (-Z)"))    SetAxis(t, Vector3.back);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Scene View: 红球 = 先消失端, 绿球 = 后消失端\n方向是 LOCAL space, 受物体 transform 影响",
                MessageType.Info);
        }
        else if (t.mode == DissolveTarget.DissolveMode.Radial)
        {
            EditorGUILayout.LabelField("Radial Presets", EditorStyles.boldLabel);
            if (GUILayout.Button("Center to Mesh Bounds Center"))
            {
                var mf = t.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Undo.RecordObject(t, "Center Radial");
                    t.radialCenter = mf.sharedMesh.bounds.center;
                    t.Apply();
                    EditorUtility.SetDirty(t);
                }
            }

            EditorGUILayout.HelpBox(t.radialReverse
                ? "Outside-In: 外圈先消失(球状物体推荐)"
                : "Inside-Out: 中心先消失",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Noise 模式使用有机扰动消失,适合大块装饰物。\nNoise Scale / Edge Width / Edge Color 在材质上调整。",
                MessageType.Info);
        }
    }

    static void SetAxis(DissolveTarget t, Vector3 axis)
    {
        Undo.RecordObject(t, "Set Dissolve Axis");
        t.axisDirection = axis;
        t.Apply();
        EditorUtility.SetDirty(t);
    }
}
#endif
