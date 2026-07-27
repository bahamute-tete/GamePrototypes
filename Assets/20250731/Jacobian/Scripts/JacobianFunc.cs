using UnityEngine;
using System.Collections.Generic;

public abstract class JacobianFunction
{
    public abstract Vector2 Transform(Vector2 uv);
    public abstract Matrix4x4 GetjacobianMatrix(Vector2 uv);
    
    public float Determinant(Vector2 uv)
    {
        Matrix4x4 J = GetjacobianMatrix(uv);
        return J.m00 * J.m11 - J.m01 * J.m10;
    }

    // 获取所有可调参数
    public virtual Dictionary<string, float> GetParameters()
    {
        return new Dictionary<string, float>();
    }

    // 设置参数值
    public virtual void SetParameter(string paramName, float value)
    {
        // 子类可以重写此方法来设置特定参数
    }

    // 获取单个参数值
    public virtual float GetParameter(string paramName, float defaultValue = 0f)
    {
        var parameters = GetParameters();
        return parameters.ContainsKey(paramName) ? parameters[paramName] : defaultValue;
    }
}


public class IdentityFunction : JacobianFunction
{
    public override Vector2 Transform(Vector2 uv)
    {
        return uv;
    }
    public override Matrix4x4 GetjacobianMatrix(Vector2 uv)
    {
        Matrix4x4 J = Matrix4x4.identity;
        return J;
    }
}

public class PolarFunction : JacobianFunction
{
    public override Vector2 Transform(Vector2 uv)
    {
        float r = uv.x;
        float theta = uv.y;
        float x = r * Mathf.Cos(theta);
        float y = r * Mathf.Sin(theta);
        return new Vector2(x, y);
    }
    public override Matrix4x4 GetjacobianMatrix(Vector2 uv)
    {
        float r = uv.x;
        float theta = uv.y;

        Matrix4x4 J = Matrix4x4.identity;


        J.m00= Mathf.Cos(theta); //∂x/∂r
        J.m01 = -r * Mathf.Sin(theta);//∂x/∂θ
        J.m10 = Mathf.Sin(theta);//∂y/∂r
        J.m11 = r * Mathf.Cos(theta);//∂y/∂θ

       return J;
    }

}

// 球面坐标变换 (u,v) -> (sin(v)*cos(u), sin(v)*sin(u))
public class SphericalFunction : JacobianFunction
{
    public override Vector2 Transform(Vector2 uv)
    {
        float u = uv.x;
        float v = uv.y;
        float x = Mathf.Sin(v) * Mathf.Cos(u);
        float y = Mathf.Sin(v) * Mathf.Sin(u);
        return new Vector2(x, y);
    }

    public override Matrix4x4 GetjacobianMatrix(Vector2 uv)
    {
        float u = uv.x;
        float v = uv.y;

        Matrix4x4 J = Matrix4x4.identity;
        J.m00 = -Mathf.Sin(v) * Mathf.Sin(u); // ∂x/∂u
        J.m01 = Mathf.Cos(v) * Mathf.Cos(u);  // ∂x/∂v
        J.m10 = Mathf.Sin(v) * Mathf.Cos(u);  // ∂y/∂u
        J.m11 = Mathf.Cos(v) * Mathf.Sin(u);  // ∂y/∂v

        return J;
    }
}

// 双曲线变换 (u,v) -> (u*v, u/v)
public class HyperbolicFunction : JacobianFunction
{
    public override Vector2 Transform(Vector2 uv)
    {
        float u = uv.x;
        float v = Mathf.Max(uv.y, 0.001f); // 避免除零
        float x = u * v;
        float y = u / v;
        return new Vector2(x, y);
    }

    public override Matrix4x4 GetjacobianMatrix(Vector2 uv)
    {
        float u = uv.x;
        float v = Mathf.Max(uv.y, 0.001f);

        Matrix4x4 J = Matrix4x4.identity;
        J.m00 = v;           // ∂x/∂u
        J.m01 = u;           // ∂x/∂v
        J.m10 = 1.0f / v;    // ∂y/∂u
        J.m11 = -u / (v * v); // ∂y/∂v

        return J;
    }
}

// 对数螺线变换 (r,θ) -> (e^(a*θ)*cos(θ), e^(a*θ)*sin(θ))
public class LogarithmicSpiralFunction : JacobianFunction
{
    public float a = 0.1f; // 螺线参数

    public override Vector2 Transform(Vector2 uv)
    {
        float r = uv.x;
        float theta = uv.y;
        float radius = r * Mathf.Exp(a * theta);
        float x = radius * Mathf.Cos(theta);
        float y = radius * Mathf.Sin(theta);
        return new Vector2(x, y);
    }

    public override Matrix4x4 GetjacobianMatrix(Vector2 uv)
    {
        float r = uv.x;
        float theta = uv.y;
        float exp_term = Mathf.Exp(a * theta);

        Matrix4x4 J = Matrix4x4.identity;
        J.m00 = exp_term * Mathf.Cos(theta);                           // ∂x/∂r
        J.m01 = r * exp_term * (a * Mathf.Cos(theta) - Mathf.Sin(theta)); // ∂x/∂θ
        J.m10 = exp_term * Mathf.Sin(theta);                           // ∂y/∂r
        J.m11 = r * exp_term * (a * Mathf.Sin(theta) + Mathf.Cos(theta)); // ∂y/∂θ

        return J;
    }

    public override Dictionary<string, float> GetParameters()
    {
        return new Dictionary<string, float>
        {
            { "a", a }
        };
    }

    public override void SetParameter(string paramName, float value)
    {
        if (paramName == "a")
            a = value;
    }
}

// 扭曲变换 - 漩涡效果
public class TwistFunction : JacobianFunction
{
    public float strength = 1.0f; // 漩涡强度
    public Vector2 center = new Vector2(0.5f, 0.5f); // 漩涡中心

    public override Vector2 Transform(Vector2 uv)
    {
        float u = uv.x;
        float v = uv.y;
        
        // 计算相对于中心的位置
        float dx = u - center.x;
        float dy = v - center.y;
        
        // 计算距离中心的距离
        float distance = Mathf.Sqrt(dx * dx + dy * dy);
        
        // 根据距离计算旋转角度(距离越近,旋转越强)
        float angle = strength * (1.0f - Mathf.Clamp01(distance / 0.707f)); // 0.707 约等于从中心到角的距离
        
        // 应用旋转
        float cosA = Mathf.Cos(angle);
        float sinA = Mathf.Sin(angle);
        
        float x = center.x + dx * cosA - dy * sinA;
        float y = center.y + dx * sinA + dy * cosA;
        
        return new Vector2(x, y);
    }

    public override Matrix4x4 GetjacobianMatrix(Vector2 uv)
    {
        float u = uv.x;
        float v = uv.y;
        
        float dx = u - center.x;
        float dy = v - center.y;
        float distance = Mathf.Sqrt(dx * dx + dy * dy);
        
        if (distance < 0.0001f) distance = 0.0001f; // 避免除零
        
        float normDist = Mathf.Clamp01(distance / 0.707f);
        float angle = strength * (1.0f - normDist);
        
        float cosA = Mathf.Cos(angle);
        float sinA = Mathf.Sin(angle);
        
        // 角度对距离的导数
        float dAngle_dDist = -strength / 0.707f;
        
        // 距离对u,v的导数
        float dDist_du = dx / distance;
        float dDist_dv = dy / distance;
        
        // 旋转矩阵的导数
        float dCos_du = -sinA * dAngle_dDist * dDist_du;
        float dCos_dv = -sinA * dAngle_dDist * dDist_dv;
        float dSin_du = cosA * dAngle_dDist * dDist_du;
        float dSin_dv = cosA * dAngle_dDist * dDist_dv;

        Matrix4x4 J = Matrix4x4.identity;
        
        // ∂x/∂u = ∂/∂u(center.x + dx*cos - dy*sin)
        J.m00 = cosA + dx * dCos_du - dy * dSin_du;
        
        // ∂x/∂v = ∂/∂v(center.x + dx*cos - dy*sin)
        J.m01 = dx * dCos_dv - sinA - dy * dSin_dv;
        
        // ∂y/∂u = ∂/∂u(center.y + dx*sin + dy*cos)
        J.m10 = sinA + dx * dSin_du + dy * dCos_du;
        
        // ∂y/∂v = ∂/∂v(center.y + dx*sin + dy*cos)
        J.m11 = dx * dSin_dv + cosA + dy * dCos_dv;

        return J;
    }

    public override Dictionary<string, float> GetParameters()
    {
        return new Dictionary<string, float>
        {
            { "strength", strength }
        };
    }

    public override void SetParameter(string paramName, float value)
    {
        if (paramName == "strength")
            strength = value;
    }
}

// 缩放旋转变换 (u,v) -> (u*cos(angle)-v*sin(angle), u*sin(angle)+v*cos(angle)) * scale
public class ScaleRotateFunction : JacobianFunction
{
    public float scaleX = 1.3f;
    public float scaleY = 0.8f;
    public float angle = Mathf.PI / 4; // 45度

    public override Vector2 Transform(Vector2 uv)
    {
        float u = uv.x;
        float v = uv.y;
        float cosA = Mathf.Cos(angle);
        float sinA = Mathf.Sin(angle);
        //先将旋转中心设为（0.5，0.5）
        float x = scaleX * ((u - 0.5f) * cosA - (v - 0.5f) * sinA) + 0.5f;
        float y = scaleY * ((u - 0.5f) * sinA + (v - 0.5f) * cosA) + 0.5f;
        return new Vector2(x, y);
    }

    public override Matrix4x4 GetjacobianMatrix(Vector2 uv)
    {
        float cosA = Mathf.Cos(angle);
        float sinA = Mathf.Sin(angle);

        Matrix4x4 J = Matrix4x4.identity;
        J.m00 = scaleX * cosA;  // ∂x/∂u
        J.m01 = -scaleX * sinA; // ∂x/∂v
        J.m10 = scaleY * sinA;  // ∂y/∂u
        J.m11 = scaleY * cosA;  // ∂y/∂v

        return J;
    }

    public override Dictionary<string, float> GetParameters()
    {
        return new Dictionary<string, float>
        {
            { "scaleX", scaleX },
            { "scaleY", scaleY },
            { "angle", angle }
        };
    }

    public override void SetParameter(string paramName, float value)
    {
        switch (paramName)
        {
            case "scaleX":
                scaleX = value;
                break;
            case "scaleY":
                scaleY = value;
                break;
            case "angle":
                angle = value;
                break;
        }
    }
}


