using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 一维浅水方程模拟 - 使用 Lax-Friedrichs 格式
/// 可视化显示水坝溃坝问题
/// </summary>
public class ShallowWater1D : MonoBehaviour
{
    [Header("物理参数")]
    public float g = 9.81f; // 重力加速度

    [Header("网格参数")]
    public int resolution = 200; // 空间分辨率
    public float totalLength = 40f; // 总长度
    [SerializeField] private float dx; // 空间步长（自动计算）

    [Header("时间步长")]
    public float dt = 0.01f; // 时间步长
    public float simulationSpeed = 1.0f; // 模拟速度倍数

    [Header("初始条件 - 溃坝问题")]
    public float damBreakPosition = 20f; // 水坝位置
    public float leftHeight = 2.0f; // 左侧初始水深
    public float rightHeight = 0.5f; // 右侧初始水深

    [Header("可视化")]
    public bool useLineRenderer = false; // 默认使用柱状图模式，可以看到速度颜色
    public Color waterColor = Color.blue;
    public Color velocityColor = Color.cyan;
    public float visualizationScale = 1.0f;
    public float maxVelocityForColor = 10f; // 速度颜色映射的最大值

    // 状态变量数组
    private float[] h;      // 水深
    private float[] u;      // 速度
    private float[] h_new;  // 下一步水深
    private float[] u_new;  // 下一步速度

    // 可视化组件
    private LineRenderer lineRenderer;
    private List<GameObject> waterObjects;
    private List<Renderer> waterRenderers; // 缓存 Renderer 组件
    private MaterialPropertyBlock materialPropertyBlock; // 用于批量更新颜色
    private bool simulationRunning = true;

    void Start()
    {
        // 根据总长度和分辨率计算 dx
        dx = totalLength / resolution;

        // 检查 CFL 条件
        float maxWaveSpeed = Mathf.Sqrt(g * Mathf.Max(leftHeight, rightHeight));
        float cflDt = dx / maxWaveSpeed;
        if (dt > cflDt * 0.8f)
        {
            Debug.LogWarning($"CFL 条件警告：dt={dt:F4}, 建议 dt < {cflDt * 0.8f:F4}");
            dt = cflDt * 0.5f;
            Debug.Log($"已自动调整 dt = {dt:F4}");
        }

        InitializeArrays();
        SetInitialConditions();

        if (useLineRenderer)
        {
            CreateLineVisualization();
        }
        else
        {
            CreateBarVisualization();
        }
    }

    void InitializeArrays()
    {
        h = new float[resolution];
        u = new float[resolution];
        h_new = new float[resolution];
        u_new = new float[resolution];
    }

    void SetInitialConditions()
    {
        // 设置溃坝问题的初始条件
        for (int i = 0; i < resolution; i++)
        {
            float x = i * dx;
            if (x < damBreakPosition)
            {
                h[i] = leftHeight;
            }
            else
            {
                h[i] = rightHeight;
            }
            u[i] = 0.0f; // 初始速度为 0
        }
    }

    void CreateLineVisualization()
    {
        // 创建 LineRenderer 用于显示水面轮廓
        GameObject lineObj = new GameObject("WaterSurface");
        lineObj.transform.SetParent(transform);
        lineRenderer = lineObj.AddComponent<LineRenderer>();

        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.material.color = waterColor;
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.positionCount = resolution;
        lineRenderer.loop = false;
        lineRenderer.useWorldSpace = true;

        // 设置渐变颜色（根据速度）
        Gradient gradient = new Gradient();
        GradientColorKey[] colorKeys = new GradientColorKey[]
        {
            new GradientColorKey(Color.blue, 0f),
            new GradientColorKey(Color.cyan, 1f)
        };
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[]
        {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(1f, 1f)
        };
        gradient.SetKeys(colorKeys, alphaKeys);
        lineRenderer.colorGradient = gradient;

        UpdateLinePositions();
    }

    void CreateBarVisualization()
    {
        waterObjects = new List<GameObject>();
        waterRenderers = new List<Renderer>();
        materialPropertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < resolution; i++)
        {
            GameObject waterCell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            waterCell.transform.SetParent(transform);
            waterCell.name = $"WaterCell_{i}";

            // 移除 collider 以提高性能
            Collider col = waterCell.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer renderer = waterCell.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = waterColor;
            renderer.sharedMaterial = mat;

            waterObjects.Add(waterCell);
            waterRenderers.Add(renderer);
        }

        UpdateBarVisualization();
    }

    /// <summary>
    /// Lax-Friedrichs 格式求解一维浅水方程
    /// 守恒形式：U_t + F(U)_x = 0
    /// U = [h, hu]^T
    /// F(U) = [hu, hu^2 + 0.5*g*h^2]^T
    /// </summary>
    void UpdateShallowWaterEquations()
    {
        // 边界条件：固定边界（零梯度）
        h[0] = h[1];
        h[resolution - 1] = h[resolution - 2];
        u[0] = 0f;  // 壁面边界，速度为 0
        u[resolution - 1] = 0f;

        // 计算通量和更新
        for (int i = 1; i < resolution - 1; i++)
        {
            // 计算动量 hu
            float hu_l = h[i - 1] * u[i - 1];
            float hu_r = h[i + 1] * u[i + 1];

            // 计算动量通量 F_momentum = hu^2 + 0.5*g*h^2
            float momentumFlux_l = hu_l * u[i - 1] + 0.5f * g * h[i - 1] * h[i - 1];
            float momentumFlux_r = hu_r * u[i + 1] + 0.5f * g * h[i + 1] * h[i + 1];

            // Lax-Friedrichs 格式
            // h_new[i] = 0.5*(h[i+1] + h[i-1]) - dt/(2*dx)*(hu[i+1] - hu[i-1])
            // (hu)_new[i] = 0.5*(hu[i+1] + hu[i-1]) - dt/(2*dx)*(F_momentum[i+1] - F_momentum[i-1])

            // 更新水深
            h_new[i] = 0.5f * (h[i + 1] + h[i - 1]) - (dt / (2 * dx)) * (hu_r - hu_l);

            // 更新动量
            float hu_new = 0.5f * (hu_r + hu_l) - (dt / (2 * dx)) * (momentumFlux_r - momentumFlux_l);

            // 确保水深为正，然后计算速度
            if (h_new[i] > 0.001f)
            {
                u_new[i] = hu_new / h_new[i];
            }
            else
            {
                h_new[i] = 0.001f; // 最小水深
                u_new[i] = 0.0f;
            }
        }

        // 复制新值
        for (int i = 1; i < resolution - 1; i++)
        {
            h[i] = Mathf.Max(h_new[i], 0.001f);
            u[i] = u_new[i];
        }
    }

    void UpdateLineVisualization()
    {
        if (lineRenderer == null) return;

        for (int i = 0; i < resolution; i++)
        {
            float x = i * dx;
            float y = h[i] * visualizationScale;
            lineRenderer.SetPosition(i, new Vector3(x, y, 0));
        }
        // LineRenderer 的颜色渐变是固定的，速度颜色在柱状图模式下显示更好
    }

    void UpdateBarVisualization()
    {
        if (waterRenderers == null || waterRenderers.Count == 0) return;

        // 计算最大速度用于颜色映射
        float maxSpeed = 0f;
        for (int i = 0; i < resolution; i++)
        {
            float speed = Mathf.Abs(u[i]);
            if (speed > maxSpeed) maxSpeed = speed;
        }
        maxSpeed = Mathf.Max(maxSpeed, 0.1f); // 避免除零

        for (int i = 0; i < resolution && i < waterObjects.Count; i++)
        {
            GameObject waterCell = waterObjects[i];
            Renderer renderer = waterRenderers[i];

            float x = i * dx;
            float height = h[i] * visualizationScale;

            // 设置位置（底部在 y=0）
            waterCell.transform.position = new Vector3(x, height / 2, 0);

            // 设置缩放
            Vector3 scale = waterCell.transform.localScale;
            scale.x = dx * 0.9f;
            scale.y = Mathf.Max(height, 0.01f); // 确保最小高度可见
            scale.z = 1f;
            waterCell.transform.localScale = scale;

            // 根据速度设置颜色（使用 MaterialPropertyBlock 优化性能）
            float speed = Mathf.Abs(u[i]);
            float colorT = Mathf.Clamp(speed / maxVelocityForColor, 0, 1);
            Color col = Color.Lerp(waterColor, velocityColor, colorT);

            renderer.GetPropertyBlock(materialPropertyBlock);
            materialPropertyBlock.SetColor("_Color", col);
            renderer.SetPropertyBlock(materialPropertyBlock);
        }
    }

    void UpdateLinePositions()
    {
        if (lineRenderer == null) return;

        for (int i = 0; i < resolution; i++)
        {
            float x = i * dx;
            float y = h[i] * visualizationScale;
            lineRenderer.SetPosition(i, new Vector3(x, y, 0));
        }
    }

    void FixedUpdate()
    {
        if (simulationRunning)
        {
            // 可以一帧多次更新以提高稳定性
            int subSteps = Mathf.Max(1, Mathf.CeilToInt(simulationSpeed));
            for (int i = 0; i < subSteps; i++)
            {
                UpdateShallowWaterEquations();
            }
        }
    }

    void LateUpdate()
    {
        if (simulationRunning)
        {
            if (useLineRenderer)
            {
                UpdateLineVisualization();
            }
            else
            {
                UpdateBarVisualization();
            }
        }
    }

    void OnDrawGizmos()
    {
        // 动态计算 dx（如果还没有计算）
        if (dx <= 0 && resolution > 0)
            dx = totalLength / resolution;

        // 检查数组是否已初始化
        if (h == null || h.Length == 0 || dx <= 0) return;

        int drawResolution = Mathf.Min(h.Length - 1, resolution - 1);

        // 绘制水底基线
        Gizmos.color = Color.gray;
        Gizmos.DrawLine(new Vector3(0, 0, 0), new Vector3(totalLength, 0, 0));

        // 绘制水面线
        Gizmos.color = waterColor;
        for (int i = 0; i < drawResolution; i++)
        {
            float x1 = i * dx;
            float x2 = (i + 1) * dx;

            // 确保索引不越界
            float h1 = (i < h.Length) ? h[i] : 0;
            float h2 = (i + 1 < h.Length) ? h[i + 1] : 0;

            Vector3 pos1 = new Vector3(x1, h1 * visualizationScale, 0);
            Vector3 pos2 = new Vector3(x2, h2 * visualizationScale, 0);

            Gizmos.DrawLine(pos1, pos2);
        }

        // 标记水坝位置
        Gizmos.color = Color.red;
        Vector3 damPos = new Vector3(damBreakPosition, 0, 0);
        Vector3 damTop = new Vector3(damBreakPosition, Mathf.Max(leftHeight, rightHeight) * visualizationScale + 1, 0);
        Gizmos.DrawLine(damPos, damTop);
    }

    // 在编辑器中初始化时调用
    void OnValidate()
    {
        if (resolution > 0 && totalLength > 0)
            dx = totalLength / resolution;
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 400));
        GUILayout.Label("一维浅水方程模拟 (Lax-Friedrichs)");
        GUILayout.Label($"分辨率：{resolution}");
        GUILayout.Label($"dx = {dx:F4}, dt = {dt:F4}");
        GUILayout.Label($"CFL: {dt * Mathf.Sqrt(g * Mathf.Max(leftHeight, rightHeight)) / dx:F2}");
        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("暂停/继续"))
        {
            simulationRunning = !simulationRunning;
        }
        if (GUILayout.Button("重置"))
        {
            SetInitialConditions();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label($"左侧水深：{leftHeight:F2}");
        GUILayout.Label($"右侧水深：{rightHeight:F2}");
        GUILayout.Label($"水坝位置：{damBreakPosition:F2}");

        GUILayout.Space(10);
        GUILayout.Label("控制:");
        GUILayout.Label("- 空格：暂停/继续");
        GUILayout.Label("- R：重置模拟");

        GUILayout.EndArea();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            simulationRunning = !simulationRunning;
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            SetInitialConditions();
        }
    }
}

/// <summary>
/// 简单的水单元可视化（可选）
/// </summary>
[ExecuteInEditMode]
public class WaterCellVisual : MonoBehaviour
{
    void Start()
    {
        if (GetComponent<Renderer>() == null)
        {
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = Color.blue;
            renderer.sharedMaterial = mat;
        }
    }
}
