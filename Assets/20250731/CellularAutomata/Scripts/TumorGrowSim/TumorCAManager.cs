using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public enum VoxelTumorlState
{
    Empty,
    TumorCell,
    ApoptoticCell,
    NecroticCell
}
public class TumorCAManager : MonoBehaviour
{
    [Header("Cell")]
    public GameObject tumorPrefab;
    public Material tumorMaterial;

    [Header("Base")]
    public int gridSize = 30;
    public float cellSize = 1.0f;
    public Vector3 cellCenter= Vector3.zero;

    public VoxelTumorlState[,,] tumorGrid;
    private GameObject[,,] tumorCellObjects;
    [SerializeField] private float updateInterval = 0.5f;
    [SerializeField] int generation=0;
    private float timer = 0f;


    [Header("Environment")]
    public float oxygenThreshold = 0.2f;
    public float oxygenDiffusionRate = 0.1f;
    public float oxygenComsuptionRate = 0.05f;
    public float necroticThreshold = 4; // 细胞进入坏死状态的时间步长阈值
    public int apoptosisDelayThreshold = 2;//细胞进入坏死前的凋零状态的延迟时间步长
    public int necroticDisappearThreshold = 1;
    private int[,,] apoptosisSteps;//record how many step cell in apoptosis state
    private int[,,] necroticSteps;//record how many step cell in necrotic state
    private float[,,] oxygenGrid;//record how many oxygen in each cell

    [Header("Anisotropy Growth Settings")]
    public bool enableAnisotropicGrowth = true;
    [Tooltip("各个方向的生长偏好 (X, Y, Z)，值越大越容易生长")]
    public Vector3 growthDirectionBias = new Vector3(1.5f, 0.8f, 1.2f);
    [Tooltip("基于氧气梯度的生长，细胞倾向于向氧气高的方向生长")]
    public bool oxygenGradientGrowth = true;
    [Tooltip("氧气梯度权重")]
    public float oxygenGradientWeight = 0.3f;

    [Header("ProliferationRuleSetting")]
    public int minNeighbourForProliferation = 2;
    public int maxNeighbourForProliferation = 6;

    [Header("Oxygen Visualization")]
    public bool visualizeOxygen = true;
    public GameObject oxygenVisualizationPrefab;
    private GameObject[,,] oxygenVisualizationObjects;
    public Color highOxygenColor = new Color(0, 1, 1, 0.3f); 
    public Color lowOxygenColor = new Color(1, 0, 0, 0.3f); 
    private MaterialPropertyBlock oxygenPropertyBlock;

    [Header("Performance")]
    [Tooltip("氧气可视化更新阈值，变化小于此值不更新")]
    public float oxygenVisualizationThreshold = 0.01f;
    [Tooltip("每帧最多更新的氧气可视化数量")]
    public int maxOxygenUpdatesPerFrame = 100;


    private MaterialPropertyBlock[] materialPropertyBlocks = new MaterialPropertyBlock[4];  //normal,apoptosis,necrosis,empty
    private Renderer[,,] renderers;

    private VoxelTumorlState[,,] tempTumorGrid;
    private int[,,] tempApoptosisSteps;
    private int[,,] tempNecroticSteps;
    private float[,,] tempOxygenGrid;

    private VoxelTumorlState[,,] previousTumorState;
    private float[,,] previousOxygenLevels;

    private int oxygenUpdateIndex = 0;

    private static readonly Color WhiteColor = Color.white;
    private static readonly Color YellowColor = Color.yellow;
    private static readonly Color BlackColor = Color.black;
    private static readonly Color ClearColor = Color.clear;
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");




    // Start is called before the first frame update
    void Start()
    {
        InitializeGrid();
        InitializeTumor();
        InitializeOxygenGrid();
        InitlalizeTumaorVisual();

        if (visualizeOxygen)
        {
            InitializeOxygenVisualization();
        }
    }
    void Update()
    // Update is called once per frame
    {
        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            timer = 0f;
            UpdateOxygenGrid();
            UpdateTumorState();
            UpdateTumorVisual();

            if (visualizeOxygen)
            {
                UpdateOxygenVisualization();
            }
            generation++;
        }
    }


    #region Initialization Methods
    private void InitializeGrid()
    {
        tumorGrid = new VoxelTumorlState[gridSize, gridSize, gridSize];
        tumorCellObjects = new GameObject[gridSize, gridSize, gridSize];
        apoptosisSteps = new int[gridSize, gridSize, gridSize];
        necroticSteps = new int[gridSize, gridSize, gridSize];


        tempTumorGrid = new VoxelTumorlState[gridSize, gridSize, gridSize];
        tempApoptosisSteps = new int[gridSize, gridSize, gridSize];
        tempNecroticSteps = new int[gridSize, gridSize, gridSize];
        previousTumorState = new VoxelTumorlState[gridSize, gridSize, gridSize];

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    tumorGrid[x, y, z] = VoxelTumorlState.Empty;

                    float xCoord = x * cellSize - (gridSize / 2.0f * cellSize) + (cellSize / 2.0f);
                    float yCoord = y * cellSize - (gridSize / 2.0f * cellSize) + (cellSize / 2.0f);
                    float zCoord = z * cellSize - (gridSize / 2.0f * cellSize) + (cellSize / 2.0f);

                    Vector3 cellPosition = new Vector3(xCoord,yCoord,zCoord);
                    GameObject cell = Instantiate(tumorPrefab, cellPosition, Quaternion.identity,transform);
                    cell.transform.localScale = Vector3.one * cellSize * 0.9f;

                    cell.SetActive(false);

                    tumorCellObjects[x, y, z] = cell;
                    apoptosisSteps[x, y, z] = 0;
                    necroticSteps[x, y, z] = 0;

                }
            }
        }
    }

    private void InitializeTumor()
    {
        int center = gridSize / 2;
        int initialRadius = 3;

        for (int x = center - initialRadius; x <= center + initialRadius; x++)
        {
            for (int y = center - initialRadius; y <= center + initialRadius; y++)
            {
                for (int z = center - initialRadius; z <= center + initialRadius; z++)
                {
                    float distance = Vector3.Distance(new Vector3(x, y, z), new Vector3(center, center, center));
                    if (distance < initialRadius)
                    {
                        tumorGrid[x, y, z] = VoxelTumorlState.TumorCell;
                        previousTumorState[x, y, z] = VoxelTumorlState.TumorCell;
                        tumorCellObjects[x, y, z].SetActive(true);
                    }
                }
            }
        }
    }

    private void InitializeOxygenGrid()
    {
        int center = gridSize / 2;
        float maxDistance = gridSize / 2f;

        oxygenGrid = new float[gridSize, gridSize, gridSize];
        tempOxygenGrid = new float[gridSize, gridSize, gridSize];
        previousOxygenLevels = new float[gridSize, gridSize, gridSize];

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    OxygenDistrbutionRule(x, y, z, center, maxDistance);
                    previousOxygenLevels[x, y, z] = oxygenGrid[x, y, z];
                }
            }
        }
    }

    void OxygenDistrbutionRule(int x,int y,int z,int center,float maxDistance)
    {
        float distance = Vector3.Distance(new Vector3(x, y, z), new Vector3(center, center, center));
        oxygenGrid[x, y, z] = 1f - (distance / maxDistance);
        //oxygenGrid[x, y, z] =1.0f;
        oxygenGrid[x, y, z] = Mathf.Clamp01(oxygenGrid[x, y, z]);
    }

    private void InitlalizeTumaorVisual()
    {
        renderers = new Renderer[gridSize, gridSize, gridSize];

        if (tumorMaterial == null)
        {
            Material tumorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            tumorMat.name = "TumorMaterial";
            tumorMaterial = tumorMat;
        }

        for (int i = 0; i < materialPropertyBlocks.Length; i++)
        {
            materialPropertyBlocks[i] = new MaterialPropertyBlock();
        }

        materialPropertyBlocks[0].SetColor(BaseColorID, WhiteColor);
        materialPropertyBlocks[1].SetColor(BaseColorID, YellowColor);
        materialPropertyBlocks[2].SetColor(BaseColorID, BlackColor);
        materialPropertyBlocks[3].SetColor(BaseColorID, ClearColor);

        if (tumorCellObjects.Length != 0)
        {
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    for (int z = 0; z < gridSize; z++)
                    {
                        Renderer renderer = tumorCellObjects[x, y, z].GetComponent<Renderer>();
                        renderer.sharedMaterial = tumorMaterial;

                        switch (tumorGrid[x, y, z])
                        {
                            case VoxelTumorlState.TumorCell:
                                renderer.SetPropertyBlock(materialPropertyBlocks[0]);
                                break;
                            case VoxelTumorlState.ApoptoticCell:
                                renderer.SetPropertyBlock(materialPropertyBlocks[1]);
                                break;
                            case VoxelTumorlState.NecroticCell:
                                renderer.SetPropertyBlock(materialPropertyBlocks[2]);
                                break;
                            case VoxelTumorlState.Empty:
                                renderer.SetPropertyBlock(materialPropertyBlocks[3]);
                                break;   
                        }

                        renderers[x, y, z] = renderer;
                    }
                }
            }
        }

        
    }

    private void InitializeOxygenVisualization()
    {

         oxygenVisualizationObjects = new GameObject[gridSize, gridSize, gridSize];
         oxygenPropertyBlock = new MaterialPropertyBlock();

         for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    float xCoord = x * cellSize - (gridSize / 2.0f * cellSize) + (cellSize / 2.0f);
                    float yCoord = y * cellSize - (gridSize / 2.0f * cellSize) + (cellSize / 2.0f);
                    float zCoord = z * cellSize - (gridSize / 2.0f * cellSize) + (cellSize / 2.0f);

                    Vector3 cellPosition = new Vector3(xCoord, yCoord, zCoord);
                    GameObject oxygenCell = Instantiate(oxygenVisualizationPrefab, cellPosition, Quaternion.identity, transform);
                    oxygenCell.transform.localScale = Vector3.one * cellSize * 0.95f;
                    oxygenCell.name = $"OxygenVis_{x}_{y}_{z}";

                    // 移除碰撞器以避免干扰
                    if (oxygenCell.GetComponent<Collider>())
                        Destroy(oxygenCell.GetComponent<Collider>());

                    oxygenVisualizationObjects[x, y, z] = oxygenCell;

                    // 初始化颜色
                    UpdateOxygenVisualizationAt(x, y, z);
                }
            }
        }
    }

    #endregion


    #region Update Methods

    private void UpdateOxygenGrid()
    {
       

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    float neighbourAverageOxygen = GetNeighbourOxygenAvaerage(x, y, z);

                    tempOxygenGrid[x, y, z] = Mathf.Lerp(oxygenGrid[x, y, z], neighbourAverageOxygen, oxygenDiffusionRate);

                    if (tumorGrid[x, y, z] == VoxelTumorlState.TumorCell)
                    {
                        //Oxygen diffusion and consumption
                        //tempOxygenGrid[x, y, z] = Mathf.Clamp01(oxygenGrid[x, y, z] + oxygenDiffusionRate * (neighbourAverageOxygen - oxygenGrid[x, y, z]) - oxygenComsuptionRate);
                        tempOxygenGrid[x, y, z] -= oxygenComsuptionRate;
                    }

                    tempOxygenGrid[x,y,z]= Mathf.Clamp01(tempOxygenGrid[x, y, z]);
                }
            }
        }

        var swap = oxygenGrid;
        oxygenGrid = tempOxygenGrid;
        tempOxygenGrid = swap;

    }

    private float GetNeighbourOxygenAvaerage(int x, int y, int z)
    {
        float sum = 0f;
        int count = 0;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dy == 0 && dz == 0) continue;
                    int nx = x + dx;
                    int ny = y + dy;
                    int nz = z + dz;
                    if (nx >= 0 && nx < gridSize &&
                        ny >= 0 && ny < gridSize &&
                        nz >= 0 && nz < gridSize)
                    {
                        sum += oxygenGrid[nx, ny, nz];
                        count++;
                    }
                }
            }
        }

        return sum/count;
    }

    private void UpdateTumorState()
    {


        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    VoxelTumorlState currentState = tumorGrid[x, y, z];
                    int neightbourCount = GetNeighbourTumorCellCount(x, y, z);

                    float currentOxygen = oxygenGrid[x, y, z];
                    int currentApoptosisStep = apoptosisSteps[x, y, z];
                    int currentNecroticStep = necroticSteps[x, y, z];

                    tempTumorGrid[x, y, z] = currentState;
                    tempApoptosisSteps[x, y, z] = currentApoptosisStep;
                    tempNecroticSteps[x, y, z] = currentNecroticStep;

                    switch (currentState)
                    {
                        case VoxelTumorlState.Empty:
                            // Check for proliferation
                            if (neightbourCount > minNeighbourForProliferation  &&
                                neightbourCount <= maxNeighbourForProliferation &&
                                currentOxygen > oxygenThreshold)
                            {
                                float proliferationProbability = CalculateAnisotropicProliferationProbability(x, y, z);
                               
                                if (UnityEngine.Random.value < proliferationProbability)
                                {
                                    tempTumorGrid[x, y, z] = VoxelTumorlState.TumorCell;
                                    tumorCellObjects[x, y, z].SetActive(true);
                                }
                            }
                            break;
                        case VoxelTumorlState.TumorCell:
                            // Check for apoptosis
                            if (currentOxygen < oxygenThreshold)
                            {
                                tempTumorGrid[x, y, z] = VoxelTumorlState.ApoptoticCell;
                                tempApoptosisSteps[x, y, z]=1;
                            }
                            break;
                        case VoxelTumorlState.ApoptoticCell:
                            // Check for necrosis
                            tempApoptosisSteps[x, y, z]++;
                            if (currentApoptosisStep > necroticThreshold)
                            {
                                tempTumorGrid[x, y, z] = VoxelTumorlState.NecroticCell;
                            }
                            // else if (currentApoptosisStep > apoptosisDelayThreshold)
                            // {
                            //     // Remain apoptotic
                            // }

                            break;
                        case VoxelTumorlState.NecroticCell:
                             tempNecroticSteps[x, y, z]++;
                            if (currentNecroticStep > necroticDisappearThreshold)
                            {
                                tempTumorGrid[x, y, z] = VoxelTumorlState.Empty;
                                tumorCellObjects[x, y, z].SetActive(false);
                                tempNecroticSteps[x, y, z] = 0; // 重置坏死步数
                            }
                            break;
                    }
                }
            }
        }

        var swapTumor = tumorGrid;
        tumorGrid = tempTumorGrid;
        tempTumorGrid = swapTumor;
        
        var swapApoptosis = apoptosisSteps;
        apoptosisSteps = tempApoptosisSteps;
        tempApoptosisSteps = swapApoptosis;
        
        var swapNecrotic = necroticSteps;
        necroticSteps = tempNecroticSteps;
        tempNecroticSteps = swapNecrotic;
    }

    private float CalculateAnisotropicProliferationProbability(int x, int y, int z)
    {
         if (!enableAnisotropicGrowth)
        {
            return 1.0f;
        }

         int center = gridSize / 2;

        Vector3 directionFromCenter = new Vector3(x - center, y - center, z - center).normalized;

        // 应用方向偏好
        float directionBias = Mathf.Abs(directionFromCenter.x) * growthDirectionBias.x +
                              Mathf.Abs(directionFromCenter.y) * growthDirectionBias.y +
                              Mathf.Abs(directionFromCenter.z) * growthDirectionBias.z;

        float probability = directionBias / (growthDirectionBias.x + growthDirectionBias.y + growthDirectionBias.z);

         if (oxygenGradientGrowth)
        {
            Vector3 oxygenGradient = CalculateOxygenGradient(x, y, z);
            float gradientMagnitude = oxygenGradient.magnitude;
            
            // 氧气梯度越大，越容易生长（倾向于向高氧区域生长）
            probability += gradientMagnitude * oxygenGradientWeight;
        }

        return Mathf.Clamp01(probability);

    }

     private Vector3 CalculateOxygenGradient(int x, int y, int z)
    {
         Vector3 gradient = Vector3.zero;
       
        if (x > 0 && x < gridSize - 1)
        {
            gradient.x = (oxygenGrid[x + 1, y, z] - oxygenGrid[x - 1, y, z]) / 2f;
        }
        
       
        if (y > 0 && y < gridSize - 1)
        {
            gradient.y = (oxygenGrid[x, y + 1, z] - oxygenGrid[x, y - 1, z]) / 2f;
        }
        
        
        if (z > 0 && z < gridSize - 1)
        {
            gradient.z = (oxygenGrid[x, y, z + 1] - oxygenGrid[x, y, z - 1]) / 2f;
        }
        
        return gradient;

        
    }
    private int GetNeighbourTumorCellCount(int x, int y, int z)
    {
        int count = 0;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dy == 0 && dz == 0) continue;
                    int nx = x + dx;
                    int ny = y + dy;
                    int nz = z + dz;
                    if (nx >= 0 && nx < gridSize &&
                        ny >= 0 && ny < gridSize &&
                        nz >= 0 && nz < gridSize)
                    {
                        if (tumorGrid[nx, ny, nz] == VoxelTumorlState.TumorCell)
                        { 
                            count++;
                        }
                    }
                }
            }
        }
        return count;
    }

    private void UpdateTumorVisual()
    {

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    VoxelTumorlState currentState = tumorGrid[x, y, z];

                    if (currentState != previousTumorState[x, y, z])
                    {
                        Renderer currentRenderer = renderers[x, y, z];

                        switch (currentState)
                        {
                            case VoxelTumorlState.TumorCell:
                                materialPropertyBlocks[0].SetColor("_BaseColor", Color.white);
                                currentRenderer.SetPropertyBlock(materialPropertyBlocks[0]);
                                break;
                            case VoxelTumorlState.ApoptoticCell:
                                materialPropertyBlocks[1].SetColor("_BaseColor", Color.yellow);
                                currentRenderer.SetPropertyBlock(materialPropertyBlocks[1]);
                                break;
                            case VoxelTumorlState.NecroticCell:
                                materialPropertyBlocks[2].SetColor("_BaseColor", Color.black);
                                currentRenderer.SetPropertyBlock(materialPropertyBlocks[2]);
                                break;
                            case VoxelTumorlState.Empty:
                                materialPropertyBlocks[3].SetColor("_BaseColor", Color.clear);
                                currentRenderer.SetPropertyBlock(materialPropertyBlocks[3]);
                                break;
                        }

                        previousTumorState[x, y, z] = currentState;

                    }

                    
                }
            }
        }
    }

    private void UpdateOxygenVisualizationAt(int x, int y, int z, bool forceUpdate = false)
    {
        if (!visualizeOxygen || oxygenVisualizationObjects == null) return;

        float oxygenLevel = oxygenGrid[x, y, z];

        if (!forceUpdate && Mathf.Abs(oxygenLevel - previousOxygenLevels[x, y, z]) < oxygenVisualizationThreshold)
        {
            return;
        }

        Color color = Color.Lerp(lowOxygenColor, highOxygenColor, oxygenLevel);

        Renderer renderer = oxygenVisualizationObjects[x, y, z].GetComponent<Renderer>();
        oxygenPropertyBlock.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(oxygenPropertyBlock);
    }

     private void UpdateOxygenVisualizationProgressive()
    {
        if (!visualizeOxygen || oxygenVisualizationObjects == null) return;
        
        int totalCells = gridSize * gridSize * gridSize;
        int updatesThisFrame = 0;
        
        // 每帧只更新一部分
        while (updatesThisFrame < maxOxygenUpdatesPerFrame && oxygenUpdateIndex < totalCells)
        {
            int x = oxygenUpdateIndex / (gridSize * gridSize);
            int remainder = oxygenUpdateIndex % (gridSize * gridSize);
            int y = remainder / gridSize;
            int z = remainder % gridSize;
            
            UpdateOxygenVisualizationAt(x, y, z);
            
            oxygenUpdateIndex++;
            updatesThisFrame++;
        }
        
        // 完成一轮后重置
        if (oxygenUpdateIndex >= totalCells)
        {
            oxygenUpdateIndex = 0;
        }
    }

    private void UpdateOxygenVisualization()
    {
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    UpdateOxygenVisualizationAt(x, y, z);
                }
            }
        }
    }
    #endregion


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
       //绘制边界
        Gizmos.DrawWireCube(cellCenter, new Vector3(gridSize * cellSize, gridSize * cellSize, gridSize * cellSize));
    }


    private void DestroyCells()
    {
        if (tumorCellObjects != null && tumorCellObjects.Length > 0)
        {
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    for (int z = 0; z < gridSize; z++)
                    {
                        if (tumorCellObjects[x, y, z] != null)
                            Destroy(tumorCellObjects[x, y, z]);
                    }
                }
            }
        }
        
        if (oxygenVisualizationObjects != null)
        {
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    for (int z = 0; z < gridSize; z++)
                    {
                        if (oxygenVisualizationObjects[x, y, z] != null)
                            Destroy(oxygenVisualizationObjects[x, y, z]);
                    }
                }
            }
        }

        tumorCellObjects = null;
        tumorGrid = null;
        apoptosisSteps= null;
        necroticSteps= null;
        oxygenGrid = null;
        tempTumorGrid = null;
        tempApoptosisSteps = null;
        tempNecroticSteps = null;
        tempOxygenGrid = null;
        previousTumorState = null;
        previousOxygenLevels = null;
        oxygenVisualizationObjects = null;

    }


    [ContextMenu("Resimulate")]
    private void ReSimulate()
    {

        DestroyCells();
        InitializeGrid();
        InitializeTumor();
        InitializeOxygenGrid();
        InitlalizeTumaorVisual();

        if (visualizeOxygen)
        {
            InitializeOxygenVisualization();
        }

        generation = 0;
        timer = 0f;
        oxygenUpdateIndex = 0;
    }

     [ContextMenu("Toggle Oxygen Visualization")]
    private void ToggleOxygenVisualization()
    {
        visualizeOxygen = !visualizeOxygen;
        
        if (oxygenVisualizationObjects != null)
        {
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    for (int z = 0; z < gridSize; z++)
                    {
                        if (oxygenVisualizationObjects[x, y, z] != null)
                            oxygenVisualizationObjects[x, y, z].SetActive(visualizeOxygen);
                    }
                }
            }
        }
        else if (visualizeOxygen)
        {
            InitializeOxygenVisualization();
        }
    }
}
