using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class WangTile
{
    public Sprite sprite;
    // 按照顺序：上(Top), 右(Right), 下(Bottom), 左(Left)
    public List<TerrainType> matchNeighbours =new List<TerrainType>();
}

public enum TerrainType
{
    Grass,
    Water,
    Stone
}

public class CAWangTileMapGenerator : MonoBehaviour
{

    [Header("MapParameters")]
    [SerializeField] private int mapWidth = 50;
    [SerializeField] private int mapHeight = 50;
    [SerializeField] private float cellSize = 1f;


    [Header("CA Parameters")]
    [SerializeField] private int caIterations = 5;
    [SerializeField] private float initialWaterProbability = 0.2f;
    [SerializeField] private float initialStoneProbability = 0.3f;

    [Header("WangTiles Parameters")]
    [SerializeField] private List<WangTile> wangTiles;
    [SerializeField] private Sprite defaultGrassSprite;


    private TerrainType[,] terrainMap;

    private static readonly int[,] neighborOffsets = new int[,]
    {
        {-1, -1}, {-1, 0}, {-1, 1}, // 0:左下, 1:左, 2:左上
        {0, -1},           {0, 1},  // 3:下,   4:上
        {1, -1},  {1, 0},  {1, 1}   // 5:右下, 6:右, 7:右上
    };


    // Start is called before the first frame update
    void Start()
    {
        RegenerateMap();
    }

    /// <summary>
    /// 重新生成地图 - 可以在Inspector右键菜单中调用
    /// </summary>
    [ContextMenu("Regenerate Map")]
    public void RegenerateMap()
    {
        ClearMap();
        GenerateTerrainMap();
        LayoutWangTile();
    }

    /// <summary>
    /// 清理现有地图
    /// </summary>
    private void ClearMap()
    {
        for (int i = this.transform.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
            {
                Destroy(this.transform.GetChild(i).gameObject);
            }
            else
            {
                DestroyImmediate(this.transform.GetChild(i).gameObject);
            }
        }
    }

    private void GenerateTerrainMap()
    {
        terrainMap = new TerrainType[mapWidth, mapHeight];

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                float randomValue = Random.value;
                if (randomValue < initialWaterProbability)
                {
                    terrainMap[x, y] = TerrainType.Water;
                }
                else if (randomValue < initialWaterProbability + initialStoneProbability)
                {
                    terrainMap[x, y] = TerrainType.Stone;
                }
                else
                {
                    terrainMap[x, y] = TerrainType.Grass;
                }
            }
        }


        // PrintTerrainDistribution("初始化后");

        for (int i = 0; i < caIterations; i++)
        {
            terrainMap = ApplyCARules();
            // 可选：每次迭代后都打印
            // PrintTerrainDistribution($"第 {i + 1} 次CA迭代后");
        }

        // // 统计最终分布
        // PrintTerrainDistribution("最终结果");

       
    }

    private TerrainType[,] ApplyCARules()
    {
        TerrainType[,] newTerrainMap = new TerrainType[mapWidth, mapHeight];
        newTerrainMap = terrainMap.Clone() as TerrainType[,];
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                var neighbourCounts = GetNeighbourCounts(x,y);
                newTerrainMap[x, y] = GenerateRules(neighbourCounts,x,y);
            }
        }
        return newTerrainMap;
    }

    private Dictionary<TerrainType,int> GetNeighbourCounts(int x,int y)
    {
        var neighbourCounts = new Dictionary<TerrainType, int>
        {
            { TerrainType.Grass, 0 },
            { TerrainType.Water, 0 },
            { TerrainType.Stone, 0 }
        };

        Vector2Int pos = new Vector2Int(x, y);

        for (int i = 0; i < 8; i++)
        {
            int neighborRow = x + neighborOffsets[i, 0];
            int neighborCol = y + neighborOffsets[i, 1];

            neighborRow = (neighborRow + mapWidth) % mapWidth;
            neighborCol = (neighborCol + mapHeight) % mapHeight;

            var type = terrainMap[neighborRow, neighborCol];
            neighbourCounts[type]++;
        }

        return neighbourCounts;
    }


    private TerrainType GenerateRules(Dictionary<TerrainType, int> neighbourCounts,int x,int y)
    {

        // 如果水邻居很多（5个以上），变成水
        if (neighbourCounts[TerrainType.Water] >= 5)
            return TerrainType.Water;
        
        // 如果石头邻居较多（3个以上），保持或变成石头
        if (neighbourCounts[TerrainType.Stone] >= 4)
            return TerrainType.Stone;
        
        // 如果水邻居中等（3-4个），也可能变成水
        if (neighbourCounts[TerrainType.Water] >= 3)
            return TerrainType.Water;
        
        // 其他情况默认草地
        return TerrainType.Grass;


        // TerrainType current = terrainMap[x, y]; 
        
        // if (neighbourCounts[TerrainType.Water] >= 5)
        //     return TerrainType.Water;
        
        // // 石头只有在自己是石头时才容易保持
        // if (current == TerrainType.Stone && neighbourCounts[TerrainType.Stone] >= 4)
        //     return TerrainType.Stone;
        // // 草地变石头需要更多邻居
        // else if (neighbourCounts[TerrainType.Stone] >= 6)
        //     return TerrainType.Stone;
        
        // if (neighbourCounts[TerrainType.Water] >= 3)
        //     return TerrainType.Water;
        
        // return TerrainType.Grass;
    }


    private void LayoutWangTile()
    {
       
        for (int x = 0; x < mapWidth; x++)
        {
           for (int y = 0; y < mapHeight; y++)
           {
               // 获取上、右、下、左四个邻居
               int topX = x;
               int topY = (y + 1 + mapHeight) % mapHeight;
               int rightX = (x + 1 + mapWidth) % mapWidth;
               int rightY = y;
               int bottomX = x;
               int bottomY = (y - 1 + mapHeight) % mapHeight;
               int leftX = (x - 1 + mapWidth) % mapWidth;
               int leftY = y;

               TerrainType top = terrainMap[topX, topY];
               TerrainType right = terrainMap[rightX, rightY];
               TerrainType bottom = terrainMap[bottomX, bottomY];
               TerrainType left = terrainMap[leftX, leftY];

               WangTile selectedTile = wangTiles.Find(tile =>
                   tile.matchNeighbours.Count >= 4 &&
                   tile.matchNeighbours[0] == top &&    // Top
                   tile.matchNeighbours[1] == right &&  // Right
                   tile.matchNeighbours[2] == bottom && // Bottom
                   tile.matchNeighbours[3] == left      // Left
               );

               Sprite spriteToUse = null;

               if (selectedTile != null && selectedTile.sprite != null)
               {
                   spriteToUse = selectedTile.sprite;
               }
               else
               {

                   spriteToUse = defaultGrassSprite;
               }


               GameObject tileGO = new GameObject($"Tile_{x}_{y}");
               tileGO.transform.position = new Vector3(x * cellSize, y * cellSize, 0);
               tileGO.transform.localScale = Vector3.one * cellSize * 0.8f;
               tileGO.transform.parent = this.transform;
               SpriteRenderer sr = tileGO.AddComponent<SpriteRenderer>();

               if (spriteToUse != null)
               {
                   sr.sprite = spriteToUse;
               }
               else
               {
                   Debug.LogWarning($"No sprite available for position ({x}, {y}) with neighbors: Top={top}, Right={right}, Bottom={bottom}, Left={left}");
               }
           }
        }
    }


    List<TerrainType> GetNeighborTypes(int x, int y)
    {
        var neighbors = new List<TerrainType>();
        // 上
        neighbors.Add(y < mapHeight - 1 ? terrainMap[x, y + 1] : terrainMap[x, y]);
        // 右
        neighbors.Add(x < mapWidth - 1 ? terrainMap[x + 1, y] : terrainMap[x, y]);
        // 下
        neighbors.Add(y > 0 ? terrainMap[x, y - 1] : terrainMap[x, y]);
        // 左
        neighbors.Add(x > 0 ? terrainMap[x - 1, y] : terrainMap[x, y]);
        return neighbors;
    }

    Sprite GetMatchWangTile(TerrainType currentType, List<TerrainType> neighbors)
    {
       
        foreach (var tile in wangTiles)
        {
            if (tile.matchNeighbours.Count != neighbors.Count) continue;

            bool isMatch = true;

            for (int i = 0; i < neighbors.Count; i++)
            {
                if (tile.matchNeighbours[i] != neighbors[i])
                {
                    isMatch = false;
                    break;
                }
            }
            if (isMatch) return tile.sprite;
        }

        // 无匹配则返回纯地形瓦片
        foreach (var tile in wangTiles)
        {
            if (tile.matchNeighbours.Count == 1 && tile.matchNeighbours[0] == currentType)
                return tile.sprite;
        }
        return defaultGrassSprite;
    }



    
    /// <summary>
    /// 统计并打印地形分布
    /// </summary>
    private void PrintTerrainDistribution(string phase)
    {
        if (terrainMap == null) return;
    
        int grassCount = 0;
        int waterCount = 0;
        int stoneCount = 0;
        int totalCells = mapWidth * mapHeight;
    
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                switch (terrainMap[x, y])
                {
                    case TerrainType.Grass:
                        grassCount++;
                        break;
                    case TerrainType.Water:
                        waterCount++;
                        break;
                    case TerrainType.Stone:
                        stoneCount++;
                        break;
                }
            }
        }
    
        float grassPercent = (grassCount / (float)totalCells) * 100f;
        float waterPercent = (waterCount / (float)totalCells) * 100f;
        float stonePercent = (stoneCount / (float)totalCells) * 100f;
    
        Debug.Log($"========== {phase} ==========");
        Debug.Log($"总格子数: {totalCells}");
        Debug.Log($"草地 (Grass): {grassCount} 格 ({grassPercent:F2}%)");
        Debug.Log($"水域 (Water): {waterCount} 格 ({waterPercent:F2}%)");
        Debug.Log($"石头 (Stone): {stoneCount} 格 ({stonePercent:F2}%)");
        Debug.Log($"================================\n");
    }
    

    // private void OnDrawGizmos()
    // {
    //     if (terrainMap == null) return;
    
    //     for (int x = 0; x < mapWidth; x++)
    //     {
    //         for (int y = 0; y < mapHeight; y++)
    //         {
    //             Vector3 pos = new Vector3(x * cellSize, y * cellSize, 0);
                
    //             // 根据地形类型设置颜色
    //             switch (terrainMap[x, y])
    //             {
    //                 case TerrainType.Grass:
    //                     Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.5f); // 绿色
    //                     break;
    //                 case TerrainType.Water:
    //                     Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.5f); // 蓝色
    //                     break;
    //                 case TerrainType.Stone:
    //                     Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.5f); // 灰色
    //                     break;
    //             }
                
    //             Gizmos.DrawCube(pos, Vector3.one * cellSize * 0.9f);
    //         }
    //     }
    // }

    // Update is called once per frame
    void Update()
    {
        
    }
}
