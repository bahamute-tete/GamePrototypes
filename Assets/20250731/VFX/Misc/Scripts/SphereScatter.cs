using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class SphereScatter : MonoBehaviour
{
    [Header("物体设置")]
    public GameObject objectCopyToPoints;
    public List<SphereCollider> sphereColliders;
    public float sphereRadius = 1f;
    public int pointCount = 50;

    [Space]
    [Header("旋转设置")]
    [Range(0f, 360f)]
    public float minZRotation = 0f;
    [Range(0f, 360f)]
    public float maxZRotation = 360f;

    [Space]
    [Header("生成控制")]
    public bool generateOnStart = true;
    public bool clearExisting = true;

    //points will be relaxed, pushed away from each other, to avoid clumping.
    //This is done gradually, to avoid chaotic behavior and to allow control over how much relaxation takes place.
    //More relaxation iterations results in points that are more separated from each other.
    //A distribution of points in which they are well separated is often called blue noise.
    public int relaxIterations = 2;

    [Space]
    [Header("运动设置")]
    public bool enableMovement = false;
    public float minSpeed = 1f;
    public float maxSpeed = 5f;


    [Header("光照面板设置")]
    public GameObject lightPannelPrefab;
    private List<GameObject> lightPannels = new List<GameObject>();
    public float groundBias = 0.1f;
    public Vector4 groundPlane = new Vector4(0, 1, 0, 0);
    [Space]
    [Header("缩放设置")]
    public float maxScale = 0.1f;
    public float minScale = 0.05f;
    public float maxHeight = 10f; // 最大高度，超过此高度缩放为最小值



    [Space]
    public string vfxPropertyName = "sdfTexture";
    public List<Texture3D> collisionTextures = new List<Texture3D>();

    [Space]
    public string vfxPropertyName2 = "timeShift";
    

    private List<Vector3> scatterPoints = new List<Vector3>();
    private List<GameObject> instantiatedObjects = new List<GameObject>();
    private List<Vector3> velocities = new List<Vector3>();

    // Start is called before the first frame update
    void Start()
    {
        if (generateOnStart)
        {
            GenerateScatter();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (enableMovement && scatterPoints.Count > 0)
        {
            UpdatePointMovement();
        }
    }


    public void GenerateScatter()
    {
        if (objectCopyToPoints == null)
        {
            Debug.LogWarning("请指定要复制的物体！");
            return;
        }

        if (clearExisting)
        {
            ClearExistingObjects();
        }

        // 生成球面上的点
        GenerateSpherePoints();

        // 松弛算法优化点分布
        RelaxPoints();

        // 实例化物体
        InstantiateObjects();
    }


    private void UpdateLightPannelPosition(GameObject lightPannel,Transform source)
    {
        if (lightPannel == null) return;


        Vector3 groundNormal = new Vector3(groundPlane.x, groundPlane.y, groundPlane.z).normalized;


        float distanceToGround = Vector3.Dot(source.position, groundNormal) + groundPlane.w;


        Vector3 projectedPosition = source.position - groundNormal * distanceToGround;


        Vector3 finalPosition = projectedPosition + groundNormal * groundBias;


        lightPannel.transform.position = finalPosition;


        float heightRatio = Mathf.Clamp01(Mathf.Abs(distanceToGround) / maxHeight);
        float scale = Mathf.Lerp(maxScale, minScale, heightRatio);


        Vector3 currentScale = lightPannel.transform.localScale;
        lightPannel.transform.localScale = new Vector3(scale, currentScale.y, scale);
    }

    private void GenerateSpherePoints()
    {
        scatterPoints.Clear();
        velocities.Clear();

        for (int i = 0; i < pointCount; i++)
        {
            // 使用Fibonacci球面分布算法，但限制在上半球
            float y = (i / (float)(pointCount - 1)); // y goes from 0 to 1 (上半球)
            float radius = Mathf.Sqrt(1 - y * y);

            float theta = Mathf.PI * (3.0f - Mathf.Sqrt(5.0f)) * i; // golden angle increment

            float x = Mathf.Cos(theta) * radius;
            float z = Mathf.Sin(theta) * radius;

            Vector3 point = new Vector3(x, y, z) * sphereRadius + transform.position;
            scatterPoints.Add(point);

            // 生成球面切线方向的随机初速度
            Vector3 radialDirection = (point - transform.position).normalized;
            Vector3 tangentDirection = GenerateRandomTangent(radialDirection);
            float speed = Random.Range(minSpeed, maxSpeed);
            velocities.Add(tangentDirection * speed);
        }
    }

    private Vector3 GenerateRandomTangent(Vector3 normal)
    {
        // 生成一个与法向量垂直的随机切线向量
        Vector3 randomVector = Random.onUnitSphere;
        Vector3 tangent = Vector3.Cross(normal, randomVector);
        
        // 如果叉积结果太小，重新生成
        if (tangent.magnitude < 0.1f)
        {
            randomVector = Random.onUnitSphere;
            tangent = Vector3.Cross(normal, randomVector);
        }
        
        return tangent.normalized;
    }

    private void UpdatePointMovement()
    {
        if (scatterPoints.Count == 0) return;

        // 检测物体间碰撞
        CheckCollisions();

        for (int i = 0; i < scatterPoints.Count; i++)
        {
           
            Vector3 newPosition = scatterPoints[i] + velocities[i] * Time.deltaTime;
            
            Vector3 directionFromCenter = (newPosition - transform.position).normalized;
            Vector3 projectedPosition = transform.position + directionFromCenter * sphereRadius;
            
            if (projectedPosition.y < transform.position.y)
            {
               
                Vector3 groundNormal = Vector3.up; 
                Vector3 currentVelocity = velocities[i];
                

                Vector3 reflectedVelocity = currentVelocity - 2 * Vector3.Dot(currentVelocity, groundNormal) * groundNormal;
                velocities[i] = reflectedVelocity;
                
                // 将点约束在半球边界上
                Vector3 constrainedDirection = new Vector3(directionFromCenter.x, 0, directionFromCenter.z).normalized;
                scatterPoints[i] = transform.position + constrainedDirection * sphereRadius;
            }
            else
            {
                // 正常移动，更新位置
                scatterPoints[i] = projectedPosition;
                
                // 更新速度方向，保持切线运动
                Vector3 radialDirection = (scatterPoints[i] - transform.position).normalized;
                Vector3 currentVelocity = velocities[i];
                
                // 移除速度的径向分量，保持切线运动
                Vector3 tangentialVelocity = currentVelocity - Vector3.Dot(currentVelocity, radialDirection) * radialDirection;
                velocities[i] = tangentialVelocity;
            }
            
            // 更新实例化物体的位置和旋转
            if (i < instantiatedObjects.Count && instantiatedObjects[i] != null)
            {
                instantiatedObjects[i].transform.position = scatterPoints[i];

                //UpdateLightPannelPosition(lightPannels[i], instantiatedObjects[i].transform);
                
                // 重新计算旋转
                Vector3 directionToCenter = (transform.position - scatterPoints[i]).normalized;
                Quaternion lookRotation = Quaternion.LookRotation(directionToCenter);
                instantiatedObjects[i].transform.rotation = lookRotation;

            }
        }
    }

    private void CheckCollisions()
    {
        for (int i = 0; i < scatterPoints.Count; i++)
        {
            for (int j = i + 1; j < scatterPoints.Count; j++)
            {
                if (i >= sphereColliders.Count || j >= sphereColliders.Count) continue;
                if (sphereColliders[i] == null || sphereColliders[j] == null) continue;

                // 计算两个碰撞体之间的距离
                float distance = Vector3.Distance(scatterPoints[i], scatterPoints[j]);
                float collisionDistance = sphereColliders[i].radius + sphereColliders[j].radius;

                // 检测碰撞
                if (distance < collisionDistance)
                {
                    // 计算碰撞方向
                    Vector3 collisionDirection = (scatterPoints[j] - scatterPoints[i]).normalized;
                    
                    // 分离重叠的物体
                    float overlap = collisionDistance - distance;
                    Vector3 separation = collisionDirection * (overlap * 0.5f);
                    
                    scatterPoints[i] -= separation;
                    scatterPoints[j] += separation;
                    
                    // 重新投影到球面
                    Vector3 directionFromCenter_i = (scatterPoints[i] - transform.position).normalized;
                    Vector3 directionFromCenter_j = (scatterPoints[j] - transform.position).normalized;
                    scatterPoints[i] = transform.position + directionFromCenter_i * sphereRadius;
                    scatterPoints[j] = transform.position + directionFromCenter_j * sphereRadius;
                    

                    Vector3 velocity1 = velocities[i];
                    Vector3 velocity2 = velocities[j];
                    
                    // 在碰撞方向上交换速度分量
                    float v1_collision = Vector3.Dot(velocity1, collisionDirection);
                    float v2_collision = Vector3.Dot(velocity2, collisionDirection);
                    
                    velocities[i] = velocity1 - v1_collision * collisionDirection + v2_collision * collisionDirection;
                    velocities[j] = velocity2 - v2_collision * collisionDirection + v1_collision * collisionDirection;
                    
                    // 触发VFX效果变化
                    TriggerCollisionVFX(i);
                    TriggerCollisionVFX(j);
                }
            }
        }
    }

    private void TriggerCollisionVFX(int objectIndex)
    {
        if (objectIndex >= instantiatedObjects.Count || instantiatedObjects[objectIndex] == null) return;
        if (collisionTextures.Count == 0) return;

        VFXPropertyChange vfxComponent = instantiatedObjects[objectIndex].GetComponent<VFXPropertyChange>();
        if (vfxComponent == null)
            vfxComponent = instantiatedObjects[objectIndex].GetComponentInChildren<VFXPropertyChange>();

        if (vfxComponent != null)
        {
            int randomIndex = Random.Range(0, collisionTextures.Count);
            vfxComponent.SetTexture3DProperty(vfxPropertyName, collisionTextures[randomIndex]);
        }
    }

    private void InitialTimeShiftFotVFX(GameObject obj)
    {

        VFXPropertyChange vfxComponent = obj.GetComponent<VFXPropertyChange>();

        if (vfxComponent != null)
        {
           
            vfxComponent.SetTimeShiftProperty(vfxPropertyName2, Random.Range(0f,100.0f));
        }
    }

    private void RelaxPoints()
    {
        for (int iteration = 0; iteration < relaxIterations; iteration++)
        {
            for (int i = 0; i < scatterPoints.Count; i++)
            {
                Vector3 repulsion = Vector3.zero;
                Vector3 currentPoint = scatterPoints[i];

                // 计算与其他点的排斥力
                for (int j = 0; j < scatterPoints.Count; j++)
                {
                    if (i == j) continue;

                    Vector3 direction = currentPoint - scatterPoints[j];
                    float distance = direction.magnitude;

                    if (distance > 0)
                    {
                        repulsion += direction.normalized / (distance * distance);
                    }
                }

                // 应用排斥力并重新投影到半球面
                Vector3 newPosition = currentPoint + repulsion * 0.1f;
                Vector3 directionFromCenter = (newPosition - transform.position).normalized;
                Vector3 projectedPosition = transform.position + directionFromCenter * sphereRadius;
                
                // 确保点保持在上半球
                if (projectedPosition.y >= transform.position.y)
                {
                    scatterPoints[i] = projectedPosition;
                }
                else
                {
                    // 如果松弛后的点跑到下半球，将其约束在边界上
                    Vector3 constrainedDirection = new Vector3(directionFromCenter.x, 0, directionFromCenter.z).normalized;
                    scatterPoints[i] = transform.position + constrainedDirection * sphereRadius;
                }
            }
        }
    }

    private void InstantiateObjects()
    {
        foreach (Vector3 point in scatterPoints)
        {
            GameObject instance = Instantiate(objectCopyToPoints, point, Quaternion.identity, transform);

            if (!instance.activeSelf)
                instance.SetActive(true);
            
           
            Vector3 directionToCenter = (transform.position - point).normalized;

          
            Quaternion lookRotation = Quaternion.LookRotation(directionToCenter);
            
           
            float randomZRotation = Random.Range(minZRotation, maxZRotation);
            Quaternion zAxisRotation = Quaternion.AngleAxis(randomZRotation, Vector3.forward);
            
           
            instance.transform.rotation = lookRotation * zAxisRotation;

            instantiatedObjects.Add(instance);

            SphereCollider sc = instance.GetComponentInChildren<SphereCollider>();
            if (sc == null)
                sc = instance.GetComponent<SphereCollider>();
            sphereColliders.Add(sc);

            //GameObject lightPannel = Instantiate(lightPannelPrefab,instance.transform.position, Quaternion.identity,transform);
            //lightPannels.Add(lightPannel);

            InitialTimeShiftFotVFX(instance);
        }
    }

    private void ClearExistingObjects()
    {
        foreach (GameObject obj in instantiatedObjects)
        {
            if (obj != null)
            {
                if (Application.isPlaying)
                    Destroy(obj);
                else
                    DestroyImmediate(obj);
            }
        }

        foreach (GameObject obj in lightPannels)
        {
            if (obj != null)
            {
                if (Application.isPlaying)
                    Destroy(obj);
                else
                    DestroyImmediate(obj);
            }
        }
        instantiatedObjects.Clear();
        sphereColliders.Clear();
        lightPannels.Clear();
    }

    public void ClearScatter()
    {
        ClearExistingObjects();
        scatterPoints.Clear();
    }

    private void OnDrawGizmosSelected()
    {
       
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sphereRadius);

      
        if (scatterPoints != null && scatterPoints.Count > 0)
        {
            Gizmos.color = Color.red;
            foreach (Vector3 point in scatterPoints)
            {
                Gizmos.DrawSphere(point, 0.05f);

                
                Vector3 directionToCenter = (transform.position - point).normalized;
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(point, directionToCenter * 0.3f);
                Gizmos.color = Color.red;
            }
        }
    }
}
