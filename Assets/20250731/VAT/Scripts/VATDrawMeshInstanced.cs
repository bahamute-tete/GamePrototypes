using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VATDrawMeshInstanced : MonoBehaviour
{
   
    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;
    [SerializeField] private int instanceCount = 1000;
    Matrix4x4[] matrices;

    MaterialPropertyBlock MaterialPropertyBlock;
    float[] biasTimes;

    ComputeBuffer offsetBuffer;



    void Start()
    {
        matrices = new Matrix4x4[instanceCount];
        biasTimes = new float[instanceCount];
        offsetBuffer = new ComputeBuffer(instanceCount, sizeof(float));
        MaterialPropertyBlock= new MaterialPropertyBlock();

      
        material.enableInstancing = true;

        GenerateMatrices();
        MPBSetting();
    }

    void GenerateMatrices()
    {
        for (int i = 0; i < instanceCount; i++)
        {
            Vector3 position = new Vector3(Random.Range(-25f, 25f), 0.0f, Random.Range(-25f, 25f));
            Quaternion rotation = Quaternion.Euler(0, Random.Range(0f, 360f),0);
            Vector3 scale = Vector3.one;
            matrices[i] = Matrix4x4.TRS(position, rotation, scale);
        }
    }

    void MPBSetting()
    {
        for (int i = 0; i < instanceCount; i++)
        {
           float biasTime = Random.Range(1f, 100f);
           biasTimes[i] = biasTime;
        }

        offsetBuffer.SetData(biasTimes);
        MaterialPropertyBlock.SetBuffer("_gameTimeAtFirstFrameBuffer", offsetBuffer);
    }

    // Update is called once per frame
    void Update()
    {
        Graphics.DrawMeshInstanced(mesh, 0, material, matrices, instanceCount, MaterialPropertyBlock);

    }

    private void OnDestroy()
    {
        offsetBuffer.Release();
    }
}
    