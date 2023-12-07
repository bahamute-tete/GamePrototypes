using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Random = Unity.Mathematics.Random;
using Unity.Collections;
using System;


public struct GenerateMazeJob : IJob
{
    public Maze maze;

    public int seed;

    public float pickLastProability, openDeadEndProbability;
    public void Execute()
    {
        
        var random = new Random((uint)seed);
        //便签：(neighbor index, passage direction, opposite passage direction)
        var scratchpad = new NativeArray<(int, MazeFlags, MazeFlags)>(
            4,Allocator.Temp,NativeArrayOptions.UninitializedMemory
            );

        //激活的cell的索引数组
        var activeIndices = new NativeArray<int>(
            maze.Length, Allocator.Temp, NativeArrayOptions.ClearMemory
        );
  
        //初始化第一个和最后一个cell的索引
        int firstActiveIndex = 0, lastActiveIndex = 0;
        //第一个索引随机初始化
        activeIndices[firstActiveIndex] = random.NextInt(maze.Length);
        // Debug.Log($"activeIndices[firstActiveIndex]==>{activeIndices[firstActiveIndex]}");



        // the first index isn't greater than the last index
        //grab the last active cell and find the available passages for it. 
        //at most one passage then we're finished
        while (firstActiveIndex <= lastActiveIndex)
        {
            bool pickLast = random.NextFloat() < pickLastProability;
            //bool pickLast = false; 
            int randomActiveIndex, index;
            if (pickLast)
            {
                randomActiveIndex = 0;
                index = activeIndices[lastActiveIndex];
            }
            else
            {
                randomActiveIndex = random.NextInt(firstActiveIndex, lastActiveIndex + 1);
                index = activeIndices[randomActiveIndex];
                 //index = activeIndices[firstActiveIndex];
            }


            
            //Debug.Log($"index==>{index}");
            int availablePassageCount = FindAvailablePassages(index, scratchpad);
            //Debug.Log($"availablePassageCount==>{availablePassageCount}");
            if (availablePassageCount <= 1)
            {
                //firstActiveIndex += 1;
                if (pickLast)
                {
                    lastActiveIndex -= 1;
                }
                else
                {
                    //firstActiveIndex += 1;
                    activeIndices[randomActiveIndex] = activeIndices[firstActiveIndex++];
                }
            }

            if (availablePassageCount > 0)
            {
                (int, MazeFlags, MazeFlags) passage = scratchpad[random.NextInt(0, availablePassageCount)];

               // Debug.Log($"passage==>{passage.Item1}_{passage.Item2.ToString()}_{passage.Item3.ToString()}");

                maze.Set(index, passage.Item2);
                
                maze[passage.Item1] = passage.Item3;
                activeIndices[++lastActiveIndex] = passage.Item1;
            }
        }

        if (openDeadEndProbability > 0f)
        {
            random = OpenDeadEnds(random, scratchpad);
        }
    }

    private Random OpenDeadEnds(Random random, NativeArray<(int, MazeFlags, MazeFlags)> scratchpad)
    {
        for (int i = 0; i < maze.Length; i++)
        {
            MazeFlags cell = maze[i];
            if (cell.HasExactlyOne() && random.NextFloat() < openDeadEndProbability)
            {
                int availablePassageCount = FindClosedPassages(i, scratchpad, cell);
                (int, MazeFlags, MazeFlags) passage = scratchpad[random.NextInt(0, availablePassageCount)];

                maze[i] = cell.With(passage.Item2);
                maze.Set(i + passage.Item1, passage.Item3);
            }
        }
        return random;
    }

    //给定单元索引
    //并将（邻居索引、通道方向、相反通道方向）元组存储在临时暂存器中
    //返回可用段落的数量
    int FindAvailablePassages(int index, NativeArray<(int, MazeFlags, MazeFlags)> scratchpad)
    { 
        int2 coordinates= maze.IndexToCoordinates(index);
        int count = 0;

        if (coordinates.x + 1 < maze.SizeEW)
        {
            int i = index + maze.StepE;
            if (maze[i] == MazeFlags.Empty)
            {
                scratchpad[count++] = (i, MazeFlags.PassageE, MazeFlags.PassageW);
            }
        }
        if (coordinates.x> 0)
        {
            int i = index + maze.StepW;
            if (maze[i] == MazeFlags.Empty)
            {
                scratchpad[count++] = (i, MazeFlags.PassageW, MazeFlags.PassageE);
            }
        }
        if (coordinates.y + 1 < maze.SizeNS)
        {
            int i = index + maze.StepN;
            if (maze[i] == MazeFlags.Empty)
            {
                scratchpad[count++] = (i, MazeFlags.PassageN, MazeFlags.PassageS);
            }
        }
        if (coordinates.y > 0)
        {
            int i = index + maze.StepS;
            if (maze[i] == MazeFlags.Empty)
            {
                scratchpad[count++] = (i, MazeFlags.PassageS, MazeFlags.PassageN);
            }
        }

        return count;
    }

    int FindClosedPassages(int index, NativeArray<(int, MazeFlags, MazeFlags)> scratchpad,MazeFlags exclude)
    {
        int2 coordinates = maze.IndexToCoordinates(index);
        int count = 0;

        if (exclude != MazeFlags.PassageE && coordinates.x + 1 < maze.SizeEW)
        {
            scratchpad[count++] = (maze.StepE, MazeFlags.PassageE, MazeFlags.PassageW);
        }
        if (exclude != MazeFlags.PassageW && coordinates.x > 0)
        {
            scratchpad[count++] = (maze.StepW, MazeFlags.PassageE, MazeFlags.PassageW);
        }
        if (exclude != MazeFlags.PassageN && coordinates.y + 1 < maze.SizeNS)
        {
            scratchpad[count++] = (maze.StepN, MazeFlags.PassageN, MazeFlags.PassageS);
        }
        if (exclude != MazeFlags.PassageS && coordinates.y > 0)
        {
            scratchpad[count++] = (maze.StepS, MazeFlags.PassageS, MazeFlags.PassageN);
        }

        return count;
    }


}
