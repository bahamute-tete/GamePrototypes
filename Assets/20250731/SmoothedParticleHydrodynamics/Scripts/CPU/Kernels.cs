
using UnityEngine;

public static class SPHKernels
{
    // 2D Poly6 Kernel
    // 用于计算密度
    // W(r, h) = (4 / (pi * h^8)) * (h^2 - r^2)^3
    public static float Poly6Kernel(float r, float h)
    {
        if (r >= h) return 0f;

        float coefficient = 4f / (Mathf.PI * Mathf.Pow(h, 8));
        // float coefficient = 315.0f / (64.0f * Mathf.PI * Mathf.Pow(h, 9));
        return coefficient * Mathf.Pow(h * h - r * r, 3);
    }

    public static Vector2 Poly6KernelGradient(Vector2 dir, float r, float h)
    {
        if (r >= h) return Vector2.zero;
        // W'(r) = -24r / (pi * h^8) * (h^2 - r^2)^2
        // ∇W = W'(r) * (-dir / r) = 24 / (pi * h^8) * (h^2 - r^2)^2 * dir
        float coefficient = 24f / (Mathf.PI * Mathf.Pow(h, 8));
        float value = coefficient * Mathf.Pow(h * h - r * r, 2);

        // 错误实现（保留）：这里用了 dir.normalized，丢失了 |dir|=r 因子，
        // 会让梯度幅值在近距离被错误放大，导致表面张力与颜色场梯度异常。
        // return dir.normalized * value;

        // 正确实现：应直接乘以位移向量 dir（不是单位向量）。
        return dir * value;
    }

    // 2D Spiky Kernel Gradient
    // 用于计算压力力 (Pressure Force)
    // grad W(r, h) = - (30 / (pi * h^5)) * (h - r)^2 * (r / |r|)
    public static Vector2 SpikyKernelGradient(Vector2 dir, float r, float h)
    {
        if (r >= h || r <= 0.0001f) return Vector2.zero;
        float coefficient = -30f / (Mathf.PI * Mathf.Pow(h, 5));
        // float coefficient = -45.0f / (Mathf.PI * Mathf.Pow(h, 6));
        float value = coefficient * Mathf.Pow(h - r, 2);
        
        return dir.normalized * value;
    }

    // 2D Viscosity Kernel Laplacian
    // 用于计算粘滞力 (Viscosity Force)
    // lapl W(r, h) = (40 / (pi * h^5)) * (h - r)
    public  static float ViscosityKernelLaplacian(float r, float h)
    {
        if (r >= h) return 0f;
        float coefficient = 40f / (Mathf.PI * Mathf.Pow(h, 5));
        // float coefficient = 45.0f / (Mathf.PI * Mathf.Pow(h, 6));
        return coefficient * (h - r);
    }


    // 用于计算颜色场的曲率
    // ∇²W = - (48 / (π * h^8)) * (h² - r²) * (h² - 3r²)
    public static float CalculatePoly6Laplacian(float r, float h)
    {
        float h2 = h * h;
        float r2 = r * r;
        // 注意：这里系数是负的，因为 Poly6 二阶导数主要部分是负的
        float coefficient = -48f / (Mathf.PI * Mathf.Pow(h, 8));
        // float coefficient = -945.0f / (32.0f * Mathf.PI * Mathf.Pow(h, 9));

        // 错误实现（保留）：(h2 - 5 * r2) 与注释公式不一致。
        // return coefficient * (h2 - r2) * (h2 - 5 * r2);

        // 正确实现：应使用 (h2 - 3 * r2)。
        return coefficient * (h2 - r2) * (h2 - 3 * r2);
    }
}
