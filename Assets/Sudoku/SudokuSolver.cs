using System;
using UnityEngine;

public class SudokuSolver : MonoBehaviour
{
    private int[,] board;

    public int[,] SolveSudoku(int[,] initialBoard)
    {
        board = (int[,])(initialBoard).Clone();
        SolveInternal();
        return board;
    }


    private bool SolveInternal()
    {
        (int row, int col) = FindEmpty();
        if (row == -1)
        {
            return true; // Puzzle solved
        }

        for (int num = 1; num <= 9; num++)
        {
            if (IsValid(num, (row, col)))
            {
                board[row, col] = num;

                if (SolveInternal())
                {
                    return true;
                }

                board[row, col] = 0; // Reset and backtrack
            }
        }

        return false; // Trigger backtracking
    }

    private bool IsValid(int num, (int, int) pos)
    {
        // Check row
        for (int i = 0; i < board.GetLength(1); i++)
        {
            if (board[pos.Item1, i] == num && pos.Item2 != i)
            {
                return false;
            }
        }

        // Check column
        for (int i = 0; i < board.GetLength(0); i++)
        {
            if (board[i, pos.Item2] == num && pos.Item1 != i)
            {
                return false;
            }
        }

        // Check box
        int boxX = pos.Item2 / 3;
        int boxY = pos.Item1 / 3;
        for (int i = boxY * 3; i < boxY * 3 + 3; i++)
        {
            for (int j = boxX * 3; j < boxX * 3 + 3; j++)
            {
                if (board[i, j] == num && (i, j) != pos)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private (int, int) FindEmpty()
    {
        for (int i = 0; i < board.GetLength(0); i++)
        {
            for (int j = 0; j < board.GetLength(1); j++)
            {
                if (board[i, j] == 0)
                {
                    return (i, j); // Row, Col
                }
            }
        }

        return (-1, -1); // No empty space found
    }

    public void PrintBoard()
    {
        for (int i = 0; i < board.GetLength(0); i++)
        {
            for (int j = 0; j < board.GetLength(1); j++)
            {
                Console.Write($"{board[i, j]} ");
            }

            Console.WriteLine();
        }
    }

    public int[,] GetBoard()
    {
        return board;
    }

    public int[,] GetOnlyNewDigits(int[,] recognizedDigits, int[,] solution)
    {
        if (recognizedDigits.GetLength(0) != solution.GetLength(0) ||
            recognizedDigits.GetLength(1) != solution.GetLength(1))
        {
            Debug.LogError("Arrays do not match in size.");
            return null;
        }

        int rows = recognizedDigits.GetLength(0);
        int cols = recognizedDigits.GetLength(1);
        int[,] onlyNewDigits = new int[rows, cols];

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                // Check if the digit was present initially and is different in the solution
                if (recognizedDigits[row, col] != 0 && solution[row, col] != recognizedDigits[row, col])
                {
                    onlyNewDigits[row, col] = 0; // This was initially understood, no new digit was added here
                }
                // Check if the digit was not recognized initially (i.e., was 0) but is present in the solution
                else if (recognizedDigits[row, col] == 0 && solution[row, col] != 0)
                {
                    onlyNewDigits[row, col] = solution[row, col]; // This is a new digit added by the solution
                }
                else
                {
                    onlyNewDigits[row, col] = 0; // For any other case, keep positions as 0
                }
            }
        }

        return onlyNewDigits;
    }
}