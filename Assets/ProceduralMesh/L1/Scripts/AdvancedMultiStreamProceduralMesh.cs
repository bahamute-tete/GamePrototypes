using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;//定义需要的数据格式NativeArray
using UnityEngine.Rendering;//定义数据格式VertexAttributeDescriptor
using Unity.Mathematics;
using static Unity.Mathematics.math;

[RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
public class AdvancedMultiStreamProceduralMesh : MonoBehaviour
{
    private void OnEnable()
    {
        int vertexAttibuteCount = 4;//数据流里的类型的数量==》位置，法线，切线，UV
        int vertexCount = 4; //四个顶点
        int triangelIndexCount = 6;//三角形索引

        // 使用AllocateWritableMeshData 方法生成一个 网格的数据结构体数组， arg=网格数量
        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        //检索数据结构组里的第一个数据
        Mesh.MeshData meshData = meshDataArray[0];

        //临时分配一个原生数组 数组类型是VertexAttributeDescriptor
        var vertexAttibutes = new NativeArray<VertexAttributeDescriptor>(
            vertexAttibuteCount,
            Allocator.Temp,
            NativeArrayOptions.UninitializedMemory);//默认会用0初始化填充内存块，但是可以跳过去这一步


        //描述四个属性:类型，格式，维度，包含该属性的流的索引，默认是位置，因为是顶点所以维度是3
        vertexAttibutes[0] = new VertexAttributeDescriptor(VertexAttribute.Position,dimension: 3,stream:0);
        
        vertexAttibutes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, dimension: 3,stream:1);
        //第二个参数降低顶点数据的大小,默认是float32
        vertexAttibutes[2] = new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float16,dimension: 4,stream:2);
        //第二个参数降低顶点数据的大小，默认是float32
        vertexAttibutes[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16,dimension: 2,stream:3);

        //分配网格的顶点数据流
        meshData.SetVertexBufferParams(vertexCount, vertexAttibutes);
        //之后不再需要了
        vertexAttibutes.Dispose();

        //获取位置数据 返回的原生数组实际上是指向网格数据组相关部分指针
        NativeArray<float3> positions = meshData.GetVertexData<float3>(0);
        positions[0] = 0f;
        positions[1] = right();
        positions[2] = up();
        positions[3] = float3(1f, 1f, 0f);

        NativeArray<float3> normals = meshData.GetVertexData<float3>(1);
        normals[0] = back();
        normals[1] = back();
        normals[2] = back();
        normals[3] = back();

        //数据类型同样需要降低精读,但是half不是Native的类型，所以需要转换
        half h0 = half(0f), h1 = half(1f);
        NativeArray<half4> tangents = meshData.GetVertexData<half4>(2);
        tangents[0] = half4(h1, h0, h0, half(-1f));
        tangents[1] = half4(h1, h0, h0, half(-1f));
        tangents[2] = half4(h1, h0, h0, half(-1f));
        tangents[3] = half4(h1, h0, h0, half(-1f));

        NativeArray<half2> texCoords = meshData.GetVertexData<half2>(3);
        texCoords[0] = h0;
        texCoords[1] = half2(h1,h0);
        texCoords[2] = half2(h0,h1);
        texCoords[3] = h1;

        //设置三角形索引，第一个参数是三角形索引计数，第二个是索引的格式，Uint32与Uint匹配
        //这种设置会导致空间变多，因为是32位的无符号整型
        //Unity 默认使用16位
        meshData.SetIndexBufferParams(triangelIndexCount, IndexFormat.UInt16);

        //获取三角形索引
        //IndexFormat.UInt32 匹配uint    IndexFormat.UInt16匹配ushort
        NativeArray<ushort> triangleIndices = meshData.GetIndexData<ushort>();
        triangleIndices[0] = 0;
        triangleIndices[1] = 2;
        triangleIndices[2] = 1;
        triangleIndices[3] = 1;
        triangleIndices[4] = 2;
        triangleIndices[5] = 3;

        //设置子网格数量
        meshData.subMeshCount = 1;
        //边界
        var bounds = new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 0f));
        //构造函数SubMeshDescriptor有两个参数，分别是索引开始和索引计数。在我们的例子中，它应该涵盖所有索引
        //这种方法Unity不会计算边界，但是确实会计算，需要设置MeshUpdateFlags.DontRecalculateBounds
        meshData.SetSubMesh(0,
            new SubMeshDescriptor(0,
            triangelIndexCount){
                                bounds = bounds,
                                vertexCount= vertexCount},
            MeshUpdateFlags.DontRecalculateBounds);

        Mesh mesh = new() { name = "ProceduralMesh",bounds= bounds };

        //清空数据，参数是数据组和需要绑定数据的网格，此后数据就不能在访问了 需要通过
        //Mesh.AcquireReadOnlyMeshData 检索
        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);

        GetComponent<MeshFilter>().mesh = mesh;
    }
}
