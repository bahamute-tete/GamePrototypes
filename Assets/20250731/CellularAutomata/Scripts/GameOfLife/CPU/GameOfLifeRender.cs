using System.Collections.Generic;
using UnityEngine;


public class GameOfLifeRender : MonoBehaviour
{

    [Header("Visual Setting")]
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private Color aliveColor = Color.white;
    [SerializeField] private Color deadColor = Color.black;

    public GameOfLifeGrid grid;
    List<CellVisual> cellVisuals = new List<CellVisual>();

    public class CellVisual
    {
        public GameObject instance;
        public Renderer renderer;
        public MaterialPropertyBlock propertyBlock;
        public CellVisual(GameObject instance)
        {
            this.instance = instance;
            this.renderer = instance.GetComponentInChildren<Renderer>();
            this.propertyBlock = new MaterialPropertyBlock();
        }

        public void SetAliveColor(Color aliveColor, Color deadColor, int alive)
        {
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", alive == 1 ? aliveColor : deadColor);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }


    public void Initialize(GameOfLifeGrid grid)
    {
        this.grid = grid;
        CreateCellVisuals();
    }

    private void CreateCellVisuals()
    {
        if (cellPrefab == null)
        {
            Debug.LogError("Cell prefab is not assigned!");
            return;
        }

        cellVisuals.Clear();

       

        for (int y = 0; y < grid.Cols; y++)
        {
            for (int x = 0; x < grid.Rows; x++)
            {
                GameObject instance = Instantiate(cellPrefab, Vector3.zero, Quaternion.identity, transform);
                instance.transform.localPosition = new Vector3(x * grid.cellSize - (grid.Rows / 2.0f - 0.5f * grid.cellSize), 0, y * grid.cellSize - (grid.Cols / 2.0f - 0.5f * grid.cellSize));
                instance.transform.localScale = Vector3.one* grid.cellSize;
                instance.name = $"Cell_{x}_{y}";

                CellVisual cellVisual = new CellVisual(instance);
                cellVisuals.Add(cellVisual);
            }
        }
    }
    public void UpdateVisuals()
    {
        if (grid == null) return;

        int index = 0;
        for (int y = 0; y < grid.Cols; y++)
        { 
            for (int x = 0; x < grid.Rows; x++)
            {
                byte alive = grid.GetCellState(x, y);
                cellVisuals[index].SetAliveColor(aliveColor, deadColor, alive);
                index++;
            }
        }
    }

    public void Cleanup()
    { 
        foreach (var cellVisual in cellVisuals)
        {
            if (cellVisual.instance != null)
            {
                Destroy(cellVisual.instance);
            }

        }
            cellVisuals.Clear();
    }

    public void OnDestroy()
    {
        Cleanup();
    }

}
