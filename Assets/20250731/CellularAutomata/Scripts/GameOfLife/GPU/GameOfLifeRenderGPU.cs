using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class GameOfLifeRenderGPU : MonoBehaviour
{

    [Header("Rendering")]
    [SerializeField] private Mesh cellMesh;
    [SerializeField] private Material cellMaterial;
    [SerializeField] private Color aliveColor = Color.white;
    [SerializeField] private Color deadColor = Color.black;
    [SerializeField] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;
    
    

    private GameOfLifeGridGPU grid;
    private ComputeBuffer positionBuffer;
    private ComputeBuffer uvBuffer;
    private ComputeBuffer argsBuffer;

    private MaterialPropertyBlock propertyBlock;
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

    private static readonly int PositionBufferID = Shader.PropertyToID("_PositionBuffer");
    private static readonly int UVBufferID = Shader.PropertyToID("_UVBuffer");
    private static readonly int StateTextureID = Shader.PropertyToID("_StateTexture");
    private static readonly int AliveColorID = Shader.PropertyToID("_AliveColor");
    private static readonly int DeadColorID = Shader.PropertyToID("_DeadColor");

    public void Initialize(GameOfLifeGridGPU grid)
    {
        this.grid = grid;
        CreateBuffers();
        SetupMaterial();
    }

    private void CreateBuffers()
    {
        int instanceCount = grid.Rows * grid.Cols;

        Vector3[] positions = new Vector3[instanceCount];
        Vector2[] uvs = new Vector2[instanceCount];

        int index = 0;
        for (int i = 0; i < grid.Rows; i++)
        {
            for (int j = 0; j < grid.Cols; j++)
            {
                float posX = j * grid.CellSize - (grid.Rows / 2.0f - 0.5f * grid.CellSize);
                float posZ = i * grid.CellSize - (grid.Cols / 2.0f - 0.5f * grid.CellSize);
                positions[index] = new Vector3(posX, 0, posZ);
                uvs[index] = new Vector2((j + 0.5f) / grid.Cols, (i + 0.5f) / grid.Rows);
                index++;
            }
        }
        positionBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 3);
        positionBuffer.SetData(positions);

        uvBuffer = new ComputeBuffer(instanceCount, sizeof(float) * 2);
        uvBuffer.SetData(uvs);

        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        args[0] = (uint)cellMesh.GetIndexCount(0);
        args[1] = (uint)instanceCount;
        args[2] = (uint)cellMesh.GetIndexStart(0);
        args[3] = (uint)cellMesh.GetBaseVertex(0);
        argsBuffer.SetData(args);

    }

    private void SetupMaterial()
    {
         if (cellMaterial == null)
        {
            Debug.LogError("Cell material is not assigned!");
            return;
        }

        propertyBlock = new MaterialPropertyBlock();
        propertyBlock.SetBuffer(PositionBufferID, positionBuffer);
        propertyBlock.SetBuffer(UVBufferID, uvBuffer);
        propertyBlock.SetTexture(StateTextureID, grid.CurrentStateTexture);
        propertyBlock.SetColor(AliveColorID, aliveColor);
        propertyBlock.SetColor(DeadColorID, deadColor);
    }

     public void Render()
    {
        if (cellMaterial == null || cellMesh == null || grid == null)
            return;

        propertyBlock.SetTexture(StateTextureID, grid.CurrentStateTexture);
        Graphics.DrawMeshInstancedIndirect(
                                            cellMesh, 
                                            0, 
                                            cellMaterial, 
                                            new Bounds(Vector3.zero, Vector3.one * 1000), 
                                            argsBuffer,
                                            0, 
                                            propertyBlock, 
                                            shadowCastingMode
                                           
         );
    }

    private void OnDestroy()
    {
        Cleanup();
    }
    
    public void Cleanup()
    {
        positionBuffer?.Release();
        uvBuffer?.Release();
        argsBuffer?.Release();
    }


}
