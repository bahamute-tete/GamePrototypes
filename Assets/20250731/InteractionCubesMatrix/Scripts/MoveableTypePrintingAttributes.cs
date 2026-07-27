using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[System.Serializable]
public class MoveableTypeObjectData
{
    public GameObject objectInstance;               
    public Vector3 relativePosition;
    // 实例一维索引
    public int linearIndex;
    // 纹理一维索引
    public int textureLinearIndex;                   
    public Renderer renderer;                      
    
    // 更新物体的相对位置
    public void UpdateRelativePosition()
    {
        if (objectInstance != null && objectInstance.transform.parent != null)
        {
            relativePosition = objectInstance.transform.localPosition;
        }
    }


    public void ApplyPropertyBlock(MaterialPropertyBlock propertyBlock)
    {
        if (renderer != null)
        {
            MaterialPropertyBlock currentBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(currentBlock);
            
            // 将新的发光颜色设置到当前属性块，保留其他属性
            if (propertyBlock.HasColor("_GlowColor"))
            {
                currentBlock.SetColor("_GlowColor", propertyBlock.GetColor("_GlowColor"));
            }
            
            // 应用更新后的属性块
            renderer.SetPropertyBlock(currentBlock);
        }
    }
}

[System.Serializable]
public class CategoryObjects
{
    public string categoryName;                      
    public List<MoveableTypeObjectData> objects;    
    public Color categoryColor;                     
    
    [ColorUsage(true, true)]
    public Color glowColor = Color.black;           
    private MaterialPropertyBlock propertyBlock;

    public Vector2 depathOffsetRange = new Vector2(-0.1f, 0.1f);
    [HideInInspector]public List<Vector3> currentLocalPositions= new List<Vector3>();
    
    // 动画相关变量
    [HideInInspector]public bool isAnimating = false;
    [HideInInspector]public float animationProgress = 0f;
    [HideInInspector]public float animationDirection = 1f; // 1表示前进，-1表示返回

    public CategoryObjects(string name, Color color)
    {
        categoryName = name;
        categoryColor = color;
        glowColor = Color.black;
        objects = new List<MoveableTypeObjectData>();
        propertyBlock = new MaterialPropertyBlock();
    }


    // 获取该分类下所有物体的本地坐标
    public void GetCurrentLocalPosition()
    {
        foreach (var obj in objects)
        {
           Vector3 pos =  obj.objectInstance.transform.localPosition;
           currentLocalPositions.Add(pos);
        }
    }
    // 设置该分类下所有物体的本地坐标
    public void SetLocalPosition(Vector3 localPosition)
    {
        foreach (var obj in objects)
        {
            obj.objectInstance.transform.localPosition = localPosition;
        }
    }
    // 设置该分类下所有物体的发光颜色
    public void SetGlowColor(Color newColor)
    {
        glowColor = newColor;
        
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();
            

        propertyBlock.Clear();
        propertyBlock.SetColor("_GlowColor", glowColor);
        
        foreach (var obj in objects)
        {
            obj.ApplyPropertyBlock(propertyBlock);
        }
    }
    
    // 获取当前的发光颜色
    public Color GetGlowColor()
    {
        return glowColor;
    }
    
    // 更新属性块中的发光颜色
    public void UpdatePropertyBlock()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();
            
        propertyBlock.Clear(); // 清除之前的所有属性
        propertyBlock.SetColor("_GlowColor", glowColor);
    }
    
    // 还原该分类下所有物体到原始位置
    public void RestoreOriginalPositions()
    {
        foreach (var obj in objects)
        {
            if (obj.objectInstance != null)
            {
                obj.objectInstance.transform.localPosition=new Vector3(0,obj.relativePosition.y,obj.relativePosition.z);
            }
        }
    }
}

public class MoveableTypePrintingAttributes : MonoBehaviour
{
    [SerializeField] private InteractionMoveableTypePrintig moveableTypePrinting;
    [SerializeField] private bool autoUpdateOnStart = true;
    
    // 按分类组织的物体列表
    [SerializeField] private List<CategoryObjects> categorizedObjects = new List<CategoryObjects>();
    
    // 所有物体的列表
    [SerializeField, HideInInspector] private List<MoveableTypeObjectData> allObjects = new List<MoveableTypeObjectData>();
    
    // 动画相关参数
    [SerializeField] private float defaultAnimationDuration = 0.5f;
    [SerializeField] private float defaultAnimationDistance = 0.2f;

    private Dictionary<string, CategoryObjects> categoryDictionary = new Dictionary<string, CategoryObjects>();
    
    // 分类与纹理索引的映射
    private Dictionary<int, string> textureIndexToCategoryMap = new Dictionary<int, string>();

    // 当前正在播放动画的协程字典
    private Dictionary<string, Coroutine> activeAnimationCoroutines = new Dictionary<string, Coroutine>();
    
    private void Start()
    {
        if (moveableTypePrinting == null)
        {
            moveableTypePrinting = GetComponent<InteractionMoveableTypePrintig>();
        }
        
        if (autoUpdateOnStart && moveableTypePrinting != null)
        {
            if(moveableTypePrinting.transform.childCount > 0)
            {
                UpdateAllObjectsData();
            }
            else
            {
                Debug.LogWarning("MoveableTypePrinting没有子物体，无法更新数据");
                StartCoroutine(DelayedUpdateCheck());
            }
        }
    }
    
    // 延迟检查并更新数据
    private IEnumerator DelayedUpdateCheck()
    {
        int checkCount = 0;
        int maxChecks = 10;
        
        while(checkCount < maxChecks)
        {
            yield return new WaitForSeconds(0.2f);
            
            if(moveableTypePrinting != null && moveableTypePrinting.transform.childCount > 0)
            {
                Debug.Log("延迟检测到数据，现在更新所有物体数据");
                UpdateAllObjectsData();
                break;
            }
            
            checkCount++;
        }
        
        if(checkCount >= maxChecks)
        {
            Debug.LogWarning("延迟检查超时，未能获取到有效数据");
        }
    }
    
    // 更新所有物体数据
    public void UpdateAllObjectsData()
    {
        if (moveableTypePrinting == null)
        {
            Debug.LogError("moveableTypePrinting为空，无法更新数据");
            return;
        }
            
        if (moveableTypePrinting.transform.childCount <= 0)
        {
            Debug.LogWarning("没有可用的子物体数据，跳过更新");
            return;
        }
        
        // 保存现有分类的发光颜色
        Dictionary<string, Color> savedGlowColors = new Dictionary<string, Color>();
        foreach (var category in categorizedObjects)
        {
            savedGlowColors[category.categoryName] = category.glowColor;
        }
        
        // 清空现有数据
        allObjects.Clear();
        categorizedObjects.Clear();
        categoryDictionary.Clear();
        textureIndexToCategoryMap.Clear();
        
        // 获取所有生成的物体
        Transform parent = moveableTypePrinting.transform;
        
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            var posHolder = child.GetComponent<GridPositionHolder>();
            
            if (posHolder != null)
            {
                // 创建物体数据
                MoveableTypeObjectData objectData = new MoveableTypeObjectData
                {
                    objectInstance = child.gameObject,
                    relativePosition = child.localPosition,
                    linearIndex = posHolder.linearIndex,
                    textureLinearIndex = posHolder.textureLineraIndex,
                    renderer = child.GetComponent<Renderer>()
                };
                
                // 添加到所有物体列表
                allObjects.Add(objectData);
                
                // 添加到对应的分类中
                AddObjectToCategory(objectData, posHolder.categoryName, posHolder.categoryColor);
                
                // 记录纹理索引到分类的映射
                if (!string.IsNullOrEmpty(posHolder.categoryName))
                {
                    textureIndexToCategoryMap[posHolder.textureLineraIndex] = posHolder.categoryName;
                }
            }
        }
        
        // 恢复之前保存的发光颜色
        foreach (var category in categorizedObjects)
        {
            if (savedGlowColors.TryGetValue(category.categoryName, out Color savedColor))
            {
                category.glowColor = savedColor;
            }
        }
        
        // 为每个分类初始化属性块
        foreach (var category in categorizedObjects)
        {
            category.UpdatePropertyBlock();
        }
        
        //Debug.Log($"已更新所有物体数据，共 {allObjects.Count} 个物体，{categorizedObjects.Count} 个分类");
    }
    
    // 将物体添加到对应的分类中
    private void AddObjectToCategory(MoveableTypeObjectData objectData, string categoryName, Color categoryColor)
    {
        if (string.IsNullOrEmpty(categoryName))
            return;
            
        // 如果分类不存在，则创建新的分类
        if (!categoryDictionary.TryGetValue(categoryName, out CategoryObjects category))
        {
            category = new CategoryObjects(categoryName, categoryColor);
            categorizedObjects.Add(category);
            categoryDictionary[categoryName] = category;
        }
        
        // 将物体添加到分类中
        category.objects.Add(objectData);
    }

    #region “公共方法”
   
    // 获取指定分类的所有物体
    public List<MoveableTypeObjectData> GetObjectsInCategory(string categoryName)
    {
        if (categoryDictionary.TryGetValue(categoryName, out CategoryObjects category))
        {
            return category.objects;
        }
        return new List<MoveableTypeObjectData>();
    }
    
    // 获取指定索引的物体
    public MoveableTypeObjectData GetObjectByLinearIndex(int linearIndex)
    {
        return allObjects.FirstOrDefault(obj => obj.linearIndex == linearIndex);
    }
    
    // 获取指定纹理索引的物体所属分类
    public string GetCategoryByTextureIndex(int textureIndex)
    {
        if (textureIndexToCategoryMap.TryGetValue(textureIndex, out string categoryName))
        {
            return categoryName;
        }
        return string.Empty;
    }
    
    // 获取指定纹理索引的所有物体
    public List<MoveableTypeObjectData> GetObjectsByTextureIndex(int textureIndex)
    {
        string categoryName = GetCategoryByTextureIndex(textureIndex);
        if (!string.IsNullOrEmpty(categoryName))
        {
            return GetObjectsInCategory(categoryName);
        }
        return new List<MoveableTypeObjectData>();
    }
    
    // 获取所有分类名称
    public string[] GetAllCategoryNames()
    {
        return categorizedObjects.Select(c => c.categoryName).ToArray();
    }
    
    // 获取所有分类列表
    public List<string> GetAllCategories()
    {
        return categorizedObjects.Select(c => c.categoryName).ToList();
    }
    
    // 根据GameObject获取其所属的分类名称
    public string GetCategoryNamesByObject(GameObject obj)
    {
        if (obj == null)
            return string.Empty;
            
        foreach (var category in categorizedObjects)
        {
            foreach (var objectData in category.objects)
            {
                if (objectData.objectInstance == obj)
                {
                    return category.categoryName;
                }
            }
        }
        
        return string.Empty;
    }
    
    // 获取分类对象
    public CategoryObjects GetCategory(string categoryName)
    {
        if (categoryDictionary.TryGetValue(categoryName, out CategoryObjects category))
        {
            return category;
        }
        return null;
    }
    
    // 为指定分类的所有物体设置发光颜色
    public void SetCategoryGlowColor(string categoryName, Color newColor)
    {
        var category = GetCategory(categoryName);
        if (category != null)
        {
            category.SetGlowColor(newColor);
        }
    }
    
    // 为指定纹理索引的所有物体设置发光颜色
    public void SetTextureIndexGlowColor(int textureIndex, Color newColor)
    {
        string categoryName = GetCategoryByTextureIndex(textureIndex);
        if (!string.IsNullOrEmpty(categoryName))
        {
            SetCategoryGlowColor(categoryName, newColor);
        }
    }


    public void SetRandomDepthPosition(string categoryName)
    {
        var category = GetCategory(categoryName);
        if (category != null)
        {
            category.currentLocalPositions.Clear();
            
            category.GetCurrentLocalPosition();
            
            for (int i = 0; i < category.objects.Count; i++)
            {
                if (i < category.currentLocalPositions.Count && category.objects[i].objectInstance != null)
                {
                    Vector3 direction = new Vector3(0, 0, 1);
                
                    float randomOffset = Random.Range(category.depathOffsetRange.x, category.depathOffsetRange.y);
                  
                    Vector3 newPos =category.objects[i].objectInstance.transform.localPosition + direction * randomOffset;
                    
                    category.objects[i].objectInstance.transform.localPosition = newPos;
                }
            }
            
            // 更新所有物体的相对位置
            foreach (var obj in category.objects)
            {
                obj.UpdateRelativePosition();
            }
        }
    }

    // 更新所有物体的位置信息
    public void UpdateAllPositions()
    {
        foreach (var objData in allObjects)
        {
            objData.UpdateRelativePosition();
        }
        //Debug.Log("已更新所有物体的位置信息");
    }
    
    // 还原指定分类的物体到原始位置
    public void RestoreCategoryOriginalPositions(string categoryName)
    {
        var category = GetCategory(categoryName);
        if (category != null)
        {
            category.RestoreOriginalPositions();
        }
    }
    
    // 还原所有分类的物体到原始位置
    public void RestoreAllOriginalPositions()
    {
        foreach (var category in categorizedObjects)
        {
            category.RestoreOriginalPositions();
        }
        
        UpdateAllPositions();
    }
    
    // 射线检测碰撞后，获取物体所在分类并触发动画
    public bool HandleRayHit(GameObject hitObject, float animationDuration = -1, float moveDistance = -1)
    {
        if (hitObject == null)
            return false;
        
        // 查找该物体所在的分类
        string categoryName = FindCategoryNameByGameObject(hitObject);
        if (string.IsNullOrEmpty(categoryName))
            return false;
        
        // 使用默认值或传入的自定义值
        if (animationDuration < 0) animationDuration = defaultAnimationDuration;
        if (moveDistance < 0) moveDistance = defaultAnimationDistance;
        
        // 启动分类向前的动画
        StartCategoryForwardAnimation(categoryName, animationDuration, moveDistance);
        return true;
    }
    
    // 射线离开物体后，触发该物体所在分类的返回动画
    public bool HandleRayExit(GameObject hitObject, float animationDuration = -1)
    {
        if (hitObject == null)
            return false;
        
        // 查找该物体所在的分类
        string categoryName = FindCategoryNameByGameObject(hitObject);
        if (string.IsNullOrEmpty(categoryName))
            return false;
        
        // 使用默认值或传入的自定义值
        if (animationDuration < 0) animationDuration = defaultAnimationDuration;
        
        // 启动分类返回的动画
        StartCategoryReturnAnimation(categoryName, animationDuration);
        return true;
    }

    // 根据GameObject查找其所在的分类名称
    private string FindCategoryNameByGameObject(GameObject targetObject)
    {
        foreach (var category in categorizedObjects)
        {
            foreach (var obj in category.objects)
            {
                if (obj.objectInstance == targetObject)
                {
                    return category.categoryName;
                }
            }
        }
        return string.Empty;
    }
    
    // 启动分类向前的动画
    private void StartCategoryForwardAnimation(string categoryName, float duration, float distance)
    {
        var category = GetCategory(categoryName);
        if (category != null)
        {
            // 如果已有该分类的动画在运行，先停止它
            if (activeAnimationCoroutines.TryGetValue(categoryName, out Coroutine coroutine))
            {
                if (coroutine != null)
                    StopCoroutine(coroutine);
                activeAnimationCoroutines.Remove(categoryName);
            }
            
            // 启动新动画
            category.animationDirection = 1f; // 前进方向
            var newCoroutine = StartCoroutine(AnimateCategoryPosition(category, distance, duration));
            activeAnimationCoroutines[categoryName] = newCoroutine;
        }
    }
    
    // 启动分类返回的动画
    private void StartCategoryReturnAnimation(string categoryName, float duration)
    {
        var category = GetCategory(categoryName);
        if (category != null)
        {
            // 如果已有该分类的动画在运行，先停止它
            if (activeAnimationCoroutines.TryGetValue(categoryName, out Coroutine coroutine))
            {
                if (coroutine != null)
                    StopCoroutine(coroutine);
                activeAnimationCoroutines.Remove(categoryName);
            }
            
            // 启动返回动画
            category.animationDirection = -1f; // 返回方向
            var newCoroutine = StartCoroutine(AnimateCategoryPosition(category, 0f, duration));
            activeAnimationCoroutines[categoryName] = newCoroutine;
        }
    }
    
    // 动画协程
    private IEnumerator AnimateCategoryPosition(CategoryObjects category, float targetDistance, float duration)
    {
        // 如果还没有获取当前位置，先获取一下
        if (category.currentLocalPositions.Count == 0)
        {
            category.GetCurrentLocalPosition();
        }
        
        category.isAnimating = true;
        float startTime = Time.time;
        
        // 获取起始和目标位置
        List<Vector3> startPositions = new List<Vector3>();
        List<Vector3> targetPositions = new List<Vector3>();
        
        for (int i = 0; i < category.objects.Count; i++)
        {
            Vector3 currentPos = category.objects[i].objectInstance.transform.localPosition;
            startPositions.Add(currentPos);
            

            Vector3 direction = new Vector3(0,0,1);
            
            if (category.animationDirection > 0)
            {
  
                targetPositions.Add(currentPos + direction * targetDistance);
            }
            else
            {
                // 返回动画：返回到原位置
                Vector3 originalPos = category.objects[i].relativePosition.z > 0 ? category.objects[i].relativePosition : 
                                     (i < category.currentLocalPositions.Count ? category.currentLocalPositions[i] : Vector3.zero);
                targetPositions.Add(new Vector3(currentPos.x, currentPos.y, originalPos.z));
            }
        }
        
        // 执行动画
        while (Time.time - startTime < duration)
        {
            float normalizedTime = (Time.time - startTime) / duration;
            
            // 使用平滑的插值
            float t = Mathf.SmoothStep(0, 1, normalizedTime);
            
            for (int i = 0; i < category.objects.Count; i++)
            {
                if (i < startPositions.Count && i < targetPositions.Count)
                {
                    Vector3 newPos = Vector3.Lerp(startPositions[i], targetPositions[i], t);
                    category.objects[i].objectInstance.transform.localPosition = newPos;
                }
            }
            
            yield return null;
        }
        
        // 确保在动画结束时精确到达目标位置
        for (int i = 0; i < category.objects.Count; i++)
        {
            if (i < targetPositions.Count)
            {
                category.objects[i].objectInstance.transform.localPosition = targetPositions[i];
            }
        }
        
        // 如果是返回动画，完成后更新相对位置
        if (category.animationDirection < 0)
        {
            foreach (var obj in category.objects)
            {
                obj.UpdateRelativePosition();
            }
        }
        
        category.isAnimating = false;
        activeAnimationCoroutines.Remove(category.categoryName);
    }
    #endregion
   
}
