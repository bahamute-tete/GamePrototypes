using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = UnityEngine.Random;

public enum MazeDirection
{ 
    North,
    East,
    South,
    West
}

public static class MazeDirections
{
    public const int count = 4;
    public static MazeDirection RamdomValue
    { 
        get=> (MazeDirection)Random.Range(0, count);
    }

    private static MazeDirection[] opposites = {
        MazeDirection.South,
        MazeDirection.West,
        MazeDirection.North,
        MazeDirection.East
    };

    private static Quaternion[] rotations = {
        Quaternion.identity,
        Quaternion.Euler(0f, 90f, 0f),
        Quaternion.Euler(0f, 180f, 0f),
        Quaternion.Euler(0f, 270f, 0f)
    };

    private static int2[] vectors = {
        new int2(0,1),
        new int2(1,0),
        new int2(0,-1),
        new int2(-1,0)
    };

    //扩展方法
    //它的行为就好像它是 的实例方法一样
    //this 指代MazeDirections
    public static int2 ToInt2Direction(this MazeDirection direction) => vectors[(int)direction];
    public static MazeDirection GetOpposite(this MazeDirection direction)=> opposites[(int)direction];

    public static Quaternion ToRotation(this MazeDirection direction) => rotations[(int)direction];


}