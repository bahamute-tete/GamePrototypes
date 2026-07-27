using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointFollowingEffect : MonoBehaviour
{
    [Header("Point Settings")]
    public Transform[] points = new Transform[4]; // P0, P1, P2, P3
    
    [Header("Follow Settings")]
    public float[] followSpeeds = new float[4] { 1f, 0.8f, 0.6f, 0.4f }; // 每个点的跟随速度（空气阻力效果）
    public float restoreSpeed = 2f; // 恢复直线的速度
    public float spacing = 2f; // 点之间的间距
    
    [Header("Control Settings")]
    public float moveSpeed = 3f; // P0移动速度
    public KeyCode upKey = KeyCode.W;
    public KeyCode downKey = KeyCode.S;
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;
    
    [Header("Constraint Settings")]
    public bool enforceDistanceConstraints = true; // 是否启用距离约束
    public int constraintIterations = 3; // 约束迭代次数
    
    private Vector3[] targetPositions = new Vector3[4]; // 目标位置
    private Vector3[] velocities = new Vector3[4]; // 当前速度
    private Vector3 lastP0Position; // 上一帧P0的位置
    private bool isMoving = false; // P0是否在移动
    
    void Start()
    {
        CreatePointsIfNeeded();
        
        // 初始化为垂直向下的直线
        for (int i = 0; i < 4; i++)
        {
            points[i].position = transform.position + Vector3.down * i * spacing;
            targetPositions[i] = points[i].position;
            velocities[i] = Vector3.zero;
        }
        
        lastP0Position = points[0].position;
    }

    void Update()
    {
        // 控制P0移动
        ControlFirstPoint();
        
        // 更新跟随逻辑
        UpdateFollowingPoints();
        
        // 应用距离约束
        if (enforceDistanceConstraints)
        {
            ApplyDistanceConstraints();
        }
    }
    
    void ControlFirstPoint()
    {
        if (points[0] == null) return;
        
        Vector3 movement = Vector3.zero;
        
        if (Input.GetKey(upKey)) movement += Vector3.forward;
        if (Input.GetKey(downKey)) movement += Vector3.back;
        if (Input.GetKey(leftKey)) movement += Vector3.left;
        if (Input.GetKey(rightKey)) movement += Vector3.right;
        
       

        if (movement != Vector3.zero)
        {
            movement = movement.normalized * moveSpeed * Time.deltaTime;
            points[0].position += movement;
            isMoving = true;
        }
        else
        {
            isMoving = false;
        }
        
        // 计算P0的移动向量
        Vector3 p0Movement = points[0].position - lastP0Position;
        lastP0Position = points[0].position;
        velocities[0] = p0Movement / Time.deltaTime;
        targetPositions[0] = points[0].position;
    }
    
    void UpdateFollowingPoints()
    {
        for (int i = 1; i < 4; i++)
        {
            if (points[i] == null) continue;
            
            if (isMoving)
            {
                Vector3 prevPoint = points[i-1].position;
                Vector3 currentPoint = points[i].position;
                
                Vector3 toPrevPoint = prevPoint - currentPoint;
                float currentDistance = toPrevPoint.magnitude;
                
                if (currentDistance > spacing)
                {
                   
                    Vector3 direction = toPrevPoint.normalized;
                    Vector3 idealPosition = prevPoint - direction * spacing;

                    
                    float lerpSpeed = followSpeeds[i]*3.0f; // 增加响应速度
                    targetPositions[i] = Vector3.Lerp(currentPoint, idealPosition, lerpSpeed * Time.deltaTime);
                }
                else
                {
                   
                    targetPositions[i] = currentPoint;
                }
            }
            else
            {
               
                Vector3 direction = Vector3.down; 
                
                Vector3 basePosition = points[0].position;

                Vector3 idealPosition = basePosition + direction * i * spacing;
                
                targetPositions[i] = Vector3.Lerp(targetPositions[i], idealPosition, restoreSpeed * Time.deltaTime);
            }

            Vector3 oldPosition = points[i].position;
            points[i].position = targetPositions[i];
            
            velocities[i] = (points[i].position - oldPosition) / Time.deltaTime;
        }
    }
    
    void ApplyDistanceConstraints()
    {
       
        for (int iteration = 0; iteration < constraintIterations; iteration++)
        {
            
            for (int i = 0; i < 3; i++)
            {
                if (points[i] == null || points[i + 1] == null) continue;
                
                Vector3 pos1 = points[i].position;
                Vector3 pos2 = points[i + 1].position;
                
                Vector3 direction = pos2 - pos1;
                float currentDistance = direction.magnitude;
                
                if (currentDistance > 0.001f) 
                {
                    Vector3 normalizedDirection = direction.normalized; //direction / currentDistance;
                    
                    // 计算新的位置
                    Vector3 newPos2;
                    
                    if (!isMoving)
                    {
                        // 恢复状态：计算理想的链式恢复方向
                        Vector3 idealDirection = Vector3.down;
                        Vector3 idealPos2 = points[0].position + idealDirection * (i + 1) * spacing;
                        
                        // 计算从pos1到理想位置的方向
                        Vector3 toIdealDirection = (idealPos2 - pos1).normalized;
                        
                        // 使用权重混合当前方向和理想方向
                        // 权重基于点的索引，越靠后的点恢复越慢
                        float restoreWeight = restoreSpeed * Time.deltaTime / (i + 1);
                        restoreWeight = Mathf.Clamp01(restoreWeight);
                        
                        Vector3 blendedDirection = Vector3.Slerp(normalizedDirection, toIdealDirection, restoreWeight).normalized;
                        newPos2 = pos1 + blendedDirection * spacing;
                    }
                    else
                    {
                        newPos2 = pos1 + normalizedDirection * spacing;
                    }
                    
                    points[i + 1].position = newPos2;
                }
            }
        }
        
        // 更新目标位置以匹配约束后的位置
        for (int i = 1; i < 4; i++)
        {
            targetPositions[i] = points[i].position;
        }
    }
    
    void CreatePointsIfNeeded()
    {
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Point_P{i}";
                sphere.transform.position = transform.position + Vector3.down * i * spacing;
                sphere.transform.localScale = Vector3.one * 0.5f;
                
                Renderer renderer = sphere.GetComponent<Renderer>();
                Material mat = new Material(Shader.Find("Unlit/Color"));
                mat.color = GetPointColor(i);
                renderer.material = mat;
                
                points[i] = sphere.transform;
            }
        }
    }
    
    Color GetPointColor(int index)
    {
        switch (index)
        {
            case 0: return Color.red;    // P0 - 可控制点
            case 1: return Color.green;  // P1
            case 2: return Color.blue;   // P2
            case 3: return Color.yellow; // P3
            default: return Color.white;
        }
    }
    
    // 可视化连接线
    void OnDrawGizmos()
    {
        if (points == null) return;
        
        // 绘制实际连接线
        Gizmos.color = Color.white;
        for (int i = 0; i < 3; i++)
        {
            if (points[i] != null && points[i + 1] != null)
            {
                Gizmos.DrawLine(points[i].position, points[i + 1].position);
                
                // 显示距离信息（调试用）
                if (enforceDistanceConstraints)
                {
                    float distance = Vector3.Distance(points[i].position, points[i + 1].position);
                    Vector3 midPoint = (points[i].position + points[i + 1].position) * 0.5f;
                    
                    // 距离接近目标值时显示绿色，否则显示红色
                    Gizmos.color = Mathf.Abs(distance - spacing) < 0.1f ? Color.green : Color.red;
                    Gizmos.DrawWireSphere(midPoint, 0.1f);
                }
            }
        }
        
        // 绘制理想直线状态（调试用）
        if (points[0] != null)
        {
            Gizmos.color = Color.gray;
            for (int i = 0; i < 3; i++)
            {
                Vector3 pos1 = points[0].position + Vector3.down * i * spacing;
                Vector3 pos2 = points[0].position + Vector3.down * (i + 1) * spacing;
                Gizmos.DrawLine(pos1, pos2);
            }
        }
    }
}


