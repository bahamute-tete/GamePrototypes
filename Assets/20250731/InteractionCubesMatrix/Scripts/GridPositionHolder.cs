using UnityEngine;

// 用于保存物体在网格中位置的组件
public class GridPositionHolder : MonoBehaviour
{
    public Vector2Int gridPosition;
    public int linearIndex;
    public int textureLineraIndex;
    public string categoryName = "";
    public Color categoryColor = Color.white; // 分类颜色属性

    public int GetCurrentObjectTextureIndex(MaterialPropertyBlock block)
    {
        Vector4 index = block.GetVector(Shader.PropertyToID("_TexIndex"));
        return textureLineraIndex = (int)index.x + (int)index.y * (int)index.z;
    }
    
    public void SetGridPosition(Vector2Int pos, int horizontalCount)
    {
        gridPosition = pos;
        linearIndex = pos.y * horizontalCount + pos.x;
    }
}
