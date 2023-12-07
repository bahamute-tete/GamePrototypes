using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections;
using Mono.Cecil;

public struct Maze 
{
    int2 size;

    //disable the access restrictions for parallel jobs
    [NativeDisableParallelForRestriction]
    NativeArray<MazeFlags> cells;
    public int Length => cells.Length;

    public Maze(int2 size)
    {
        this.size = size;
        cells = new NativeArray<MazeFlags>(size.x * size.y, Allocator.Persistent);
    }

    public int2 IndexToCoordinates(int index)
    {
        int2 coordinates;

        coordinates.y = index / size.x;
        coordinates.x = index - size.x * coordinates.y;

        //coordinates.x = index % size.x;
        //coordinates.y = (index - coordinates.x) / size.x;
        return coordinates;
    }

    public Vector3 CoordinatesToWorldPosition(int2 coordinates, float y = 0f)
    {
        return new Vector3(coordinates.x * 2.0f + 1f - size.x, y, coordinates.y * 2f + 1f - size.y);
    }

    public Vector3 IndexToWorldPosition(int index, float y = 0f)
    { 
        return CoordinatesToWorldPosition(IndexToCoordinates(index), y);
    }

    public void Dispose()
    {
        if (cells.IsCreated)
        {
            cells.Dispose();
        }
    }

    //��������Indexer����������ͨ�����ʵ�������������Ԫ��һ�����ʻ����ö���ĳ�Ա
    public MazeFlags this[int index]
    { 
        get=> cells[index];
        set => cells[index] = value;
    }

    public MazeFlags Set(int index, MazeFlags mask) =>
        cells[index] = cells[index].With(mask);

    public MazeFlags Unset(int index, MazeFlags mask) =>
        cells[index] = cells[index].Without(mask);

    //�Թ���������ϱ���ߴ�� getter ����
    //�ĸ������ϵ�����ƫ�Ʋ���
    public int SizeEW => size.x;
    public int SizeNS => size.y;
    public int StepN => size.x;
    public int StepE => 1;
    public int StepS => -size.x;
    public int StepW => -1;


}
