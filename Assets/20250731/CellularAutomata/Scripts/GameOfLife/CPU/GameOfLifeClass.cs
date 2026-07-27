using System;
using UnityEngine;
using Random = UnityEngine.Random;

#region CELL 
public class Cell
{
    public int x;
    public int y;
    public byte state; // Use byte instead of int to save memory (0 = dead, 1 = alive)
    public Cell(int x, int y,byte state=0)
    {
        this.x = x;
        this.y = y;
        this.state = state;
    }
}

public interface ICellularAutomataRule
{
     byte GetNextState(byte currentState, int aliveNeighbors);
}
#endregion

#region RULES
// <summary>
/// Conway's Game of Life Rule (B3/S23)
/// Born: Cell is born with 3 neighbors
/// Survive: Cell survives with 2 or 3 neighbors
/// </summary>
public class ConwayRule : ICellularAutomataRule
{
    public byte GetNextState(byte currentState, int aliveNeighbors)
    {
        byte nextState = currentState;
        //alive
        if (currentState == 1)
        {
            // Less than 2 neighbors -> die
            // 2 or 3 neighbors -> survive
            if (aliveNeighbors < 2)
                nextState =(byte) 0;
            else if (aliveNeighbors > 3)// More than 3 neighbors -> die
                nextState = (byte)0;
        }
        //dead
        else
        {
            // Exactly 3 neighbors -> born
            if (aliveNeighbors == 3)
                nextState = (byte)1;
        }
        return nextState;
    }
}

/// <summary>
/// HighLife Rule (B36/S23)
/// Similar to Conway's rule, but also born with 6 neighbors
/// This rule produces replicator patterns
/// </summary>
public class HighLifeRule : ICellularAutomataRule
{
    public byte GetNextState(byte currentState, int aliveNeighbors)
    {
        byte nextState = currentState;
        //alive
        if (currentState == 1)
        {
            // Less than 2 neighbors -> die
            // 2 or 3 neighbors -> survive
            if (aliveNeighbors < 2)
                nextState = (byte)0;
            else if (aliveNeighbors > 3)// More than 3 neighbors -> die
                nextState = (byte)0;
        }
        //dead
        else
        {
            // Born with 3 or 6 neighbors
            if (aliveNeighbors == 3 || aliveNeighbors == 6)
                nextState = (byte)1;
        }
        return nextState;
    }
}


/// <summary>
/// Day & Night Rule (B3678/S34678)
/// Symmetric: rules for live and dead cells are similar
/// Produces beautiful symmetric patterns
/// </summary>
public class DayAndNightRule : ICellularAutomataRule
{ 
    public byte GetNextState(byte currentState, int aliveNeighbors)
    {
        byte nextState = currentState;
        //alive
        if (currentState == 1)
        {
            // Less than 3 neighbors -> die
            // 3,4,6,7,8 neighbors -> survive
            if (aliveNeighbors < 3)
                nextState = (byte)0;
            else if (aliveNeighbors == 5)// 5 neighbors -> die
                nextState = (byte)0;
        }
        //dead
        else
        {
            // Born with 3,6,7,8 neighbors
            if (aliveNeighbors == 3 || aliveNeighbors >= 6)
                nextState = (byte)1;
        }
        return nextState;
    }
}

/// <summary>
/// Seeds Rule (B2/S)
/// All live cells die in the next generation
/// Only dead cells with exactly 2 neighbors are born
/// Produces ever-expanding 'seeds' effect
/// </summary>
public class SeedsRule : ICellularAutomataRule
{
    public byte GetNextState(byte currentState, int aliveNeighbors)
    {
        if (currentState == 1)
        {
            // All live cells die
            return 0;
        }
        else
        {
            // Born with exactly 2 neighbors
            return aliveNeighbors == 2 ? (byte)1 : (byte)0;
        }
    }
}

/// <summary>
/// Maze Rule (B3/S12345)
/// Produces maze-like patterns
/// More relaxed survival conditions
/// </summary>
public class MazeRule : ICellularAutomataRule
{
    public byte GetNextState(byte currentState, int aliveNeighbors)
    {
        if (currentState == 1)
        {
            // Survive with 1-5 neighbors
            return (aliveNeighbors >= 1 && aliveNeighbors <= 5) ? (byte)1 : (byte)0;
        }
        else
        {
            // Born with 3 neighbors
            return aliveNeighbors == 3 ? (byte)1 : (byte)0;
        }
    }
}

/// <summary>
/// Coral Rule (B3/S45678)
/// Produces coral-like branching structures
/// </summary>
public class CoralRule : ICellularAutomataRule
{
    public byte GetNextState(byte currentState, int aliveNeighbors)
    {
        if (currentState == 1)
        {
            // Survive with 4-8 neighbors
            return (aliveNeighbors >= 4 && aliveNeighbors <= 8) ? (byte)1 : (byte)0;
        }
        else
        {
            // Born with 3 neighbors
            return aliveNeighbors == 3 ? (byte)1 : (byte)0;
        }
    }
}

/// <summary>
/// 2x2 Rule (B36/S125)
/// Produces stable 2x2 block patterns
/// </summary>
public class TwoByTwoRule : ICellularAutomataRule
{
    public byte GetNextState(byte currentState, int aliveNeighbors)
    {
        if (currentState == 1)
        {
            // Survive with 1,2,5 neighbors
            return (aliveNeighbors == 1 || aliveNeighbors == 2 || aliveNeighbors == 5) ? (byte)1 : (byte)0;
        }
        else
        {
            // Born with 3 or 6 neighbors
            return (aliveNeighbors == 3 || aliveNeighbors == 6) ? (byte)1 : (byte)0;
        }
    }
}

public class CustomRule: ICellularAutomataRule
{
    // Birth neighbors count
    private readonly bool[] birthRules = new bool[9];
    // Survival neighbors count
    private readonly bool[] survivalRules = new bool[9];

    public CustomRule(int[] birthNumbers, int[] surviveNumbers)
    {
        foreach (int n in birthNumbers)
        { 
            if (n < 0 || n > 8)
                throw new ArgumentOutOfRangeException("Birth rule values must be between 0 and 8.");
            birthRules[n] = true;
        }

        foreach (int n in surviveNumbers)
        {
            if (n < 0 || n > 8)
                throw new ArgumentOutOfRangeException("SurvivalRule rule values must be between 0 and 8.");
            survivalRules[n] = true;
        }

    }

    public byte GetNextState(byte currentState, int aliveNeighbors)
    {
        if (currentState == 1)
        {
            return survivalRules[aliveNeighbors]? (byte)1 : (byte)0;
        }
        else
        { 
            return birthRules[aliveNeighbors]? (byte)1 : (byte)0;
        }
    }
}
#endregion

#region GameOfLifeGrid
public class GameOfLifeGrid
{
    public int Rows { get; private set; }
    public int Cols { get; private set; }

    public float cellSize;
    private byte[,] currentStates;
    private byte[,] nextStates;

    private ICellularAutomataRule rule;

    private static readonly int[,] neighborOffsets = new int[,]
    {
        {-1, -1}, {-1, 0}, {-1, 1},
        {0, -1},           {0, 1},
        {1, -1},  {1, 0},  {1, 1}
    };
    public GameOfLifeGrid(int rows, int cols, float cellSize, ICellularAutomataRule rule)
    {
        this.Rows = rows;
        this.Cols = cols;
        this.cellSize = cellSize;
        this.rule =rule??new ConwayRule();

        currentStates = new byte[rows, cols];
        nextStates = new byte[rows, cols];
        
    }
   
    public byte GetCellState(int row, int col)
    {
        if (isValidPosition(row,col))
        {
            return currentStates[row, col];
        }
        return 0;

    }

    private bool isValidPosition(int row, int col)
    {
       return row >= 0 && row < Rows && col >= 0 && col < Cols;
    }

    public int CountAliveNeighbors(int row, int col)
    {
        
        int count = 0;

        for (int i = 0; i < 8; i++)
        {
            int neighborRow = row + neighborOffsets[i, 0];
            int neighborCol = col + neighborOffsets[i, 1];

            neighborRow = (neighborRow + Rows) % Rows;
            neighborCol = (neighborCol + Cols) % Cols;

            if (isValidPosition(neighborRow, neighborCol) && currentStates[neighborRow, neighborCol] == 1)
            {
                count++;
            }
        }

        return count;
    }

    public void RandomInitialize(float aliveProbability)
    {
        for (int x = 0; x < Rows; x++)
        {
            for (int y = 0; y < Cols; y++)
            {
                currentStates[x, y] = Random.value< aliveProbability?(byte)1:(byte)0;
            }
        }
    }

    /// <summary>
    /// Place a pattern at the specified position
    /// </summary>
    /// <param name="pattern">Array of relative coordinates for the pattern</param>
    /// <param name="offsetX">Start X coordinate</param>
    /// <param name="offsetY">Start Y coordinate</param>
    public void SetPatterns(Vector2Int[] pattern,int offsetX,int offsetY)
    {
        if (pattern == null || pattern.Length == 0)
        {
            Debug.Log("Pattern is null or empty!");
            return;
        }

        foreach (var cell in pattern)
        {
            int x = offsetX + cell.x;
            int y = offsetY + cell.y;

            if (isValidPosition(x, y))
            {
                currentStates[x, y] = 1;
            }
        }
    }

    /// <summary>
    /// Set the state of a single cell
    /// </summary>
    public void SetCellState(int row, int col, byte state)
    {
        if (isValidPosition(row, col))
        {
            currentStates[row, col] = state;
        }
    }

    public void UpdateNextStates()
    {
        for (int x = 0; x < Rows; x++)
        {
            for (int y = 0; y < Cols; y++)
            {
                int aliveNeighbors = CountAliveNeighbors(x, y);
                nextStates[x, y] = rule.GetNextState(currentStates[x, y], aliveNeighbors);
            }
        }

        SwapBuffers();
    }

    private void SwapBuffers()
    {
        var temp = currentStates;
        currentStates = nextStates;
        nextStates = temp;

    }

    public void Clear()
    {
        System.Array.Clear(currentStates, 0, currentStates.Length);
    }


}
#endregion
