using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(GameOfLifeRender))]
public class GameOfLife : MonoBehaviour
{

    [Header("Grid Settings")]
    [SerializeField] private int rows = 10;
    [SerializeField] private int cols = 10;
    [SerializeField] private float cellSize = 1f;

    [Header("Simulation Settings")]
    [SerializeField] private float updateInterval = 0.5f;
    [SerializeField] private bool autoPlay = true;
    
    [SerializeField] private int limitedGenerations = 0; // 0表示无限制
    [SerializeField] private float initialAliveProbability = 0.3f;

    [Header("Mouse Input Settings")]
    [SerializeField] private KeyCode spawnKey = KeyCode.Mouse0; // 左键点击
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float raycastDistance = 1000f;
    [SerializeField] private bool enableMouseInput = true; // 是否启用鼠标输入

    [Header("Rule Settings")]
    private GameOfLifeGrid grid;
    private GameOfLifeRender gridRender;
    private float timer = 0f;
    private bool isPlaying = true;
    [SerializeField] int generation;
    private bool hasReachedLimit = false;

    public enum RuleType
    {
        Conway_B3_S23,         // 经典康威生命游戏
        HighLife_B36_S23,       // B36/S23 - 复制机图案
        DayAndNight_B3678_S34678,    // B3678/S34678 - 对称图案
        Seeds_B2_S,          // B2/S - 种子扩散
        Maze_B3_S12345,           // B3/S12345 - 迷宫图案
        Coral_B3_S45678_,          // B3/S45678 - 珊瑚结构
        TwoByTwo_B36_S125,       // B36/S125 - 2x2方块
        Custom          // 自定义规则
    }

    [SerializeField] private RuleType ruleType = RuleType.Conway_B3_S23;

    [Header("Custom Rule (仅当选择Custom时生效)")]
    [SerializeField] private int[] customBirthNumbers = new int[] { 3 };
    [SerializeField] private int[] customSurviveNumbers = new int[] { 2, 3 };


    GameOfLifePatterns patterns;

    void OnGUI()
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

    // Start is called before the first frame update
    void Start()
    {
        patterns = GetComponent<GameOfLifePatterns>();

        if (patterns == null)
        {
            Debug.LogError("GameOfLifePatterns component not found! Please add it to the GameObject.");
            enabled = false;
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("No camera found! Mouse input will be disabled.");
                enableMouseInput = false;
            }
        }

        Initialize();
    }

    private void Initialize()
    {
        ICellularAutomataRule rule = CreateRule(ruleType);

        grid = new GameOfLifeGrid(rows, cols, cellSize, rule);

        if(initialAliveProbability>0.0)
            grid.RandomInitialize(initialAliveProbability);
        
        gridRender = GetComponent<GameOfLifeRender>();
        gridRender.Initialize(grid);
        gridRender.UpdateVisuals();

        isPlaying = autoPlay;

        generation = 0;


    }

    // Update is called once per frame
    void Update()
    {
        if (enableMouseInput || initialAliveProbability == 0)
        {
            HandleMouseInput();
        }

        if (limitedGenerations > 0 && generation >= limitedGenerations)
        {
            if (isPlaying)
            {
                hasReachedLimit = true;
                isPlaying = false;

                Debug.Log($"Simulation stopped at generation {generation}");
            }
            return; 
        }



        if (!isPlaying) return;

        timer += Time.deltaTime; 
           
        if (timer >= updateInterval)
        {
            timer = 0f;
            StepSimulation();
        }
    }


    private void HandleMouseInput()
    {
        // 鼠标点击生成图案
        if (Input.GetKeyDown(spawnKey))
        {
            SpawnPatternAtMousePosition();
        }

        // 快捷键控制
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TogglePause();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            Reset();
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            Clear();
        }
    }


    private void SpawnPatternAtMousePosition()
    {
        if (mainCamera == null || patterns == null) return;

        // 获取当前图案
        Vector2Int[] pattern = patterns.GetCurrentPattern();

        //Debug.Log($"Attempting to spawn pattern at mouse position. Pattern length: {(pattern != null ? pattern.Length : 0)}");
        if (pattern == null || pattern.Length == 0)
        {
            Debug.LogWarning("No pattern available! Please configure the pattern in GameOfLifePatterns component.");
            return;
        }

        // 获取鼠标对应的网格坐标
        Vector2Int gridPos = GetGridPositionFromMouse();

        if (IsValidGridPosition(gridPos.x, gridPos.y))
        {
            // 在网格上放置图案
            grid.SetPatterns(pattern, gridPos.x, gridPos.y);
            gridRender.UpdateVisuals();

            Debug.Log($"Spawned pattern with {pattern.Length} cells at grid position ({gridPos.x}, {gridPos.y})");
        }
        else
        {
            Debug.LogWarning($"Invalid grid position: ({gridPos.x}, {gridPos.y})");
        }
    }

    private Vector2Int GetGridPositionFromMouse()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // 方法1：使用 Raycast（如果有碰撞体）
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, raycastDistance))
        {
            return WorldToGridPosition(hit.point);
        }

        // 方法2：使用平面相交（y=0 平面）
        Plane gridPlane = new Plane(Vector3.up, Vector3.zero);
        float enter;
        if (gridPlane.Raycast(ray, out enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            return WorldToGridPosition(hitPoint);
        }

        return new Vector2Int(-1, -1);
    }

    private Vector2Int WorldToGridPosition(Vector3 worldPosition)
    {
        float offsetX = (rows / 2.0f - 0.5f * cellSize);
        float offsetY = (cols / 2.0f - 0.5f * cellSize);

        int gridX = Mathf.RoundToInt((worldPosition.x + offsetX) / cellSize);
        int gridY = Mathf.RoundToInt((worldPosition.z + offsetY) / cellSize);

        return new Vector2Int(gridX, gridY);
    }

    private bool IsValidGridPosition(int x, int y)
    {
        return x >= 0 && x < rows && y >= 0 && y < cols;
    }
    private void StepSimulation()
    {
        grid.UpdateNextStates();
        gridRender.UpdateVisuals();
        generation++;
    }

    public void TogglePause()
    {
        // 如果已达到限制，不允许继续播放
        if (hasReachedLimit && !isPlaying)
        {
            Debug.Log("Cannot resume: generation limit reached. Use Reset to restart.");
            return;
        }

        isPlaying = !isPlaying;
    }

    public void Reset()
    {
        grid.RandomInitialize(initialAliveProbability);
        gridRender.UpdateVisuals();
        generation = 0;
        timer = 0f;
        hasReachedLimit = false;
    }

    public void Clear()
    {
        grid.Clear();
        gridRender.UpdateVisuals();
        generation = 0;
        hasReachedLimit = false;
    }

    public void SetUpdateInterval(float interval)
    {
        updateInterval = Mathf.Max(0.01f, interval);
    }

    private ICellularAutomataRule CreateRule(RuleType ruleType)
    {
        switch (ruleType)
        {
            case RuleType.Conway_B3_S23:
                return new ConwayRule();
            case RuleType.HighLife_B36_S23:
                return new HighLifeRule();
            case RuleType.DayAndNight_B3678_S34678:
                return new DayAndNightRule();
            case RuleType.Seeds_B2_S:
                return new SeedsRule();
            case RuleType.Maze_B3_S12345:
                return new MazeRule();
            case RuleType.Coral_B3_S45678_:
                return new CoralRule();
            case RuleType.TwoByTwo_B36_S125:
                return new TwoByTwoRule();
            case RuleType.Custom:
                return new CustomRule(customBirthNumbers, customSurviveNumbers);
            default:
                return new ConwayRule();
        }    
    }

    public void ChangeRule(RuleType newRuleType)
    {
        ruleType = newRuleType;
        ICellularAutomataRule newRule = CreateRule(newRuleType);
        grid = new GameOfLifeGrid(rows, cols, cellSize, newRule);
        grid.RandomInitialize(initialAliveProbability);
        gridRender.UpdateVisuals();
        generation = 0;
        timer = 0f;
        hasReachedLimit = false;
    }

    private ICellularAutomataRule CreateCustomRule()
    {
        // 使用自定义规则
        // 示例：创建一个 B36/S23 规则（HighLife）
        return new CustomRule(
            birthNumbers: new int[] { 3, 6 },    // 3或6个邻居时诞生
            surviveNumbers: new int[] { 2, 3 }   // 2或3个邻居时存活
        );
    }
}
