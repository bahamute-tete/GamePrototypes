// =============================================================================
//  SceneLitColorController.cs  (v2 - DissolveController compatible)
//  Unity 2022.3 / URP 14.x / XR Single Pass Instanced / Mobile VR (PICO 4U)
//
//  统一控制父物体下所有子 Renderer 的 _BaseColor 和 _EmissionColor (HDR)
//  - renderer-level MaterialPropertyBlock 写入,与 DissolveController 完全兼容
//  - 不创建 material 实例,不破坏 SRP Batcher
//  - 公开 serialized 字段可被 Animation Track 直接 K 帧,Timeline 友好
//  - shader 名过滤防止误伤父级下其他 shader 的物体
//  - ExecuteAlways:编辑器下实时预览 (含 Timeline preview)
//
//  *** 与 DissolveController 共存机制 ***
//    Unity 的 Renderer 有两套独立 MPB 存储:
//      r.SetPropertyBlock(block)            → renderer-level (共享给所有 slot)
//      r.SetPropertyBlock(block, slotIdx)   → per-slot,优先级高于 renderer-level
//    一旦 per-slot MPB 存在,renderer-level MPB 在该 slot 上失效。
//    DissolveController 用 renderer-level API,本组件必须保持一致,
//    才能让两个 controller 通过 Get→modify→Set 串联共享同一 MPB。
//
//  适配 shader:
//    Custom/SceneLitFoggedDissolve
//    Custom/SceneGlassFogged
//    Custom/FresnelTransparent_URP
//  (任何以 _BaseColor + _EmissionColor 为命名的 URP shader 均兼容)
//
//  Timeline 用法:
//    1. 在 Road 物体上挂 Animator 组件 (Timeline Animation Track 的 binding)
//    2. 把本组件挂在同一物体上
//    3. Timeline 加 Animation Track → bind 到 Road 的 Animator
//    4. Record → 调 baseColor / emissionColor / emissionIntensity 即生成关键帧
// =============================================================================

using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Rendering/Scene Lit Color Controller")]
public sealed class SceneLitColorController : MonoBehaviour
{
    // ---------------- Shader Property IDs (cached) ----------------
    static readonly int ID_BaseColor     = Shader.PropertyToID("_BaseColor");
    static readonly int ID_EmissionColor = Shader.PropertyToID("_EmissionColor");

    // =========================================================================
    // Base Color
    // =========================================================================
    [Header("Base Color")]
    [Tooltip("勾选后才会向 children 推送 _BaseColor;关闭则保留材质原值")]
    public bool overrideBaseColor = true;

    [Tooltip("基础色 (sRGB),覆盖材质的 _BaseColor")]
    public Color baseColor = Color.white;

    // =========================================================================
    // Emission (HDR)
    //   最终写入 shader 的 _EmissionColor = emissionColor.rgb * emissionIntensity
    //   alpha 透传 (shader 不使用)
    // =========================================================================
    [Header("Emission (HDR)")]
    [Tooltip("勾选后才会向 children 推送 _EmissionColor;关闭则保留材质原值")]
    public bool overrideEmission = true;

    [Tooltip("自发光颜色 (HDR)。最终 _EmissionColor = emissionColor * emissionIntensity")]
    [ColorUsage(true, true)]
    public Color emissionColor = Color.black;

    [Tooltip("自发光强度乘数,方便 K 帧脉冲/呼吸效果而不需要动 color hue")]
    [Min(0f)]
    public float emissionIntensity = 1f;

    // =========================================================================
    // Renderer Collection
    // =========================================================================
    [Header("Renderer Collection")]
    [Tooltip("是否包含 Inactive 的子物体")]
    public bool includeInactive = true;

    [Tooltip("仅对 shader 名包含指定字符串的 renderer 生效。\n" +
             "renderer 的 sharedMaterials 中至少有一个 material 的 shader 名包含此串 → 受控。\n" +
             "空字符串 = 不过滤,全部应用")]
    public string shaderNameFilter = "Custom/";

    [Tooltip("Hierarchy 变化(spawn/destroy 子物体)后自动重新收集。\n" +
             "如果子物体在运行时不变化,关闭以省 CPU")]
    public bool autoRecollectOnEnable = true;

    // =========================================================================
    // Runtime state
    // =========================================================================
    Renderer[] _renderers;
    bool[]     _rendererMask;     // renderer 级 mask:是否包含至少一个匹配 shader 的 material
    MaterialPropertyBlock _block;
    bool _dirty = true;

    // dirty 检测缓存
    Color _lastBaseColor;
    Color _lastEmissionWritten;
    bool  _lastOverrideBase;
    bool  _lastOverrideEmission;

    // =========================================================================
    // Lifecycle
    // =========================================================================
    void OnEnable()
    {
        if (_block == null) _block = new MaterialPropertyBlock();
        if (autoRecollectOnEnable || _renderers == null) CollectRenderers();
        _dirty = true;
        Apply();
    }

    void OnDisable()
    {
        // 默认不动 MPB(避免清掉 DissolveController 等并行写入的键)
        // 需要恢复材质原始外观时,显式调 RestoreOriginal()
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Inspector 改值后立即应用(编辑器友好)
        _dirty = true;

        // OnValidate 可能在 import / domain reload 时被调,不能直接访问 GameObject
        // 用 delayCall 推迟到下一个编辑器 tick
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            if (!isActiveAndEnabled) return;
            if (_block == null) _block = new MaterialPropertyBlock();
            if (_renderers == null) CollectRenderers();
            Apply();
        };
    }
#endif

    void LateUpdate()
    {
        // LateUpdate 而非 Update:Animation Track 动画化字段在 Update 阶段完成,
        // 在 LateUpdate 应用 MPB 确保 K 帧值最终生效。
        // 与 DissolveController 同样在 LateUpdate Apply,顺序无关
        // (两者都走 renderer-level Get→modify→Set,additive 合并)
        if (DetectChange()) _dirty = true;
        if (_dirty)
        {
            Apply();
            _dirty = false;
        }
    }

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>
    /// 重新收集子 Renderer 列表 + 重建 shader 过滤 mask。
    /// Hierarchy 变化(运行时新增/删除子物体)后需要手动调一次。
    /// </summary>
    [ContextMenu("Collect Renderers")]
    public void CollectRenderers()
    {
        _renderers = GetComponentsInChildren<Renderer>(includeInactive);
        _rendererMask = new bool[_renderers.Length];

        bool useFilter = !string.IsNullOrEmpty(shaderNameFilter);

        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (r == null) { _rendererMask[i] = false; continue; }

            if (!useFilter) { _rendererMask[i] = true; continue; }

            // 只要 renderer 任一 slot 的 shader 匹配,就整 renderer 受控
            // (renderer-level MPB 对所有 slot 应用,非匹配 shader 没该 property 会自动忽略)
            var mats = r.sharedMaterials;
            bool anyMatch = false;
            for (int j = 0; j < mats.Length; j++)
            {
                var m = mats[j];
                if (m == null || m.shader == null) continue;
                if (m.shader.name.Contains(shaderNameFilter))
                {
                    anyMatch = true;
                    break;
                }
            }
            _rendererMask[i] = anyMatch;
        }

        _dirty = true;
    }

    /// <summary>
    /// 立即把当前字段值推送到所有目标 Renderer 的 MPB。
    /// 正常情况下由 LateUpdate 在脏标记触发时自动调用,无需手动。
    /// </summary>
    [ContextMenu("Apply Now")]
    public void Apply()
    {
        if (_renderers == null || _renderers.Length == 0) return;
        if (_block == null) _block = new MaterialPropertyBlock();

        // 计算最终 emission(rgb 乘 intensity,alpha 透传)
        Color finalEmission = new Color(
            emissionColor.r * emissionIntensity,
            emissionColor.g * emissionIntensity,
            emissionColor.b * emissionIntensity,
            emissionColor.a);

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (!_rendererMask[i]) continue;
            var r = _renderers[i];
            if (r == null) continue;

            // *** 关键:renderer-level API (不带 materialIndex) ***
            // 与 DissolveController 共用同一存储槽,Get 会读到 dissolve 写入的字段,
            // 我们只修改 baseColor/emission 后写回,dissolve 字段保持不动
            r.GetPropertyBlock(_block);

            if (overrideBaseColor)
                _block.SetColor(ID_BaseColor, baseColor);

            if (overrideEmission)
                _block.SetColor(ID_EmissionColor, finalEmission);

            r.SetPropertyBlock(_block);
        }

        // 更新 dirty 缓存
        _lastBaseColor        = baseColor;
        _lastEmissionWritten  = finalEmission;
        _lastOverrideBase     = overrideBaseColor;
        _lastOverrideEmission = overrideEmission;
    }

    /// <summary>
    /// 恢复所有目标 Renderer 的材质原始外观(整块清掉 renderer-level MPB)。
    /// 警告:会同时清掉 DissolveController 写入的 dissolve 字段。
    /// 仅在确认无并行 controller 时调用。
    /// </summary>
    [ContextMenu("Restore Original (clears full renderer MPB)")]
    public void RestoreOriginal()
    {
        if (_renderers == null) return;
        if (_block == null) _block = new MaterialPropertyBlock();

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (!_rendererMask[i]) continue;
            var r = _renderers[i];
            if (r == null) continue;
            _block.Clear();
            r.SetPropertyBlock(_block);
        }
    }

    /// <summary>
    /// 仅把 _BaseColor / _EmissionColor 这两个键写回材质 sharedMaterial 上的原始值,
    /// 保留 MPB 上其他来源(如 dissolve)的键。
    /// 推荐 Timeline 结束 / 切场景前调用,与 DissolveController 安全共存。
    /// </summary>
    [ContextMenu("Restore Color Keys Only (dissolve-safe)")]
    public void RestoreColorKeysOnly()
    {
        if (_renderers == null) return;
        if (_block == null) _block = new MaterialPropertyBlock();

        bool useFilter = !string.IsNullOrEmpty(shaderNameFilter);

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (!_rendererMask[i]) continue;
            var r = _renderers[i];
            if (r == null) continue;

            // 从该 renderer 的 materials 找第一个匹配 shader 的原始值
            var mats = r.sharedMaterials;
            Color origBase = Color.white;
            Color origEmission = Color.black;
            bool foundBase = false, foundEmission = false;

            for (int j = 0; j < mats.Length; j++)
            {
                var m = mats[j];
                if (m == null || m.shader == null) continue;
                if (useFilter && !m.shader.name.Contains(shaderNameFilter)) continue;

                if (!foundBase && m.HasProperty(ID_BaseColor))
                {
                    origBase = m.GetColor(ID_BaseColor);
                    foundBase = true;
                }
                if (!foundEmission && m.HasProperty(ID_EmissionColor))
                {
                    origEmission = m.GetColor(ID_EmissionColor);
                    foundEmission = true;
                }
                if (foundBase && foundEmission) break;
            }

            r.GetPropertyBlock(_block);
            if (foundBase)     _block.SetColor(ID_BaseColor,     origBase);
            if (foundEmission) _block.SetColor(ID_EmissionColor, origEmission);
            r.SetPropertyBlock(_block);
        }

        // 重置 dirty 缓存,避免 LateUpdate 立刻又把当前 inspector 值写回去
        // (若希望继续覆盖,关掉 overrideBaseColor/Emission 后再调本方法)
        _lastBaseColor        = baseColor;
        _lastEmissionWritten  = new Color(
            emissionColor.r * emissionIntensity,
            emissionColor.g * emissionIntensity,
            emissionColor.b * emissionIntensity,
            emissionColor.a);
        _lastOverrideBase     = overrideBaseColor;
        _lastOverrideEmission = overrideEmission;
    }

    // =========================================================================
    // Internal
    // =========================================================================
    bool DetectChange()
    {
        Color targetEmission = new Color(
            emissionColor.r * emissionIntensity,
            emissionColor.g * emissionIntensity,
            emissionColor.b * emissionIntensity,
            emissionColor.a);

        return baseColor          != _lastBaseColor
            || targetEmission     != _lastEmissionWritten
            || overrideBaseColor  != _lastOverrideBase
            || overrideEmission   != _lastOverrideEmission;
    }
}
