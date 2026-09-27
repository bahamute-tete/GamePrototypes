using UnityEngine;

/// <summary>独立维护平面 UV 空间的轮廓历史，不改写基础接触纹理。</summary>
[ExecuteAlways, DefaultExecutionOrder(1200), DisallowMultipleComponent]
[RequireComponent(typeof(DissolvePlaneContact))]
public sealed class ContactPlaneHistory : MonoBehaviour
{
    [Min(0.01f), Tooltip("旧轮廓亮度减半所需的秒数。0.5 表示约两秒后只剩 6.25%。")]
    public float halfLife = 0.5f;
    [Range(0.0001f,0.02f), Tooltip("历史强度低于此值时清零，确保最后完全消失。")]
    public float cutoff = 0.001f;
    [ColorUsage(false,true), Tooltip("历史细轮廓的 HDR 颜色，与当前接触线分开控制。")]
    public Color trailLineColor = new Color(0.04f,0.5f,1.0f,1);
    [ColorUsage(false,true), Tooltip("历史柔光及流动能量的 HDR 颜色。")]
    public Color trailGlowColor = new Color(0.015f,0.25f,0.7f,1);
    [Tooltip("请保留引用，确保打包时包含历史更新 Shader。")]
    public Shader historyShader;
    public RenderTexture HistoryTexture => historyRead;

    DissolvePlaneContact contact;
    MeshRenderer display;
    MaterialPropertyBlock properties;
    Material material;
    RenderTexture historyRead, historyWrite, source;
    Vector2 historySize;
    DissolveController historyController;

    void OnEnable()
    {
        contact = GetComponent<DissolvePlaneContact>();
        display = GetComponent<MeshRenderer>();
        properties = new MaterialPropertyBlock();
        if (historyShader == null) historyShader = Shader.Find("Hidden/LiangZhu/ContactHistory");
    }

    void LateUpdate()
    {
        // 正常播放才累积历史，编辑场景不会因拖动对象而留下难以清除的痕迹。
        // 顺序 1200 晚于求交 1000 和能量设置 1100，读取的都是当前帧数据。
        if (Application.isPlaying) StepHistory(Time.deltaTime);
        else Bind(false);
    }

    /// <summary>在当前 ContactTexture 已更新后调用。公开入口也方便定时验证半衰期。</summary>
    public void StepHistory(float deltaTime)
    {
        if (!isActiveAndEnabled) return;
        Vector3 point, axis;
        if (contact == null || !contact.isActiveAndEnabled || contact.ContactTexture == null ||
            !contact.TryGetPlane(out point,out axis))
        {
            ClearHistory(); Bind(false); return;
        }
        if (!EnsureResources()) { Bind(false); return; }
        // 同一个 RT 在重新启用时也可能换了实例；平面尺寸/目标 Controller 变化时清空历史。
        // 位移和旋转不清空：本版残影附着在门的 UV 上，随门一起移动。
        if (source != contact.ContactTexture || historySize != contact.size || historyController != contact.controller)
        {
            ClearHistory(); source = contact.ContactTexture;
            historySize = contact.size; historyController = contact.controller;
        }
        material.SetTexture("_CurrentMask",source);
        material.SetFloat("_Decay",Mathf.Pow(0.5f,Mathf.Max(0,deltaTime)/Mathf.Max(0.01f,halfLife)));
        material.SetFloat("_Cutoff",Mathf.Clamp(cutoff,0.0001f,0.02f));
        var previous = RenderTexture.active;
        try { Graphics.Blit(historyRead,historyWrite,material); }
        finally { RenderTexture.active = previous; }
        // 交换角色；绝不在同一次绘制中读取并写入同一张 RT。
        var swap = historyRead; historyRead = historyWrite; historyWrite = swap;
        Bind(true);
    }

    bool EnsureResources()
    {
        if (historyShader == null || !historyShader.isSupported) return false;
        if (material == null) material = new Material(historyShader) { hideFlags = HideFlags.HideAndDontSave };
        int width = contact.ContactTexture.width, height = contact.ContactTexture.height;
        if (historyRead != null && historyRead.width == width && historyRead.height == height &&
            historyRead.IsCreated() && historyWrite.IsCreated()) return true;
        ReleaseTextures();
        // 避免 ARGB32 在多次乘衰减系数后出现量化残留；不支持 Half 时尝试 Float。
        var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
            ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGBFloat;
        if (!SystemInfo.SupportsRenderTextureFormat(format)) return false;
        historyRead = CreateTexture(width,height,format,"Contact history A");
        historyWrite = CreateTexture(width,height,format,"Contact history B");
        ClearHistory();
        return true;
    }
    static RenderTexture CreateTexture(int width,int height,RenderTextureFormat format,string label)
    {
        var rt = new RenderTexture(width,height,0,format,RenderTextureReadWrite.Linear)
        { name = label, hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear,
          wrapMode = TextureWrapMode.Clamp, useMipMap = false, antiAliasing = 1 };
        rt.Create(); return rt;
    }

    [ContextMenu("Clear History / 清空残影")]
    public void ClearHistory()
    {
        var previous = RenderTexture.active;
        try
        {
            if (historyRead != null) { RenderTexture.active = historyRead; GL.Clear(false,true,Color.clear); }
            if (historyWrite != null) { RenderTexture.active = historyWrite; GL.Clear(false,true,Color.clear); }
        }
        finally { RenderTexture.active = previous; }
    }
    void Bind(bool enabled)
    {
        if (display == null || properties == null) return;
        // 先读回属性块，只改自己拥有的属性，保留能量门的噪声和颜色设置。
        display.GetPropertyBlock(properties);
        properties.SetFloat("_TrailEnabled",enabled ? 1 : 0);
        properties.SetTexture("_HistoryMask",enabled && historyRead != null ? (Texture)historyRead : Texture2D.blackTexture);
        properties.SetColor("_TrailLineColor",trailLineColor);
        properties.SetColor("_TrailGlowColor",trailGlowColor);
        display.SetPropertyBlock(properties);
    }
    void OnDisable()
    {
        Bind(false); ReleaseTextures(); Dispose(material); material = null;
    }
    void ReleaseTextures()
    {
        if (historyRead != null) { historyRead.Release(); Dispose(historyRead); }
        if (historyWrite != null) { historyWrite.Release(); Dispose(historyWrite); }
        historyRead = historyWrite = source = null;
    }
    static void Dispose(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
    }
}
