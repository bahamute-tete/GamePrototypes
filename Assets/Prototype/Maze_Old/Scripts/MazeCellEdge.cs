using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class MazeCellEdge : MonoBehaviour
{
    public MazeCell_Old cell, otherCell;
    public MazeDirection direction;

    public void Initialize(MazeCell_Old cell,MazeCell_Old otherCell,MazeDirection direction)
    { 
        this.cell = cell;
        this.otherCell = otherCell;
        this.direction = direction;

        cell.SetEdge(direction, this);
        transform.parent =cell.transform;
        transform.localPosition = Vector3.zero;

        transform.localRotation = direction.ToRotation();
    }

   
}
