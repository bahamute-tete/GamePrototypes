using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class MazeCell_Old : MonoBehaviour
{
    public int2 coordinates;


    private MazeCellEdge[] edges = new MazeCellEdge[MazeDirections.count];

    public MazeCellEdge GetEdge(MazeDirection direction)
    {
        return edges[(int)direction];
    }
    public void SetEdge(MazeDirection direction, MazeCellEdge edge)
    {
        edges[(int)direction] = edge;
    }
}
