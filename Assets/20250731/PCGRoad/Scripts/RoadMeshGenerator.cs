using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class RoadMeshGenerator : MonoBehaviour
{
    [Header("Road Settings")]
    public RoadPathManager pathManager;
    public float roadWidth = 5f;
    [Range(0f, 2f)]
    [Tooltip("道路厚度，设为0则不生成厚度")]
    public float roadDepth = 0.2f; // 添加厚度参数

    [Header("Material Settings")]
    [Tooltip("设为true时使用多材质，否则仅使用第一个材质")]
    public bool useMultipleMaterials = false;
    [Tooltip("顶面材质")]
    public Material topMaterial;
    [Tooltip("侧面材质")]
    public Material sideMaterial;
    [Tooltip("底面材质")]
    public Material bottomMaterial;
    
    [Header("Update Settings")]
    [SerializeField] // 为了让编辑器可以看到这个变量
    public bool autoUpdate = false;  // 改为 public，让编辑器可以访问
    
    [Header("UV Settings")]
    public Vector2 uvRepeat = new Vector2(1f, 1f); // 修改为Vector2，分别控制U和V方向
    public bool flipUV = false; // 添加UV翻转选项
    
    // 添加采样模式枚举定义
    public enum SamplingMode
    {
        UniformPerSegment, // 每段固定数量点
        UniformGlobal      // 全局等距分布点
    }

    [Header("Sampling Settings")]
    [Tooltip("选择采样点分布模式")]
    public SamplingMode samplingMode = SamplingMode.UniformGlobal;

    // 将原来的单一pointsPerSegment属性拆分成两个属性，配合不同的采样模式
    [Range(2, 50)]
    [Tooltip("每段曲线的采样点数，值越大曲线越平滑")]
    public int pointsPerSegment = 10; // UniformPerSegment模式下使用

    [Range(0.1f, 5f)]
    [Tooltip("采样密度调整因子，值越大采样点越密集")]
    public float densityFactor = 1f; // UniformGlobal模式下使用

    [Range(5, 100)]
    [Tooltip("全局模式下曲线总点数")]
    public int totalCurvePoints = 30; // UniformGlobal模式下使用的总点数

    [Header("Debug Visualization")]
    public bool showDebugVisuals = true;
    public Color leftCurveColor = Color.red;
    public Color rightCurveColor = Color.blue;
    public float debugPointSize = 0.1f;
    
    private Mesh mesh;
    private MeshFilter meshFilter;
    private List<Vector3> leftCurvePoints = new List<Vector3>();
    private List<Vector3> rightCurvePoints = new List<Vector3>();

    public void GenerateRoadMesh()
    {
        if (!ValidateComponents()) return;
        
        var controlPointsData = pathManager.GetControlPointsData();
        if (controlPointsData == null || controlPointsData.Length == 0)
        {
            Debug.LogWarning("没有可用的控制点数据");
            return;
        }

        GenerateExtraCurves(controlPointsData);
        GenerateMesh();
    }

    private void GenerateExtraCurves(RoadPathManager.ControlPointData[] controlPoints)
    {
        leftCurvePoints.Clear();
        rightCurvePoints.Clear();
        
        if (controlPoints.Length == 0)
        {
            Debug.LogWarning("无控制点数据，无法生成道路曲线");
            return;
        }
        
        // 同步采样模式与RoadPathManager
        if (pathManager != null)
        {
            // 如果有这些属性，则同步设置
            try
            {
                samplingMode = (SamplingMode)System.Enum.Parse(typeof(SamplingMode), 
                    pathManager.pathSamplingMode.ToString());
                densityFactor = pathManager.densityFactor;
            }
            catch (System.Exception) 
            {
                // 忽略错误，使用默认值
            }
        }
        
        // 生成左右侧控制点
        List<Vector3> leftControlPoints = new List<Vector3>();
        List<Vector3> rightControlPoints = new List<Vector3>();
        float halfWidth = roadWidth * 0.5f;
        
        // 获取父物体的世界坐标变换
        Transform parent = transform;
        
        foreach (var point in controlPoints)
        {
            // 将世界坐标转换为相对于当前对象的局部坐标
            Vector3 localPosition = parent.InverseTransformPoint(point.position);
            
            // 将世界方向向量转换为局部方向向量
            Vector3 localBitangent = parent.InverseTransformDirection(point.bitangent);
            
            // 计算基于局部坐标系的左右点位置
            leftControlPoints.Add(localPosition - localBitangent.normalized * halfWidth);
            rightControlPoints.Add(localPosition + localBitangent.normalized * halfWidth);
        }

        // 生成曲线（使用当前选择的采样模式）
        leftCurvePoints = GenerateCurveFromPoints(leftControlPoints);
        rightCurvePoints = GenerateCurveFromPoints(rightControlPoints);
        
        // 调试信息
        Debug.Log($"生成道路曲线: 模式={samplingMode}, 左侧点数={leftCurvePoints.Count}, 右侧点数={rightCurvePoints.Count}");
    }

    private List<Vector3> GenerateCurveFromPoints(List<Vector3> points)
    {
        if (points.Count < 2) return points; // 至少需要两个点才能形成一条线
    
        // 处理少于4个点的情况 - 直接返回线性插值点
        if (points.Count < 4)
        {
            return GenerateLinearPoints(points);
        }
    
        // 根据采样模式选择不同的曲线生成方法
        if (samplingMode == SamplingMode.UniformGlobal)
        {
            return GenerateGloballyUniformCurve(points);
        }
        else
        {
            return GenerateSegmentUniformCurve(points);
        }
    }

    // 新增全局均匀采样方法
    private List<Vector3> GenerateGloballyUniformCurve(List<Vector3> points)
    {
        // 步骤1: 先进行密集采样，计算累积长度
        List<Vector3> denseSamples = new List<Vector3>();
        List<float> cumulativeDistances = new List<float>();
        float totalLength = 0f;
        
        // 确保起点被添加
        denseSamples.Add(points[0]);
        cumulativeDistances.Add(0f);
        
        int segments = points.Count - 1;
        int densityMultiplier = Mathf.Max(5, Mathf.CeilToInt(densityFactor * 10)); // 根据密度因子调整采样密度
        
        // 对每段进行密集采样
        for (int i = 0; i < segments; i++)
        {
            // 获取段控制点
            Vector3 p0, p1, p2, p3;
            
            if (i == 0)
            {
                p0 = points[0] + (points[0] - points[1]);
                p1 = points[0];
                p2 = points[1];
                p3 = (points.Count > 2) ? points[2] : points[1] + (points[1] - points[0]);
            }
            else if (i == segments - 1)
            {
                p0 = points[i - 1];
                p1 = points[i];
                p2 = points[i + 1];
                p3 = points[i + 1] + (points[i + 1] - points[i]);
            }
            else
            {
                p0 = points[i - 1];
                p1 = points[i];
                p2 = points[i + 1];
                p3 = (i + 2 < points.Count) ? points[i + 2] : points[i + 1] + (points[i + 1] - points[i]);
            }
            
            // 计算该段应该采样的点数（段长度越长，采样越密集）
            float segmentLength = Vector3.Distance(p1, p2);
            int sampleCount = Mathf.Max(10, Mathf.CeilToInt(segmentLength * densityMultiplier));
            
            // 对当前段进行密集采样 - 跳过起点，因为它已经被添加
            Vector3 prevPoint = denseSamples[denseSamples.Count - 1];
            
            for (int j = 1; j <= sampleCount; j++) // 从1开始，跳过起点
            {
                float t = j / (float)sampleCount;
                Vector3 curPoint = CatmullRomPoint(t, p0, p1, p2, p3);
                
                float dist = Vector3.Distance(prevPoint, curPoint);
                totalLength += dist;
                denseSamples.Add(curPoint);
                cumulativeDistances.Add(totalLength);
                prevPoint = curPoint;
            }
        }
        
        // 步骤2: 根据总长度均匀分布采样点
        int targetPointCount = Mathf.Max(2, totalCurvePoints); // 使用指定的总点数
        List<Vector3> resultPoints = new List<Vector3>();
        
        // 确保起点被添加
        resultPoints.Add(points[0]);
        
        // 只在有足够的点时进行均匀分布
        if (denseSamples.Count > 1 && totalLength > 0)
        {
            // 在中间等距添加点
            for (int i = 1; i < targetPointCount - 1; i++)
            {
                float targetDistance = (i / (float)(targetPointCount - 1)) * totalLength;
                
                // 二分查找最接近的点
                int index = FindClosestDistanceIndex(cumulativeDistances, targetDistance);
                
                if (index >= 0 && index < denseSamples.Count - 1)
                {
                    // 计算插值比例
                    float previousDist = cumulativeDistances[index];
                    float nextDist = cumulativeDistances[index + 1];
                    float ratio = 0f;
                    
                    if (nextDist > previousDist) // 避免除零错误
                    {
                        ratio = (targetDistance - previousDist) / (nextDist - previousDist);
                    }
                    
                    // 线性插值获取精确位置
                    Vector3 point = Vector3.Lerp(
                        denseSamples[index],
                        denseSamples[index + 1],
                        ratio
                    );
                    
                    resultPoints.Add(point);
                }
            }
        }
        
        // 确保终点被添加，并且它就是原始控制点的终点
        if (!resultPoints.Contains(points[points.Count - 1]))
        {
            resultPoints.Add(points[points.Count - 1]);
        }
        
        return resultPoints;
    }

    // 添加每段均匀采样方法(原有方法重命名)
    private List<Vector3> GenerateSegmentUniformCurve(List<Vector3> points)
    {
        if (points.Count < 2) return new List<Vector3>(points);
        
        int actualPointCount = Mathf.Max(2, pointsPerSegment);
        int totalSegments = points.Count - 1;
        List<Vector3> curvePoints = new List<Vector3>();

        // 确保起点被添加
        curvePoints.Add(points[0]);
        
        // 遍历所有段
        for (int i = 0; i < totalSegments; i++)
        {
            // 获取当前段的四个控制点
            Vector3 p0, p1, p2, p3;
        
            if (i == 0) // 第一段
            {
                // 使用第一个点的镜像作为p0
                p0 = points[0] + (points[0] - points[1]);
                p1 = points[0];
                p2 = points[1];
                p3 = (points.Count > 2) ? points[2] : p2 + (p2 - p1);
            }
            else if (i == totalSegments - 1) // 最后一段
            {
                p0 = points[i - 1];
                p1 = points[i];
                p2 = points[i + 1];
                // 使用最后一个点的镜像作为p3
                p3 = points[i + 1] + (points[i + 1] - points[i]);
            }
            else // 中间段
            {
                p0 = points[i - 1];
                p1 = points[i];
                p2 = points[i + 1];
                p3 = points[i + 2];
            }
        
            // 对当前段进行采样(不包含起点，因为它已经被添加)
            for (int j = 1; j <= actualPointCount; j++) // 从1开始，跳过起点
            {
                float t = j / (float)actualPointCount;
                Vector3 point = CatmullRomPoint(t, p0, p1, p2, p3);
                
                // 如果是终点，用原始终点替代计算值
                if (i == totalSegments - 1 && j == actualPointCount)
                {
                    point = points[points.Count - 1]; // 确保使用原始控制点作为终点
                }
                
                curvePoints.Add(point);
            }
        }

        return curvePoints;
    }

    // 添加新方法：直线模式下的点生成
    // 修改线性点生成方法，确保所有控制点都在曲线上
    private List<Vector3> GenerateLinearPoints(List<Vector3> points)
    {
        List<Vector3> linearPoints = new List<Vector3>();
        
        // 特殊情况处理
        if (points.Count == 0) return linearPoints;
        if (points.Count == 1)
        {
            // 当只有一个点时，生成至少两个点以形成一条线段
            linearPoints.Add(points[0]);
            linearPoints.Add(points[0] + Vector3.forward * 0.01f); // 添加一个微小偏移的点
            return linearPoints;
        }
        
        // 添加第一个点
        linearPoints.Add(points[0]);
        
        // 为每段生成插值点
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 start = points[i];
            Vector3 end = points[i + 1];
            float distance = Vector3.Distance(start, end);
            
            // 计算该段上插值点的数量(根据两点距离动态调整)
            // 确保至少有4个点，避免网格生成问题
            int segmentPoints = Mathf.Max(4, Mathf.RoundToInt(distance * pointsPerSegment / 10.0f));
            
            // 生成均匀分布的点(排除起点，因为起点已添加)
            for (int j = 1; j < segmentPoints; j++)
            {
                float t = j / (float)(segmentPoints - 1);
                linearPoints.Add(Vector3.Lerp(start, end, t));
            }
            
            // 对于最后一段，添加终点
            if (i == points.Count - 2)
            {
                linearPoints.Add(end); // 确保终点被添加
            }
        }
        
        return linearPoints;
    }

    // 修改GenerateMesh方法，避免在OnValidate期间发送消息
    private void GenerateMesh()
    {
        // 检查曲线点的有效性
        if (leftCurvePoints == null || rightCurvePoints == null)
        {
            Debug.LogError("曲线点列表为空");
            return;
        }
        
        int leftCount = leftCurvePoints.Count;
        int rightCount = rightCurvePoints.Count;
        
        // 确保至少有2个点可以形成网格
        if (leftCount < 2 || rightCount < 2)
        {
            // 修复：如果有一个点，则创建第二个点
            if (leftCount == 1)
            {
                leftCurvePoints.Add(leftCurvePoints[0] + Vector3.forward * 0.01f);
                leftCount = 2;
            }
            if (rightCount == 1)
            {
                rightCurvePoints.Add(rightCurvePoints[0] + Vector3.forward * 0.01f);
                rightCount = 2;
            }
            
            // 如果仍然无法形成网格，则报错并返回
            if (leftCount < 2 || rightCount < 2)
            {
                Debug.LogError($"曲线点数量不足，无法形成网格: 左侧{leftCount}, 右侧{rightCount}");
                return;
            }
        }
        
        // 修复左右曲线点数量不匹配的问题
        if (leftCount != rightCount)
        {
            Debug.Log($"曲线点数量不匹配，将进行修正: 左侧{leftCount}, 右侧{rightCount}");
            EqualizeCurvePoints(ref leftCurvePoints, ref rightCurvePoints);
        }

        // 创建网格并生成顶面
        mesh = new Mesh();
        mesh.name = "RoadMesh";
        
        // 如果不需要厚度，生成简单平面
        if (roadDepth <= 0 || !useMultipleMaterials)
        {
            GenerateSimpleRoadMesh();
        }
        else
        {
            GenerateExtrudedRoadMesh();
        }
        
        // 后处理网格 - 应用到MeshFilter
        if (meshFilter != null)
        {
            bool previousMesh = meshFilter.sharedMesh != null;
            meshFilter.sharedMesh = mesh;
            
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(meshFilter);
                
                // 检查当前是否在验证阶段
                bool isValidating = UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || 
                                   Event.current != null && Event.current.type == EventType.ValidateCommand;
                
                if (!isValidating)
                {
                    // 只在非验证阶段标记场景为脏
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
            }
            #endif
            
            // 设置网格的材质
            ApplyMaterials();
        }
    }

    // 生成简单的平面路面(无厚度)
    private void GenerateSimpleRoadMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        
        float totalDistance = 0f;
        Vector3 previousLeft = leftCurvePoints[0];
        Vector3 previousRight = rightCurvePoints[0];
        
        // 使用统一后的点数进行遍历
        int pointsCount = leftCurvePoints.Count; // 此时左右点数应该相等
        
        for (int i = 0; i < pointsCount; i++)
        {
            // 添加顶点
            vertices.Add(rightCurvePoints[i]);
            vertices.Add(leftCurvePoints[i]);
            
            if (i > 0)
            {
                float segmentLength = (Vector3.Distance(leftCurvePoints[i], previousLeft) + 
                                     Vector3.Distance(rightCurvePoints[i], previousRight)) * 0.5f;
                totalDistance += segmentLength;
            }
            
            // UV坐标
            float u = flipUV ? 0 : 1;
            float v = totalDistance / roadWidth * uvRepeat.y;
            uvs.Add(new Vector2(u * uvRepeat.x, v));      // 右侧UV
            
            u = flipUV ? 1 : 0;
            uvs.Add(new Vector2(u * uvRepeat.x, v));      // 左侧UV
            
            if (i < pointsCount - 1)
            {
                int baseIndex = i * 2;
                // 三角形
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
                
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 3);
            }
            
            previousLeft = leftCurvePoints[i];
            previousRight = rightCurvePoints[i];
        }
        
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
    }

    // 生成带厚度的路面(多材质) - 修复侧面和底面法线方向
    private void GenerateExtrudedRoadMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        
        // 为不同材质的子网格准备三角形索引
        List<int> topTriangles = new List<int>();    // 顶面(第0个子网格)
        List<int> sideTriangles = new List<int>();   // 侧面(第1个子网格)
        List<int> bottomTriangles = new List<int>(); // 底面(第2个子网格)
        
        int pointsCount = leftCurvePoints.Count;
        float totalDistance = 0f;
        
        Vector3 previousLeft = leftCurvePoints[0];
        Vector3 previousRight = rightCurvePoints[0];
        
        // 在顶部添加左右点
        for (int i = 0; i < pointsCount; i++)
        {
            // 添加顶部顶点 - 右侧点和左侧点
            vertices.Add(rightCurvePoints[i]);         // 索引: i*4
            vertices.Add(leftCurvePoints[i]);          // 索引: i*4 + 1
            
            // 添加底部顶点 - 右侧点和左侧点(下移roadDepth)
            vertices.Add(rightCurvePoints[i] - Vector3.up * roadDepth);  // 索引: i*4 + 2
            vertices.Add(leftCurvePoints[i] - Vector3.up * roadDepth);   // 索引: i*4 + 3
            
            if (i > 0)
            {
                float segmentLength = (Vector3.Distance(leftCurvePoints[i], previousLeft) + 
                                     Vector3.Distance(rightCurvePoints[i], previousRight)) * 0.5f;
                totalDistance += segmentLength;
            }
            
            // 为顶面生成UV坐标
            float u = flipUV ? 0 : 1;
            float v = totalDistance / roadWidth * uvRepeat.y;
            
            uvs.Add(new Vector2(u * uvRepeat.x, v));        // 顶面右侧UV
            
            u = flipUV ? 1 : 0;
            uvs.Add(new Vector2(u * uvRepeat.x, v));        // 顶面左侧UV
            
            // 为底面和侧面生成UV坐标
            float sideU = 0;
            float sideV = totalDistance / roadWidth * uvRepeat.y;
            
            uvs.Add(new Vector2(sideU, sideV));             // 底面右侧UV
            uvs.Add(new Vector2(sideU + roadWidth / roadDepth * uvRepeat.x, sideV)); // 底面左侧UV
            
            // 生成三角形
            if (i < pointsCount - 1)
            {
                int baseIndex = i * 4;
                
                // 顶面三角形 - 这些是正确的，保持不变
                topTriangles.Add(baseIndex);
                topTriangles.Add(baseIndex + 1);
                topTriangles.Add(baseIndex + 4);
                
                topTriangles.Add(baseIndex + 4);
                topTriangles.Add(baseIndex + 1);
                topTriangles.Add(baseIndex + 5);
                
                // 右侧面三角形 - 修复顶点顺序使法线朝外
                sideTriangles.Add(baseIndex);      // 顶部右
                sideTriangles.Add(baseIndex + 4);  // 下一个顶部右
                sideTriangles.Add(baseIndex + 2);  // 底部右
                
                sideTriangles.Add(baseIndex + 4);  // 下一个顶部右
                sideTriangles.Add(baseIndex + 6);  // 下一个底部右
                sideTriangles.Add(baseIndex + 2);  // 底部右
                
                // 左侧面三角形 - 修复顶点顺序使法线朝外
                sideTriangles.Add(baseIndex + 1);  // 顶部左
                sideTriangles.Add(baseIndex + 3);  // 底部左
                sideTriangles.Add(baseIndex + 5);  // 下一个顶部左
                
                sideTriangles.Add(baseIndex + 3);  // 底部左
                sideTriangles.Add(baseIndex + 7);  // 下一个底部左
                sideTriangles.Add(baseIndex + 5);  // 下一个顶部左
                
                // 底面三角形 - 修复顶点顺序使法线朝下
                bottomTriangles.Add(baseIndex + 2);  // 底部右
                bottomTriangles.Add(baseIndex + 6);  // 下一个底部右
                bottomTriangles.Add(baseIndex + 3);  // 底部左
                
                bottomTriangles.Add(baseIndex + 6);  // 下一个底部右
                bottomTriangles.Add(baseIndex + 7);  // 下一个底部左
                bottomTriangles.Add(baseIndex + 3);  // 底部左
            }
            
            previousLeft = leftCurvePoints[i];
            previousRight = rightCurvePoints[i];
        }
        
        // 添加首尾封闭侧面 - 修复三角形顺序
        int lastIndex = (pointsCount - 1) * 4;
        
        // 起点封闭面(两个三角形) - 修复顺序
        sideTriangles.Add(0);   // 顶部右
        sideTriangles.Add(2);   // 底部右
        sideTriangles.Add(1);   // 顶部左
        
        sideTriangles.Add(2);   // 底部右
        sideTriangles.Add(3);   // 底部左
        sideTriangles.Add(1);   // 顶部左
        
        // 终点封闭面(两个三角形) - 修复顺序
        sideTriangles.Add(lastIndex);       // 顶部右
        sideTriangles.Add(lastIndex + 1);   // 顶部左
        sideTriangles.Add(lastIndex + 2);   // 底部右
        
        sideTriangles.Add(lastIndex + 1);   // 顶部左
        sideTriangles.Add(lastIndex + 3);   // 底部左
        sideTriangles.Add(lastIndex + 2);   // 底部右
        
        // 应用顶点和UV
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        
        // 设置子网格数量并分配三角形
        mesh.subMeshCount = 3;
        mesh.SetTriangles(topTriangles.ToArray(), 0);    // 顶面
        mesh.SetTriangles(sideTriangles.ToArray(), 1);   // 侧面
        mesh.SetTriangles(bottomTriangles.ToArray(), 2); // 底面
        
        // 计算基础法线
        mesh.RecalculateNormals();
        
        // 应用自定义法线平滑
        SmoothNormals();
        
        // 计算切线
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
    }

    // 添加法线平滑方法
    private void SmoothNormals()
    {
        // 获取计算好的基础法线
        Vector3[] normals = mesh.normals;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        
        // 创建位置到索引的映射
        Dictionary<Vector3, List<int>> vertexPositionToIndices = new Dictionary<Vector3, List<int>>(vertices.Length);
        
        // 使用位置找到所有共享相同位置的顶点索引
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 position = vertices[i];
            if (!vertexPositionToIndices.ContainsKey(position))
            {
                vertexPositionToIndices[position] = new List<int>();
            }
            vertexPositionToIndices[position].Add(i);
        }
        
        // 针对每个子网格分别平滑法线
        SmoothNormalsForSubmesh(0, normals, vertices); // 顶面
        SmoothNormalsForSubmesh(1, normals, vertices); // 侧面
        SmoothNormalsForSubmesh(2, normals, vertices); // 底面
        
        // 现在平滑连接处的法线
        SmoothEdgeNormals(normals, vertices, vertexPositionToIndices);
        
        // 应用平滑处理后的法线
        mesh.normals = normals;
    }

    // 针对特定子网格平滑法线
    private void SmoothNormalsForSubmesh(int submeshIndex, Vector3[] normals, Vector3[] vertices)
    {
        // 获取子网格的三角形
        int[] triangles = mesh.GetTriangles(submeshIndex);
        
        // 创建顶点组
        Dictionary<Vector3, List<int>> vertexGroups = new Dictionary<Vector3, List<int>>();
        
        // 根据位置将顶点分组
        for (int i = 0; i < triangles.Length; i++)
        {
            int vertexIndex = triangles[i];
            Vector3 position = vertices[vertexIndex];
            
            if (!vertexGroups.ContainsKey(position))
            {
                vertexGroups[position] = new List<int>();
            }
            
            if (!vertexGroups[position].Contains(vertexIndex))
            {
                vertexGroups[position].Add(vertexIndex);
            }
        }
        
        // 对每组顶点执行法线平均
        foreach (var group in vertexGroups)
        {
            if (group.Value.Count <= 1) continue;
            
            // 计算平均法线
            Vector3 averageNormal = Vector3.zero;
            foreach (int index in group.Value)
            {
                averageNormal += normals[index];
            }
            averageNormal.Normalize();
            
            // 应用平均法线
            foreach (int index in group.Value)
            {
                normals[index] = averageNormal;
            }
        }
    }

    // 平滑边缘法线
    private void SmoothEdgeNormals(Vector3[] normals, Vector3[] vertices, Dictionary<Vector3, List<int>> vertexPositionToIndices)
    {
        // 路面厚度的一半，用于识别顶部和底部的顶点
        float halfHeight = roadDepth * 0.5f;
        
        // 处理每个位置的顶点组
        foreach (var positionGroup in vertexPositionToIndices)
        {
            Vector3 position = positionGroup.Key;
            List<int> indices = positionGroup.Value;
            
            if (indices.Count <= 1) continue; // 没有共享顶点，跳过
            
            // 分离顶部和底部顶点
            List<int> topIndices = new List<int>();
            List<int> sideIndices = new List<int>();
            List<int> bottomIndices = new List<int>();
            
            foreach (int index in indices)
            {
                float yDiff = vertices[index].y - position.y;
                
                if (Mathf.Abs(yDiff) < 0.001f) // 顶部顶点
                {
                    topIndices.Add(index);
                }
                else if (Mathf.Abs(yDiff + roadDepth) < 0.001f) // 底部顶点
                {
                    bottomIndices.Add(index);
                }
                else // 侧面顶点
                {
                    sideIndices.Add(index);
                }
            }
            
            // 对不同部分的顶点分别平滑法线
            if (topIndices.Count > 1)
            {
                SmoothNormalsForIndices(topIndices, normals);
            }
            
            if (sideIndices.Count > 1)
            {
                SmoothNormalsForIndices(sideIndices, normals);
            }
            
            if (bottomIndices.Count > 1)
            {
                SmoothNormalsForIndices(bottomIndices, normals);
            }
            
            // 特别处理边缘处的法线平滑，将侧面和顶面/底面的法线适当融合
            if (topIndices.Count > 0 && sideIndices.Count > 0)
            {
                SmoothEdgeNormalsBetweenGroups(topIndices, sideIndices, normals, 0.5f);
            }
            
            if (bottomIndices.Count > 0 && sideIndices.Count > 0)
            {
                SmoothEdgeNormalsBetweenGroups(bottomIndices, sideIndices, normals, 0.5f);
            }
        }
    }

    // 对指定索引列表中的顶点进行法线平滑
    private void SmoothNormalsForIndices(List<int> indices, Vector3[] normals)
    {
        if (indices.Count <= 1) return;
        
        // 计算平均法线
        Vector3 averageNormal = Vector3.zero;
        foreach (int index in indices)
        {
            averageNormal += normals[index];
        }
        averageNormal.Normalize();
        
        // 应用平均法线
        foreach (int index in indices)
        {
            normals[index] = averageNormal;
        }
    }

    // 在两组顶点之间平滑法线
    private void SmoothEdgeNormalsBetweenGroups(List<int> group1, List<int> group2, Vector3[] normals, float blendFactor)
    {
        // 计算第一组的平均法线
        Vector3 avgNormal1 = Vector3.zero;
        foreach (int index in group1)
        {
            avgNormal1 += normals[index];
        }
        avgNormal1.Normalize();
        
        // 计算第二组的平均法线
        Vector3 avgNormal2 = Vector3.zero;
        foreach (int index in group2)
        {
            avgNormal2 += normals[index];
        }
        avgNormal2.Normalize();
        
        // 计算混合法线
        Vector3 blendedNormal = Vector3.Lerp(avgNormal1, avgNormal2, blendFactor).normalized;
        
        // 应用到边缘顶点
        // 这里可以根据需要只应用到真正的边缘顶点上，例如在侧面靠近顶部或底部的顶点
        foreach (int index in group2)
        {
            normals[index] = Vector3.Lerp(normals[index], blendedNormal, 0.3f).normalized;
        }
    }

    // 应用材质到网格
    private void ApplyMaterials()
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer == null) return;
        
        // 确保至少有一个默认材质
        if (topMaterial == null) 
            topMaterial = renderer.sharedMaterial;
        
        if (useMultipleMaterials && roadDepth > 0)
        {
            // 准备材质数组
            Material[] materials = new Material[3];
            materials[0] = topMaterial;
            materials[1] = sideMaterial != null ? sideMaterial : topMaterial;
            materials[2] = bottomMaterial != null ? bottomMaterial : topMaterial;
            
            // 应用材质数组
            renderer.sharedMaterials = materials;
            
            // 设置接收阴影
            renderer.receiveShadows = true;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        else
        {
            // 只使用顶面材质
            renderer.sharedMaterial = topMaterial;
        }
    }

    // 添加新方法：确保左右两侧曲线点数量相等
    private void EqualizeCurvePoints(ref List<Vector3> leftPoints, ref List<Vector3> rightPoints)
    {
        int leftCount = leftPoints.Count;
        int rightCount = rightPoints.Count;
        
        // 如果两侧点数相等，不需要处理
        if (leftCount == rightCount) return;
        
        // 确定目标点数为较大的点数，以保留更多细节
        int targetCount = Mathf.Max(leftCount, rightCount);
        
        // 重新采样点数较少的曲线
        if (leftCount < rightCount)
        {
            leftPoints = ResampleCurve(leftPoints, targetCount);
            Debug.Log($"重新采样左侧曲线，从 {leftCount} 点到 {targetCount} 点");
        }
        else
        {
            rightPoints = ResampleCurve(rightPoints, targetCount);
            Debug.Log($"重新采样右侧曲线，从 {rightCount} 点到 {targetCount} 点");
        }
    }

    // 添加新方法：根据目标点数重新采样曲线
    private List<Vector3> ResampleCurve(List<Vector3> points, int targetCount)
    {
        // 如果点数太少，无法进行插值，则直接返回复制后的点列表
        if (points.Count < 2)
        {
            List<Vector3> result = new List<Vector3>();
            for (int i = 0; i < targetCount; i++)
            {
                if (points.Count > 0)
                    result.Add(points[0]);
                else
                    result.Add(Vector3.zero);
            }
            return result;
        }
        
        List<Vector3> newPoints = new List<Vector3>();
        
        // 计算原始曲线的总长度
        float totalLength = 0;
        for (int i = 0; i < points.Count - 1; i++)
        {
            totalLength += Vector3.Distance(points[i], points[i + 1]);
        }
        
        // 添加起点
        newPoints.Add(points[0]);
        
        // 基于均匀距离进行采样
        float segmentLength = totalLength / (targetCount - 1);
        float currentDistance = 0;
        int currentPointIndex = 0;
        
        for (int i = 1; i < targetCount - 1; i++)
        {
            float targetDistance = i * segmentLength;
            
            // 找到目标距离所在的点索引
            while (currentDistance + Vector3.Distance(points[currentPointIndex], points[currentPointIndex + 1]) < targetDistance 
                   && currentPointIndex < points.Count - 2)
            {
                currentDistance += Vector3.Distance(points[currentPointIndex], points[currentPointIndex + 1]);
                currentPointIndex++;
            }
            
            // 计算当前段内的插值参数
            float segmentDistance = Vector3.Distance(points[currentPointIndex], points[currentPointIndex + 1]);
            if (segmentDistance > 0)
            {
                float t = (targetDistance - currentDistance) / segmentDistance;
                newPoints.Add(Vector3.Lerp(points[currentPointIndex], points[currentPointIndex + 1], t));
            }
            else
            {
                // 如果段长度为0，直接使用当前点
                newPoints.Add(points[currentPointIndex]);
            }
        }
        
        // 添加终点
        newPoints.Add(points[points.Count - 1]);
        
        return newPoints;
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

    // 二分查找辅助方法
    private int FindClosestDistanceIndex(List<float> distances, float target)
    {
        if (distances == null || distances.Count == 0) return -1;
        if (target <= distances[0]) return 0;
        if (target >= distances[distances.Count - 1]) return distances.Count - 1;
        
        int low = 0;
        int high = distances.Count - 1;
        
        while (low <= high)
        {
            int mid = (low + high) / 2;
            
            if (distances[mid] == target)
            {
                return mid;
            }
            else if (distances[mid] < target)
            {
                if (mid < distances.Count - 1 && distances[mid + 1] > target)
                {
                    return mid;
                }
                low = mid + 1;
            }
            else
            {
                if (mid > 0 && distances[mid - 1] < target)
                {
                    return mid - 1;
                }
                high = mid - 1;
            }
        }
        
        return low;
    }

    // Debug visualization
    void OnDrawGizmos()
    {
        if (!showDebugVisuals || pathManager == null) return;
        
        var controlPoints = pathManager.GetControlPointsData();
        if (controlPoints == null) return;

        DrawControlPointsDebug(controlPoints);
        DrawCurvesDebug();
    }

    private void DrawControlPointsDebug(RoadPathManager.ControlPointData[] points)
    {
        float halfWidth = roadWidth * 0.5f;
        Transform parent = transform;
        
        // 绘制控制点的扩展位置
        for (int i = 0; i < points.Length; i++)
        {
            var pointData = points[i];
            
            // 将世界坐标转换为局部坐标
            Vector3 localPosition = parent.InverseTransformPoint(pointData.position);
            Vector3 localBitangent = parent.InverseTransformDirection(pointData.bitangent);
            Vector3 localTangent = parent.InverseTransformDirection(pointData.tangent);
            Vector3 localUp = parent.InverseTransformDirection(pointData.up);
            
            // 使用准确的bitangent计算扩展位置
            Vector3 leftPoint = localPosition - localBitangent.normalized * halfWidth;
            Vector3 rightPoint = localPosition + localBitangent.normalized * halfWidth;
            
            // 将局部坐标转换回世界坐标用于绘制
            Vector3 worldLeftPoint = parent.TransformPoint(leftPoint);
            Vector3 worldRightPoint = parent.TransformPoint(rightPoint);
            Vector3 worldPosition = parent.TransformPoint(localPosition);
            
            // 绘制扩展点和连接线
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(worldLeftPoint, debugPointSize);
            Gizmos.DrawWireSphere(worldRightPoint, debugPointSize);
            Gizmos.DrawLine(worldLeftPoint, worldRightPoint);
            
            // 绘制方向指示器
            float dirLength = debugPointSize * 2;
            Gizmos.color = Color.blue;  // 切线
            Gizmos.DrawRay(worldPosition, parent.TransformDirection(localTangent) * dirLength);
            Gizmos.color = Color.green; // 法线
            Gizmos.DrawRay(worldPosition, parent.TransformDirection(localUp) * dirLength);
            Gizmos.color = Color.red;   // 副切线
            Gizmos.DrawRay(worldPosition, parent.TransformDirection(localBitangent) * dirLength);
        }
    }

    private void DrawCurvesDebug()
    {
        // 左侧曲线
        Gizmos.color = leftCurveColor;
        DrawCurvePoints(leftCurvePoints);
        
        // 右侧曲线
        Gizmos.color = rightCurveColor;
        DrawCurvePoints(rightCurvePoints);
    }

    private void DrawCurvePoints(List<Vector3> points)
    {
        if (points == null || points.Count < 2) return;
        Transform parent = transform;
        
        for (int i = 0; i < points.Count - 1; i++)
        {
            // 将局部坐标转换为世界坐标进行绘制
            Vector3 worldPos1 = parent.TransformPoint(points[i]);
            Vector3 worldPos2 = parent.TransformPoint(points[i + 1]);
            
            Gizmos.DrawLine(worldPos1, worldPos2);
            Gizmos.DrawWireSphere(worldPos1, debugPointSize * 0.5f);
        }
        
        // 绘制最后一个点
        Gizmos.DrawWireSphere(parent.TransformPoint(points[points.Count - 1]), debugPointSize * 0.5f);
    }

    // Component validation
    private bool ValidateComponents()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                Debug.LogError("缺少MeshFilter组件");
                return false;
            }
        }

        if (pathManager == null)
        {
            pathManager = GetComponent<RoadPathManager>();
            if (pathManager == null)
            {
                Debug.LogError("缺少RoadPathManager组件");
                return false;
            }
        }
        else
        {
            // 确保路径管理器知道有监听器在使用它
            pathManager.onPathChanged += OnPathChanged;
        }

        return true;
    }

    void OnEnable()
    {
        ValidateComponents();
        if (pathManager != null)
        {
            pathManager.onPathChanged += OnPathChanged;  // 订阅路径变化事件
        }
    }

    void OnDisable()
    {
        if (pathManager != null)
        {
            pathManager.onPathChanged -= OnPathChanged;  // 取消订阅
        }
    }

    // 修改OnPathChanged方法，避免在验证阶段生成网格
    private void OnPathChanged()
    {
        if (autoUpdate)
        {
            #if UNITY_EDITOR
            if (Application.isPlaying || !IsInValidationCallback())
            {
                // 直接生成网格
                GenerateRoadMesh();
            }
            else
            {
                // 在验证阶段，延迟生成网格
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null && enabled) // 检查对象是否仍然存在
                    {
                        GenerateRoadMesh();
                    }
                };
            }
            #else
            GenerateRoadMesh();
            #endif
        }
    }

    // 添加一个辅助方法来判断当前是否处于OnValidate等回调中
    private bool IsInValidationCallback()
    {
        System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace();
        for (int i = 0; i < stackTrace.FrameCount; i++)
        {
            var method = stackTrace.GetFrame(i).GetMethod();
            if (method.Name == "OnValidate" || method.Name == "Awake" || method.Name == "CheckConsistency")
                return true;
        }
        return false;
    }
}
