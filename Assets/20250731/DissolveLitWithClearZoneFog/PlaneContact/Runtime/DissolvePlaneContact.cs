using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Plane-space contact mask. The existing controller remains the source of truth.</summary>
[ExecuteAlways, DefaultExecutionOrder(1000), DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
// 核心数据链：姿态网格 -> 平面坐标 -> 交线 -> 矩形网格 -> RT -> 显示平面。
public sealed class DissolvePlaneContact : MonoBehaviour
{
    public DissolveController controller;
    [Tooltip("Follow World Direction, Origin and Plane Offset. Reverse changes the dissolved side only.")]
    public bool followControllerPlane = true;
    [Tooltip("World-space metres; independent of this object's scale.")]
    public Vector2 size = new Vector2(3, 3);
    [Range(128, 2048)] public int resolution = 512;
    [Min(0.001f)] public float lineWidth = 0.018f;
    [Min(0.001f)] public float glowRadius = 0.075f;
    [ColorUsage(false, true)] public Color lineColor = new Color(0.35f, 1.8f, 2.4f, 1);
    [ColorUsage(false, true)] public Color glowColor = new Color(0.02f, 0.55f, 0.9f, 1);
    public Color surfaceTint = new Color(0.02f, 0.12f, 0.18f, 0.035f);
    [Tooltip("Optional overrides, for low-poly contact proxies. Empty uses controlledRenderers.")]
    public List<Renderer> contactRenderers = new List<Renderer>();
    [Tooltip("Assign assets to keep both shaders in player builds.")]
    public Shader maskShader;
    public Shader surfaceShader;
    public RenderTexture ContactTexture => texture;
    public int SegmentCount { get; private set; }
    public string Status { get; private set; }

    // 每个 Renderer 复用一份姿态/拓扑缓存。假设同一 Mesh 的拓扑不在运行时原地变化。
    sealed class Source
    {
        public Renderer renderer;
        public Mesh original, baked;
        public readonly List<Vector3> vertices = new List<Vector3>();
        public Vector3[] planeVertices;
        public readonly List<int> triangles = new List<int>();
        public bool initialized;
    }
    readonly Dictionary<Renderer, Source> cache = new Dictionary<Renderer, Source>();
    readonly HashSet<Renderer> visited = new HashSet<Renderer>();
    readonly List<Renderer> stale = new List<Renderer>();
    readonly List<int> submesh = new List<int>();
    readonly List<Vector3> positions = new List<Vector3>();
    readonly List<Vector2> coordinates = new List<Vector2>();
    readonly List<Vector2> lengths = new List<Vector2>();
    readonly List<int> indices = new List<int>();
    // strokes 只用于生成遮罩；quad 才是场景中可见的承载平面。
    Mesh strokes, quad, previousMesh;
    Material maskMaterial, surfaceMaterial, previousMaterial;
    MeshFilter filter;
    MeshRenderer display;
    RenderTexture texture;
    CommandBuffer commands;
    Vector3 origin, right, up, normal;
    readonly Vector3[] quadVertices = new Vector3[4];

    void OnEnable()
    {
        filter = GetComponent<MeshFilter>(); display = GetComponent<MeshRenderer>();
        previousMesh = filter.sharedMesh; previousMaterial = display.sharedMaterial;
    }
    // 在默认顺序的移动/溶解脚本之后读取当前帧数据；编辑模式也可刷新。
    void LateUpdate() { RefreshContact(); }

    // 默认读取现有 Controller 的 World Direction 平面，而不是重新定义一套裁剪规则。
    public bool TryGetPlane(out Vector3 point, out Vector3 axis)
    {
        point = transform.position; axis = transform.forward;
        // 关闭跟随时，改用本物体的位置和 forward；适合独立接触代理用途。
        if (!followControllerPlane) return true;
        if (controller == null || !controller.isActiveAndEnabled ||
            controller.space != DissolveController.DissolveSpace.World ||
            controller.mode != DissolveController.DissolveMode.Direction) return false;
        // 法线必须单位化，才能让偏移和点积距离都保持米制。
        axis = controller.axisDirection.sqrMagnitude > 1e-6f ? controller.axisDirection.normalized : Vector3.up;
        // Reverse 只改变消失侧，不能改变实体平面的位置；这里不反转法线。
        point = (controller.worldOrigin != null ? controller.worldOrigin.position : Vector3.zero) + axis * controller.planeOffset;
        return true;
    }

    // 每次重建当前接触图，不保留上一帧，因此本版没有残留/波纹历史状态。
    public void RefreshContact()
    {
        if (!isActiveAndEnabled) return;
        size = new Vector2(Mathf.Max(.01f, size.x), Mathf.Max(.01f, size.y));
        resolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(resolution), 128, 2048);
        if (!EnsureResources()) return;
        SegmentCount = 0; Status = "Ready";
        positions.Clear(); coordinates.Clear(); lengths.Clear(); indices.Clear(); visited.Clear();
        bool valid = TryGetPlane(out origin, out normal);
        if (valid)
        {
            if (followControllerPlane)
            {
                // 当法线近乎平行世界 Up 时更换参考轴，避免 LookRotation 的朝向退化。
                Vector3 hint = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > .99f ? Vector3.forward : Vector3.up;
                transform.SetPositionAndRotation(origin, Quaternion.LookRotation(normal, hint));
            }
            right = transform.right; up = transform.up;
            // 可指定低模蒙皮代理以降低 CPU 成本；留空时沿用角色溶解目标。
            var sources = contactRenderers.Count > 0 ? contactRenderers : controller != null ? controller.controlledRenderers : null;
            if (sources != null) foreach (var renderer in sources)
            {
                if (renderer == null || renderer == display || !renderer.gameObject.activeInHierarchy || !visited.Add(renderer)) continue;
                // Do not depend on Renderer.enabled: the dissolve controller may cull fully dissolved renderers.
                // 包围盒先做粗筛：盒子沿法线的投影区间不含平面，就不需要 BakeMesh/求交。
                Bounds b = renderer.bounds;
                // dot(abs(normal), extents) 是世界 AABB 在法线方向上的投影半长度。
                float radius = Vector3.Dot(new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z)), b.extents);
                if (Mathf.Abs(Vector3.Dot(b.center - origin, normal)) > radius + .001f) continue;
                AppendRenderer(renderer);
            }
            if (controller != null && controller.edgeNoiseStrength > 0)
                Status = "Contact follows the geometric plane; dissolve edge noise is not included.";
        }
        else Status = "Assign an enabled World / Direction Dissolve Controller.";
        // 清理已移除的目标缓存，避免其临时蒙皮 Mesh 长期占用内存。
        stale.Clear();
        foreach (var entry in cache) if (entry.Key == null || !visited.Contains(entry.Key)) stale.Add(entry.Key);
        foreach (var key in stale) { DisposeObject(cache[key].baked); cache.Remove(key); }
        // 将所有交线矩形合并上传。UV0=段内米制坐标，UV1.x=段长。
        strokes.Clear();
        strokes.SetVertices(positions); strokes.SetUVs(0, coordinates); strokes.SetUVs(1, lengths);
        strokes.SetTriangles(indices, 0, true);
        // UI 提供总线宽，Shader 使用半宽；柔光范围也是世界米。
        maskMaterial.SetFloat("_CoreHalfWidth", Mathf.Max(.0005f, lineWidth * .5f));
        maskMaterial.SetFloat("_GlowRadius", Mathf.Max(.001f, glowRadius));
        // 直接向 RT 绘制，不需要额外摄像机，因为 strokes 已是裁剪空间二维坐标。
        commands.Clear(); commands.SetRenderTarget(texture);
        commands.SetViewport(new Rect(0, 0, resolution, resolution));
        // 即使没有交线也先清空，防止角色离开后残留上一帧光圈。
        commands.ClearRenderTarget(false, true, Color.clear);
        if (indices.Count > 0) commands.DrawMesh(strokes, Matrix4x4.identity, maskMaterial);
        var previousTarget = RenderTexture.active;
        Graphics.ExecuteCommandBuffer(commands);
        RenderTexture.active = previousTarget;
        // 将同一张贴图及同一套平面坐标传给显示 Shader，完成几何到显示的对应。
        surfaceMaterial.SetTexture("_ContactMask", texture);
        surfaceMaterial.SetColor("_LineColor", lineColor);
        surfaceMaterial.SetColor("_GlowColor", glowColor);
        surfaceMaterial.SetColor("_SurfaceTint", surfaceTint);
        surfaceMaterial.SetVector("_PlaneOrigin", origin);
        surfaceMaterial.SetVector("_PlaneRight", right);
        surfaceMaterial.SetVector("_PlaneUp", up);
        surfaceMaterial.SetVector("_PlaneSize", new Vector4(size.x, size.y, 0, 0));
        // World-space mapping allows the component to live under a scaled parent.
        if (quad != null)
        {
            var inv = transform.worldToLocalMatrix;
            quadVertices[0] = inv.MultiplyPoint3x4(origin-right*size.x*.5f-up*size.y*.5f);
            quadVertices[1] = inv.MultiplyPoint3x4(origin+right*size.x*.5f-up*size.y*.5f);
            quadVertices[2] = inv.MultiplyPoint3x4(origin+right*size.x*.5f+up*size.y*.5f);
            quadVertices[3] = inv.MultiplyPoint3x4(origin-right*size.x*.5f+up*size.y*.5f);
            quad.vertices = quadVertices;
            quad.RecalculateBounds();
        }
    }

    // 取得该对象的当前几何并为每个相交三角形生成一段轮廓。
    void AppendRenderer(Renderer renderer)
    {
        var skin = renderer as SkinnedMeshRenderer;
        var mf = skin == null ? renderer.GetComponent<MeshFilter>() : null;
        var mesh = skin != null ? skin.sharedMesh : mf != null ? mf.sharedMesh : null;
        if (mesh == null) return;
        if (!cache.TryGetValue(renderer, out var source) || source.original != mesh)
        {
            if (source != null) DisposeObject(source.baked);
            source = new Source { renderer = renderer, original = mesh };
            if (skin != null) { source.baked = new Mesh { name = "Contact pose", hideFlags = HideFlags.HideAndDontSave }; source.baked.MarkDynamic(); }
            cache[renderer] = source;
        }
        Mesh readable = mesh;
        // BakeMesh 在 CPU 取得当前骨骼/BlendShape 姿态，复用目标 Mesh；不是磁盘烘焙。
        // false 不把 Transform 缩放额外烘入，后续通过 localToWorldMatrix 应用变换。
        if (skin != null) { skin.BakeMesh(source.baked, false); readable = source.baked; }
        if (!readable.isReadable) { Status = renderer.name + ": enable mesh Read/Write or use a readable contact proxy."; return; }
        // 索引描述三角形连接关系，普通骨骼动画不会改变它，只需首次缓存。
        if (!source.initialized)
        {
            for (int s = 0; s < readable.subMeshCount; s++)
            {
                if (readable.GetTopology(s) != MeshTopology.Triangles) continue;
                readable.GetTriangles(submesh, s); source.triangles.AddRange(submesh);
            }
            source.initialized = true;
        }
        // 蒙皮顶点每次更新；静态顶点只读一次，移动通过后面的矩阵反映。
        if (skin != null || source.vertices.Count == 0) readable.GetVertices(source.vertices);
        if (source.planeVertices == null || source.planeVertices.Length != source.vertices.Count)
            source.planeVertices = new Vector3[source.vertices.Count];
        // 世界点 P -> (dot(P-origin,right), dot(P-origin,up), dot(P-origin,normal))。
        // 前两项为平面内坐标，第三项为有符号距离；接触平面统一变成 z=0。
        var worldToPlane = Matrix4x4.identity;
        worldToPlane.SetRow(0, new Vector4(right.x,right.y,right.z,-Vector3.Dot(origin,right)));
        worldToPlane.SetRow(1, new Vector4(up.x,up.y,up.z,-Vector3.Dot(origin,up)));
        worldToPlane.SetRow(2, new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(origin,normal)));
        // 预先合成两次变换，避免逐顶点重复做世界变换、减原点和三次点积。
        var localToPlane = worldToPlane * renderer.localToWorldMatrix;
        for (int i = 0; i < source.vertices.Count; i++)
        {
            source.planeVertices[i] = localToPlane.MultiplyPoint3x4(source.vertices[i]);
        }
        var v = source.planeVertices; var t = source.triangles;
        for (int i = 0; i + 2 < t.Count; i += 3)
            if (IntersectTriangle(v[t[i]], v[t[i+1]], v[t[i+2]], out var a, out var b)) AddSegment(a, b);
    }

    // 输入已在平面坐标系中。成功时输出两个二维交点，组成当前三角形的截线。
    public static bool IntersectTriangle(Vector3 a, Vector3 b, Vector3 c, out Vector2 start, out Vector2 end)
    {
        start = end = default; const float epsilon = 1e-6f;
        // 三个顶点严格同侧，不可能穿过 z=0，先快速跳过。
        if ((a.z > epsilon && b.z > epsilon && c.z > epsilon) ||
            (a.z < -epsilon && b.z < -epsilon && c.z < -epsilon)) return false;
        // 完全共面的面不画内部三角网格边；相邻非共面三角形仍可提供边界。
        if (Mathf.Abs(a.z) < epsilon && Mathf.Abs(b.z) < epsilon && Mathf.Abs(c.z) < epsilon) return false;
        int count = 0;
        CrossEdge(a, b, ref count, ref start, ref end);
        CrossEdge(b, c, ref count, ref start, ref end);
        CrossEdge(c, a, ref count, ref start, ref end);
        return count >= 2 && (start-end).sqrMagnitude > 1e-12f;
    }
    // 检查一条三角形边与 z=0 的交点，并处理端点恰好落在平面上的情况。
    static void CrossEdge(Vector3 a, Vector3 b, ref int count, ref Vector2 first, ref Vector2 second)
    {
        if (Mathf.Abs(a.z) < 1e-6f) AddPoint(a, ref count, ref first, ref second);
        if (Mathf.Abs(b.z) < 1e-6f) AddPoint(b, ref count, ref first, ref second);
        // 两端异号时，令 a.z+t*(b.z-a.z)=0，解得 t=a.z/(a.z-b.z)。
        if ((a.z < 0 && b.z > 0) || (a.z > 0 && b.z < 0))
            AddPoint(Vector3.LerpUnclamped(a, b, a.z / (a.z-b.z)), ref count, ref first, ref second);
    }
    // 同一顶点可能被相邻两条边重复发现，距离去重后只保留两个不同端点。
    static void AddPoint(Vector2 p, ref int count, ref Vector2 first, ref Vector2 second)
    {
        if (count == 0) { first = p; count = 1; }
        else if ((p-first).sqrMagnitude > 1e-12f && count == 1) { second = p; count = 2; }
    }
    // 每段交线扩成一个矩形。矩形只是光栅化范围，圆头线/柔光由 Shader 求距离生成。
    void AddSegment(Vector2 a, Vector2 b)
    {
        float pixel = Mathf.Max(size.x, size.y) / resolution;
        // 确保矩形能装下亮线和柔光，并给边缘抗锯齿留下两个像素余量。
        float radius = Mathf.Max(lineWidth*.5f, glowRadius) + pixel*2;
        if (Mathf.Min(a.x,b.x) > size.x*.5f+radius || Mathf.Max(a.x,b.x) < -size.x*.5f-radius ||
            Mathf.Min(a.y,b.y) > size.y*.5f+radius || Mathf.Max(a.y,b.y) < -size.y*.5f-radius) return;
        float length = (b-a).magnitude; if (length < 1e-6f) return;
        // along 沿线段，(-y,x) 将其转 90 度得到垂直方向 side。
        Vector2 along = (b-a)/length, side = new Vector2(-along.y, along.x);
        int n = positions.Count;
        // 四角位置沿线段两端和两侧扩展；UV 原点在 a，终点 b 对应 (length,0)。
        AddCorner(a-along*radius-side*radius, new Vector2(-radius,-radius), length);
        AddCorner(b+along*radius-side*radius, new Vector2(length+radius,-radius), length);
        AddCorner(b+along*radius+side*radius, new Vector2(length+radius,radius), length);
        // 四角位置沿线段两端和两侧扩展；UV 原点在 a，终点 b 对应 (length,0)。
        AddCorner(a-along*radius+side*radius, new Vector2(-radius,radius), length);
        // 四个顶点组成两个三角形；n 是当前矩形在总顶点列表中的起始索引。
        indices.Add(n); indices.Add(n+1); indices.Add(n+2); indices.Add(n); indices.Add(n+2); indices.Add(n+3);
        SegmentCount++;
    }
    // POSITION 用整个平面的归一化坐标；UV 用每条线自己的米制坐标，二者不能混淆。
    void AddCorner(Vector2 p, Vector2 uv, float length)
    {
        // 平面左右边缘由 +/-size.x/2 映射到 +/-1，上下同理；Shader 中直接输出。
        positions.Add(new Vector3(p.x*2/size.x, p.y*2/size.y, 0));
        coordinates.Add(uv); lengths.Add(new Vector2(length, 0));
    }
    // 按需创建并复用资源，避免每帧分配 Mesh/Material/RT；分辨率变化时重建 RT。
    bool EnsureResources()
    {
        if (maskShader == null) maskShader = Shader.Find("Hidden/LiangZhu/ContactMask");
        if (surfaceShader == null) surfaceShader = Shader.Find("Custom/LiangZhu/ContactPlane");
        if (maskShader == null || surfaceShader == null) { Status = "Missing contact shaders."; return false; }
        if (maskMaterial == null) maskMaterial = new Material(maskShader) { hideFlags = HideFlags.HideAndDontSave };
        if (surfaceMaterial == null) surfaceMaterial = new Material(surfaceShader) { hideFlags = HideFlags.HideAndDontSave };
        if (strokes == null) { strokes = new Mesh { name = "Contact segments", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.HideAndDontSave }; strokes.MarkDynamic(); }
        if (quad == null)
        {
            quad = new Mesh { name = "Contact plane", hideFlags = HideFlags.HideAndDontSave };
            quad.vertices = new Vector3[4]; quad.triangles = new[] {0,2,1,0,3,2};
        }
        filter.sharedMesh = quad; display.sharedMaterial = surfaceMaterial;
        if (texture == null || texture.width != resolution)
        {
            if (texture != null) { texture.Release(); DisposeObject(texture); }
            // 遮罩是数值数据，使用线性读写。无深度附件；默认双线性过滤并 Clamp 边缘。
            texture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            { name = "Contact mask (R line / G glow)", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.Create();
        }
        if (commands == null) commands = new CommandBuffer { name = "Dissolve plane contact" };
        return true;
    }
    // 关闭组件或重载脚本时释放自建资源，并恢复被本组件替换的显示 Mesh/材质。
    void OnDisable()
    {
        if (filter != null && filter.sharedMesh == quad) filter.sharedMesh = previousMesh;
        if (display != null && display.sharedMaterial == surfaceMaterial) display.sharedMaterial = previousMaterial;
        foreach (var source in cache.Values) DisposeObject(source.baked);
        cache.Clear(); DisposeObject(strokes); DisposeObject(quad); DisposeObject(maskMaterial); DisposeObject(surfaceMaterial);
        if (texture != null) { texture.Release(); DisposeObject(texture); }
        commands?.Release(); commands = null;
        strokes = quad = null; maskMaterial = surfaceMaterial = null; texture = null;
    }
    static void DisposeObject(Object obj) { if (obj == null) return; if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
}
