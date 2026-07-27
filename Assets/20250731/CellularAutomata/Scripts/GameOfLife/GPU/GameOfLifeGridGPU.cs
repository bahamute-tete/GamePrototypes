using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameOfLifeGridGPU : System.IDisposable
{

    public int Rows { get; private set; }
    public int Cols { get; private set; }
    public float CellSize { get; private set; }
    
    private ComputeShader computeShader;
    private RenderTexture currentStateTexture;
    private RenderTexture nextStateTexture;
    
    private int initKernel;
    private int updateKernel;
    private int clearKernel;
    private int placePatternKernel;
    
    private int widthID;
    private int heightID;
    private int aliveProbabilityID;
    private int randomSeedID;

    private int patternCountID;
    private int patternOffsetID;
    private int patternPositionsID;

    public RenderTexture CurrentStateTexture => currentStateTexture;

    public GameOfLifeGridGPU(int rows, int cols, float cellSize, ComputeShader computeShader)
    {
        this.Rows = rows;
        this.Cols = cols;
        this.CellSize = cellSize;
        this.computeShader = computeShader;
        
        InitializeComputeShader();
        CreateTextures();
    }

    private void InitializeComputeShader()
    {
        initKernel = computeShader.FindKernel("InitializeRandom");
        updateKernel = computeShader.FindKernel("UpdateCells");
        clearKernel = computeShader.FindKernel("ClearCells");

        placePatternKernel = computeShader.FindKernel("PlacePattern");
        
        widthID = Shader.PropertyToID("width");
        heightID = Shader.PropertyToID("height");
        aliveProbabilityID = Shader.PropertyToID("probability");
        randomSeedID = Shader.PropertyToID("seed");

        patternCountID = Shader.PropertyToID("patternCount");
        patternOffsetID = Shader.PropertyToID("patternOffset");
        patternPositionsID = Shader.PropertyToID("patternPositions");
        
        computeShader.SetInt(widthID, Cols);
        computeShader.SetInt(heightID, Rows);
    }

    public void PlacePattern(Vector2Int[] cells, int offsetX, int offsetY)
    {
        if (cells == null || cells.Length == 0) return;

        ComputeBuffer buffer = new ComputeBuffer(cells.Length, sizeof(int) * 2); // 2 integers per cell (x, y)
        buffer.SetData(cells);

        computeShader.SetBuffer(placePatternKernel, patternPositionsID, buffer);
        computeShader.SetInt(patternCountID, cells.Length);
        computeShader.SetInts(patternOffsetID, offsetX, offsetY);
        computeShader.SetTexture(placePatternKernel, "currentState", currentStateTexture);

        int threadGroups = Mathf.CeilToInt(cells.Length / 8.0f);
        computeShader.Dispatch(placePatternKernel, threadGroups, 1, 1);

        buffer.Release();
    }

     private void CreateTextures()
    {
        currentStateTexture = CreateStateTexture();
        nextStateTexture = CreateStateTexture();

        // ClearTexture(currentStateTexture);
        // ClearTexture(nextStateTexture);
    }

    private RenderTexture CreateStateTexture()
    {
        RenderTexture rt = new RenderTexture(Cols, Rows, 0, RenderTextureFormat.RFloat);
        rt.enableRandomWrite = true;
        rt.filterMode = FilterMode.Point;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.Create();
        return rt;
    }

    private void ClearTexture(RenderTexture rt)
    {
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = null;

    }
    public void RandomInitialize(float aliveProbability)
    {
        computeShader.SetFloat(aliveProbabilityID, aliveProbability);
        computeShader.SetInt(randomSeedID, Random.Range(0, 100000));
        
        computeShader.SetTexture(initKernel, "currentState", currentStateTexture);
        
        int threadGroupsX = Mathf.CeilToInt(Cols / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Rows / 8.0f);
        
        computeShader.Dispatch(initKernel, threadGroupsX, threadGroupsY, 1);
    }

    public void UpdateStates()
    {
        computeShader.SetTexture(updateKernel, "readState", currentStateTexture);
        computeShader.SetTexture(updateKernel, "nextState", nextStateTexture);
        
        int threadGroupsX = Mathf.CeilToInt(Cols / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Rows / 8.0f);
        
        computeShader.Dispatch(updateKernel, threadGroupsX, threadGroupsY, 1);
        
        // 交换纹理
        SwapTextures();
    }

    private void SwapTextures()
    {
        var temp = currentStateTexture;
        currentStateTexture = nextStateTexture;
        nextStateTexture = temp;
    }

    public void Clear()
    {
        computeShader.SetTexture(clearKernel, "currentState", currentStateTexture);
        
        int threadGroupsX = Mathf.CeilToInt(Rows / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Cols / 8.0f);
        
        computeShader.Dispatch(clearKernel, threadGroupsX, threadGroupsY, 1);
    }
    
    public void Dispose()
    {
        if (currentStateTexture != null)
        {
            currentStateTexture.Release();
            Object.Destroy(currentStateTexture);
        }
        
        if (nextStateTexture != null)
        {
            nextStateTexture.Release();
            Object.Destroy(nextStateTexture);
        }
    }


}
