using UnityEngine;
using UnityEngine.Rendering;

/// <summary>独立轮廓快照：捕获米制 SDF，按各自年龄向外扩散并淡出。</summary>
[ExecuteAlways, DefaultExecutionOrder(1300), DisallowMultipleComponent]
public sealed class ContactContourSnapshots : MonoBehaviour
{
    [Tooltip("拖入原 DissolvePlaneContact。只读取公开数据，不更换其 Mesh、材质或参数。")]
    public DissolvePlaneContact source;
    [Range(1,16), Tooltip("固定快照数量，满池时替换最老一份。默认六份。")]
    public int capacity = 6;
    [Min(0.01f), Tooltip("持续接触期间的捕获间隔（秒）；新接触立即捕获。")]
    public float captureInterval = 0.2f;
    [Min(0.01f), Tooltip("每份快照独立存在的秒数，到期完全消失。")]
    public float lifetime = 1.0f;
    [ColorUsage(false,true)] public Color lineColor = new Color(0.03f,0.25f,0.55f,1);
    [ColorUsage(false,true)] public Color glowColor = new Color(0.01f,0.08f,0.22f,1);
    [Tooltip("独立显示 Shader；保留引用以供构建使用。")]
    public Shader displayShader;
    [Tooltip("编辑模式自动捕获并淡出，使用编辑器时间；关闭后冻结已有快照，便于检查。默认关闭。")]
    public bool editorPreview;
    // 追加枚举值，保持旧场景中保存的检查模式不变。
    public enum SnapshotView { OriginalMask, FilledSection, SignedDistance, OffsetContour, ExpandingWave, Interference, MergedFront }
    [Tooltip("捕获时生成独立的米制 SDF。CPU 基线实现，会额外读取当前姿态，仅用于此套快照。")]
    public bool generateSdf = true;
    [Range(64,512), Tooltip("距离场分辨率。默认 256；越高边界越精细，捕获计算也越多。")]
    public int sdfResolution = 256;
    [Tooltip("原轮廓 / 填充 / 距离色带 / 固定等值线 / Expanding Wave 外扩 / Interference 干涉 / Merged Front 合并扩散区域，只画并集边界。")]
    public SnapshotView view = SnapshotView.OriginalMask;
    [Min(0), Tooltip("固定等值线离原边缘的距离，单位米；这是检查参数，还不是动态扩散。")]
    public float previewDistance = 0.1f;
    [Min(0.001f)] public float previewLineWidth = 0.015f;
    [Min(0), Tooltip("动态外扩速度，米/秒；每份快照使用自己的年龄。")]
    public float spreadSpeed = 0.3f;
    [Min(0.001f), Tooltip("动态波纹亮带总宽度，单位米。")]
    public float waveWidth = 0.025f;
    [Min(0.001f), Tooltip("亮带两侧柔和过渡的宽度，单位米。")]
    public float waveSoftness = 0.02f;
    [ColorUsage(false,true), Tooltip("动态扩散波纹的独立 HDR 颜色。")]
    public Color waveColor = new Color(0.08f,0.8f,1.4f,1);
    [Min(0.01f), Tooltip("干涉波长，单位米；越小波峰波谷越密。建议大于 SDF 像素尺寸的四倍。")]
    public float wavelength = 0.12f;
    [Min(0.001f), Tooltip("高斯波包宽度（标准差，米）；越大越容易与其他圈重叠。")]
    public float packetWidth = 0.18f;
    [Min(0), Tooltip("每圈的振幅系数，先带符号累加，最后才转换为亮度。")]
    public float amplitude = 0.5f;
    [Range(-180,180), Tooltip("新捕获波包的相位（度），捕获后固定。通常保持 0；180 可用于验证反相抵消。")]
    public float sourcePhase;
    [Tooltip("干涉振幅累加 Shader，保留引用以供打包。")]
    public Shader amplitudeShader;
    public RenderTexture InterferenceTexture => heightTexture;
    [Tooltip("融合波前 Shader；保留引用用于构建。Merged Front 只画所有扩散区域的并集边界。")]
    public Shader unionShader;
    public RenderTexture MergedTexture => mergedRead;
    public string SdfDiagnostic { get; private set; }
    public double LastSdfMilliseconds { get; private set; }
    public int ActiveCount { get; private set; }
    public int CapturedCount { get; private set; }
    public string Status { get; private set; }

    sealed class Snapshot
    {
        public RenderTexture texture;
        public Texture2D sdf;
        public bool hasSdf;
        public GameObject quad;
        public MeshRenderer renderer;
        public float birthTime;
        public bool active;
        public float phase;
    }
    Snapshot[] pool;
    Mesh mesh;
    Material material;
    MaterialPropertyBlock properties;
    GameObject displayRoot;
    RenderTexture sourceTexture;
    DissolvePlaneContact previousSource;
    Vector3 planeOrigin;
    Quaternion planeRotation;
    Vector2 planeSize;
    float clock, nextCapture;
    bool hadContact;
    ContactSnapshotSdf sdfBuilder;
    RenderTexture heightTexture;
    Material amplitudeMaterial;
    CommandBuffer amplitudeCommands;
    MeshRenderer interferenceDisplay;
    RenderTexture mergedRead,mergedWrite;
    Material unionMaterial;
    MeshRenderer mergedDisplay;

    void OnEnable()
    {
        if (displayShader == null) displayShader = Shader.Find("Custom/LiangZhu/ContactSnapshotDisplay");
        if (amplitudeShader == null) amplitudeShader = Shader.Find("Hidden/LiangZhu/ContactWaveAmplitude");
        if (unionShader == null) unionShader = Shader.Find("Hidden/LiangZhu/ContactWaveUnion");
        properties = new MaterialPropertyBlock();
        clock = nextCapture = 0;
        Status = "手动捕获或开启预览，生成轮廓快照及可选 SDF。";
#if UNITY_EDITOR
        // 编辑器回调只在 Editor 编译；构建后的播放器不依赖 UnityEditor。
        lastEditorTick = UnityEditor.EditorApplication.timeSinceStartup;
        UnityEditor.EditorApplication.update -= EditorTick;
        UnityEditor.EditorApplication.update += EditorTick;
        UnityEditor.EditorApplication.playModeStateChanged -= PlayModeChanged;
        UnityEditor.EditorApplication.playModeStateChanged += PlayModeChanged;
#endif
    }
    void LateUpdate()
    {
        // 编辑模式由 EditorTick 驱动，避免 ExecuteAlways 与编辑器回调重复推进年龄。
        if (Application.isPlaying) Step(Time.deltaTime);
    }

    /// <summary>在基础组件更新之后推进快照时钟。长帧只捕获当前姿态，不伪造遗漏的过去帧。</summary>
    public void Step(float deltaTime)
    {
        Advance(deltaTime,false);
    }

    [ContextMenu("Capture Snapshot / 手动捕获")]
    public void CaptureSnapshotNow()
    {
        if (!isActiveAndEnabled) return;
#if UNITY_EDITOR
        if (!Application.isPlaying && !CanPreviewScene()) return;
#endif
        // 手动点击前刷新当前姿态，避免捕获到上一次编辑器更新留下的旧 RT。
        // 只调用基础组件已有的刷新接口，不修改它的参数或实现。
        if (source != null && source.isActiveAndEnabled) source.RefreshContact();
        Advance(0,true);
        if (source != null && source.SegmentCount == 0) Status = "当前没有接触轮廓，未生成新快照。";
#if UNITY_EDITOR
        lastEditorTick = UnityEditor.EditorApplication.timeSinceStartup;
        UnityEditor.SceneView.RepaintAll();
#endif
    }

    void Advance(float deltaTime,bool forceCapture)
    {
        if (!isActiveAndEnabled) return;
        clock += Mathf.Max(0,deltaTime);
        Vector3 point, axis;
        if (source == null || !source.isActiveAndEnabled || source.ContactTexture == null ||
            !source.TryGetPlane(out point,out axis))
        {
            ClearSnapshots(); Status = "Waiting for a valid contact plane."; return;
        }
        var rt = source.ContactTexture;
        var rotation = source.transform.rotation;
        // 分辨率/容量变更重建池。平面坐标定义变化清空，避免把旧轮廓拉伸或搬到新位置。
        if (pool == null || pool.Length != Mathf.Clamp(capacity,1,16) || pool[0].texture.width != rt.width ||
            pool[0].texture.height != rt.height || !pool[0].texture.IsCreated())
        {
            ReleaseResources();
            if (!CreateResources(rt.width,rt.height)) return;
        }
        if (source != previousSource || sourceTexture != rt || planeSize != source.size ||
            (point-planeOrigin).sqrMagnitude > 1e-8f || Quaternion.Angle(rotation,planeRotation) > 0.01f)
        {
            ClearSnapshots(); previousSource = source; sourceTexture = rt;
            planeOrigin = point; planeRotation = rotation; planeSize = source.size;
        }
        bool touching = source.SegmentCount > 0;
        // 暂停时不新捕获。无接触仅停止产生新快照，已经存在的快照继续淡出。
        if (touching && (forceCapture || (deltaTime > 0 && (!hadContact || clock >= nextCapture))))
        {
            Capture(); nextCapture = clock + Mathf.Max(0.01f,captureInterval);
        }
        hadContact = touching && (forceCapture || deltaTime > 0 || hadContact);
        ActiveCount = 0;
        foreach (var s in pool)
        {
            if (!s.active) continue;
            float age = clock-s.birthTime;
            if (age >= Mathf.Max(0.01f,lifetime)) { s.active = false; s.renderer.enabled = false; continue; }
            ActiveCount++;
            // 干涉模式不再逐圈向屏幕输出颜色，统一显示后面的合成振幅图。
            s.renderer.enabled = view != SnapshotView.Interference && view != SnapshotView.MergedFront;
            properties.Clear();
            properties.SetTexture("_SnapshotMask",s.texture);
            properties.SetColor("_LineColor",lineColor);
            properties.SetColor("_GlowColor",glowColor);
            properties.SetTexture("_SnapshotSdf",s.hasSdf ? (Texture)s.sdf : Texture2D.blackTexture);
            properties.SetFloat("_HasSdf",s.hasSdf ? 1 : 0);
            properties.SetFloat("_ViewMode",(float)view);
            properties.SetFloat("_PreviewDistance",Mathf.Max(0,previewDistance));
            properties.SetFloat("_PreviewLineWidth",Mathf.Max(0.001f,previewLineWidth));
            // 只移动 SDF 等值线，不缩放平面或修改快照。离开接触后年龄仍持续增长。
            properties.SetFloat("_WaveRadius",Mathf.Max(0,spreadSpeed)*age);
            properties.SetFloat("_WaveWidth",Mathf.Max(0.001f,waveWidth));
            properties.SetFloat("_WaveSoftness",Mathf.Max(0.001f,waveSoftness));
            properties.SetColor("_WaveColor",waveColor);
            // 只改变显示强度，不写回纹理。纹理内容在该快照整个寿命期间保持固定。
            properties.SetFloat("_Fade",Mathf.Clamp01(1-age/Mathf.Max(0.01f,lifetime)));
            s.renderer.SetPropertyBlock(properties);
        }
        if (view == SnapshotView.Interference) RenderInterference();
        else if(interferenceDisplay!=null) interferenceDisplay.enabled=false;
        if(view==SnapshotView.MergedFront)RenderMergedFront();
        else if(mergedDisplay!=null)mergedDisplay.enabled=false;
        if(view==SnapshotView.MergedFront)return;
        if (view == SnapshotView.Interference) return;
        Status = view == SnapshotView.ExpandingWave
            ? "动态外扩：速度 × 每份快照年龄，按 Lifetime 淡出；无有效 SDF 时不显示波纹。"
            : "独立快照 + 米制 SDF。切换 View 到 Expanding Wave 查看动态外扩。";
    }

    void Capture()
    {
        Snapshot selected = null;
        foreach (var s in pool)
        {
            if (!s.active || clock-s.birthTime >= Mathf.Max(0.01f,lifetime)) { selected = s; break; }
            if (selected == null || s.birthTime < selected.birthTime) selected = s;
        }
        // 真正复制像素；不能写 selected.texture = source.ContactTexture，那只是引用实时 RT。
        var previous = RenderTexture.active;
        try { Graphics.Blit(source.ContactTexture,selected.texture); }
        finally { RenderTexture.active = previous; }
        selected.hasSdf = false;
        if (generateSdf)
        {
            if (sdfBuilder == null) sdfBuilder = new ContactSnapshotSdf();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            string diagnostic;
            selected.hasSdf = sdfBuilder.Build(source,Mathf.Clamp(Mathf.ClosestPowerOfTwo(sdfResolution),64,512),ref selected.sdf,out diagnostic);
            timer.Stop(); LastSdfMilliseconds = timer.Elapsed.TotalMilliseconds; SdfDiagnostic = diagnostic;
        }
        else SdfDiagnostic = "SDF 生成已关闭，仅保存 R/G 快照。";
        selected.birthTime = clock; selected.active = true;
        selected.phase = sourcePhase*Mathf.Deg2Rad;
        selected.quad.layer = source.gameObject.layer;
        selected.quad.transform.SetPositionAndRotation(planeOrigin,planeRotation);
        selected.quad.transform.localScale = new Vector3(planeSize.x,planeSize.y,1);
        selected.renderer.enabled = true;
        CapturedCount++;
    }

    bool CreateResources(int width,int height)
    {
        if (displayShader == null || !displayShader.isSupported) { Status = "Missing snapshot display shader."; return false; }
        material = new Material(displayShader) { hideFlags = HideFlags.HideAndDontSave };
        // 独立单位四边形，不借用/修改原组件生成的 Mesh。无父级缩放，世界尺寸明确。
        mesh = new Mesh { name = "Snapshot unit quad", hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = new[] {new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
        mesh.uv = new[] {Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
        mesh.triangles = new[] {0,1,2,0,2,3}; mesh.RecalculateBounds();
        displayRoot = new GameObject("Contact snapshots (runtime only)") { hideFlags = HideFlags.HideAndDontSave };
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(displayRoot,gameObject.scene);
        pool = new Snapshot[Mathf.Clamp(capacity,1,16)];
        for (int i=0;i<pool.Length;i++)
        {
            var s = new Snapshot(); pool[i] = s;
            // 快照从不反复衰减写回，ARGB32 足以保留原遮罩的精度。
            s.texture = new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear)
            { name = "Contour snapshot "+i, hideFlags = HideFlags.HideAndDontSave,
              wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, useMipMap = false };
            s.texture.Create();
            s.quad = new GameObject("Snapshot "+i) { hideFlags = HideFlags.HideAndDontSave };
            s.quad.transform.SetParent(displayRoot.transform,false);
            s.quad.AddComponent<MeshFilter>().sharedMesh = mesh;
            s.renderer = s.quad.AddComponent<MeshRenderer>(); s.renderer.sharedMaterial = material;
            s.renderer.shadowCastingMode = ShadowCastingMode.Off; s.renderer.receiveShadows = false;
            s.renderer.lightProbeUsage = LightProbeUsage.Off; s.renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            s.renderer.enabled = false;
        }
        return true;
    }
    // 只读调试入口：可检查某个槽的纹理是否独立、过期后是否被复用。
    public RenderTexture GetSnapshotTexture(int index) => pool != null && index >= 0 && index < pool.Length ? pool[index].texture : null;
    public Texture2D GetSnapshotSdf(int index) => pool != null && index >= 0 && index < pool.Length && pool[index].hasSdf ? pool[index].sdf : null;
    [ContextMenu("Clear Snapshots / 清空快照")]
    public void ClearSnapshots()
    {
        if (pool != null) foreach (var s in pool) { s.active = false; s.renderer.enabled = false; }
        ActiveCount = 0; hadContact = false; nextCapture = clock;
        if(interferenceDisplay!=null)interferenceDisplay.enabled=false;
        if(mergedDisplay!=null)mergedDisplay.enabled=false;
        ClearMergedTexture();
        if(heightTexture!=null){var old=RenderTexture.active;RenderTexture.active=heightTexture;GL.Clear(false,true,Color.clear);RenderTexture.active=old;}
    }

    void ClearMergedTexture()
    {
        if(mergedRead==null)return;
        var old=RenderTexture.active;
        try{RenderTexture.active=mergedRead;GL.Clear(false,true,new Color(10000,0,0,0));}
        finally{RenderTexture.active=old;}
    }
    void RenderMergedFront()
    {
        if(unionShader==null || !unionShader.isSupported || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
        {Status="融合 Shader 或半浮点 RT 不可用。";if(mergedDisplay!=null)mergedDisplay.enabled=false;return;}
        if(mergedRead==null)
        {
            mergedRead=new RenderTexture(sourceTexture.width,sourceTexture.height,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear)
            {name="Union distance A",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};mergedRead.Create();
            mergedWrite=new RenderTexture(mergedRead.descriptor)
            {name="Union distance B",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};mergedWrite.Create();
            unionMaterial=new Material(unionShader){hideFlags=HideFlags.HideAndDontSave};
            var go=new GameObject("Unified merged front"){hideFlags=HideFlags.HideAndDontSave};go.transform.SetParent(displayRoot.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;mergedDisplay=go.AddComponent<MeshRenderer>();mergedDisplay.sharedMaterial=material;
            mergedDisplay.shadowCastingMode=ShadowCastingMode.Off;mergedDisplay.receiveShadows=false;
        }
        // 每次从空并集开始重算；ping-pong 保持距离与其所属淡出值同步，禁止原地读写。
        ClearMergedTexture();int valid=0;
        var old=RenderTexture.active;
        try{foreach(var s in pool)
        {
            if(!s.active || !s.hasSdf)continue;valid++;
            float age=clock-s.birthTime;
            unionMaterial.SetTexture("_SnapshotSdf",s.sdf);
            unionMaterial.SetFloat("_Radius",Mathf.Max(0,spreadSpeed)*age);
            unionMaterial.SetFloat("_Fade",Mathf.Clamp01(1-age/Mathf.Max(.01f,lifetime)));
            Graphics.Blit(mergedRead,mergedWrite,unionMaterial);
            var swap=mergedRead;mergedRead=mergedWrite;mergedWrite=swap;
        }}finally{RenderTexture.active=old;}
        mergedDisplay.gameObject.layer=source.gameObject.layer;
        mergedDisplay.transform.SetPositionAndRotation(planeOrigin,planeRotation);
        mergedDisplay.transform.localScale=new Vector3(planeSize.x,planeSize.y,1);
        properties.Clear();properties.SetFloat("_ViewMode",6);properties.SetTexture("_MergedDistance",mergedRead);
        properties.SetFloat("_WaveWidth",Mathf.Max(.001f,waveWidth));properties.SetFloat("_WaveSoftness",Mathf.Max(.001f,waveSoftness));properties.SetColor("_WaveColor",waveColor);
        mergedDisplay.SetPropertyBlock(properties);mergedDisplay.enabled=valid>0;
        Status="融合波前：仅画扩散区域的并集边界。相交内部线和被包含的内圈隐藏。";
    }
    void RenderInterference()
    {
        if(amplitudeShader==null || !amplitudeShader.isSupported || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
        {Status="干涉 Shader 或半浮点 RT 不可用。";if(interferenceDisplay!=null)interferenceDisplay.enabled=false;return;}
        if(heightTexture==null)
        {
            // 浮点 RT 才能保留负振幅。每次重算并清零，不保存历史累计，避免振幅无限增长。
            heightTexture=new RenderTexture(sourceTexture.width,sourceTexture.height,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear)
            {name="Signed interference height",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};heightTexture.Create();
            amplitudeMaterial=new Material(amplitudeShader){hideFlags=HideFlags.HideAndDontSave};
            amplitudeCommands=new CommandBuffer{name="Snapshot wave interference"};
            var go=new GameObject("Unified interference display"){hideFlags=HideFlags.HideAndDontSave};go.transform.SetParent(displayRoot.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;interferenceDisplay=go.AddComponent<MeshRenderer>();interferenceDisplay.sharedMaterial=material;
            interferenceDisplay.shadowCastingMode=ShadowCastingMode.Off;interferenceDisplay.receiveShadows=false;
        }
        amplitudeCommands.Clear();amplitudeCommands.SetRenderTarget(heightTexture);
        amplitudeCommands.SetViewport(new Rect(0,0,heightTexture.width,heightTexture.height));
        amplitudeCommands.ClearRenderTarget(false,true,Color.clear);
        foreach(var s in pool)
        {
            if(!s.active || !s.hasSdf)continue;
            float age=clock-s.birthTime;properties.Clear();
            properties.SetTexture("_SnapshotSdf",s.sdf);
            properties.SetFloat("_Radius",Mathf.Max(0,spreadSpeed)*age);
            properties.SetFloat("_Wavelength",Mathf.Max(0.01f,wavelength));
            properties.SetFloat("_PacketWidth",Mathf.Max(0.001f,packetWidth));
            properties.SetFloat("_Amplitude",Mathf.Max(0,amplitude));
            properties.SetFloat("_Phase",s.phase);
            properties.SetFloat("_Fade",Mathf.Clamp01(1-age/Mathf.Max(0.01f,lifetime)));
            amplitudeCommands.DrawMesh(mesh,Matrix4x4.identity,amplitudeMaterial,0,0,properties);
        }
        var previous=RenderTexture.active;
        try{Graphics.ExecuteCommandBuffer(amplitudeCommands);}finally{RenderTexture.active=previous;}
        interferenceDisplay.gameObject.layer=source.gameObject.layer;
        interferenceDisplay.transform.SetPositionAndRotation(planeOrigin,planeRotation);
        interferenceDisplay.transform.localScale=new Vector3(planeSize.x,planeSize.y,1);
        properties.Clear();properties.SetFloat("_ViewMode",5);properties.SetTexture("_InterferenceHeight",heightTexture);properties.SetColor("_WaveColor",waveColor);
        interferenceDisplay.SetPropertyBlock(properties);interferenceDisplay.enabled=ActiveCount>0;
        Status="干涉模式：正负振幅先累加，再统一取绝对值显示；不是完整水体模拟。";
    }
    void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
        UnityEditor.EditorApplication.playModeStateChanged -= PlayModeChanged;
#endif
        ReleaseResources();
    }

#if UNITY_EDITOR
    double lastEditorTick;
    bool CanPreviewScene()
    {
        // 不在 Prefab 资产或预览场景里创建临时显示对象。
        return gameObject.scene.IsValid() && gameObject.scene.isLoaded &&
            !UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene) &&
            !UnityEditor.EditorUtility.IsPersistent(this) &&
            (source == null || (!UnityEditor.EditorUtility.IsPersistent(source) &&
                !UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(source.gameObject.scene)));
    }
    void EditorTick()
    {
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (Application.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode ||
            UnityEditor.EditorApplication.isCompiling || !editorPreview || !isActiveAndEnabled || !CanPreviewScene())
        {
            // 关闭预览时不断同步计时基准，重新打开不会把暂停时长算进寿命。
            lastEditorTick = now; return;
        }
        double elapsed = now-lastEditorTick;
        if (elapsed < 1.0/30.0) return; // 限制到约 30 Hz，避免编辑器空闲时全速 BakeMesh。
        lastEditorTick = now;
        if (source != null && source.isActiveAndEnabled) source.RefreshContact();
        Step((float)elapsed);
        UnityEditor.SceneView.RepaintAll();
    }
    void PlayModeChanged(UnityEditor.PlayModeStateChange state)
    {
        // 兼容关闭 Domain Reload 的项目，进出 Play 时清除编辑预览并重新计时。
        ReleaseResources(); clock = nextCapture = 0;
        lastEditorTick = UnityEditor.EditorApplication.timeSinceStartup;
    }
#endif
    void ReleaseResources()
    {
        if(mergedDisplay!=null)mergedDisplay.enabled=false;
        if(interferenceDisplay!=null)interferenceDisplay.enabled=false;
        if (pool != null) foreach (var s in pool)
        {
            s.renderer.enabled = false;
            s.texture.Release(); Dispose(s.texture);
            Dispose(s.sdf);
        }
        Dispose(displayRoot); Dispose(mesh); Dispose(material);
        pool = null; displayRoot = null; mesh = null; material = null;
        sourceTexture = null; previousSource = null; ActiveCount = 0; hadContact = false;
        sdfBuilder?.Dispose(); sdfBuilder = null;
        if(heightTexture!=null){heightTexture.Release();Dispose(heightTexture);}heightTexture=null;
        Dispose(amplitudeMaterial);amplitudeMaterial=null;amplitudeCommands?.Release();amplitudeCommands=null;interferenceDisplay=null;
        if(mergedRead!=null){mergedRead.Release();Dispose(mergedRead);}if(mergedWrite!=null){mergedWrite.Release();Dispose(mergedWrite);}
        mergedRead=mergedWrite=null;Dispose(unionMaterial);unionMaterial=null;mergedDisplay=null;
    }
    static void Dispose(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}


