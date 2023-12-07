using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using TMPro;
using Unity.Jobs;
using Unity.Collections;
using Random = UnityEngine.Random;

public class MazeGame : MonoBehaviour
{

    [SerializeField, Range(0f, 1f)]
    float pickLastProbability = 0.5f, openDeadEndProbability = 0.5f;

    [SerializeField]
    MazeVisualization visualization;

    [SerializeField]
    int2 mazeSize = int2(5, 5);

    [SerializeField, Tooltip("Use zero for random seed")]
    int seed;

    Maze maze;

    private void Awake()
    {
        maze = new Maze(mazeSize);
        new GenerateMazeJob
        {
            maze = maze,
            seed =seed !=0 ? seed : Random.Range(1,int.MaxValue),
            pickLastProability = pickLastProbability,
            openDeadEndProbability =openDeadEndProbability

        }.Schedule().Complete();
        visualization.Visualize(maze);
    }

    private void OnDestroy()
    {
        maze.Dispose();
    }
}
