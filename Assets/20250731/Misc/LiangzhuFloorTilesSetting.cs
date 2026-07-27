using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 地砖材质 + 图集子块分配器 (v2: 自动推断网格尺寸)。
/// - 收集挂载点下所有(直接)子物体；
/// - 自动按 localPosition 推断每块的 (列,行) 坐标(不再依赖手填的行列数)；
/// - 每块从 2 个材质中选其一 + 从 5x5 图集取一个子块(走 MPB 写 _BaseMap_ST)；
/// - 保证每块与其八邻位(含对角)不出现相同的"完整变体(材质+子块)"。
/// </summary>
[ExecuteAlways]
public class LiangzhuFloorTilesSetting : MonoBehaviour
{
    public enum GridPlane { XZ, XY, ZY }

    [Header("材质 (二选一分配)")]
    [SerializeField] Material floorTileMatA;
    [SerializeField] Material floorTileMatB;

    [Header("图集分块 (每行/列子块数, 5 = 5x5)")]
    [SerializeField] int tilesPerRow = 5;

    [Header("网格推断")]
    [Tooltip("默认: 按 localPosition 自动推断真实网格(任意行列数)。\n取消: 改用手填的 fallbackCountX 按 Hierarchy 顺序行优先排布。")]
    [SerializeField] bool autoDetectGrid = true;
    [SerializeField] GridPlane gridPlane = GridPlane.XZ;
    [Tooltip("推断网格 pitch 时忽略小于此值的位置抖动(本地单位)。需 < 实际地砖间距。")]
    [SerializeField] float gridSnapEpsilon = 0.001f;

    [Header("Fallback: 按 Hierarchy 顺序 (autoDetectGrid 关闭时)")]
    [SerializeField] int fallbackCountX = 5;

    [Header("随机种子 (相同种子结果可复现)")]
    [SerializeField] int seed = 12345;

    [Header("UV / 图集")]
    [Tooltip("tiling/offset 写入的属性名。URP Lit / 本项目 SceneLitFoggedDissolve = _BaseMap_ST。")]
    [SerializeField] string stPropertyName = "_BaseMap_ST";
    [Tooltip("每个子块四周内缩，防止 bilinear/mip 采样到相邻子块(UV 单位, 0=不内缩)。")]
    [SerializeField, Range(0f, 0.1f)] float uvInset = 0f;

    [Header("调试")]
    [SerializeField] bool logGridInfo = true;

    int _stPropId;
    MaterialPropertyBlock _mpb;

    void OnEnable()
    {
        Rebuild();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        EditorApplication.delayCall += DeferredRebuild;
    }

    void DeferredRebuild()
    {
        EditorApplication.delayCall -= DeferredRebuild;
        if (this == null) return;
        if (!isActiveAndEnabled) return;
        Rebuild();
    }
#endif

    [ContextMenu("重新生成地砖")]
    public void Rebuild()
    {
        if (floorTileMatA == null || floorTileMatB == null)
        {
            Debug.LogWarning($"[{name}] 需要同时指定 floorTileMatA 与 floorTileMatB。", this);
            return;
        }
        if (tilesPerRow < 1)
        {
            Debug.LogWarning($"[{name}] tilesPerRow 必须 >= 1。", this);
            return;
        }

        int totalVariants = 2 * tilesPerRow * tilesPerRow; // 2 材质 × (n×n) 子块
        if (totalVariants < 5)
            Debug.LogWarning($"[{name}] 可用变体数 {totalVariants} 过少，八邻位去重可能无解(建议 >= 5)。", this);

        var renderers = CollectChildRenderers();
        if (renderers.Count == 0)
        {
            Debug.LogWarning($"[{name}] 没有找到带 Renderer 的直接子物体。", this);
            return;
        }

        _stPropId = Shader.PropertyToID(stPropertyName);
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        Renderer[,] grid = BuildGrid(renderers, out int countX, out int countY, out int placed);

        if (logGridInfo)
            Debug.Log($"[{name}] 网格 {countX}×{countY}, 已放置 {placed}/{renderers.Count} 块, 变体池 {totalVariants}。", this);

        // -1 = 未着色
        int[,] variant = new int[countX, countY];
        for (int y = 0; y < countY; y++)
            for (int x = 0; x < countX; x++)
                variant[x, y] = -1;

        var rng = new System.Random(seed);
        var forbidden = new HashSet<int>();
        var candidates = new List<int>(totalVariants);

        // 行优先贪心：只看"已落子"的 4 个邻居(左/左上/上/右上)即可覆盖全部 8 邻对
        for (int y = 0; y < countY; y++)
        {
            for (int x = 0; x < countX; x++)
            {
                Renderer r = grid[x, y];
                if (r == null) continue;

                forbidden.Clear();
                AddForbidden(forbidden, variant, grid, x - 1, y);
                AddForbidden(forbidden, variant, grid, x - 1, y - 1);
                AddForbidden(forbidden, variant, grid, x, y - 1);
                AddForbidden(forbidden, variant, grid, x + 1, y - 1);

                candidates.Clear();
                for (int v = 0; v < totalVariants; v++)
                    if (!forbidden.Contains(v)) candidates.Add(v);

                int chosen = (candidates.Count > 0)
                    ? candidates[rng.Next(candidates.Count)]
                    : rng.Next(totalVariants);

                variant[x, y] = chosen;
                ApplyVariant(r, chosen);
            }
        }
    }

    // ---------------------------------------------------------------------

    void AddForbidden(HashSet<int> set, int[,] variant, Renderer[,] grid, int x, int y)
    {
        int cx = grid.GetLength(0);
        int cy = grid.GetLength(1);
        if (x < 0 || y < 0 || x >= cx || y >= cy) return;
        if (grid[x, y] == null) return;
        int v = variant[x, y];
        if (v < 0) return;
        set.Add(v);
        // 想"连子块下标都不许相邻相同(忽略材质)": 改成 set.Add(v % (tilesPerRow*tilesPerRow));
        // 并在挑选时同样按 (v % perMat) 比对。
    }

    void ApplyVariant(Renderer r, int variantId)
    {
        int perMat = tilesPerRow * tilesPerRow;
        int matIndex = variantId / perMat;   // 0 -> A, 1 -> B
        int sub = variantId % perMat;
        int subX = sub % tilesPerRow;
        int subY = sub / tilesPerRow;

        r.sharedMaterial = (matIndex == 0) ? floorTileMatA : floorTileMatB;

        float scale = 1f / tilesPerRow;
        float tiling = scale - 2f * uvInset;
        float offX = subX * scale + uvInset;
        float offY = subY * scale + uvInset;

        // 加法式 MPB：取回当前块 -> 只改 _ST -> 写回，不覆盖 DissolveController 等写入的属性
        r.GetPropertyBlock(_mpb);
        _mpb.SetVector(_stPropId, new Vector4(tiling, tiling, offX, offY));
        r.SetPropertyBlock(_mpb);
    }

    // ---------------------------------------------------------------------

    List<Renderer> CollectChildRenderers()
    {
        var list = new List<Renderer>();
        for (int i = 0; i < transform.childCount; i++)
        {
            var r = transform.GetChild(i).GetComponent<Renderer>();
            if (r != null) list.Add(r);
        }
        return list;
    }

    /// <summary>
    /// 把子物体排进 grid[col, row]。
    /// autoDetectGrid: 用最小正间距(pitch)把 localPosition 四舍五入到整数行列，处理全部子物体。
    /// 否则: 按 Hierarchy 顺序行优先，stride = fallbackCountX。
    /// </summary>
    Renderer[,] BuildGrid(List<Renderer> renderers, out int countX, out int countY, out int placed)
    {
        placed = 0;

        if (!autoDetectGrid)
        {
            countX = Mathf.Max(1, fallbackCountX);
            countY = Mathf.CeilToInt(renderers.Count / (float)countX);
            var g = new Renderer[countX, countY];
            for (int i = 0; i < renderers.Count; i++)
            {
                int x = i % countX;
                int y = i / countX;
                g[x, y] = renderers[i];
                placed++;
            }
            return g;
        }

        // --- 位置自动推断 ---
        int n = renderers.Count;
        var us = new float[n];
        var vs = new float[n];
        for (int i = 0; i < n; i++)
        {
            GetPlaneCoords(renderers[i].transform.localPosition, out float u, out float v);
            us[i] = u; vs[i] = v;
        }

        float minU = float.MaxValue, minV = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            if (us[i] < minU) minU = us[i];
            if (vs[i] < minV) minV = vs[i];
        }

        float pitchU = DetectPitch(us, gridSnapEpsilon);
        float pitchV = DetectPitch(vs, gridSnapEpsilon);

        // pitch 为 0 表示该轴只有一条线
        int maxCol = 0, maxRow = 0;
        var cols = new int[n];
        var rows = new int[n];
        for (int i = 0; i < n; i++)
        {
            cols[i] = (pitchU > 0f) ? Mathf.RoundToInt((us[i] - minU) / pitchU) : 0;
            rows[i] = (pitchV > 0f) ? Mathf.RoundToInt((vs[i] - minV) / pitchV) : 0;
            if (cols[i] > maxCol) maxCol = cols[i];
            if (rows[i] > maxRow) maxRow = rows[i];
        }

        countX = maxCol + 1;
        countY = maxRow + 1;
        var grid = new Renderer[countX, countY];
        for (int i = 0; i < n; i++)
        {
            int x = cols[i], y = rows[i];
            if (grid[x, y] != null)
            {
                Debug.LogWarning($"[{name}] 网格格子 ({x},{y}) 冲突: '{grid[x, y].name}' 与 '{renderers[i].name}' 位置重叠, 保留前者。检查 gridPlane / gridSnapEpsilon 或地砖摆放是否规则。", renderers[i]);
                continue;
            }
            grid[x, y] = renderers[i];
            placed++;
        }
        return grid;
    }

    /// <summary>在一组数值里找最小正间距(忽略 < eps 的抖动), 作为规则网格的 pitch。</summary>
    static float DetectPitch(float[] values, float eps)
    {
        var sorted = (float[])values.Clone();
        System.Array.Sort(sorted);
        float pitch = float.MaxValue;
        for (int i = 1; i < sorted.Length; i++)
        {
            float d = sorted[i] - sorted[i - 1];
            if (d > eps && d < pitch) pitch = d;
        }
        return (pitch == float.MaxValue) ? 0f : pitch;
    }

    void GetPlaneCoords(Vector3 p, out float u, out float v)
    {
        switch (gridPlane)
        {
            case GridPlane.XY: u = p.x; v = p.y; break;
            case GridPlane.ZY: u = p.z; v = p.y; break;
            default:           u = p.x; v = p.z; break; // XZ
        }
    }
}
