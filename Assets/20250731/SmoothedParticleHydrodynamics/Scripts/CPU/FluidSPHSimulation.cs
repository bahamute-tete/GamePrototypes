
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class FluidSPHSimulation : MonoBehaviour
{
    public enum IntegrationMethod { SemiImplicitEuler, Leapfrog }

    [Header("Simulation Settings")]
    public Vector2 initlaVelocity = new Vector2(-5.0f, 0);
    public int numParticles = 400;
    public IntegrationMethod integrationMethod = IntegrationMethod.SemiImplicitEuler;
    public float timeStep = 0.002f;
    
    [Header("SPH Parameters")]
    public float kernelRadius = 1.0f; //h 
    public float restDensity = 2.5f;  // ρ0
    public float stiffness = 150f;    // k
    public float viscosity = 3.0f;   // μ
    public float particleMass = 0.2f;// m

    [Header("Surface Tension")]
    public bool enableSurfaceTension = true;
    public bool enableSurfaceVisualization = false;
    public bool enableNormalVisualization = false;
    public float surfaceTensionCoefficient = 0.0728f; // σ
    public float surfaceThreshold = 7.065f; // 临界值，用于判断表面粒子

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
    private List<WaterParticle2D> particles = new List<WaterParticle2D>();
    private List<GameObject> entitys = new List<GameObject>();
    private List<SpriteRenderer> waterRenders = new List<SpriteRenderer>();

    // Start is called before the first frame update
    void Start()
    {
        grid2D = new SpatialGrid2D(kernelRadius);
        // CalibrateParticleMass();
        SpawnParticles();
        

    }

    private void SpawnParticlesInGrid()
    {
        particles.Clear();
        //waterRenders.Clear();
        //entitys.Clear();

        float spacing = kernelRadius * 0.5f; 
        int particlesPerRow = (int)Mathf.Sqrt(numParticles);

        Vector2 startPos = new Vector2(-particlesPerRow * spacing * 0.5f, -particlesPerRow * spacing * 0.5f);

        for (int i = 0; i < numParticles; i++)
        {
            float x = (i % particlesPerRow) * spacing;
            float y = (i / particlesPerRow) * spacing;

            Vector2 pos = startPos + new Vector2(x, y);
            pos += new Vector2(Random.value * 0.01f, Random.value * 0.01f);

            var p = new WaterParticle2D(pos);
            p.mass = particleMass;
            p.velocity = initlaVelocity;
            particles.Add(p);

            //GameObject particleEntity = Instantiate(waterPrefab, transform);
            //waterRenders.Add(particleEntity.GetComponent<SpriteRenderer>());
            //particleEntity.transform.position = new Vector3(p.position.x, p.position.y, 0);
            //particleEntity.transform.localScale = Vector3.one * kernelRadius * 3.0f;
            //particleEntity.transform.parent = transform;
            //entitys.Add(particleEntity);
        }
        
    }

    /// <summary>
    /// 根据核半径和间距，自动计算所需的粒子质量，使初始密度的粒子产生的密度接近 RestDensity
    /// </summary>
    private void CalibrateParticleMass()
    {
        float spacing = kernelRadius * 0.5f;
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

            // 【技巧】稍微增加 5% 的质量，让初始密度略微大于 restDensity
            // 这样会产生微小的正压力来预先抵抗重力，减少初始的下沉幅度
            particleMass *= 1.05f;

            Debug.Log($"[SPH] Auto-calibrated Mass: {particleMass} to match RestDensity: {restDensity}");
        }
    }

    private void SpawnParticles()
    {
        particles.Clear();
        float spacing = kernelRadius * 0.5f;

        // float poolHeight = bounds.size.y * 0.25f;
        // Vector2 poolMin = new Vector2(bounds.min.x, bounds.min.y);

        // int cols = Mathf.FloorToInt(bounds.size.x / spacing);
        // int rows = Mathf.FloorToInt(poolHeight / spacing);

        // // 居中偏移，保证粒子在边界内
        // float xOffset = (bounds.size.x - cols * spacing) * 0.5f;

        // for (int y = 0; y < rows; y++)
        // {
        //     for (int x = 0; x < cols; x++)
        //     {
        //         Vector2 pos = new Vector2(
        //             poolMin.x + xOffset + x * spacing + spacing * 0.5f,
        //             poolMin.y + y * spacing + spacing * 0.5f
        //         );

        //         // 随机扰动
        //         pos += new Vector2(Random.value * 0.01f, Random.value * 0.01f);

        //         var p = new WaterParticle2D(pos);
        //         p.mass = particleMass;
        //         p.velocity = Vector2.zero; // 水池初始静止
        //         particles.Add(p);
        //     }
        // }

        // 使用费马螺旋 (Fermat's Spiral) 生成均匀分布的圆形粒子团
        // c 是缩放系数。为了让平均间距接近 spacing，c 需要调整
        // 理论推导：c ≈ spacing / sqrt(π) ≈ spacing * 0.56
        // 这里取 0.6f 稍微宽松一点，防止初始压力过大
        float c = spacing * 0.6f;

        for (int i = 0; i < numParticles; i++)
        {
            // 黄金角度 ≈ 137.508度，能保证最优的填充效率
            float theta = i * 137.50776f * Mathf.Deg2Rad;
            float r = c * Mathf.Sqrt(i);

            float x = r * Mathf.Cos(theta);
            float y = r * Mathf.Sin(theta);

            Vector2 pos = new Vector2(x, y);

            // 添加微小随机偏移，打破完美对称，避免数值计算中的奇异性
            pos += new Vector2(Random.value * 0.01f, Random.value * 0.01f);

            var p = new WaterParticle2D(pos+new Vector2(0,bounds.max.y/4.0f));
            p.mass = particleMass;
            p.velocity = initlaVelocity;
            particles.Add(p);
        }
    }
    // Update is called once per frame
    void Update()
    {
        if (grid2D.cellSize != kernelRadius) grid2D = new SpatialGrid2D(kernelRadius);
           grid2D.Clear();

        // timeStep = Time.fixedDeltaTime;

        foreach (var p in particles)
        {
            p.mass = particleMass; // 确保运行时修改生效
            grid2D.InsertParticle(p);
        }
       
        foreach (var p in particles)
        {
            var neighbors = grid2D.GetNeighbors(p);
            UpdateDensityAndPressure(p, neighbors);
        }

        foreach (var p in particles)
        {
            var neighbors = grid2D.GetNeighbors(p);
            ApplyInternalForces(p, neighbors);
            ApplyExternalForces(p);

            if (integrationMethod == IntegrationMethod.Leapfrog)
            {
                IntegrateLeapfrog(p, timeStep);
            }
            else
            {
                IntergrateSemiImplicitEuler(p, timeStep);
            }

            ResolveBoundaries(p);
        }

        //UpdatePosition();

        if (infoText != null)
        {
            infoText.text = 
                $"h: {kernelRadius}\n" +
                $"rho0: {restDensity}\n" +
                $"k: {stiffness}\n" +
                $"m: {particleMass}\n" +
                $"mu: {viscosity}\n"+
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
        float colorLaplacian = 0f;//用于累加颜色场的拉普拉斯值

        foreach (var neighbor in neighbours)
        {
            Vector2 dir = neighbor.position - p.position;

            float r = dir.magnitude;

            if (r < kernelRadius && r > 0.0001f)
            {
                if (p.density > 0.0001f && neighbor.density > 0.0001f)
                {
                    // 错误实现（保留）：旧公式是 (p_i + p_j) / (2 * rho_j)，随后整体再除以 rho_i。
                    // 这不是常用的对称压力加速度形式，近自由表面时更容易偏软或不稳定。
                    // float pressureTerm = (p.pressure + neighbor.pressure) / (2f * neighbor.density);
                    // F_pressure -= SPHKernels.SpikyKernelGradient(dir, r, kernelRadius) * pressureTerm * neighbor.mass;

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
                    // 错误实现（保留）：旧写法先累加“力”再统一除以 rho_i，
                    // 与多数 WCSPH 实现中的粘性加速度写法不一致，量纲更不直观。
                    // float viscosityTerm = viscosity * neighbor.mass  / neighbor.density;
                    // F_viscosity += viscosityTerm * SPHKernels.ViscosityKernelLaplacian(r, kernelRadius) * relativeV;

                    // 正确实现：直接累加粘性加速度项。
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
            if (gradientMag > surfaceThreshold)
            {
                // 表面张力公式 (Müller 2003): F_surface = -σ * (∇²c) * (n / |n|)
                // 其中 n = ∇c (colorGradient)
                Vector2 n = colorGradient / gradientMag;
                Vector2 surfaceTensionForce = -surfaceTensionCoefficient * colorLaplacian * n;
                // 该项是单位质量下的附加加速度，直接累加到 aPressure。
                aPressure += surfaceTensionForce;
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

    void IntegrateLeapfrog(WaterParticle2D p, float t)
    {
        // 限制最大加速度
        if (p.acceleration.sqrMagnitude > 10000f)
            p.acceleration = p.acceleration.normalized * 100f;

        // 1. 计算半步速度: v(t + 0.5dt)
        Vector2 v_half = p.velocity + p.acceleration * t * 0.5f;

        // 2. 使用半步速度更新位置: x(t + dt) = x(t) + v(t + 0.5dt) * dt
        // 相比原方法，这里隐含了 0.5 * a * t^2 的位移项，位置更新更准确
        p.position += v_half * t;

        // 3. 更新全步速度: v(t + dt) = v(t + 0.5dt) + 0.5 * a(t) * dt
        // 注意：严格的 Verlet 需要在这里重新计算力得到 a(t+dt)，但为了性能这里近似使用 a(t)
        p.velocity = v_half + p.acceleration * t * 0.5f;

        // 空气阻力/全局阻尼
        p.velocity *= 0.99f;

        p.acceleration = Vector2.zero;
    }

    void IntergrateSemiImplicitEuler(WaterParticle2D p, float t)
    {
        // 限制最大加速度
        if (p.acceleration.sqrMagnitude > 10000f)
            p.acceleration = p.acceleration.normalized * 100f;

        p.velocity += p.acceleration * t;

        // 空气阻力/全局阻尼 (模拟空气阻力，数值越小阻力越大)
        p.velocity *= 0.99f; 

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
                    if (gradientMag > surfaceThreshold)
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
