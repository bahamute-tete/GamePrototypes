using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[System.Serializable]
public class ObjectCategory
{
    public string categoryName; // 分类名称
    public string indexList; // 包含的物体编号列表，用逗号分隔
    [HideInInspector]
    public List<int> parsedIndices; // 解析后的索引列表
    
    // 物体的材质颜色，用于可视化区分不同分类
    public Color categoryColor = Color.white;
    
    // 解析索引字符串
    public void ParseIndices()
    {
        parsedIndices = new List<int>();
        if (string.IsNullOrEmpty(indexList))
            return;
            
        string[] indexStrings = indexList.Split(',');
        foreach (var indexStr in indexStrings)
        {
            if (int.TryParse(indexStr.Trim(), out int index))
            {
                parsedIndices.Add(index);
            }
        }
    }
    
    // 检查是否包含指定的一维索引
    public bool ContainsIndex(int index)
    {
        return parsedIndices != null && parsedIndices.Contains(index);
    }
}

[RequireComponent(typeof(BoxCollider))]
public class InteractionMoveableTypePrintig : MonoBehaviour
{
    [Header("生成设置")]
    [SerializeField] private GameObject objectPrefab; 
    [SerializeField] private int horizontalCount = 5; 
    [SerializeField] private int verticalCount = 5;   
    [SerializeField] private float gap = 0.1f;        // 物体之间的间距
    [SerializeField] private float offset = 0.1f;     // Z轴随机偏移的最大值

    [Header("物体分类")]
    [SerializeField] private List<ObjectCategory> objectCategories = new List<ObjectCategory>();
    // 移除可视化开关，转移到Editor中
    
    [Header("纹理设置")]
    static private int texIndexID=Shader.PropertyToID("_TexIndex");
    [SerializeField] private Vector2Int texIndexRange = new Vector2Int(8, 8); // 纹理索引范围(_TexIndex.zw)
    [SerializeField] private bool useRandomTexIndexXY = true; // 是否使用随机纹理索引
    [SerializeField] private Vector2Int defaultTexIndexXY = new Vector2Int(0, 0); 
    [SerializeField] private int maxAttempts = 10; // 尝试寻找不重复纹理的最大次数

    private Vector3 objectSize;                       // 物体的尺寸
    private List<GameObject> generatedObjects = new List<GameObject>(); // 存储生成的所有物体
    
    // 存储物体位置和纹理索引的字典
    private Dictionary<Vector2Int, Vector2Int> objectPositionToTexIndex = new Dictionary<Vector2Int, Vector2Int>();

    // Start is called before the first frame update
    void Start()
    {
        // 解析所有分类中的索引
        foreach (var category in objectCategories)
        {
            category.ParseIndices();
        }

        // 检查是否已存在生成的物体
        if (transform.childCount > 0)
        {

            generatedObjects.Clear();
            foreach (Transform child in transform)
            {
                GameObject obj = child.gameObject;
                GridPositionHolder posHolder = obj.GetComponent<GridPositionHolder>();
                if (posHolder != null)
                {
                    generatedObjects.Add(obj);
                    Vector2 indexXY = new Vector2(posHolder.textureLineraIndex % texIndexRange.x, 
                                            posHolder.textureLineraIndex / texIndexRange.x);

                    Renderer renderer = obj.GetComponent<Renderer>();
                    Material[] materials = renderer.sharedMaterials;
                    int textureCoordIndex = 0;
                    if (renderer != null && materials != null)
                        {
                            foreach (var m in materials)
                            {
                                if (m.shader.name.Contains("Parallax_Occlusion_Mapping_TileIndex"))
                                {
                                MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

                                renderer.GetPropertyBlock(propBlock);
                                // 应用属性
                                Vector4 texIndex = new Vector4(indexXY.x, indexXY.y, texIndexRange.x, texIndexRange.y);
                                propBlock.SetVector(texIndexID, texIndex);
                                renderer.SetPropertyBlock(propBlock);
                                textureCoordIndex = posHolder.GetCurrentObjectTextureIndex(propBlock);
                                }
                            }
                        }
                    AssignObjectToCategory(obj, textureCoordIndex);
                }

            }

            return;
        }
        

        if (objectPrefab != null)
        {
            GetObjectSize();
            GenerateObjectGrid();
            ApplyTextureIndices();
        }
        else
        {
            Debug.LogError("未设置物体预制体！请在Inspector中设置objectPrefab。");
        }
    }


    // 获取物体的尺寸，基于BoxCollider
    private void GetObjectSize()
    {
        BoxCollider boxCollider = objectPrefab.GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            objectSize = Vector3.Scale(boxCollider.size, objectPrefab.transform.localScale);
            //Debug.Log($"物体尺寸: {objectSize}");
        }
        else
        {
            //Debug.LogWarning("预制体上没有BoxCollider组件，将使用默认尺寸(1,1,1)");
            objectSize = Vector3.one;
        }
    }

    // 按网格生成物体
    private void GenerateObjectGrid()
    {
        ClearGeneratedObjects();
        objectPositionToTexIndex.Clear(); // 清除之前的纹理索引记录

        // 计算起始位置（使整个网格居中）
        float startX = -(horizontalCount - 1) * (objectSize.z + gap) / 2;
        float startY = -(verticalCount - 1) * (objectSize.y + gap) / 2;

        for (int y = 0; y < verticalCount; y++)
        {
            for (int x = 0; x < horizontalCount; x++)
            {
                // 计算当前物体的位置
                float posX = startX + x * (objectSize.z + gap);
                float posY = startY + y * (objectSize.y + gap);
                // 为Z轴添加随机偏移
                float posZ = Random.Range(0f, offset);
                Vector3 localPosition = new Vector3(posX, posY, posZ);

                // 实例化物体，保持在父对象的局部坐标系中
                GameObject newObject = Instantiate(objectPrefab, transform);
                newObject.transform.localPosition = localPosition; // 设置局部位置
                newObject.name = $"MoveableTypePrinting_{y}_{x}";

                // 记录物体在网格中的位置索引
                Vector2Int gridPos = new Vector2Int(x, y);
                
                // 检查是否已经有 GridPositionHolder 组件
                var gridPosHolder = newObject.GetComponent<GridPositionHolder>();
                if (gridPosHolder == null)
                {
                    // 如果没有，则添加
                    gridPosHolder = newObject.AddComponent<GridPositionHolder>();
                }
                
                gridPosHolder.SetGridPosition(gridPos, horizontalCount);

                generatedObjects.Add(newObject);
            }
        }

        //Debug.Log($"已生成 {generatedObjects.Count} 个物体");
    }
    
    // 将物体分配到对应的分类
    private void AssignObjectToCategory(GameObject obj, int linearIndex)
    {
       
        GridPositionHolder posHolder = obj.GetComponent<GridPositionHolder>();
        if (posHolder == null)
            return;
            
        foreach (var category in objectCategories)
        {
            if (category.ContainsIndex(linearIndex))
            {
                posHolder.categoryName = category.categoryName;
                posHolder.categoryColor = category.categoryColor; // 存储分类颜色，用于Gizmo显示
                
               // Debug.Log($"物体 {obj.name} (索引: {linearIndex}) 被分类到: {category.categoryName}");
                break;
            }
        }
    }
    
    // 获取所有属于指定分类的物体
    public List<GameObject> GetObjectsInCategory(string categoryName)
    {
        List<GameObject> result = new List<GameObject>();
        
        foreach (var obj in generatedObjects)
        {
            if (obj == null)
                continue;
                
            GridPositionHolder posHolder = obj.GetComponent<GridPositionHolder>();
            if (posHolder != null && posHolder.categoryName == categoryName)
            {
                result.Add(obj);
            }
        }
        
        return result;
    }

    // 获取指定网格位置的四邻域位置
    private List<Vector2Int> GetNeighbors(Vector2Int position)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();
        
        // 上下左右四个邻域
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(0, 1),  // 上
            new Vector2Int(0, -1), // 下
            new Vector2Int(-1, 0), // 左
            new Vector2Int(1, 0)   // 右
        };
        
        foreach (Vector2Int dir in directions)
        {
            Vector2Int neighborPos = position + dir;
            // 检查边界
            if (neighborPos.x >= 0 && neighborPos.x < horizontalCount && 
                neighborPos.y >= 0 && neighborPos.y < verticalCount)
            {
                neighbors.Add(neighborPos);
            }
        }
        
        return neighbors;
    }
    
    // 检查纹理索引是否与邻域冲突
    private bool IsTexIndexConflict(Vector2Int position, Vector2Int texIndex)
    {
        List<Vector2Int> neighbors = GetNeighbors(position);
        
        foreach (Vector2Int neighbor in neighbors)
        {
            if (objectPositionToTexIndex.TryGetValue(neighbor, out Vector2Int neighborTexIndex))
            {
                if (neighborTexIndex == texIndex)
                {
                    return true; // 存在冲突
                }
            }
        }
        
        return false; // 无冲突
    }
    
    // 为某个位置生成不冲突的纹理索引
    private Vector2Int GenerateNonConflictingTexIndex(Vector2Int position)
    {
        if (!useRandomTexIndexXY)
        {
            return defaultTexIndexXY;
        }
        
        Vector2Int texIndex;
        int attempts = 0;
        
        do
        {
            // 生成随机纹理索引
            texIndex = new Vector2Int(
                Random.Range(0, texIndexRange.x),
                Random.Range(0, texIndexRange.y)
            );
            
            attempts++;
            
            if (attempts >= maxAttempts)
            {
                Debug.LogWarning($"在位置 {position} 无法找到不冲突的纹理索引，使用可能冲突的索引");
                break;
            }
        }
        while (IsTexIndexConflict(position, texIndex));
        
        return texIndex;
    }


    public void ApplyCurrentTextureIndices()
    {
        foreach (GameObject obj in generatedObjects)
        {
            if (obj != null)
            {
                // 获取网格位置
                GridPositionHolder posHolder = obj.GetComponent<GridPositionHolder>();
                if (posHolder == null)
                {
                    Debug.LogError($"物体 {obj.name} 没有GridPositionHolder组件");
                    continue;
                }
                //一维索引转二维索引
                Vector2 indexXY = new Vector2(posHolder.textureLineraIndex % texIndexRange.x, 
                                            posHolder.textureLineraIndex / texIndexRange.x);

                Debug.Log(indexXY);
                
                Renderer renderer = obj.GetComponent<Renderer>();
                Material[] materials = renderer.sharedMaterials;
               
                if (renderer != null && materials != null)
                {
                    foreach (var m in materials)
                    {
                        if (m.shader.name.Contains("Parallax_Occlusion_Mapping_TileIndex"))
                        {
                            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

                            renderer.GetPropertyBlock(propBlock);
                            // 应用属性
                            Vector4 texIndex = new Vector4(indexXY.x, indexXY.y, texIndexRange.x, texIndexRange.y);
                            Debug.Log(texIndex);
                            propBlock.SetVector(texIndexID, texIndex);
                            renderer.SetPropertyBlock(propBlock);
                        }
                    }
                }

            }
        }
    }

     public void ResetTextureIndices()
     {
         if (transform.childCount > 0)
        {
           
            foreach (Transform child in transform)
            {
                GameObject obj = child.gameObject;
                 if (obj != null)
                {
                    // 获取网格位置
                    GridPositionHolder posHolder = obj.GetComponent<GridPositionHolder>();
                    if (posHolder == null)
                    {
                        Debug.LogError($"物体 {obj.name} 没有GridPositionHolder组件");
                        continue;
                    }
                    //一维索引转二维索引
                    Vector2 indexXY = new Vector2(posHolder.textureLineraIndex % texIndexRange.x, 
                                                posHolder.textureLineraIndex / texIndexRange.x);

                    Debug.Log(indexXY);
                    
                    Renderer renderer = obj.GetComponent<Renderer>();
                    Material[] materials = renderer.sharedMaterials;
                
                    if (renderer != null && materials != null)
                    {
                        foreach (var m in materials)
                        {
                            if (m.shader.name.Contains("Parallax_Occlusion_Mapping_TileIndex"))
                            {
                                MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

                                renderer.GetPropertyBlock(propBlock);
                                // 应用属性
                                Vector4 texIndex = new Vector4(indexXY.x, indexXY.y, texIndexRange.x, texIndexRange.y);
                                Debug.Log(texIndex);
                                propBlock.SetVector(texIndexID, texIndex);
                                renderer.SetPropertyBlock(propBlock);
                            }
                        }
                    }

                }
            }

        }


     }
    // 为所有生成的物体应用纹理索引
    public void ApplyTextureIndices()
    {
        Shader.SetGlobalVector(texIndexID, new Vector4(0, 0, texIndexRange.x, texIndexRange.y));
        objectPositionToTexIndex.Clear(); // 清空旧的索引记录
        
        foreach (GameObject obj in generatedObjects)
        {
            if (obj != null)
            {
                // 获取网格位置
                GridPositionHolder posHolder = obj.GetComponent<GridPositionHolder>();
                if (posHolder == null)
                {
                    Debug.LogError($"物体 {obj.name} 没有GridPositionHolder组件");
                    continue;
                }


                Vector2Int gridPosition = posHolder.gridPosition;
                
                // 生成不冲突的纹理索引
                Vector2Int indexXY = GenerateNonConflictingTexIndex(gridPosition);
                
                // 记录这个位置的纹理索引
                objectPositionToTexIndex[gridPosition] = indexXY;
                
                Renderer renderer = obj.GetComponent<Renderer>();
                Material[] materials = renderer.sharedMaterials;
                int textureCoordIndex = 0;
                if (renderer != null && materials != null)
                {
                    foreach (var m in materials)
                    {
                        if (m.shader.name.Contains("Parallax_Occlusion_Mapping_TileIndex"))
                        {
                            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

                            renderer.GetPropertyBlock(propBlock);
                            // 应用属性
                            Vector4 texIndex = new Vector4(indexXY.x, indexXY.y, texIndexRange.x, texIndexRange.y);
                            propBlock.SetVector(texIndexID, texIndex);
                            renderer.SetPropertyBlock(propBlock);
                            textureCoordIndex = posHolder.GetCurrentObjectTextureIndex(propBlock);
                        }
                    }
                }
                AssignObjectToCategory(obj, textureCoordIndex);
            }

        }
        
        //Debug.Log($"已应用纹理索引，共有 {objectPositionToTexIndex.Count} 个物体设置了纹理");
    }

    public void ClearGeneratedObjects()
    {
        foreach (GameObject obj in generatedObjects)
        {
           if (obj != null)
           {
               DestroyImmediate(obj);
           }
        }
        generatedObjects.Clear();
        objectPositionToTexIndex.Clear();
    }

    // 重新生成网格并应用纹理索引
    public void RegenerateGrid()
    {
        // 解析所有分类中的索引
        foreach (var category in objectCategories)
        {
            category.ParseIndices();
        }
        
        if (objectPrefab != null)
        {
            GetObjectSize();
            GenerateObjectGrid();
            ApplyTextureIndices();
        }
    }
    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDrawGizmos() 
    {
        #if UNITY_EDITOR

        bool visualizeCategoriesWithGizmo = UnityEditor.EditorPrefs.GetBool("MoveableTypePrinting_VisualizeCategories", true);
        
        if (!visualizeCategoriesWithGizmo || generatedObjects == null)
            return;
        
        foreach (var obj in generatedObjects)
        {
            if (obj == null)
                continue;
                
            GridPositionHolder posHolder = obj.GetComponent<GridPositionHolder>();
            if (posHolder != null && !string.IsNullOrEmpty(posHolder.categoryName))
            {
                // 使用分类的颜色绘制Gizmo
                Gizmos.color = posHolder.categoryColor;
                
                // 获取物体的渲染器和尺寸
                Renderer renderer = obj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    // 绘制一个线框立方体表示分类
                    Bounds bounds = renderer.bounds;
                    Gizmos.DrawCube(bounds.center, bounds.size*1.02f);
                
                    // 对于选中的物体，显示额外信息
                    if (UnityEditor.Selection.Contains(obj))
                    {
                        UnityEditor.Handles.color = posHolder.categoryColor;
                        UnityEditor.Handles.Label(bounds.center + Vector3.up * bounds.extents.y, 
                            $"{posHolder.categoryName}\nIndex: {posHolder.textureLineraIndex}");
                    }
                }
            }
        }
        #endif
    }
}
