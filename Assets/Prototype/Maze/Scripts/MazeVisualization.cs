using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class MazeVisualization : ScriptableObject
{
    [SerializeField]
    MazeCellObject end, straight, conner, tJunction, xJunction;

    // tuple struct
    //switch 语法
    (MazeCellObject, int) GetPrefab(MazeFlags flags) => flags switch
    {
        MazeFlags.PassageN => (end,0),
        MazeFlags.PassageE => (end,1),
        MazeFlags.PassageS => (end,2),
        MazeFlags.PassageW => (end,3),

        MazeFlags.PassageN | MazeFlags.PassageS => (straight,0),
        MazeFlags.PassageE | MazeFlags.PassageW => (straight,1),

        MazeFlags.PassageN | MazeFlags.PassageE => (conner,0),
        MazeFlags.PassageE | MazeFlags.PassageS => (conner,1),
        MazeFlags.PassageS | MazeFlags.PassageW => (conner,2),
        MazeFlags.PassageW | MazeFlags.PassageN => (conner,3),

        MazeFlags.PassageAll & ~MazeFlags.PassageW => (tJunction,0),
        MazeFlags.PassageAll & ~MazeFlags.PassageN => (tJunction,1),
        MazeFlags.PassageAll & ~MazeFlags.PassageE => (tJunction,2),
        MazeFlags.PassageAll & ~MazeFlags.PassageS => (tJunction,3),

        _=> (xJunction,0)
    };

    static Quaternion[] rotations =
    {
        Quaternion.identity,
        Quaternion.Euler(0f,90f,0f),
        Quaternion.Euler(0f,180f,0f),
        Quaternion.Euler(0f,270f,0f)
    };
   
    public void Visualize(Maze maze)
    {
        for (int i = 0; i < maze.Length; i++)
        {
            //maze[index] 索引器cell[index]
            //cell是nativeArray<MazeFlags>
            //cell还没有赋值是空的所以mazeFlags= empty =0 =xjunction
            (MazeCellObject, int) prefabWithRotation = GetPrefab(maze[i]);
            MazeCellObject instance = prefabWithRotation.Item1.GetInstance();
            //instance.transform.localPosition = maze.IndexToWorldPosition(i);
            instance.transform.SetLocalPositionAndRotation( maze.IndexToWorldPosition(i),
                                                            rotations[prefabWithRotation.Item2]);
        }
    }
}
