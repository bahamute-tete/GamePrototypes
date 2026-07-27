using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

#if UNITY_EDITOR
[ExecuteInEditMode]
#endif
public class TypePrintingSweepEffect : MonoBehaviour
{
    [Range(0.05f, 1f)] 
    public float glowProgress = 0f; 

    [Range(0f, 1f)] 
    public float glowGloableIntensityControl = 1f; 
    

    [Range(0.1f, 10f)]
    public float diffusionSpeed = 1f;

    [Range(1f, 50f)]
    public float maxGlowDistance = 10f;

    [ColorUsage(true, true)]
    public Color glowColor = Color.white;
    [ColorUsage(true, true)]
    public Color originalColor = Color.black;
    
    private List<Renderer> allRenderers = new List<Renderer>();
    
    private Dictionary<Renderer, Vector2Int> rendererPositions = new Dictionary<Renderer, Vector2Int>();

    public List<Vector2Int> startPositions = new List<Vector2Int>();

    void Start()
    {
        CollectAndCategorizeChildren();
    }
    
    void Update()
    {
        UpdateGlowEffect();
    }
    
    private void CollectAndCategorizeChildren()
    {
        allRenderers.Clear();
        rendererPositions.Clear(); // 清空渲染器位置字典
        
        foreach (Transform child in transform)
        {
            GridPositionHolder gridPosHolder = child.GetComponent<GridPositionHolder>();
            if (gridPosHolder != null)
            {
                Renderer renderer = child.GetComponent<Renderer>();
                if (renderer != null)
                {
                    allRenderers.Add(renderer);
                    rendererPositions[renderer] = gridPosHolder.gridPosition; 
                }
            }
        }
        
        foreach (var renderer in allRenderers)
        {
            SetGlowColorForRenderer(renderer, originalColor);
        }
    }
    
    private void UpdateGlowEffect()
    {
        // 使用多个起始点
        if (startPositions != null && startPositions.Count > 0)
        {
            GlowDiffusionMultipleStart(startPositions);
        }
        else
        {
            // 如果没有设置起始点，默认使用 (0,0)
            GlowDiffusionMultipleStart(new List<Vector2Int> { new Vector2Int(0, 0) });
        }
    }
    
    private void GlowDiffusionMultipleStart(List<Vector2Int> startPositions)
    {
        if (glowProgress <= 0f)
        {
            // 如果进度为0，全部恢复原始颜色
            foreach (var renderer in allRenderers)
            {
                SetGlowColorForRenderer(renderer, originalColor);
            }
            return;
        }
        
        foreach (var renderer in allRenderers)
        {
            if (rendererPositions.TryGetValue(renderer, out Vector2Int position))
            {
                // 计算到所有起始点的最短距离
                float minDistance = float.MaxValue;
                foreach (var startPos in startPositions)
                {
                    float distance = Vector2Int.Distance(startPos, position);
                    minDistance = Mathf.Min(minDistance, distance);
                }
                
                float normalizedDistance = Mathf.Clamp01(minDistance / maxGlowDistance);
                
                float glowThreshold = normalizedDistance * diffusionSpeed;
                
                float intensity;
                if (glowProgress <= glowThreshold)
                {
                    intensity = 0f;
                }
                else
                {
                    float progressRange = glowProgress - glowThreshold;
                    intensity = Mathf.Clamp01(progressRange * (1 + diffusionSpeed)) * glowGloableIntensityControl;
                }
                              
                if (intensity <= 0)
                {
                    SetGlowColorForRenderer(renderer, originalColor);
                }
                else
                {
                    Color adjustedGlowColor = new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a) * intensity;
                    SetGlowColorForRenderer(renderer, adjustedGlowColor);
                }
            }
        }
    }
    
    private void SetGlowColorForRenderer(Renderer renderer, Color color)
    {
        if (renderer == null) return;
    
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(propertyBlock);
        
        Material[] materials = renderer.sharedMaterials;
        
        if (materials.Length > 1 && materials[1] != null && materials[1].HasProperty("_GlowColor"))
        {
            propertyBlock.SetColor("_GlowColor", color);
            renderer.SetPropertyBlock(propertyBlock, 1); 
        }
        else if (materials.Length > 0 && materials[0] != null && materials[0].HasProperty("_GlowColor"))
        {
            propertyBlock.SetColor("_GlowColor", color);
            renderer.SetPropertyBlock(propertyBlock, 0); 
        }
    }

    private void OnDisable()
    {
        // 遍历所有Renderer，清除所有材质索引的PropertyBlock
        foreach (var renderer in allRenderers)
        {
            if (renderer != null)
            {
                // 获取当前Renderer的材质数量
                int materialCount = renderer.sharedMaterials.Length;
                // 遍历所有材质索引，逐个清除PropertyBlock
                for (int i = 0; i < materialCount; i++)
                {
                    renderer.SetPropertyBlock(null, i); // 清除指定索引的PropertyBlock
                }
            }
        }
    }

    public void RevertBlock()
    {
       GetComponent<TypePrintingSweepEffect>().enabled = false;
        Debug.Log("OnDisable");
    }
}
