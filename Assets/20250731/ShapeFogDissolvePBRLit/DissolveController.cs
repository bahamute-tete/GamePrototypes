// =============================================================================
//  DissolveController.cs  (v4)
//  按 Space 区分 Local / World 语义,在 World 模式下计算"整组共享"的归一化范围,
//  这样 amount=0..1 真正对应"沿世界轴/世界球扫过整组"。
//
//  关键差异:
//    Axis + Local : 每个 renderer 在自身 OS 沿 axisDirection 消融, 速率独立
//                   (各物体同时进度 0→1)
//    Axis + World : 整组先投影到世界 axisDirection,找出 globalMin / globalMax,
//                   所有 renderer 共享这个范围, amount 把"消融面"从 minProj 推到 maxProj
//                   (世界轴上靠前的物体先消失)
//
//    Radial + Local : 每个 renderer 从自身 bounds 中心 (或指定的 local point) 向外
//    Radial + World : 整组从一个世界点向外, 共享 maxDist = 最远 renderer corner 的距离
//
//    Noise + Local : 噪声贴在物体上 (positionOS), 物体动噪点跟着动
//    Noise + World : 噪声在世界空间 (positionWS), 物体经过噪声场
//                    + 可选贴图 (置空走过程化)
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class DissolveController : MonoBehaviour
{
    // ---- 枚举 ---------------------------------------------------------------
    public enum DissolveMode  { Noise = 0, Axis = 1, Radial = 2 }
    public enum DissolveSpace { Local = 0, World = 1 }

    // ---- 动画驱动量 ---------------------------------------------------------
    [Header("Animation")]
    [Range(0f, 1f)]
    [Tooltip("Timeline 或代码驱动的消融进度。Editor 里拖动也能实时预览。")]
    public float amount = 0f;

    // ---- Mode + Space -------------------------------------------------------
    [Header("Mode & Space")]
    public DissolveMode  mode  = DissolveMode.Noise;

    [Tooltip("Local: 各 renderer 在自身本地空间消融, 互相独立 (各物体同时 0→1).\n" +
             "World: 整组按世界范围共享归一化, amount 真正扫过整个世界范围.")]
    public DissolveSpace space = DissolveSpace.World;

    // ---- Noise 模式 ---------------------------------------------------------
    [Header("Noise Mode")]
    [Range(0.001f, 5f)]
    [Tooltip("Noise 频率。值越大噪点越密。")]
    public float noiseScale = 2.0f;

    [Tooltip("噪声贴图 (R 通道)。置空时走过程化 ValueNoise。\n" +
             "Triplanar 等权三向投影采样,3 个 tap。\n" +
             "美术能直接给出想要的消融图案。")]
    public Texture2D noiseTexture;

    // ---- Axis 模式 ----------------------------------------------------------
    [Header("Axis Mode")]
    [Tooltip("方向。\n" +
             "Space=Local: 各 renderer 自身本地方向 (0,1,0)=各自的 +Y\n" +
             "Space=World: 世界方向 (0,1,0)=世界 +Y, 整组共享")]
    public Vector3 axisDirection = Vector3.up;

    // ---- Radial 模式 --------------------------------------------------------
    [Header("Radial Mode")]
    [Tooltip("仅 Local 模式生效: 勾选 → 每个 renderer 从自身 mesh bounds 中心向外消融。\n" +
             "关闭则使用下方 Radial Center 当作每个 renderer 的局部中心点。")]
    public bool radialUseEachBoundsCenter = true;

    [Tooltip("Space=Local: 各 renderer 局部坐标的中心 (radialUseEachBoundsCenter 关闭时生效)\n" +
             "Space=World: 共享世界坐标。所有 renderer 都从这里向外消")]
    public Vector3 radialCenter = Vector3.zero;

    [Tooltip("勾选后从外向内消失 (球状物体推荐)")]
    public bool radialReverse = false;

    // ---- 受控对象 -----------------------------------------------------------
    [Header("Controlled Renderers")]
    public List<Renderer> controlledRenderers = new List<Renderer>();

    // ---- 末态处理 -----------------------------------------------------------
    [Header("Final State")]
    [Range(0.9f, 1.0f)]
    [Tooltip("amount 超此阈值时自动关 Renderer.enabled, 彻底消除 A2C 残留点。\n" +
             "回落时自动恢复。设为 1 = 不自动关。")]
    public float hideThreshold = 0.995f;

    public bool autoToggleRenderer = true;

    // ---- Shader Property IDs ------------------------------------------------
    static readonly int ID_Amount        = Shader.PropertyToID("_DissolveAmount");
    static readonly int ID_Mode          = Shader.PropertyToID("_DissolveMode");
    static readonly int ID_Space         = Shader.PropertyToID("_DissolveSpace");
    static readonly int ID_NoiseScale    = Shader.PropertyToID("_DissolveNoiseScale");
    static readonly int ID_NoiseTex      = Shader.PropertyToID("_DissolveNoiseTex");
    static readonly int ID_UseNoiseTex   = Shader.PropertyToID("_DissolveUseNoiseTex");
    static readonly int ID_Axis          = Shader.PropertyToID("_DissolveAxis");
    static readonly int ID_AxisCenter    = Shader.PropertyToID("_DissolveAxisCenter");
    static readonly int ID_Radial        = Shader.PropertyToID("_DissolveRadial");
    static readonly int ID_RadialReverse = Shader.PropertyToID("_DissolveRadialReverse");

    // ---- Runtime ------------------------------------------------------------
    MaterialPropertyBlock _mpb;
    float _lastAmount = float.NaN;

    public void SetAmount(float a) { amount = Mathf.Clamp01(a); Apply(); }
    public void ForceRefresh()     { _lastAmount = float.NaN;   Apply(); }

    // ---- 生命周期 -----------------------------------------------------------
    void OnEnable()  { _lastAmount = float.NaN; Apply(); }
    void OnValidate(){ _lastAmount = float.NaN; if (isActiveAndEnabled) Apply(); }

    void OnDisable()
    {
        if (autoToggleRenderer)
            for (int i = 0; i < controlledRenderers.Count; i++)
                if (controlledRenderers[i] != null) controlledRenderers[i].enabled = true;
    }

    void LateUpdate()
    {
        if (!Mathf.Approximately(amount, _lastAmount)) Apply();
    }

    // ---- 核心 Apply ---------------------------------------------------------
    void Apply()
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        float clamped      = Mathf.Clamp01(amount);
        bool  shouldRender = !autoToggleRenderer || clamped < hideThreshold;
        float modeF        = (float)mode;
        float spaceF       = (float)space;
        float reverseF     = radialReverse ? 1f : 0f;
        float useTexF      = (noiseTexture != null) ? 1f : 0f;
        bool  isWorld      = (space == DissolveSpace.World);

        // ===== World 模式: 预计算整组共享聚合量 =====
        Vector3 axisDirWorld = axisDirection.sqrMagnitude > 1e-6f
                             ? axisDirection.normalized : Vector3.up;

        float worldAxisCenter = 0f, worldAxisHalfExtent = 1f;
        if (isWorld && mode == DissolveMode.Axis)
            ComputeWorldAxisRange(axisDirWorld, out worldAxisCenter, out worldAxisHalfExtent);

        float worldRadialMaxDist = 1f;
        if (isWorld && mode == DissolveMode.Radial)
            worldRadialMaxDist = ComputeWorldRadialMaxDist(radialCenter);

        // ===== 推送每个 renderer =====
        for (int i = 0; i < controlledRenderers.Count; i++)
        {
            var r = controlledRenderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(_mpb);

            // ---- 共享字段 ----
            _mpb.SetFloat(ID_Amount,      clamped);
            _mpb.SetFloat(ID_Mode,        modeF);
            _mpb.SetFloat(ID_Space,       spaceF);
            _mpb.SetFloat(ID_NoiseScale,  noiseScale);
            _mpb.SetFloat(ID_UseNoiseTex, useTexF);
            if (noiseTexture != null)
                _mpb.SetTexture(ID_NoiseTex, noiseTexture);

            _mpb.SetFloat(ID_RadialReverse, reverseF);

            // ---- Axis 参数 (按 space 推不同语义) ----
            if (isWorld)
            {
                // 世界方向 + 整组共享范围,所有 renderer 一致
                _mpb.SetVector(ID_Axis,       new Vector4(axisDirWorld.x, axisDirWorld.y, axisDirWorld.z, worldAxisHalfExtent));
                _mpb.SetFloat (ID_AxisCenter, worldAxisCenter);
            }
            else
            {
                // 本地方向 + 本 renderer 的局部 bounds
                Vector3 axisLocal = axisDirection.sqrMagnitude > 1e-6f
                                  ? axisDirection.normalized : Vector3.up;
                Bounds  localBounds = GetLocalBounds(r);
                float halfExt = Mathf.Abs(axisLocal.x) * localBounds.extents.x
                              + Mathf.Abs(axisLocal.y) * localBounds.extents.y
                              + Mathf.Abs(axisLocal.z) * localBounds.extents.z;
                float center  = Vector3.Dot(localBounds.center, axisLocal);

                _mpb.SetVector(ID_Axis,       new Vector4(axisLocal.x, axisLocal.y, axisLocal.z, halfExt));
                _mpb.SetFloat (ID_AxisCenter, center);
            }

            // ---- Radial 参数 (按 space 推不同语义) ----
            if (isWorld)
            {
                _mpb.SetVector(ID_Radial,
                    new Vector4(radialCenter.x, radialCenter.y, radialCenter.z, worldRadialMaxDist));
            }
            else
            {
                // Local 模式: 中心点 + 最远 corner 距离 (按 mesh 局部坐标)
                Bounds  localBounds = GetLocalBounds(r);
                Vector3 localCenter = radialUseEachBoundsCenter
                                    ? localBounds.center
                                    : radialCenter;
                float maxD = ComputeMaxDistanceFromCenterToCorners(localBounds, localCenter);

                _mpb.SetVector(ID_Radial,
                    new Vector4(localCenter.x, localCenter.y, localCenter.z, maxD));
            }

            r.SetPropertyBlock(_mpb);

            if (r.enabled != shouldRender) r.enabled = shouldRender;
        }

        _lastAmount = amount;
    }

    // ---- World 聚合计算 -----------------------------------------------------
    void ComputeWorldAxisRange(Vector3 axisNorm, out float centerProj, out float halfExtent)
    {
        float minP = float.MaxValue;
        float maxP = float.MinValue;
        bool any = false;

        for (int i = 0; i < controlledRenderers.Count; i++)
        {
            var r = controlledRenderers[i];
            if (r == null) continue;

            // r.bounds 是世界空间 AABB,自动随 transform 更新
            Bounds wb = r.bounds;
            Vector3 mn = wb.min, mx = wb.max;
            for (int xi = 0; xi < 2; xi++)
            for (int yi = 0; yi < 2; yi++)
            for (int zi = 0; zi < 2; zi++)
            {
                Vector3 c = new Vector3(xi == 0 ? mn.x : mx.x,
                                         yi == 0 ? mn.y : mx.y,
                                         zi == 0 ? mn.z : mx.z);
                float p = Vector3.Dot(c, axisNorm);
                if (p < minP) minP = p;
                if (p > maxP) maxP = p;
                any = true;
            }
        }

        if (!any) { centerProj = 0f; halfExtent = 1f; return; }

        centerProj  = (maxP + minP) * 0.5f;
        halfExtent  = Mathf.Max((maxP - minP) * 0.5f, 1e-4f);
    }

    float ComputeWorldRadialMaxDist(Vector3 worldCenter)
    {
        float maxD = 0f;
        for (int i = 0; i < controlledRenderers.Count; i++)
        {
            var r = controlledRenderers[i];
            if (r == null) continue;

            Bounds wb = r.bounds;
            Vector3 mn = wb.min, mx = wb.max;
            for (int xi = 0; xi < 2; xi++)
            for (int yi = 0; yi < 2; yi++)
            for (int zi = 0; zi < 2; zi++)
            {
                Vector3 c = new Vector3(xi == 0 ? mn.x : mx.x,
                                         yi == 0 ? mn.y : mx.y,
                                         zi == 0 ? mn.z : mx.z);
                float d = (c - worldCenter).magnitude;
                if (d > maxD) maxD = d;
            }
        }
        return Mathf.Max(maxD, 1e-4f);
    }

    // ---- Helpers ------------------------------------------------------------
    static Bounds GetLocalBounds(Renderer r)
    {
        var mf  = r.GetComponent<MeshFilter>();
        if (mf  != null && mf.sharedMesh  != null) return mf.sharedMesh.bounds;
        var smr = r as SkinnedMeshRenderer;
        if (smr != null && smr.sharedMesh != null) return smr.sharedMesh.bounds;
        return new Bounds(Vector3.zero, Vector3.one);
    }

    static float ComputeMaxDistanceFromCenterToCorners(Bounds b, Vector3 center)
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
        return Mathf.Max(d, 1e-4f);
    }

    // ---- 编辑器便利 ---------------------------------------------------------
#if UNITY_EDITOR
    [ContextMenu("Auto-Find Renderers In Children")]
    void AutoFindRenderers()
    {
        var found = GetComponentsInChildren<Renderer>(true);
        controlledRenderers.Clear();
        controlledRenderers.AddRange(found);
        UnityEditor.EditorUtility.SetDirty(this);
        ForceRefresh();
    }

    [ContextMenu("Set Radial Center To Manager Position (World)")]
    void SetRadialToManagerWorldPos()
    {
        radialCenter = transform.position;
        space = DissolveSpace.World;
        UnityEditor.EditorUtility.SetDirty(this);
        ForceRefresh();
    }

    [ContextMenu("Clear Renderers")]
    void ClearRenderers()
    {
        controlledRenderers.Clear();
        UnityEditor.EditorUtility.SetDirty(this);
    }

    void OnDrawGizmosSelected()
    {
        bool isWorld = (space == DissolveSpace.World);

        // ===== Radial World 中心点可视化 =====
        if (mode == DissolveMode.Radial && isWorld)
        {
            Gizmos.color = radialReverse ? Color.cyan : Color.magenta;
            Gizmos.DrawWireSphere(radialCenter, 0.3f);
            Gizmos.DrawLine(radialCenter - Vector3.up    * 0.5f, radialCenter + Vector3.up    * 0.5f);
            Gizmos.DrawLine(radialCenter - Vector3.right * 0.5f, radialCenter + Vector3.right * 0.5f);
            Gizmos.DrawLine(radialCenter - Vector3.forward*0.5f, radialCenter + Vector3.forward*0.5f);

            // 包围球可视化 (使用上次 Apply 算出的 maxDist 不太靠谱,这里现算)
            float r = ComputeWorldRadialMaxDist(radialCenter);
            Gizmos.color = new Color(1f, 0.3f, 1f, 0.25f);
            Gizmos.DrawWireSphere(radialCenter, r);
        }

        // ===== Axis 方向可视化 =====
        if (mode == DissolveMode.Axis)
        {
            Vector3 dir = axisDirection.sqrMagnitude > 1e-4f
                ? axisDirection.normalized : Vector3.up;

            if (isWorld)
            {
                // 在世界轴中心位置画一条横贯整组的线
                float center, half;
                ComputeWorldAxisRange(dir, out center, out half);
                Vector3 mid  = dir * center;
                Vector3 head = mid + dir * half;
                Vector3 tail = mid - dir * half;
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(tail, 0.15f);   // 先消失端
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(head, 0.15f);   // 后消失端
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(tail, head);
            }
            else
            {
                // Local 模式 — 在 manager 位置画一支局部方向箭头
                Vector3 tail = transform.position;
                Vector3 head = tail + dir * 1.5f;
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(tail, head);
                Gizmos.DrawSphere(head, 0.1f);
            }
        }
    }
#endif
}
