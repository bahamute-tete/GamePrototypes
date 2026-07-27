using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpatialGrid2D 
{
    public float cellSize;
    private Dictionary<Vector2Int, List<WaterParticle2D>> grid;

    public SpatialGrid2D(float cellSize)
    {
        this.cellSize = cellSize;
        grid = new Dictionary<Vector2Int, List<WaterParticle2D>>();
    }

    public Vector2Int GetCellIndex(Vector2 position)
    {
        int x = Mathf.FloorToInt(position.x / cellSize);
        int y = Mathf.FloorToInt(position.y / cellSize);
        return new Vector2Int(x, y);
    }

    public void Clear()
    {
        grid.Clear();
    }

    public void InsertParticle(WaterParticle2D particle)
    {
        Vector2Int cellIndex = GetCellIndex(particle.position);
        if (!grid.ContainsKey(cellIndex))
        {
            grid[cellIndex] = new List<WaterParticle2D>();
        }
        grid[cellIndex].Add(particle);
    }


    public List<WaterParticle2D> GetNeighbors(WaterParticle2D particle)
    {
        List<WaterParticle2D> neighbors = new List<WaterParticle2D>();
        Vector2Int cellIndex = GetCellIndex(particle.position);
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int neighborCell = new Vector2Int(cellIndex.x + x, cellIndex.y + y);
                if (grid.ContainsKey(neighborCell))
                {
                    neighbors.AddRange(grid[neighborCell]);
                }
            }
        }

        neighbors.Remove(particle);

        return neighbors;
    }
}
