using Newtonsoft.Json.Bson;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = UnityEngine.Random;

public class Maze_Old : MonoBehaviour
{


    public int2 size;
    public MazeCell_Old cellPrefab;
    private MazeCell_Old[,] cells;

    public int2 RandomCoordinates => new int2(Random.Range(0, size.x), Random.Range(0, size.y));
    public float generationStepDelay;

    public MazePassage passagePrefab;
    public MazeWall wallPrefab;


    public MazeCell_Old GetCell(int2 coordinates) => cells[coordinates.x, coordinates.y];
   

    public IEnumerator Generate()
    {   

        cells = new MazeCell_Old[size.x, size.y];

        //int2 coordinates =new int2( Random.Range(0, size.x), Random.Range(0, size.y));
        //while (ContainsCoordinates(coordinates) && GetCell(coordinates)==null)
        //{
        //    yield return new WaitForSeconds(generationStepDelay);
        //    CreateCell(coordinates);
        //    coordinates += MazeDirections.RamdomValue.ToInt2Direction();
        //}


        //create a cell, we add it to this list.
        //Then the next generation step we try to move one random step from the last cell in this list
        //If we cannot do this move,
        //instead of immediately stopping, we remove the current cell from the active list.
        //This way we will do a step backward and try again each time we fail,
        //until the list is empty.
        List<MazeCell_Old> activeCells = new List<MazeCell_Old>();
        DoFirstGenerationStep(activeCells);
        while (activeCells.Count > 0)
        { 
            yield return new WaitForSeconds(generationStepDelay);
            DoNextGenerationStep(activeCells);
        }
    }

    private void DoNextGenerationStep(List<MazeCell_Old> activeCells)
    {
        int currentIndex = activeCells.Count - 1;
        MazeCell_Old currentCell = activeCells[currentIndex];
        MazeDirection direction = MazeDirections.RamdomValue;
        int2 coordinates = currentCell.coordinates + direction.ToInt2Direction();
        if (ContainsCoordinates(coordinates) && GetCell(coordinates) == null)
        {
            MazeCell_Old neighbor = GetCell(coordinates);
            if (neighbor == null)
            {
                neighbor = CreateCell(coordinates);
                CreatePassage(currentCell, neighbor, direction);
                activeCells.Add(neighbor);

            }
            else
            {
                CreateWall(currentCell, neighbor, direction);
                activeCells.RemoveAt(currentIndex);
            }
        }
        else
        {
            CreateWall(currentCell, null, direction);
            activeCells.RemoveAt(currentIndex);
        }

    }

    private void CreateWall(MazeCell_Old currentCell, MazeCell_Old neighbor, MazeDirection direction)
    {
        MazeWall wall = Instantiate(wallPrefab);
        wall.Initialize(currentCell, neighbor, direction);
        if (neighbor != null)
        { 
            wall = Instantiate(wallPrefab);
            wall.Initialize(neighbor, currentCell, direction.GetOpposite());
        }
    }

    private void CreatePassage(MazeCell_Old currentCell, MazeCell_Old neighbor, MazeDirection direction)
    {
        MazePassage passage = Instantiate(passagePrefab);
        passage.Initialize(currentCell, neighbor, direction);
        passage =Instantiate(passagePrefab);
        passage.Initialize(neighbor, currentCell, direction.GetOpposite());
    }

    private void DoFirstGenerationStep(List<MazeCell_Old> activeCells)
    {
        activeCells.Add(CreateCell(RandomCoordinates));
    }

    private bool ContainsCoordinates(int2 coordinates) =>
        coordinates.x >= 0 && coordinates.x < size.x && coordinates.y >= 0 && coordinates.y < size.y;
   

    private MazeCell_Old CreateCell(int2 coordinates)
    {
        MazeCell_Old newCell = Instantiate(cellPrefab);
        cells[coordinates.x,coordinates.y] = newCell;
        newCell.name = "MazeCell " + coordinates.x + "," + coordinates.y;
        newCell.transform.parent = transform;
        newCell.transform.localPosition = new Vector3(
            coordinates.x - size.x * 0.5f + 0.5f, 
            0,
             coordinates.y - size.y * 0.5f + 0.5f);
        newCell.coordinates = coordinates;

        return newCell;
    }


}
