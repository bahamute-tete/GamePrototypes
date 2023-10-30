using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProceduralMeshes;
using ProceduralMeshes.Streams;
using ProceduralMeshes.Generators;
using UnityEngine.Rendering;
using System;

[RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
public class ProceduralMesh : MonoBehaviour
{

    static MeshJobScheduleDelegate[] jobs =
    {
        MeshJob<SquareGrid,SingleStream>.ScheduleParallel,
        MeshJob<SharedSquareGrid,SingleStream>.ScheduleParallel
    };

    public enum MeshType {
        SquareGrid,
        SharedSquareGrid
    }

    [SerializeField]
    MeshType meshType;

    Mesh mesh;

    [SerializeField, Range(1, 50)]
    int resolution = 1;
    
    private void Awake()
    {
        mesh = new Mesh() { name = "ProceduralMesh" };
        
        GetComponent<MeshFilter>().mesh = mesh;
    }

    private void GenerateMesh()
    {
        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        Mesh.MeshData meshData = meshDataArray[0];


        jobs[(int)meshType](mesh, meshData, resolution, default).Complete();

        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
    }

    private void OnValidate()
    {
        //启用在比方模式下更改网格
        enabled = true;
    }

    private void Update()
    {
        GenerateMesh();
        enabled = false;
    }
}
