using UnityEngine;
using System.Collections.Generic;

[ExecuteInEditMode]
public class RoadPathManager : MonoBehaviour
{
    // 添加路径改变事件
    public event System.Action onPathChanged;

    [Header("Path Settings")]
    public List<Transform> controlPoints = new List<Transform>();
    public int samplingPoints = 20;
    public bool autoUpdate = true;

    [Header("Debug Visualization")]
    public bool showGizmos = true;
    public float gizmoSize = 0.1f;
    public float directionLength = 1f;
    public float controlPointSize = 0.5f;  // 添加这行：控制点大小
    [Space]
    public Color pathColor = Color.green;
    public Color tangentColor = Color.blue;
    public Color upDirectionColor = Color.red;
    public Color controlPointColor = Color.yellow;
    
    private PathPoint[] currentPathPoints;
    private Vector3[] lastPositions;
    private Quaternion[] lastRotations; // 添加字段
    private ControlPointData[] controlPointsData;

    [Header("Path Sampling Settings")]
    [Tooltip("控制采样模式")]
    public SamplingMode pathSamplingMode = SamplingMode.UniformGlobal;
    [Range(0.1f, 5f)]
    public float densityFactor = 1f; // 点密度调整因子

    // 添加采样模式枚举
    public enum SamplingMode
    {
        UniformPerSegment, // 每段固定数量点
        UniformGlobal      // 全局等距分布点
    }

    // 简化的数据结构
    [System.Serializable]
    public struct PathPoint
    {
        public Vector3 position;
        public Vector3 tangent;
        public Vector3 up;
        
        // 添加默认构造函数
        public PathPoint(Vector3 pos, Vector3 tan, Vector3 upDir)
        {
            position = pos;
            tangent = tan;
            up = upDir;
        }
    }

    public struct ControlPointData
    {
        public Vector3 position;
        public Vector3 tangent;
        public Vector3 up;
        public Vector3 bitangent;
    }

    // 公共访问方法
    public ControlPointData[] GetControlPointsData()
    {
        // 即使点数量少于4，也需要更新和返回数据
        if (controlPointsData == null || controlPointsData.Length != controlPoints.Count)
        {
            UpdateControlPointsData();
        }
        return controlPointsData;
    }

    public PathPoint[] GetPathPoints() => currentPathPoints;

    // 核心更新方法
    public void UpdatePath()
    {
        if (!ValidateControlPoints()) return;
        
        UpdateControlPointsData();
        GeneratePathPoints();
        NotifyDependents();
    }

    private bool ValidateControlPoints()
    {
        // 修改验证规则，允许少于4个点
        if (controlPoints == null || controlPoints.Count == 0)
        {
            currentPathPoints = null;
            return false;
        }

        // 检查空引用
        for (int i = 0; i < controlPoints.Count; i++)
        {
            if (controlPoints[i] == null)
            {
                Debug.LogError($"控制点 {i} 为空");
                return false;
            }
        }

        return true;
    }

    // Path generation methods
    private void UpdateControlPointsData()
    {
        // 点数检查，但不直接返回
        if (controlPoints == null || controlPoints.Count == 0)
        {
            controlPointsData = null;
            return;
        }

        // 创建新的控制点数据数组
        controlPointsData = new ControlPointData[controlPoints.Count];

        // 不同点数情况的处理
        if (controlPoints.Count < 4)
        {
            // 少于4个点的情况使用直线模式
            UpdateControlPointsDataLinear();
        }
        else
        {
            // 4个或更多点的情况使用曲线模式(原有代码)
            for (int i = 0; i < controlPoints.Count; i++)
            {
                Transform controlPoint = controlPoints[i];
                Vector3 position = controlPoint.position;
                Vector3 tangent;

                // 计算切线方向
                if (i == 0)
                {
                    Vector3 p0 = position + (position - controlPoints[1].position);
                    Vector3 p2 = controlPoints[1].position;
                    Vector3 p3 = controlPoints[2].position;
                    tangent = CatmullRomTangent(0, p0, position, p2, p3);
                }
                else if (i == controlPoints.Count - 1)
                {
                    Vector3 p0 = controlPoints[i - 2].position;
                    Vector3 p1 = controlPoints[i - 1].position;
                    Vector3 p3 = position + (position - controlPoints[i - 1].position);
                    tangent = CatmullRomTangent(1, p0, p1, position, p3);
                }
                else
                {
                    Vector3 p0 = controlPoints[i - 1].position;
                    Vector3 p2 = controlPoints[i + 1].position;
                    Vector3 p3 = i < controlPoints.Count - 2 ? controlPoints[i + 2].position : p2 + (p2 - controlPoints[i].position);
                    tangent = CatmullRomTangent(0, p0, position, p2, p3);
                }

                // 对齐控制点的forward(Z轴)到切线方向
                Quaternion targetRotation = Quaternion.LookRotation(tangent, controlPoint.up);
                controlPoint.rotation = targetRotation;

                // 使用控制点的本地坐标轴来定义方向
                controlPointsData[i] = new ControlPointData
                {
                    position = position,
                    tangent = controlPoint.forward,     // 切线方向 (Z轴)
                    up = controlPoint.up,               // 上方向 (Y轴)
                    bitangent = controlPoint.right      // 副切线方向 (X轴) - 注意这里改为right
                };

                #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorUtility.SetDirty(controlPoint);
                }
                #endif
            }
        }
    }

    // 添加新方法：处理少于4个点的控制点数据
    private void UpdateControlPointsDataLinear()
    {
        for (int i = 0; i < controlPoints.Count; i++)
        {
            Transform controlPoint = controlPoints[i];
            Vector3 position = controlPoint.position;
            Vector3 tangent;
            
            if (controlPoints.Count == 1)
            {
                // 只有一个点，使用其当前朝向作为切线
                tangent = controlPoint.forward;
            }
            else if (i == 0)
            {
                // 第一个点，朝向下一个点
                tangent = (controlPoints[1].position - position).normalized;
            }
            else if (i == controlPoints.Count - 1)
            {
                // 最后一个点，使用来自前一个点的方向
                tangent = (position - controlPoints[i - 1].position).normalized;
            }
            else
            {
                // 中间点，使用前后点的平均方向
                Vector3 prevDir = (position - controlPoints[i - 1].position).normalized;
                Vector3 nextDir = (controlPoints[i + 1].position - position).normalized;
                tangent = ((prevDir + nextDir) * 0.5f).normalized;
            }
            
            // 对齐控制点的forward到切线方向
            Quaternion targetRotation = Quaternion.LookRotation(tangent, controlPoint.up);
            controlPoint.rotation = targetRotation;
            
            // 保存数据
            controlPointsData[i] = new ControlPointData
            {
                position = position,
                tangent = controlPoint.forward,
                up = controlPoint.up,
                bitangent = controlPoint.right
            };
            
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(controlPoint);
            }
            #endif
        }
    }

    private void GeneratePathPoints()
    {
        // 检查控制点列表
        if (controlPoints == null || controlPoints.Count == 0)
        {
            Debug.LogWarning("控制点列表为空，请添加控制点");
            return;
        }
        
        // 检查是否存在空引用
        for (int i = 0; i < controlPoints.Count; i++)
        {
            if (controlPoints[i] == null)
            {
                Debug.LogError($"控制点列表中第 {i} 个点为空，请检查控制点");
                return;
            }
        }

        // 处理少于4个控制点的情况 - 使用直线连接
        if (controlPoints.Count < 4)
        {
            GeneratePathPointsLinear();
            return;
        }

        // 采用全局均匀采样
        if (pathSamplingMode == SamplingMode.UniformGlobal)
        {
            GenerateGloballyUniformPathPoints();
        }
        else // 默认采用原有的每段固定数量采样
        {
            GenerateSegmentUniformPathPoints();
        }
    }

    // 修改直线模式下的路径点生成方法
    private void GeneratePathPointsLinear()
    {
        List<PathPoint> pathPoints = new List<PathPoint>();
        
        // 特殊情况：只有一个点
        if (controlPoints.Count == 1)
        {
            Vector3 position = controlPoints[0].position;
            Vector3 tangent = controlPoints[0].forward;
            Vector3 up = CalculateUp(tangent);
            
            pathPoints.Add(new PathPoint(position, tangent, up));
            currentPathPoints = pathPoints.ToArray();
            return;
        }
        
        // 计算每段路径应该分配的采样点数
        int totalSegments = controlPoints.Count - 1;
        int pointsPerSegment = Mathf.Max(2, samplingPoints / totalSegments);
        
        // 生成路径点
        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            Vector3 startPos = controlPoints[i].position;
            Vector3 endPos = controlPoints[i + 1].position;
            Vector3 direction = (endPos - startPos).normalized;
            Vector3 up = CalculateUp(direction);
            
            // 添加起始点(除第一段外，其他段不需要重复添加)
            if (i == 0)
            {
                pathPoints.Add(new PathPoint(startPos, direction, up));
            }
            
            // 添加中间点
            for (int j = 1; j < pointsPerSegment; j++)
            {
                float t = j / (float)pointsPerSegment;
                Vector3 position = Vector3.Lerp(startPos, endPos, t);
                pathPoints.Add(new PathPoint(position, direction, up));
            }
            
            // 添加终点(作为下一段的起点，最后一段除外)
            if (i < controlPoints.Count - 2)
            {
                // 对于中间控制点，需要计算平滑的切线方向
                Vector3 prevDirection = direction;
                Vector3 nextDirection = (controlPoints[i + 2].position - endPos).normalized;
                Vector3 blendedDirection = (prevDirection + nextDirection).normalized;
                Vector3 blendedUp = CalculateUp(blendedDirection);
                
                pathPoints.Add(new PathPoint(endPos, blendedDirection, blendedUp));
            }
        }
        
        // 添加最终点 - 确保使用原始控制点
        Vector3 finalPos = controlPoints[controlPoints.Count - 1].position;
        Vector3 finalDir = (finalPos - controlPoints[controlPoints.Count - 2].position).normalized;
        Vector3 finalUp = CalculateUp(finalDir);
        
        pathPoints.Add(new PathPoint(finalPos, finalDir, finalUp));
        
        currentPathPoints = pathPoints.ToArray();
    }

    // 原有的每段固定点数采样方法 - 修复起点和终点处理
    private void GenerateSegmentUniformPathPoints()
    {
        // 首先生成密集的临时采样点来计算总长度
        List<PathPoint> temporaryPoints = new List<PathPoint>();
        float smallIncrement = 0.01f; // 使用较小的增量进行密集采样
        
        // 确保起点被添加
        Vector3 startPos = controlPoints[0].position;
        Vector3 startTangent = (controlPoints[1].position - startPos).normalized;
        Vector3 startUp = CalculateUp(startTangent);
        temporaryPoints.Add(new PathPoint(startPos, startTangent, startUp));
        
        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            Vector3 p0 = GetPoint(i - 1);
            Vector3 p1 = GetPoint(i);
            Vector3 p2 = GetPoint(i + 1);
            Vector3 p3 = GetPoint(i + 2);
            
            // 对当前段进行密集采样 - 跳过起点
            for (float t = smallIncrement; t <= 1f; t += smallIncrement)
            {
                Vector3 position = CatmullRomPoint(t, p0, p1, p2, p3);
                Vector3 tangent = CatmullRomTangent(t, p0, p1, p2, p3);
                Vector3 up = CalculateUp(tangent);
                temporaryPoints.Add(new PathPoint(position, tangent, up));
                
                // 确保最后一段的终点使用原始控制点
                if (i == controlPoints.Count - 2 && t >= 1f - smallIncrement)
                {
                    Vector3 endPos = controlPoints[controlPoints.Count - 1].position;
                    Vector3 endTangent = (endPos - controlPoints[controlPoints.Count - 2].position).normalized;
                    Vector3 endUp = CalculateUp(endTangent);
                    temporaryPoints.Add(new PathPoint(endPos, endTangent, endUp));
                }
            }
        }
        
        // 计算总长度
        float totalLength = 0f;
        for (int i = 0; i < temporaryPoints.Count - 1; i++)
        {
            totalLength += Vector3.Distance(temporaryPoints[i].position, temporaryPoints[i + 1].position);
        }
        
        // 计算目标间隔距离
        float targetSegmentLength = totalLength / (samplingPoints - 1);
        
        // 生成最终的均匀分布点
        List<PathPoint> finalPoints = new List<PathPoint>();
        finalPoints.Add(temporaryPoints[0]); // 添加起点
        
        float currentDistance = 0f;
        float accumulatedDistance = 0f;
        int currentIndex = 0;
        
        // 在目标间隔处采样点
        for (int i = 1; i < samplingPoints - 1; i++)
        {
            float targetDistance = i * targetSegmentLength;
            
            // 找到目标距离所在的位置
            while (accumulatedDistance < targetDistance && currentIndex < temporaryPoints.Count - 1)
            {
                currentDistance = Vector3.Distance(temporaryPoints[currentIndex].position, 
                    temporaryPoints[currentIndex + 1].position);
                accumulatedDistance += currentDistance;
                currentIndex++;
            }
            
            // 计算插值位置
            float overshoot = accumulatedDistance - targetDistance;
            float t = 1 - (overshoot / currentDistance);
            
            // 在临近点之间插值
            int prevIndex = currentIndex - 1;
            Vector3 position = Vector3.Lerp(temporaryPoints[prevIndex].position, 
                temporaryPoints[currentIndex].position, t);
            Vector3 tangent = Vector3.Lerp(temporaryPoints[prevIndex].tangent, 
                temporaryPoints[currentIndex].tangent, t).normalized;
            Vector3 up = CalculateUp(tangent);
            
            finalPoints.Add(new PathPoint(position, tangent, up));
        }
        
        // 添加终点(确保使用原始控制点的终点)
        Vector3 finalPos = controlPoints[controlPoints.Count - 1].position;
        Vector3 finalTangent = (finalPos - controlPoints[controlPoints.Count - 2].position).normalized;
        Vector3 finalUp = CalculateUp(finalTangent);
        finalPoints.Add(new PathPoint(finalPos, finalTangent, finalUp));
        
        currentPathPoints = finalPoints.ToArray();
    }

    // 新增全局均匀采样方法 - 修复起点和终点处理
    private void GenerateGloballyUniformPathPoints()
    {
        // 第一步：创建一个详细的长度映射数组，存储每个控制点段落的近似累积长度
        float[] segmentLengths = new float[controlPoints.Count - 1];
        float[] cumulativeLengths = new float[controlPoints.Count];
        cumulativeLengths[0] = 0f;
        float totalApproxLength = 0f;
        
        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            // 使用直线距离作为每段曲线的粗略长度估计
            Vector3 p1 = controlPoints[i].position;
            Vector3 p2 = controlPoints[i + 1].position;
            float segmentLength = Vector3.Distance(p1, p2);
            
            // 曲线通常比直线长，因此添加一个系数来修正（可根据实际曲率调整）
            segmentLength *= 1.2f; 
            
            segmentLengths[i] = segmentLength;
            totalApproxLength += segmentLength;
            cumulativeLengths[i + 1] = totalApproxLength;
        }
        
        // 第二步：精确计算每段曲线的实际长度
        List<List<PathPoint>> segmentPoints = new List<List<PathPoint>>();
        List<float> actualSegmentLengths = new List<float>();
        float totalActualLength = 0f;
        
        // 为每段曲线生成密集采样点
        for (int i = 0; i < controlPoints.Count - 3; i++) // 注意范围变化，考虑曲线需要4个控制点
        {
            Vector3 p0 = GetPoint(i);
            Vector3 p1 = GetPoint(i + 1);
            Vector3 p2 = GetPoint(i + 2);
            Vector3 p3 = GetPoint(i + 3);
            
            // 根据估计长度动态调整采样密度
            float segmentEstimatedLength = segmentLengths[i];
            int pointCount = Mathf.Max(10, Mathf.CeilToInt(segmentEstimatedLength * 5 * densityFactor)); // 密集采样
            float increment = 1f / pointCount;
            
            List<PathPoint> currentSegmentPoints = new List<PathPoint>();
            float currentSegmentLength = 0f;
            
            PathPoint? prevPoint = null; // 使用可空类型
            
            for (float t = 0f; t <= 1f; t += increment)
            {
                Vector3 pos = CatmullRomPoint(t, p0, p1, p2, p3);
                Vector3 tan = CatmullRomTangent(t, p0, p1, p2, p3);
                Vector3 upDir = CalculateUp(tan);
                
                PathPoint point = new PathPoint(pos, tan, upDir);
                
                if (prevPoint.HasValue) // 使用可空类型的HasValue检查
                {
                    currentSegmentLength += Vector3.Distance(prevPoint.Value.position, point.position);
                }
                
                currentSegmentPoints.Add(point);
                prevPoint = point;
            }
            
            segmentPoints.Add(currentSegmentPoints);
            actualSegmentLengths.Add(currentSegmentLength);
            totalActualLength += currentSegmentLength;
        }
        
        // 特殊处理：如果只有3个控制点，则使用线性插值
        if (controlPoints.Count == 3)
        {
            GeneratePathPointsLinear();
            return;
        }
        
        // 第三步：全局均匀采样
        List<PathPoint> uniformPoints = new List<PathPoint>();
        float spacingDistance = totalActualLength / (samplingPoints - 1);
        
        // 总是添加第一个控制点作为起点
        Vector3 firstPos = controlPoints[0].position;
        Vector3 firstTangent = (controlPoints[1].position - firstPos).normalized;
        Vector3 firstUp = CalculateUp(firstTangent);
        uniformPoints.Add(new PathPoint(firstPos, firstTangent, firstUp));
        
        float accumulatedDistance = 0f;
        int currentSegment = 0;
        int currentPointIndex = 0;
        
        for (int i = 1; i < samplingPoints - 1; i++)
        {
            float targetDistance = i * spacingDistance;
            
            // 找到目标距离所在的段
            while (currentSegment < segmentPoints.Count && 
                   accumulatedDistance + actualSegmentLengths[currentSegment] < targetDistance)
            {
                accumulatedDistance += actualSegmentLengths[currentSegment];
                currentSegment++;
                currentPointIndex = 0;
            }
            
            if (currentSegment >= segmentPoints.Count)
            {
                // 已经超出了所有段，直接添加最后一个点
                break;
            }
            
            // 在当前段内，找到目标距离所在的具体位置
            List<PathPoint> currentPoints = segmentPoints[currentSegment];
            float segmentAccumDistance = 0f;
            
            while (currentPointIndex < currentPoints.Count - 1 && 
                   accumulatedDistance + segmentAccumDistance < targetDistance)
            {
                segmentAccumDistance += Vector3.Distance(
                    currentPoints[currentPointIndex].position,
                    currentPoints[currentPointIndex + 1].position);
                currentPointIndex++;
            }
            
            // 计算插值参数
            float segmentTargetDistance = targetDistance - accumulatedDistance;
            float pointDistance = Vector3.Distance(
                currentPoints[Mathf.Max(0, currentPointIndex - 1)].position,
                currentPoints[currentPointIndex].position);
            float localRatio = (segmentTargetDistance - (segmentAccumDistance - pointDistance)) / pointDistance;
            
            // 对位置、切线和上向量进行插值
            PathPoint prevPoint = currentPoints[Mathf.Max(0, currentPointIndex - 1)];
            PathPoint nextPoint = currentPoints[currentPointIndex];
            
            PathPoint interpolatedPoint = new PathPoint(
                Vector3.Lerp(prevPoint.position, nextPoint.position, localRatio),
                Vector3.Lerp(prevPoint.tangent, nextPoint.tangent, localRatio).normalized,
                Vector3.Lerp(prevPoint.up, nextPoint.up, localRatio).normalized
            );
            
            uniformPoints.Add(interpolatedPoint);
        }
        
        // 添加最后一个控制点作为终点
        int lastIndex = controlPoints.Count - 1;
        Vector3 lastPos = controlPoints[lastIndex].position;
        Vector3 lastTangent = (lastPos - controlPoints[lastIndex - 1].position).normalized;
        Vector3 lastUp = CalculateUp(lastTangent);
        uniformPoints.Add(new PathPoint(lastPos, lastTangent, lastUp));
        
        currentPathPoints = uniformPoints.ToArray();
    }

    // Utility methods
    private Vector3 GetPoint(int index)
    {
        if (index < 0)
            return controlPoints[0].position * 2 - controlPoints[1].position;
        if (index >= controlPoints.Count)
            return controlPoints[controlPoints.Count - 1].position * 2 - controlPoints[controlPoints.Count - 2].position;
        return controlPoints[index].position;
    }
    
    private Vector3 CatmullRomPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        
        return 0.5f * (
            (-t3 + 2f * t2 - t) * p0 +
            (3f * t3 - 5f * t2 + 2f) * p1 +
            (-3f * t3 + 4f * t2 + t) * p2 +
            (t3 - t2) * p3
        );
    }
    
    private Vector3 CatmullRomTangent(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float t2 = t * t;
        
        return 0.5f * (
            (-3f * t2 + 4f * t - 1f) * p0 +
            (9f * t2 - 10f * t) * p1 +
            (-9f * t2 + 8f * t + 1f) * p2 +
            (3f * t2 - 2f * t) * p3
        ).normalized;
    }
    
    private Vector3 CalculateUp(Vector3 tangent)
    {
        Vector3 up = Vector3.up;
        Vector3 right = Vector3.Cross(up, tangent).normalized;
        return Vector3.Cross(tangent, right).normalized;
    }

    // Control point management
    public void CreateControlPoint()
    {
        GameObject pointObject = new GameObject($"ControlPoint_{controlPoints.Count}");
        Transform pointTransform = pointObject.transform;
        pointTransform.SetParent(transform);
        
        // 设置初始位置
        Vector3 newPosition;
        if (controlPoints.Count == 0)
        {
            newPosition = transform.position;
            pointTransform.rotation = Quaternion.identity; // 第一个点使用默认旋转
        }
        else
        {
            // 使用前一个点的方向信息
            Transform lastPoint = controlPoints[controlPoints.Count - 1];
            newPosition = lastPoint.position + lastPoint.forward * 2f; // 使用forward(Z轴)作为前进方向
            pointTransform.rotation = lastPoint.rotation; // 继承前一个点的旋转
        }
        
        pointTransform.position = newPosition;
        controlPoints.Add(pointTransform);
        UpdatePath();
    }

    private void NotifyDependents()
    {
        // 触发路径改变事件
        onPathChanged?.Invoke();

        var roadMesh = GetComponent<RoadMeshGenerator>();
        if (roadMesh != null) roadMesh.GenerateRoadMesh();
    }

    // Unity callbacks
    void OnValidate()
    {
        // 避免在检查阶段执行重量级操作
        // 而是通过延迟执行方式安排在下一帧执行
        if (autoUpdate)
        {
            #if UNITY_EDITOR
            if (Application.isPlaying)
            {
                // 在游戏运行时可以直接更新
                UpdatePath();
            }
            else
            {
                // 在编辑模式下，延迟更新
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null) // 检查对象是否仍然存在
                    {
                        UpdatePath();
                    }
                };
            }
            #else
            UpdatePath();
            #endif
        }
    }

    void Update()
    {
        if (autoUpdate) CheckControlPointsChanged();
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;
        DrawControlPoints();
        DrawPath();
    }

    // Debug visualization
    private void DrawControlPoints()
    {
        if (controlPoints != null)
        {
            // 获取控制点数据
            var pointsData = GetControlPointsData();
            
            if (pointsData != null)
            {
                for (int i = 0; i < controlPoints.Count; i++)
                {
                    if (controlPoints[i] != null)
                    {
                        Vector3 position = controlPoints[i].position;
                        
                        // 绘制控制点球体
                        Gizmos.color = controlPointColor;
                        Gizmos.DrawWireSphere(position, controlPointSize);
                        
                        // 绘制坐标轴
                        float axisSize = directionLength;
                        
                        // 切线方向 (蓝色)
                        Gizmos.color = Color.blue;
                        Gizmos.DrawRay(position, pointsData[i].tangent * axisSize);
                        
                        // Up方向 (绿色)
                        Gizmos.color = Color.green;
                        Gizmos.DrawRay(position, pointsData[i].up * axisSize);
                        
                        // 副切线方向 (红色)
                        Gizmos.color = Color.red;
                        Gizmos.DrawRay(position, pointsData[i].bitangent * axisSize);
                        
                        // 绘制小球标示轴端点
                        float endPointSize = controlPointSize * 0.3f;
                        Gizmos.color = Color.blue;
                        Gizmos.DrawSphere(position + pointsData[i].tangent * axisSize, endPointSize);
                        Gizmos.color = Color.green;
                        Gizmos.DrawSphere(position + pointsData[i].up * axisSize, endPointSize);
                        Gizmos.color = Color.red;
                        Gizmos.DrawSphere(position + pointsData[i].bitangent * axisSize, endPointSize);
                    }
                }
            }
        }
    }

    // 修改DrawPath方法以正确显示路径
    private void DrawPath()
    {
        if (currentPathPoints != null && currentPathPoints.Length > 1)
        {
            // 绘制路径线
            Gizmos.color = pathColor;
            for (int i = 0; i < currentPathPoints.Length - 1; i++)
            {
                // 这里的position已经是世界坐标，所以不需要转换
                Gizmos.DrawLine(currentPathPoints[i].position, currentPathPoints[i + 1].position);
            }

            // 绘制采样点的方向
            for (int i = 0; i < currentPathPoints.Length; i++)
            {
                Vector3 position = currentPathPoints[i].position;
                
                // 绘制采样点位置
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(position, gizmoSize * 0.5f);
                
                // 绘制切线方向
                Gizmos.color = tangentColor;
                Gizmos.DrawRay(position, currentPathPoints[i].tangent * directionLength * 0.5f);

                // 绘制上方向
                Gizmos.color = upDirectionColor;
                Gizmos.DrawRay(position, currentPathPoints[i].up * directionLength * 0.5f);
            }
        }
    }

    private void CheckControlPointsChanged()
    {
        if (controlPoints == null || controlPoints.Count == 0) return;

        // 初始化或重新初始化数组
        if (lastPositions == null || lastRotations == null || 
            lastPositions.Length != controlPoints.Count || 
            lastRotations.Length != controlPoints.Count)
        {
            lastPositions = new Vector3[controlPoints.Count];
            lastRotations = new Quaternion[controlPoints.Count];
            
            // 初始化时保存当前状态
            for (int i = 0; i < controlPoints.Count; i++)
            {
                if (controlPoints[i] != null)
                {
                    lastPositions[i] = controlPoints[i].position;
                    lastRotations[i] = controlPoints[i].rotation;
                }
            }
            return;
        }

        // 检查变化
        bool changed = false;
        for (int i = 0; i < controlPoints.Count; i++)
        {
            Transform controlPoint = controlPoints[i];
            if (controlPoint != null)
            {
                if (lastPositions[i] != controlPoint.position || 
                    lastRotations[i] != controlPoint.rotation)
                {
                    lastPositions[i] = controlPoint.position;
                    lastRotations[i] = controlPoint.rotation;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            UpdatePath();
        }
    }
}
