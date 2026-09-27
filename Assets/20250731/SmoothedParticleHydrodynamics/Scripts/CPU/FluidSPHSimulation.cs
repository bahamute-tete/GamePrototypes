
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FluidSPHSimulation : MonoBehaviour
{
    public enum IntegrationMethod { SemiImplicitEuler, Leapfrog }

    [Header("Simulation Settings")]
    public Vector2 initlaVelocity = new Vector2(-5.0f, 0);
    public int numParticles = 400;
    public IntegrationMethod integrationMethod = IntegrationMethod.SemiImplicitEuler;
    [Min(0.0001f)] public float timeStep = 0.002f;
    [Tooltip("每帧最多执行的子步数；未处理时间保留到下一帧。")]
    [Min(1)] public int maxSubstepsPerFrame = 64;
    [Tooltip("每秒速度衰减率，0 表示关闭额外全局阻尼。")]
    [Min(0f)] public float dampingRate = 0f;
    
    [Header("SPH Parameters")]
    public float kernelRadius = 1.0f; //h 
    public float restDensity = 2.5f;  // ρ0
    public float stiffness = 150f;    // k
    public float viscosity = 3.0f;   // 运动黏度 ν
    public float particleMass = 0.2f;// m
    [Range(0.25f, 0.9f)] public float particleSpacingRatio = 0.5f;
    [Tooltip("启动/重置时按规则晶格的内部密度标定质量；修改间距后请重置。")]
    public bool autoCalibrateMass = true;

    [Header("Surface Tension")]
    public bool enableSurfaceTension = true;
    public bool enableSurfaceVisualization = false;
    public bool enableNormalVisualization = false;
    public float surfaceTensionCoefficient = 0.0728f; // σ
    [Tooltip("无量纲阈值：h * |颜色场梯度| 超过此值时判定为表面。")]
    public float surfaceThreshold = 0.5f;

    [Header("Environment")]
    [Range(0, 1)] public float boundaryDamping = 0.5f; // 法向反弹系数 (0=不反弹, 1=完全反弹)
    [Range(0, 1)] public float boundaryFriction = 0.5f; // 0=无摩擦, 1=完全停止
    public Color boundColor = new Color(0, 1, 0, 0.5f);
    public Bounds bounds = new Bounds(Vector3.zero, new Vector3(10, 10, 0));

    [Header("Render")]
    public GameObject waterPrefab;

    [Header("UI")]
    public TextMeshProUGUI infoText;
    public TextMeshProUGUI fps;

    private SpatialGrid2D grid2D;
    private double accumulatedTime;
    private float SurfaceGradientThreshold => surfaceThreshold / kernelRadius;
    private List<WaterParticle2D> particles = new List<WaterParticle2D>();
    private List<GameObject> entitys = new List<GameObject>();
    private List<SpriteRenderer> waterRenders = new List<SpriteRenderer>();

    void Start()
    {
        ValidateParameters();
        accumulatedTime = 0.0;
        grid2D = new SpatialGrid2D(kernelRadius);
        if (autoCalibrateMass) CalibrateParticleMass();
        SpawnParticles();
        RebuildGridAndDensities();
    }

    private void OnValidate()
    {
        ValidateParameters();
    }

    private void ValidateParameters()
    {
        kernelRadius = Mathf.Max(0.001f, kernelRadius);
        restDensity = Mathf.Max(0.0001f, restDensity);
        particleMass = Mathf.Max(0.0001f, particleMass);
        particleSpacingRatio = Mathf.Clamp(particleSpacingRatio, 0.25f, 0.9f);
        numParticles = Mathf.Max(1, numParticles);
        timeStep = Mathf.Max(0.0001f, timeStep);
        maxSubstepsPerFrame = Mathf.Max(1, maxSubstepsPerFrame);
        stiffness = Mathf.Max(0f, stiffness);
        viscosity = Mathf.Max(0f, viscosity);
        dampingRate = Mathf.Max(0f, dampingRate);
        surfaceThreshold = Mathf.Max(0f, surfaceThreshold);
        surfaceTensionCoefficient = Mathf.Max(0f, surfaceTensionCoefficient);
    }

    /// <summary>
    /// 根据核半径和间距，自动计算所需的粒子质量，使初始密度的粒子产生的密度接近 RestDensity
    /// </summary>
    private void CalibrateParticleMass()
    {
        float spacing = kernelRadius * particleSpacingRatio;
        float kernelSum = 0f;

        // 模拟一个完美的粒子晶格，计算中心粒子的核函数总和
        // 采样范围覆盖整个核半径
        int range = Mathf.CeilToInt(kernelRadius / spacing);

        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                Vector2 pos = new Vector2(x * spacing, y * spacing);
                float r = pos.magnitude;
                if (r < kernelRadius)
                {
                    // 假设 mass = 1 进行累加
                    kernelSum += SPHKernels.Poly6Kernel(r, kernelRadius);
                }
            }
        }

        if (kernelSum > 0.0001f)
        {
            //  kernelSum * mass = restDensity
            //  mass = restDensity / kernelSum
            particleMass = restDensity / kernelSum;


            Debug.Log($"[SPH] Auto-calibrated Mass: {particleMass} to match RestDensity: {restDensity}");
        }
    }

    private void SpawnParticles()
    {
        particles.Clear();
        float spacing = kernelRadius * particleSpacingRatio;
        int columns = Mathf.CeilToInt(Mathf.Sqrt(numParticles));
        int rows = Mathf.CeilToInt((float)numParticles / columns);
        Vector2 extent = new Vector2((columns - 1) * spacing, (rows - 1) * spacing);
        Vector2 minimum = new Vector2(bounds.min.x + 0.1f, bounds.min.y + 0.1f);
        Vector2 maximum = new Vector2(bounds.max.x - 0.1f, bounds.max.y - 0.1f);
        if (extent.x > maximum.x - minimum.x || extent.y > maximum.y - minimum.y)
        {
            Debug.LogError("[SPH] 初始晶格超出边界，请减少粒子数/间距或扩大 bounds。", this);
            return;
        }

        // 规则晶格与质量标定使用相同间距，避免随机扰动引入初始密度噪声。
        Vector2 center = new Vector2(bounds.center.x, bounds.center.y + bounds.size.y * 0.125f);
        Vector2 start = center - extent * 0.5f;
        start.x = Mathf.Clamp(start.x, minimum.x, maximum.x - extent.x);
        start.y = Mathf.Clamp(start.y, minimum.y, maximum.y - extent.y);
        for (int i = 0; i < numParticles; i++)
        {
            Vector2 pos = start + new Vector2(i % columns, i / columns) * spacing;
            particles.Add(new WaterParticle2D(pos) { mass = particleMass, velocity = initlaVelocity });
        }
    }

    void Update()
    {
        AdvanceSimulation(Time.deltaTime);
        UpdateInfo();
    }

    private void AdvanceSimulation(float elapsedTime)
    {
        if (particles.Count == 0) return;
        accumulatedTime += elapsedTime;
        int steps = 0;
        while (accumulatedTime >= timeStep && steps < maxSubstepsPerFrame)
        {
            SimulateStep(timeStep);
            accumulatedTime -= timeStep;
            steps++;
        }
        // 不丢弃不足一个子步的余数；过载时保留积压时间，避免依赖渲染 FPS。
    }

    private void RebuildGridAndDensities()
    {
        if (grid2D == null || grid2D.cellSize != kernelRadius)
            grid2D = new SpatialGrid2D(kernelRadius);
        grid2D.Clear();
        foreach (var p in particles)
        {
            p.mass = particleMass;
            grid2D.InsertParticle(p);
        }
        foreach (var p in particles)
            UpdateDensityAndPressure(p, grid2D.GetNeighbors(p));
    }

    private void SimulateStep(float dt)
    {
        RebuildGridAndDensities();

        // 所有粒子的力都从同一时刻的位置、速度、密度计算。
        foreach (var p in particles)
        {
            p.acceleration = Vector2.zero;
            ApplyInternalForces(p, grid2D.GetNeighbors(p));
            ApplyExternalForces(p);
        }

        // 完成全部力计算后才能修改位置和速度。
        foreach (var p in particles)
        {
            if (integrationMethod == IntegrationMethod.Leapfrog)
                IntegrateLeapfrog(p, dt);
            else
                IntergrateSemiImplicitEuler(p, dt);
            ResolveBoundaries(p);
        }
    }

    private void UpdateInfo()
    {
        if (infoText != null)
        {
            infoText.text = 
                $"h: {kernelRadius}\n" +
                $"rho0: {restDensity}\n" +
                $"k: {stiffness}\n" +
                $"m: {particleMass}\n" +
                $"nu: {viscosity}\n"+
                $"Time Step: {timeStep}\n"+
                $"ParticleNumbers: {particles.Count}\n";

            infoText.wordSpacing = 30;
            infoText.lineSpacing = 50;
        }

        if (fps != null)
            fps.text = $"FPS: {(int)(1.0f / Time.deltaTime)}";
    }


    void ApplyExternalForces(WaterParticle2D p)
    { 
        p.acceleration += new Vector2(0,-9.81f);
    }

    void ApplyInternalForces(WaterParticle2D p, List<WaterParticle2D> neighbours)
    {
        Vector2 aPressure = Vector2.zero;
        Vector2 aViscosity = Vector2.zero;
        Vector2 colorGradient = Vector2.zero;
        // 自身的梯度为零，但拉普拉斯不为零，必须计入颜色场求和。
        float colorLaplacian = p.mass / p.density
            * SPHKernels.CalculatePoly6Laplacian(0f, kernelRadius);

        foreach (var neighbor in neighbours)
        {
            Vector2 dir = neighbor.position - p.position;

            float r = dir.magnitude;

            if (r < kernelRadius)
            {
                if (p.density > 0.0001f && neighbor.density > 0.0001f)
                {
                    // 正确实现：使用对称压力加速度
                    // a_i^pressure = -Σ m_j * (p_i/rho_i^2 + p_j/rho_j^2) * ∇W_ij
                    float pressureTerm =
                        (p.pressure / (p.density * p.density)) +
                        (neighbor.pressure / (neighbor.density * neighbor.density));
                    aPressure -= neighbor.mass * pressureTerm * SPHKernels.SpikyKernelGradient(dir, r, kernelRadius);
                }

                Vector2 relativeV = neighbor.velocity - p.velocity;
                if (neighbor.density > 0.0001f)
                {
                    // viscosity 表示运动黏度 ν，直接计算黏性加速度。
                    float viscosityTerm = viscosity * neighbor.mass / neighbor.density;
                    aViscosity += viscosityTerm * SPHKernels.ViscosityKernelLaplacian(r, kernelRadius) * relativeV;
                }


                float densityTerm = neighbor.mass / neighbor.density;
                // 计算颜色场梯度: ∇c_i = Σ (m_j / ρ_j) * ∇W_ij
                colorGradient += densityTerm * SPHKernels.Poly6KernelGradient(dir, r, kernelRadius);
                // ∇²c_i = Σ (m_j / ρ_j) * ∇²W_ij (用于计算曲率 κ)
                colorLaplacian += densityTerm * SPHKernels.CalculatePoly6Laplacian(r, kernelRadius);
            }
        }

        p.surfaceGradient = colorGradient;

        if (enableSurfaceTension)
        {
            float gradientMag = colorGradient.magnitude;
            if (gradientMag > SurfaceGradientThreshold && gradientMag > 0.0001f)
            {
                // 表面张力公式 (Müller 2003): F_surface = -σ * (∇²c) * (n / |n|)
                // 其中 n = ∇c (colorGradient)
                Vector2 n = colorGradient / gradientMag;
                Vector2 surfaceTensionForce = -surfaceTensionCoefficient * colorLaplacian * n;
                // Müller 的表面张力项是力密度，除以 rho_i 后才是加速度。
                aPressure += surfaceTensionForce / p.density;
            }
          

        }

        p.acceleration += aPressure + aViscosity;
    }

    void UpdateDensityAndPressure(WaterParticle2D p, List<WaterParticle2D> neighbours)
    { 
        p.density = 0f;

        //self contribution
        p.density += SPHKernels.Poly6Kernel(0, kernelRadius) * p.mass;

        foreach (var neighbor in neighbours)
        {
            Vector2 dir = neighbor.position - p.position;
            float r = dir.magnitude;
            if (r < kernelRadius)
            {
                p.density += SPHKernels.Poly6Kernel(r, kernelRadius) * neighbor.mass;
            }
        }

        p.pressure = stiffness * (p.density - restDensity);
        p.pressure = Mathf.Max(0f, p.pressure); 
    }

    // 保留旧枚举值以兼容序列化；这是恒加速度近似，并非完整 Leapfrog。
    // 当前示例场景使用 SemiImplicitEuler。
    void IntegrateLeapfrog(WaterParticle2D p, float t)
    {
        // 1. 计算半步速度: v(t + 0.5dt)
        Vector2 v_half = p.velocity + p.acceleration * t * 0.5f;

        // 2. 使用半步速度更新位置: x(t + dt) = x(t) + v(t + 0.5dt) * dt
        // 相比原方法，这里隐含了 0.5 * a * t^2 的位移项，位置更新更准确
        p.position += v_half * t;

        // 3. 更新全步速度: v(t + dt) = v(t + 0.5dt) + 0.5 * a(t) * dt
        // 注意：严格的 Verlet 需要在这里重新计算力得到 a(t+dt)，但为了性能这里近似使用 a(t)
        p.velocity = v_half + p.acceleration * t * 0.5f;

        // 空气阻力/全局阻尼
        p.velocity *= Mathf.Exp(-dampingRate * t);

        p.acceleration = Vector2.zero;
    }

    void IntergrateSemiImplicitEuler(WaterParticle2D p, float t)
    {
        p.velocity += p.acceleration * t;

        // 按模拟时间衰减，改变子步数不会改变每秒阻尼强度。
        p.velocity *= Mathf.Exp(-dampingRate * t);

        p.position += p.velocity * t;
        p.acceleration = Vector2.zero;
    }

    void ResolveBoundaries(WaterParticle2D p)
    {
        Vector2 pos = p.position;
        Vector2 vel = p.velocity;
        
        float minX = bounds.min.x + 0.1f;
        float maxX = bounds.max.x - 0.1f;
        float minY = bounds.min.y + 0.1f;
        float maxY = bounds.max.y - 0.1f;

        // 摩擦力因子 (1 - friction)
        float frictionFactor = 1.0f - boundaryFriction;

        // X轴边界
        if (pos.x < minX)
        {
            pos.x = minX;
            vel.x *= -boundaryDamping; // 法向反弹
            vel.y *= frictionFactor;   // 切向摩擦
        }
        else if (pos.x > maxX)
        {
            pos.x = maxX;
            vel.x *= -boundaryDamping;
            vel.y *= frictionFactor;
        }

        // Y轴边界
        if (pos.y < minY)
        {
            pos.y = minY;
            vel.y *= -boundaryDamping; // 法向反弹
            vel.x *= frictionFactor;   // 切向摩擦
        }
        else if (pos.y > maxY)
        {
            pos.y = maxY;
            vel.y *= -boundaryDamping;
            vel.x *= frictionFactor;
        }

        p.position = pos;
        p.velocity = vel;
    }

    void UpdatePosition()
    {
        foreach (var e in entitys)
        { 
            e.transform.position = new Vector3(particles[entitys.IndexOf(e)].position.x, particles[entitys.IndexOf(e)].position.y, 0);
            float alpha = particles[entitys.IndexOf(e)].density / restDensity;
            waterRenders[entitys.IndexOf(e)].color = new Color(0, 0.5f, 1f, Mathf.Clamp01(alpha));
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = boundColor;
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        if (particles != null)
        {
            foreach (var p in particles)
            {
                float alpha = p.density / restDensity;
                Gizmos.color = new Color(0, 0.5f, 1f, Mathf.Clamp01(alpha));

                if (enableSurfaceVisualization)
                {
                    float gradientMag = p.surfaceGradient.magnitude;
                    if (gradientMag > SurfaceGradientThreshold && gradientMag > 0.0001f)
                    {
                        Gizmos.color = Color.Lerp(new Color(0, 0.5f, 1f, Mathf.Clamp01(alpha)), Color.white, Mathf.Clamp01(alpha));
                        if (enableNormalVisualization)
                            Gizmos.DrawLine(p.position, p.position + p.surfaceGradient.normalized * 0.5f);
                    }
                }
                Gizmos.DrawSphere(p.position, kernelRadius * 0.2f);
            }
        }
    }

    [ContextMenu("Reset Simulation")]
    public void ResetSimulation()
    {
        Start();
    }
}
