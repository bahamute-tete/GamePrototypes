using System.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;

using Unity.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;

using Unity.Burst;
using Unity.Jobs;


namespace ProceduralMeshes
{
    //顶点数据结构
    public struct Vertex
    {
        public float3 position, normal;
        public float4 tangent;
        public float2 texCoords0;
    };

    public delegate JobHandle MeshJobScheduleDelegate(
         Mesh mesh, Mesh.MeshData meshData, int resoulution, JobHandle dependency
         );


    public struct MeshJob<G, S> : IJobFor where G : struct, IMeshGenerator where S : struct, IMeshStreams
    {
        G generator;
        [WriteOnly]
        S streams;

        //Execute方法只是将调用转发给生成器，并向其传递索引和流
        public void Execute(int i)
        {
            generator.Execute(i, streams);
        }

        public static JobHandle ScheduleParallel(Mesh mesh,Mesh.MeshData meshData,int resolution, JobHandle dependency)
        {
            var job = new MeshJob<G, S>();
            job.generator.Resolution = resolution;
            job.streams.Setup(  meshData,
                                job.generator.Bounds,
                                job.generator.VertexCount,
                                job.generator.IndexCount);

            //工作数据,数组长度,内循环批次计数(batchSize),依赖性
            return job.ScheduleParallel(job.generator.JobLength, 1, dependency);
        }
    }

    //定义顶点和索引缓冲区，引入接口来隔离代码，而不是为每个Job 显示定义
    public interface IMeshStreams {
        //网格数据，顶点计数，索引计数
        //初始化网格数据
        void Setup(Mesh.MeshData data, Bounds bounds,int vertexCount, int indexCount);

        //将顶点设置到网格顶点缓冲区
        void SetVertex(int index, Vertex data);
        //使用三角形索引所以有int3
        void SetTriangle(int index, int3 triangle);
    }


    public interface IMeshGenerator
    {
        void Execute<S>(int i, S stream) where S : struct, IMeshStreams;

        int VertexCount { get; }

        int IndexCount { get; }

        //获得Job的长度
        int JobLength { get; }

        Bounds Bounds { get; }

        int Resolution { get; set; }
    }


}



namespace ProceduralMeshes.Streams
{
    public struct SingleStream : IMeshStreams
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct TriangleUint16
        {
            public ushort a, b, c;
            public static implicit operator TriangleUint16(int3 t) => new TriangleUint16
            {
                a = (ushort)t.x,
                b = (ushort)t.y,
                c = (ushort)t.z

            };
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Stream0
        {
            public float3 position, normal;
            public float4 tangent;
            public float2 texCoord0;
        }

        //禁用安全性 
        [NativeDisableContainerSafetyRestriction]
        NativeArray<Stream0> stream0;

        [NativeDisableContainerSafetyRestriction]
        NativeArray<TriangleUint16> triangles;

        public void Setup(Mesh.MeshData meshData,Bounds bounds, int vertexCount, int indexCount)
        {
            var descriptor = new NativeArray<VertexAttributeDescriptor>
            (4, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            descriptor[0] = new VertexAttributeDescriptor(dimension:3);

            descriptor[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, dimension: 3);

            descriptor[2] = new VertexAttributeDescriptor(VertexAttribute.Tangent, dimension: 4);

            descriptor[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, dimension: 2);

            meshData.SetVertexBufferParams(vertexCount, descriptor);
            descriptor.Dispose();

            meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt16);

            meshData.subMeshCount = 1;
            //setSubMesh会立即验证三角形索引并重新计算边界，因为还没有计算，索引缓冲区包含任意数据。
            //需要告诉SetSubMesh不对数据执行操作
            //使用二元符号合并2个选项
            meshData.SetSubMesh(0,
                new SubMeshDescriptor(0, indexCount) {bounds= bounds,vertexCount= vertexCount },
                MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontValidateIndices);

            stream0 = meshData.GetVertexData<Stream0>();
            //重新索引数据解释为三角形数据 ,The expected size (in bytes, as given by sizeof)
            triangles = meshData.GetIndexData<ushort>().Reinterpret<TriangleUint16>(2);
        }

        

        //指示 Burst 始终内联插入整个代码，而不是进行调用
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVertex(int index, Vertex vertex)
        {
            stream0[index] = new Stream0
            {
                position = vertex.position,
                normal = vertex.normal,
                tangent = vertex.tangent,
                texCoord0 = vertex.texCoords0
            };

           
        }

        public void SetTriangle(int index, int3 triangle)
        {
            triangles[index] = triangle;
        }
    }

    public struct MultiStream : IMeshStreams
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct TriangleUint16
        {
            public ushort a, b, c;
            public static implicit operator TriangleUint16(int3 t) => new TriangleUint16
            {
                a = (ushort)t.x,
                b = (ushort)t.y,
                c = (ushort)t.z

            };
        }

        [NativeDisableContainerSafetyRestriction]
        NativeArray<TriangleUint16> triangles;

        [NativeDisableContainerSafetyRestriction]
        NativeArray<float3> stream0, stream1;

        [NativeDisableContainerSafetyRestriction]
        NativeArray<float4> stream2;

        [NativeDisableContainerSafetyRestriction]
        NativeArray<float2> stream3;


        public void Setup(Mesh.MeshData meshData, Bounds bounds, int vertexCount, int indexCount)
        {
            var descriptor = new NativeArray<VertexAttributeDescriptor>
            (4, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            descriptor[0] = new VertexAttributeDescriptor(dimension: 3);

            descriptor[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, dimension: 3,stream:1);

            descriptor[2] = new VertexAttributeDescriptor(VertexAttribute.Tangent, dimension: 4,stream:2);

            descriptor[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, dimension: 2,stream:3);

            meshData.SetVertexBufferParams(vertexCount, descriptor);
            descriptor.Dispose();

            meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt16);

            meshData.subMeshCount = 1;
            //setSubMesh会立即验证三角形索引并重新计算边界，因为还没有计算，索引缓冲区包含任意数据。
            //需要告诉SetSubMesh不对数据执行操作
            //使用二元符号合并2个选项
            meshData.SetSubMesh(0,
                new SubMeshDescriptor(0, indexCount) { bounds = bounds, vertexCount = vertexCount },
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            stream0 = meshData.GetVertexData<float3>(0);
            stream1 = meshData.GetVertexData<float3>(1);
            stream2 = meshData.GetVertexData<float4>(2);
            stream3 = meshData.GetVertexData<float2>(3);

            //重新索引数据解释为三角形数据 ,The expected size (in bytes, as given by sizeof) 
            triangles = meshData.GetIndexData<ushort>().Reinterpret<TriangleUint16>(2);
        }



        //指示 Burst 始终内联插入整个代码，而不是进行调用
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVertex(int index, Vertex vertex)
        {
            stream0[index] = vertex.position;
            stream1[index] = vertex.normal;
            stream2[index] = vertex.tangent;
            stream3[index] = vertex.texCoords0;
        }

        public void SetTriangle(int index, int3 triangle)
        {
            triangles[index] = triangle;
        }
    }

}


namespace ProceduralMeshes.Generators
{
    //每个四边形不共享顶点
    public struct SquareGrid : IMeshGenerator
    {
        public int Resolution { get; set; }

        public int VertexCount => 4 * Resolution * Resolution;

        public int IndexCount => 6 * Resolution* Resolution;

        public int JobLength => 1 * Resolution;//方块的个数-->调整为沿着X轴的一整行

        public Bounds Bounds => new Bounds(Vector3.zero, new Vector3(1f, 0f,1f));

        
        //z调整为按Z方向的偏移量，而不是按一个一个的面片
        public void Execute<S>(int z, S stream) where S : struct, IMeshStreams
        {
            int vi = 4 * z *Resolution , ti = 2 * z *Resolution;//4个顶点，2个三角形

            ////确定每一块的坐标
            //int z = i / Resolution;
            //int x = i - Resolution * z;

            //按一行来循环
            //一行循环完后顶点计数+4
            for (int x = 0; x < Resolution; x++,vi+=4,ti+=2)
            {
                //设定每一块的4个顶点的坐标偏移
                //并归一化后将坐标中心挪到网格的中间
                //分开写是为了Burst优化，Burst会将循环内不变的量提取出去，所以对z 坐标进行一点优化
                var xCoordinates = float2(x, x + 1f) / Resolution - 0.5f;
                var zCoordinates = float2(z, z + 1f) / Resolution - 0.5f;
                //已经初始化为0 了，所以只设定非0的部分
                var vertex = new Vertex();
                vertex.normal.y = 1f;
                vertex.tangent.xw = float2(1f, -1f);

                vertex.position.x = xCoordinates.x;
                vertex.position.z = zCoordinates.x;
                stream.SetVertex(vi + 0, vertex);

                vertex.position.x = xCoordinates.y;
                vertex.texCoords0 = float2(1f, 0f);
                stream.SetVertex(vi + 1, vertex);

                vertex.position.x = xCoordinates.x;
                vertex.position.z = zCoordinates.y;
                vertex.texCoords0 = float2(0f, 1f);
                stream.SetVertex(vi + 2, vertex);

                vertex.position.x = xCoordinates.y;
                vertex.texCoords0 = 1f;
                stream.SetVertex(vi + 3, vertex);

                stream.SetTriangle(ti + 0, vi + int3(0, 2, 1));
                stream.SetTriangle(ti + 1, vi + int3(1, 2, 3));
            }
           

        }
    }

    //共享顶点
    public struct SharedSquareGrid : IMeshGenerator
    {
        public int Resolution { get; set; }

        public int VertexCount =>(Resolution+1) * (Resolution+1);

        public int IndexCount => 6 * Resolution * Resolution;

        public int JobLength => Resolution+1;//顶点行数-->调整为沿着X轴的一整行 3*3 的片就是4行

        public Bounds Bounds => new Bounds(Vector3.zero, new Vector3(1f, 0f, 1f));


        //z调整为按Z方向的偏移量,z代表的是顶点的行数
        public void Execute<S>(int z, S stream) where S : struct, IMeshStreams
        {
            ////顶点数
            int vi = (Resolution + 1) * z;

            //////////设定三角形
            ///
            int ti = 2 * Resolution * (z - 1);
            /////
            //    -1*****0
            //      *   *
            //      *   *
            //      *   *
            //  -R-2*****-R-1


            var vertex = new Vertex();
            vertex.normal.y = 1f;
            vertex.tangent.xw = float2(1f, -1f);

            //最左边的X坐标是-0.5f
            vertex.position.x = -0.5f;
            //最下面的坐标的是-0.5f
            vertex.position.z = (float)z / Resolution - 0.5f;
            //UV
            vertex.texCoords0.y = (float)z / Resolution;
            stream.SetVertex(vi, vertex);

            //第一个顶点已经设置了，所以从第二个开始
            vi += 1;
            for (int x = 1; x <= Resolution; x++, vi++,ti+=2)
            {
                vertex.position.x = (float)x /Resolution-0.5f;
                vertex.texCoords0.x = (float)x / Resolution;
                stream.SetVertex(vi, vertex);

                //第一排是没有三角形的
                if (z > 0)
                {
                    //以右上角为0 点
                    stream.SetTriangle(
                        ti + 0, vi + int3(-Resolution - 2, -1, -Resolution - 1)
                        );
                    stream.SetTriangle(
                        ti + 1, vi + int3(-Resolution - 1, -1, 0)
                        ); ;
                }
               
            }

      



        }
    }
}

