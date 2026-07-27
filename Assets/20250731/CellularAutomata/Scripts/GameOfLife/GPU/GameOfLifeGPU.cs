using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GameOfLifeRenderGPU))]
[RequireComponent(typeof(GameOfLifePatterns))]
public class GameOfLifeGPU: MonoBehaviour
{
    [Header("Mouse Input Settings")]
    [SerializeField] private KeyCode spawnKey = KeyCode.Mouse0; // 左键点击
    [SerializeField] private KeyCode clearKey = KeyCode.C; // 清除
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float raycastDistance = 1000f;


    [Header("Grid Settings")]
    [SerializeField] private int rows = 1000;
    [SerializeField] private int cols = 1000;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private bool showGrid= true;
    [SerializeField] private GameObject backGroundPrfab;
    private GameObject backGroundInstance;

    [Header("Compute Shader")]
    [SerializeField] private ComputeShader computeShader;
    
    [Header("Simulation Settings")]
    [SerializeField] private float updateInterval = 0.1f;
    [SerializeField] private bool autoPlay = true;
    [SerializeField] private bool manualMode =false;
    [SerializeField] private int limitedGenerations = 0; // 0表示无限制
    [SerializeField] private float initialAliveProbability = 0.3f;


    private GameOfLifePatterns patterns; 

    private GameOfLifeGridGPU grid;
    private GameOfLifeRenderGPU gridRender;
    private float timer = 0f;
    private bool isPlaying = false;
    private int generation = 0;



     private void OnGUI()
    {

        if (GUI.Button(new Rect(10, 50, 100, 30), "Pause"))
        {
            isPlaying = !isPlaying;
        }

        if (GUI.Button(new Rect(10, 100, 100, 30), "Clear"))
        {
            grid.Clear();
            generation = 0;
        }
    }

    void Start()
    {
        Initialize();
    }

    private void Initialize()
    {

        patterns = GetComponent<GameOfLifePatterns>();

        if (computeShader == null)
        {
            Debug.LogError("Compute Shader is not assigned!");
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("No camera found! Please assign a camera.");
                return;
            }
        }

        if (backGroundPrfab != null)
        {
            backGroundInstance = Instantiate(backGroundPrfab, Vector3.zero-new Vector3(0,0.2f,0), Quaternion.identity,transform);
            backGroundInstance.transform.localScale = new Vector3(cols * cellSize / 10f, 1f, rows * cellSize / 10f);

            backGroundInstance.SetActive(showGrid);
        }

        grid = new GameOfLifeGridGPU(rows, cols, cellSize, computeShader);

        if (!manualMode)
        {
            grid.RandomInitialize(initialAliveProbability);
        }
        else
        {
            grid.Clear(); 
        }
        
        gridRender = GetComponent<GameOfLifeRenderGPU>();
        gridRender.Initialize(grid);
        
        isPlaying = autoPlay;
        generation = 0;
    }

    // Update is called once per frame
    void Update()
    {
        if (manualMode && Input.GetKeyDown(spawnKey))
        {
            SpawnPatternAtMousePosition();
        }

         gridRender.Render();

        if (backGroundInstance != null)
        backGroundInstance.SetActive(showGrid);

        if (!isPlaying) return;
        
        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            timer = 0f;

            StepSimulation();
        }

        // 检查迭代限制
        if (limitedGenerations > 0 && generation >= limitedGenerations)
        {
            isPlaying = false;
        }

       
    }

    # region Place Patterns
     private void SpawnPatternAtMousePosition()
    {
        if (GetMouseGridPosition(out Vector2Int gridPos))
        {
            SpawnPatternAtGrid(gridPos.x, gridPos.y);
        }
    }

    private bool GetMouseGridPosition(out Vector2Int gridPos)
    {
        gridPos = Vector2Int.zero;
        if (mainCamera == null) return false;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, raycastDistance))
        {
            gridPos = WorldToGridPosition(hit.point);
            return IsValidGridPosition(gridPos.x, gridPos.y);
        }
        else
        {
            gridPos = ScreenToGridPosition(Input.mousePosition);
            return IsValidGridPosition(gridPos.x, gridPos.y);
        }
    }
    #endregion


    #region Utils
    private Vector2Int WorldToGridPosition(Vector3 worldPosition)
    {
        // 考虑网格的中心偏移
        float offsetX = (rows / 2.0f - 0.5f * cellSize);
        float offsetZ = (cols / 2.0f - 0.5f * cellSize);

        int gridX = Mathf.RoundToInt((worldPosition.x + offsetX) / cellSize);
        int gridY = Mathf.RoundToInt((worldPosition.z + offsetZ) / cellSize);

        return new Vector2Int(gridX, gridY);
    }

    private Vector2Int ScreenToGridPosition(Vector3 screenPosition)
    {
        // 创建一个 y=0 的平面
        Plane gridPlane = new Plane(Vector3.up, Vector3.zero);
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);

        float enter;
        if (gridPlane.Raycast(ray, out enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            return WorldToGridPosition(hitPoint);
        }

        return new Vector2Int(-1, -1); // 无效位置
    }

    private bool IsValidGridPosition(int x, int y)
    {
        return x >= 0 && x < cols && y >= 0 && y < rows;
    }

    #endregion



    private void SpawnPatternAtGrid(int x, int y)
    {
        Vector2Int[] pattern = patterns.GetCurrentPattern();
        if (pattern != null && pattern.Length > 0)
        {
            grid.PlacePattern(pattern, x, y);
            
        }
    }

    private void StepSimulation()
    {
        grid.UpdateStates();
        generation++;
    }
    
    public void TogglePlay()
    {
        isPlaying = !isPlaying;
    }
    
    public void Reset()
    {
        if (!manualMode)
        {
            grid.RandomInitialize(initialAliveProbability);
        }
        else
        {
            grid.Clear();
        }
        generation = 0;
        timer = 0f;
    }
    
    public void Clear()
    {
        grid.Clear();
        generation = 0;
    }
    
    public void SetUpdateInterval(float interval)
    {
        updateInterval = Mathf.Max(0.01f, interval);
    }
    
    private void OnDestroy()
    {
        grid?.Dispose();
    }

   
}
